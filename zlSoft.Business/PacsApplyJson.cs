using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using zlSoft.Common;

namespace zlSoft.Business
{
    /// <summary>
    /// 三方（调鼎）"体检系统推送申请（撤销）信息" JSON 接口处理（PACS）。
    ///
    /// 协议要点：
    /// 1. 患者信息在报文外层，检查项目在 examitems 数组中，每个 examitem 自带
    ///    applyCode（申请单号）、applyType（1-申请 2-撤销）、checkType（检查类型）；
    /// 2. checkType = ECG（心电图）的项目不下发 PACS，直接过滤；
    /// 3. 申请按 checkType 分组下发（PACS 的 MODALITY 取 checkType）；
    ///    撤销按 applyCode 逐单撤销，与检查类型无关；
    /// 4. 只做协议转换，PACS 库写入逻辑完全复用 HisPacsService，不修改。
    /// </summary>
    public class PacsApplyJson
    {
        // 三方返回码
        private const string CODE_SUCCESS = "1001";
        private const string CODE_ERROR = "9999";

        // 需要过滤的检查类型：心电图不推送 PACS
        private const string CHECK_TYPE_ECG = "ECG";

        public static string Process(string requestJson)
        {
            string strCheckCode = "";
            try
            {
                if (string.IsNullOrWhiteSpace(requestJson))
                {
                    return BuildErrorResponse("请求报文不能为空");
                }

                JObject request = JObject.Parse(requestJson);

                strCheckCode = GetString(request, "checkCode");
                if (string.IsNullOrEmpty(strCheckCode))
                {
                    return BuildErrorResponse("checkCode不能为空");
                }

                JToken examitems = request["examitems"];
                if (examitems == null || examitems.Type != JTokenType.Array)
                {
                    return BuildErrorResponse("examitems不能为空");
                }

                var applyItems = new List<JObject>();    // applyType=1 申请
                var cancelItems = new List<JObject>();   // applyType=2 撤销
                var dataArr = new JArray();              // 返回给三方的回执（含被过滤项）

                foreach (JToken token in (JArray)examitems)
                {
                    if (token == null || token.Type != JTokenType.Object) continue;

                    JObject item = (JObject)token;
                    string strExamitemCode = GetString(item, "examitemCode");
                    string strApplyCode = GetString(item, "applyCode");

                    //回执：原样回传接收到的项目
                    var joData = new JObject();
                    joData["examitemCode"] = strExamitemCode;
                    joData["applyCode"] = strApplyCode;
                    dataArr.Add(joData);

                    //过滤心电图（ECG）
                    if (IsFiltered(item))
                    {
                        LogHelper.LogInfo("PACS申请过滤checkType=ECG，体检号=" + strCheckCode +
                                         "，申请单号=" + strApplyCode);
                        continue;
                    }

                    if (string.IsNullOrEmpty(strApplyCode))
                    {
                        LogHelper.LogInfo("PACS申请项目缺少applyCode，体检号=" + strCheckCode +
                                         "，项目编码=" + strExamitemCode);
                        continue;
                    }

                    string strApplyType = GetString(item, "applyType");
                    if (strApplyType == "2")
                    {
                        cancelItems.Add(item);
                    }
                    else
                    {
                        applyItems.Add(item);
                    }
                }

                var failMsgs = new List<string>();

                #region 申请：按 checkType 分组下发（MODALITY = checkType）
                var groups = new Dictionary<string, List<JObject>>(StringComparer.OrdinalIgnoreCase);
                foreach (JObject item in applyItems)
                {
                    string strCheckType = GetString(item, "checkType");
                    if (!groups.ContainsKey(strCheckType))
                    {
                        groups[strCheckType] = new List<JObject>();
                    }
                    groups[strCheckType].Add(item);
                }

                foreach (KeyValuePair<string, List<JObject>> kv in groups)
                {
                    PacsApplyInfo info = BuildApplyInfo(request, kv.Value, kv.Key, "1");
                    PacsApplyResult result = HisPacsService.SendApply(info);
                    if (result == null || !result.Success)
                    {
                        failMsgs.Add("检查类型[" + kv.Key + "]" + (result == null ? "PACS处理失败" : result.Message));
                    }
                }
                #endregion

                #region 撤销：按申请单号逐单撤销
                if (cancelItems.Count > 0)
                {
                    PacsApplyInfo info = BuildApplyInfo(request, cancelItems, "", "2");
                    PacsApplyResult result = HisPacsService.CancelApply(info);
                    if (result == null || !result.Success)
                    {
                        failMsgs.Add("撤销：" + (result == null ? "PACS处理失败" : result.Message));
                    }
                }
                #endregion

                if (failMsgs.Count > 0)
                {
                    return BuildErrorResponse(string.Join("；", failMsgs.ToArray()));
                }

                return BuildResponse(CODE_SUCCESS, "发送成功", dataArr);
            }
            catch (JsonReaderException ex)
            {
                LogHelper.LogInfo("三方PACS申请JSON解析异常：" + ex.Message);
                return BuildErrorResponse("请求报文格式错误：" + ex.Message);
            }
            catch (Exception ex)
            {
                LogHelper.LogInfo("三方PACS申请接口异常，体检号=" + strCheckCode + "，错误：" + ex.Message);
                return BuildErrorResponse("系统异常：" + ex.Message);
            }
        }

