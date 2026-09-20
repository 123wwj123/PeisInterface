using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using zlSoft.Common;

namespace zlSoft.Business
{
    /// <summary>
    /// 三方（调鼎）"体检系统推送申请(或撤销申请)信息" JSON 接口处理。
    ///
    /// 协议要点：
    /// 1. 体检系统按条码分类发送，几个条码就调用几次本接口，每次请求只处理一个条码（barcode）；
    /// 2. 条码合管时 detail 会带多个体检项目（如肝功能+肾功能），逐条作为 LIS 申请明细下发，
    ///    共用同一个条码号；
    /// 3. 申请单号 applyCode 放在 detail 每个明细项的内层，每条明细各自一个申请单号
    ///    （LIS 的 ShenQingID 取该明细的 applyCode）；外层 applyCode 仅作兼容/兜底；
    /// 4. applyType：1-申请  2-撤销；两者共用同一入口，内部按 applyType 分流；
    /// 5. 只做协议转换，LIS（羚云）的下发与撤销逻辑仍完全复用 HisLisService，不修改。
    /// </summary>
    public class LisApplyJson
    {
        // 三方返回码
        private const string CODE_SUCCESS = "1001";
        private const string CODE_ERROR = "9999";

        public static string Process(string requestJson)
        {
            string strApplyCode = "";
            try
            {
                if (string.IsNullOrWhiteSpace(requestJson))
                {
                    return BuildErrorResponse("请求报文不能为空");
                }

                JObject request = JObject.Parse(requestJson);

                //strApplyCode = GetString(request, "applyCode");   // 外层仅作兼容/兜底，可为空
                string strCheckCode = GetString(request, "checkCode");
                string strApplyType = GetString(request, "applyType");

                if (string.IsNullOrEmpty(strCheckCode))
                {
                    return BuildErrorResponse("checkCode不能为空");
                }
                if (strApplyType != "1" && strApplyType != "2")
                {
                    return BuildErrorResponse("applyType取值错误，1-申请 2-撤销");
                }

                //1. 三方 JSON -> 现有 LisApplyInfo
                LisApplyInfo info = ParseApply(request, strApplyType);

                //申请单号以 detail 内层为准，全部为空时不允许下发/撤销
                if (string.IsNullOrEmpty(info.ApplyNo))
                {
                    return BuildErrorResponse("applyCode不能为空（需位于 detail 明细内）");
                }

                //2. 沿用现有 LIS 下发/撤销逻辑
                bool blnCancel = strApplyType == "2";
                LisApplyResult result = blnCancel
                    ? HisLisService.CancelApply(info)
                    : HisLisService.SendApply(info);

                if (result == null || !result.Success)
                {
                    return BuildErrorResponse(result == null ? "LIS处理失败" : result.Message);
                }

                return BuildResponse(CODE_SUCCESS, blnCancel ? "撤销成功" : "发送成功", result.ApplyNo);
            }
            catch (JsonReaderException ex)
            {
                LogHelper.LogInfo("三方LIS申请JSON解析异常：" + ex.Message);
                return BuildErrorResponse("请求报文格式错误：" + ex.Message);
            }
            catch (Exception ex)
            {
                LogHelper.LogInfo("三方LIS申请接口异常，applyCode=" + strApplyCode + "，错误：" + ex.Message);
                return BuildErrorResponse("系统异常：" + ex.Message);
            }
        }

        /// <summary>三方 LIS 申请/撤销报文 -> LisApplyInfo</summary>
        private static LisApplyInfo ParseApply(JObject request, string applyType)
        {
            //外层 applyCode：老格式兼容，同时作为明细未带 applyCode 时的兜底值
            string strApplyCode = GetString(request, "applyCode");

            LisApplyInfo info = new LisApplyInfo();
            //info.ApplyNo = strApplyCode;

            //患者信息
            info.PatId = GetString(request, "alternateNo");   // 档案号（HIS用户唯一号）
            info.PhysicalNo = GetString(request, "checkCode");// 体检号
            info.PatName = GetString(request, "name");
            info.GenderCode = MapGender(GetString(request, "sex"));
            info.DateBirth = GetString(request, "birth");
            info.IdNumber = GetString(request, "idcard");
            info.PhoneNo = GetString(request, "phone");

            //申请信息
            info.ApplyState = applyType == "2" ? "3" : "1";   // 1申请 3取消
            info.BarCode = GetString(request, "barcode");
            info.ApplyDrCode = GetString(request, "applyDctCode");
            info.ApplyDrName = GetString(request, "applyDctName");
            info.ApplyDeptCode = GetString(request, "applyDepartCode");
            info.ApplyDeptName = GetString(request, "applyDepartName");

            //开单时间：申请时间优先，其次标本采集时间、条码打印时间
            string strApplyTime = GetString(request, "applyTime");
            if (string.IsNullOrEmpty(strApplyTime)) strApplyTime = GetString(request, "sampleTime");
            if (string.IsNullOrEmpty(strApplyTime)) strApplyTime = GetString(request, "barcodePrintTime");
            info.ApplyDate = strApplyTime;

            //项目明细：同一条码下的多个项目（合管）逐条作为 LIS 申请明细，共用同一个条码号；
            //申请单号取明细内层的 applyCode（为空时回退外层 applyCode）
            JToken detail = request["detail"];
            if (detail != null && detail.Type == JTokenType.Array)
            {
                foreach (JToken d in (JArray)detail)
                {
                    if (d == null || d.Type != JTokenType.Object) continue;

                    string strItemApplyCode = GetString((JObject)d, "applyCode");
                    if (string.IsNullOrEmpty(strItemApplyCode)) strItemApplyCode = strApplyCode;

                    //YiZhuID 取第一条有效明细的申请单号
                    //if (string.IsNullOrEmpty(info.ApplyNo)) info.ApplyNo = strItemApplyCode;
                    info.ApplyNo = strItemApplyCode;
                    LisApplyItem item = new LisApplyItem();
                    item.OrderNo = strItemApplyCode;
                    item.TestItemCode = GetString((JObject)d, "examitemCode");
                    item.TestItemName = GetString((JObject)d, "examitemName");
                    info.Items.Add(item);
                }
            }

            //撤销时体检可能不下发明细，此处保证申请单号不丢失
            if (info.Items.Count == 0)
            {
                info.Items.Add(new LisApplyItem { OrderNo = strApplyCode });
            }

            return info;
        }

        /// <summary>
        /// 性别代码映射：三方 sex（0-女 1-男） -> LIS 性别码（1-男 2-女）。
        /// 与中间表模式（CASE WHEN '男' THEN 1 ELSE 2）保持一致。
        /// </summary>
        private static string MapGender(string sex)
        {
            switch ((sex ?? "").Trim())
            {
                case "1": return "1";   // 男
                case "0": return "2";   // 女
                case "2": return "2";   // 已按 1男/2女 传入
                default: return sex ?? "";
            }
        }

        private static string BuildResponse(string code, string msg, string applyCode)
        {
            JObject response = new JObject();
            response["code"] = code;
            response["msg"] = msg ?? "";
            response["applyCode"] = applyCode ?? "";
            return response.ToString(Formatting.None);
        }

        private static string BuildErrorResponse(string message)
        {
            return BuildResponse(CODE_ERROR, message, "");
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
