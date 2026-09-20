using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using zlSoft.Common;

namespace zlSoft.Business
{
    /// <summary>
    /// LIS（羚云）检验申请业务服务。
    /// 调用 LIS 接口的方式与原 ClsSendLisApply 完全一致（同一报文格式、同一 LisApplyUrl），
    /// 仅将“从体检中间表取数据”改为“从 API 入参取数据”，不读写体检中间库。
    /// </summary>
    public class HisLisService
    {
        public static LisApplyResult SendApply(LisApplyInfo info)
        {
            var result = new LisApplyResult { Success = false };
            if (info == null)
            {
                result.Message = "申请信息为空";
                return result;
            }
            result.ApplyNo = !string.IsNullOrEmpty(info.ApplyNo)
                ? info.ApplyNo
                : (info.Items != null && info.Items.Count > 0 ? info.Items[0].OrderNo : "");

            if (info.Items == null || info.Items.Count == 0)
            {
                result.Message = "申请明细为空";
                return result;
            }

            try
            {
                string token = GetLisToken();
                if (string.IsNullOrEmpty(token))
                {
                    result.Message = "获取羚云Token失败";
                    LogHelper.LogInfo("检验申请失败：获取羚云Token失败！体检号=" + info.PhysicalNo);
                    return result;
                }

                string strIn = BuildApplyJson(info);
                string strUrl = zlConfiguration.GetAppSetting("LisApplyUrl");
                string strOut = HttpService.Post(strIn, strUrl, token, "POST");

                if (string.IsNullOrEmpty(strOut))
                {
                    result.Message = "LIS无返回";
                    return result;
                }

                JObject jobj = JObject.Parse(strOut);
                string code = jobj["code"] != null ? jobj["code"].ToString() : "";
                string message = jobj["message"] != null ? jobj["message"].ToString() : "";

                // 沿用原逻辑：code==0 成功；“已存在”也视为成功（幂等）
                if (code == "0" || message.Contains("已存在"))
                {
                    result.Success = true;
                    result.Message = "成功";
                    return result;
                }
                else
                {
                    result.Message = string.IsNullOrEmpty(message) ? strOut : message;
                    LogHelper.LogInfo("体检号【" + info.PhysicalNo + "】的LIS申请推送失败，" + result.Message);
                    return result;
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogInfo("检验申请API异常，体检号=" + info.PhysicalNo + "，错误：" + ex.Message);
                result.Success = false;
                result.Message = "系统异常：" + ex.Message;
                return result;
            }
        }
        /// <summary>
        /// 撤销检验申请（API 模式）。调用 LIS 撤销接口的方式与原 ClsCancelLisApply 一致（同一 LisCancelUrl），
        /// 仅将“从中间表取待撤销”改为“从 API 入参取”，不读写中间库。按申请单号(order_no)去重逐单撤销。
        /// </summary>
        public static LisApplyResult CancelApply(LisApplyInfo info)
        {
            var result = new LisApplyResult { Success = false };
            if (info == null)
            {
                result.Message = "撤销信息为空";
                return result;
            }
            result.ApplyNo = !string.IsNullOrEmpty(info.ApplyNo)
                ? info.ApplyNo
                : (info.Items != null && info.Items.Count > 0 ? info.Items[0].OrderNo : "");

            if (info.Items == null || info.Items.Count == 0)
            {
                result.Message = "撤销明细为空";
                return result;
            }

            try
            {
                string token = GetLisToken();
                if (string.IsNullOrEmpty(token))
                {
                    result.Message = "获取羚云Token失败";
                    LogHelper.LogInfo("检验撤销失败：获取羚云Token失败！体检号=" + info.PhysicalNo);
                    return result;
                }

                string strUrl = zlConfiguration.GetAppSetting("LisCancelUrl");
                var handled = new System.Collections.Generic.HashSet<string>();
                var failMsgs = new System.Collections.Generic.List<string>();

                foreach (var it in info.Items)
                {
                    string reqNo = it.OrderNo ?? "";
                    if (string.IsNullOrEmpty(reqNo) || handled.Contains(reqNo)) continue;
                    handled.Add(reqNo);

                    string strIn = BuildCancelJson(info, reqNo);
                    string strOut = HttpService.Post(strIn, strUrl, token, "POST");
                    if (string.IsNullOrEmpty(strOut))
                    {
                        failMsgs.Add("申请单号[" + reqNo + "]LIS无返回");
                        continue;
                    }

                    JObject jobj = JObject.Parse(strOut);
                    string code = jobj["code"] != null ? jobj["code"].ToString() : "";
                    string message = jobj["message"] != null ? jobj["message"].ToString() : "";

                    // 沿用原逻辑：code==0 成功；“不存在/已撤销”视为成功（幂等）
                    if (code == "0" || message.Contains("不存在") || message.Contains("已撤销"))
                    {
                        // 撤销成功
                    }
                    else
                    {
                        failMsgs.Add("申请单号[" + reqNo + "]" + (string.IsNullOrEmpty(message) ? strOut : message));
                        LogHelper.LogInfo("申请单号【" + reqNo + "】的LIS申请撤销失败，" + message);
                    }
                }

                if (failMsgs.Count == 0)
                {
                    result.Success = true;
                    result.Message = "成功";
                }
                else
                {
                    result.Success = false;
                    result.Message = string.Join("；", failMsgs.ToArray());
                }
                return result;
            }
            catch (Exception ex)
            {
                LogHelper.LogInfo("检验撤销API异常，体检号=" + info.PhysicalNo + "，错误：" + ex.Message);
                result.Success = false;
                result.Message = "系统异常：" + ex.Message;
                return result;
            }
        }

        /// <summary>组装羚云 revokeShengQingDanBySQD 撤销报文（结构与原 ClsCancelLisApply 一致）</summary>
        private static string BuildCancelJson(LisApplyInfo info, string reqNo)
        {
            string orgId = zlConfiguration.GetAppSetting("LisOrgID");
            string orgName = zlConfiguration.GetAppSetting("LisOrgName");

            var dataItem = new JObject();
            dataItem["ShenQingDanID"] = reqNo;
            dataItem["kaiDanJGID"] = orgId;
            dataItem["kaiDanJGMC"] = orgName;
            var data = new JArray();
            data.Add(dataItem);

            var root = new JObject();
            root["caoZuoRID"] = info.ApplyDrCode ?? "";
            root["caoZuoRXM"] = info.ApplyDrName ?? "";
            root["caoZuoJGID"] = orgId;
            root["caoZuoJGMC"] = orgName;
            root["caoZuoKSID"] = zlConfiguration.GetAppSetting("LDeptID");
            root["caoZuoKSMC"] = zlConfiguration.GetAppSetting("LisOrgName");
            root["caoZuoSJ"] = info.ApplyDate ?? "";
            root["data"] = data;
            return root.ToString(Formatting.None);
        }
        /// <summary>组装羚云 insertShengQingDan 报文（结构与原 ClsSendLisApply 一致）</summary>
        private static string BuildApplyJson(LisApplyInfo info)
        {
            var pat = new JObject();
            pat["bingRenID"] = info.PatId ?? "";
            pat["BingAnHao"] = info.PhysicalNo ?? "";
            pat["MPI"] = info.PhysicalNo ?? "";
            pat["XingMing"] = info.PatName ?? "";
            pat["XingBie"] = info.GenderCode ?? "";
            pat["ChuShengRQ"] = info.DateBirth ?? "";
            pat["ZhengJianLX"] = 1;
            pat["ZhengJianHM"] = info.IdNumber ?? "";
            pat["DianHuaHM"] = info.PhoneNo ?? "";
            pat["JiuZhenHao"] = info.PhysicalNo ?? "";
            pat["JiuZhenLX"] = 3;
            pat["jiZhenBZ"] = 0;
            pat["shouFeiZT"] = 1;
            pat["shiShouFY"] = 0;
            pat["ziFeiBZ"] = 1;
            pat["feiYongLB"] = "全自费";
            pat["LuSeTD"] = 0;

            string kaiDanKSID = zlConfiguration.GetAppSetting("LDeptID");
            string kaiDanKSMC = zlConfiguration.GetAppSetting("LDeptName");

            var shengQingDXX = new JArray();
            foreach (var it in info.Items)
            {
                var item = new JObject();
                item["JianYanXMMC"] = it.TestItemName ?? "";
                item["JianYanXMDM"] = it.TestItemCode ?? "";
                item["XiangMuJE"] = 0;
                item["ShenQingID"] = it.OrderNo ?? "";
                //item["YiZhuID"] = info.ApplyNo ?? "";
                item["YiZhuID"] = it.OrderNo ?? "";
                item["TiaoMaHao"] = info.BarCode ?? "";
                item["jiZhenBZ"] = 0;
                item["kaiDanKSID"] = kaiDanKSID;
                item["kaiDanKSMC"] = kaiDanKSMC;
                item["kaiDanYSID"] = info.ApplyDrCode ?? "";
                item["kaiDanYSMC"] = info.ApplyDrName ?? "";
                item["kaiDanSJ"] = FormatDateTime(info.ApplyDate) ?? "";
                item["daYinRID"] = info.ApplyDrCode ?? "";
                item["daYinRXM"] = info.ApplyDrName ?? "";
                item["daYinKSID"] = kaiDanKSID;
                item["daYinKSMC"] = kaiDanKSMC;
                item["daYinSJ"] = FormatDateTime(info.ApplyDate) ?? "";
                shengQingDXX.Add(item);
            }
            pat["ShengQingDXX"] = shengQingDXX;

            var data = new JArray();
            data.Add(pat);

            var root = new JObject();
            root["kaiDanJGID"] = zlConfiguration.GetAppSetting("LisOrgID");
            root["kaiDanJGMC"] = zlConfiguration.GetAppSetting("LisOrgName");
            root["tiaoMaDYBZ"] = 1;
            root["data"] = data;
            return root.ToString(Formatting.None);
        }

        /// <summary>XML传入值优先，为空时回退到配置文件</summary>
        private static string ValueOrConfig(string value, string configKey)
        {
            if (!string.IsNullOrEmpty(value)) return value;
            return zlConfiguration.GetAppSetting(configKey) ?? "";
        }

        /// <summary>获取羚云 LIS 的 Token（与 frmMain 中逻辑一致）</summary>
        private static string GetLisToken()
        {
            string strIn = "grant_type=client_credentials&client_id=" + zlConfiguration.GetAppSetting("ClientID") +
                           "&client_secret=" + zlConfiguration.GetAppSetting("ClientSecret");
            string strOut = HttpService.Post(strIn, zlConfiguration.GetAppSetting("LisTokenUrl"), "", "POST");
            if (!string.IsNullOrEmpty(strOut) && strOut.Contains("access_token"))
            {
                JObject jobj = JObject.Parse(strOut);
                return jobj["access_token"].ToString();
            }
            LogHelper.LogInfo("获取羚云Token异常，返回内容：" + strOut);
            return "";
        }
        private static string FormatDateTime(string dtStr)
        {
            if (string.IsNullOrWhiteSpace(dtStr))
                return dtStr;

            // 尝试解析 yyyyMMddHHmmss
            if (DateTime.TryParseExact(dtStr, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
                return dt.ToString("yyyy-MM-dd HH:mm:ss");

            // 尝试解析 yyyy-MM-dd HH:mm:ss（已是目标格式）
            if (DateTime.TryParseExact(dtStr, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                return dtStr;

            // 其他通用解析（如带毫秒等）
            if (DateTime.TryParse(dtStr, out dt))
                return dt.ToString("yyyy-MM-dd HH:mm:ss");

            return dtStr; // 无法解析，原样返回
        }
    }
}