        /// <summary>三方患者信息(examitems 外) + 一组项目 -> PacsApplyInfo</summary>
        private static PacsApplyInfo BuildApplyInfo(JObject request, List<JObject> items, string checkType, string applyType)
        {
            PacsApplyInfo info = new PacsApplyInfo();

            info.PatId = GetString(request, "alternateNo");     // 档案号（HIS用户唯一号）
            info.PhysicalNo = GetString(request, "checkCode");  // 体检号
            info.PatName = GetString(request, "name");
            info.GenderCode = MapGender(GetString(request, "sex"));
            info.DateBirth = GetString(request, "birth");
            info.IdNumber = GetString(request, "idcard");
            info.PhoneNo = GetString(request, "phone");
            info.ApplyDate = GetString(request, "applyTime");
            info.Modality = checkType ?? "";                    // MODALITY = checkType
            info.ApplyState = applyType == "2" ? "3" : "1";     // 1申请 3取消

            if (items.Count > 0)
            {
                //申请单号、申请医生在每个 examitem 上
                info.ApplyNo = GetString(items[0], "applyCode");
                info.ApplyDrName = GetString(items[0], "applyDct");
                //协议只提供申请医生姓名，工号一并以姓名填充
                info.ApplyDrCode = GetString(items[0], "applyDct");
            }

            foreach (JObject item in items)
            {
                PacsApplyItem pi = new PacsApplyItem();
                pi.OrderNo = GetString(item, "applyCode");       // 申请单号 -> HIS_ID
                pi.TestItemCode = GetString(item, "examitemCode");
                pi.TestItemName = GetString(item, "examitemName");
                info.Items.Add(pi);
            }

            return info;
        }

        /// <summary>是否需要过滤（ECG 心电图不下发 PACS）</summary>
        private static bool IsFiltered(JObject item)
        {
            string strCheckType = GetString(item, "checkType");
            if (strCheckType.Equals(CHECK_TYPE_ECG, StringComparison.OrdinalIgnoreCase)) return true;

            //接口类型为 ECG 的一并过滤
            string strInterfaceType = GetString(item, "applyInterfaceType");
            if (strInterfaceType.Equals(CHECK_TYPE_ECG, StringComparison.OrdinalIgnoreCase)) return true;

            return false;
        }

        /// <summary>
        /// 性别映射 -> PACS 的 PATSEX（中文 男/女）。
        /// 三方可能传 0-女/1-男，也可能直接传 男/女。
        /// </summary>
        private static string MapGender(string sex)
        {
            switch ((sex ?? "").Trim())
            {
                case "1":
                case "男":
                case "M":
                    return "男";
                case "0":
                case "2":
                case "女":
                case "F":
                    return "女";
                default:
                    return sex ?? "";
            }
        }

        private static string BuildResponse(string code, string msg, JArray data)
        {
            JObject response = new JObject();
            response["code"] = code;
            response["msg"] = msg ?? "";
            JArray arr = data ?? new JArray();
            response["data"] = arr;
            return response.ToString(Formatting.None);
        }

        private static string BuildErrorResponse(string message)
        {
            return BuildResponse(CODE_ERROR, message, null);
        }

        private static string GetString(JObject obj, string name)
        {
            if (obj == null || string.IsNullOrEmpty(name)) return "";
            JToken token = obj[name];
            if (token == null || token.Type == JTokenType.Null) return "";
            return token.ToString().Trim();
        }
    }
}
