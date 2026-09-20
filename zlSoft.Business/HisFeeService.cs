using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using zlSoft.Common;

namespace zlSoft.Business
{
    /// <summary>
    /// HIS 门诊费用（收费申请 / 撤销收费申请）业务服务。
    /// 从原 ClsSyncFee 的逻辑解耦而来，入参为强类型 FeeInfo，不读写体检中间库。
    /// shouTuiBz=1（收费申请）  -> /shouFei/createMenZhenFy 生成待收费；
    /// shouTuiBz=2（撤销收费） -> /shouFei/cheXiaoMzFy 撤销尚未收费的费用，
    ///                            feiYongId 按登记流水号（settleCode）到 HIS 库反查。
    /// </summary>
    public class HisFeeService
    {
        public static FeeResult CreateFee(FeeInfo info)
        {
            var result = new FeeResult { Success = false };
            if (info == null)
            {
                result.Message = "费用信息为空";
                return result;
            }

            try
            {
                string dfHttp = zlConfiguration.GetAppSetting("DFHttp");
                string token = GetDfToken(dfHttp);
                if (string.IsNullOrEmpty(token))
                {
                    result.Message = "获取东昉Token失败";
                    LogHelper.LogInfo("生成费用失败：获取东昉Token失败！登记流水号=" + info.DengJiLsh);
                    return result;
                }

                //撤销收费申请（shouTuiBz=2）：费用尚未收费，走 HIS 撤销门诊费用接口。
                //体检侧传回的仍是收费时的 settleCode（=登记流水号=HIS waibujklsh），
                //先按该流水号到 HIS 库反查 feiYongId。
                if (IsRefund(info))
                {
                    FillRefundFeeId(info);
                    return CancelFee(info, dfHttp, token);
                }

                //收费申请（shouTuiBz=1）：生成门诊费用，走原逻辑
                string strIn = BuildCreateFeeJson(info);
                string strUrl = dfHttp + "/shouFei/createMenZhenFy";
                string strOut = HttpService.Post(strIn, strUrl, token, "POST");

                if (string.IsNullOrEmpty(strOut))
                {
                    result.Message = "HIS无返回";
                    return result;
                }

                JObject jobj = JObject.Parse(strOut);
                if (jobj["returnCode"] != null && jobj["returnCode"].Value<string>() == "1")
                {
                    JToken rd = jobj["returnData"];
                    if (rd != null && rd.Type != JTokenType.Null)
                    {
                        // returnData 可能是对象，也可能是已序列化的字符串
                        result.ReturnData = rd.Type == JTokenType.String
                            ? JObject.Parse(rd.Value<string>())
                            : JObject.Parse(rd.ToString());
                    }
                    result.Success = true;
                    result.Message = "成功";
                    return result;
                }
                else
                {
                    result.Message = jobj["returnMessage"] != null ? jobj["returnMessage"].Value<string>() : strOut;
                    LogHelper.LogInfo("生成费用失败，登记流水号=" + info.DengJiLsh + "，" + result.Message);
                    return result;
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogInfo("生成费用API异常，登记流水号=" + info.DengJiLsh + "，错误：" + ex.Message);
                result.Success = false;
                result.Message = "系统异常：" + ex.Message;
                return result;
            }
        }

        /// <summary>
        /// 撤销门诊费用（/shouFei/cheXiaoMzFy）。
        /// 用于已生成但尚未收费的费用；feiYongId 由 FillRefundFeeId 按登记流水号反查补全。
        /// 报文：{"jiuZhenKh":"","bingRenId":"","feiYongIdList":[{"feiYongId":""}]}
        /// </summary>
        private static FeeResult CancelFee(FeeInfo info, string dfHttp, string token)
        {
            var result = new FeeResult { Success = false };

            if (string.IsNullOrEmpty(info.DengJiLsh))
            {
                result.Message = "撤销费用失败：登记流水号（settleCode）为空";
                LogHelper.LogInfo(result.Message);
                return result;
            }

            //同一笔费用可能查出多行明细，按费用ID去重
            var arrFeiYongId = new JArray();
            var listFeiYongId = new List<string>();
            if (info.Items != null)
            {
                foreach (FeeItem it in info.Items)
                {
                    if (string.IsNullOrEmpty(it.FeiYongId)) continue;
                    if (listFeiYongId.Contains(it.FeiYongId)) continue;

                    listFeiYongId.Add(it.FeiYongId);
                    var joId = new JObject();
                    joId["feiYongId"] = it.FeiYongId;
                    arrFeiYongId.Add(joId);
                }
            }

            if (arrFeiYongId.Count == 0)
            {
                result.Message = "撤销费用失败：未查询到HIS费用信息，登记流水号=" + info.DengJiLsh;
                LogHelper.LogInfo(result.Message);
                return result;
            }

            var jo = new JObject();
            jo["jiuZhenKh"] = info.JiuZhenKh ?? "";
            jo["bingRenId"] = "";
            jo["feiYongIdList"] = arrFeiYongId;

            string strIn = jo.ToString(Formatting.None);
            string strUrl = dfHttp + "/shouFei/cheXiaoMzFy";
            string strOut = HttpService.Post(strIn, strUrl, token, "POST");

            if (string.IsNullOrEmpty(strOut))
            {
                result.Message = "HIS无返回";
                return result;
            }

            JObject jobj = JObject.Parse(strOut);
            if (jobj["returnCode"] != null && jobj["returnCode"].Value<string>() == "1")
            {
                JObject data = null;
                JToken rd = jobj["returnData"];
                if (rd != null && rd.Type != JTokenType.Null)
                {
                    string strData = rd.Type == JTokenType.String ? rd.Value<string>() : rd.ToString();
                    strData = strData == null ? "" : strData.Trim();
                    if (strData.StartsWith("{")) data = JObject.Parse(strData);
                }
                if (data == null) data = new JObject();
                if (data["feiYongId"] == null || data["feiYongId"].Type == JTokenType.Null)
                {
                    data["feiYongId"] = listFeiYongId[0];
                }

                result.ReturnData = data;
                result.Success = true;
                result.Message = "成功";
                return result;
            }

            result.Message = jobj["returnMessage"] != null ? jobj["returnMessage"].Value<string>() : strOut;
            LogHelper.LogInfo("撤销费用失败，登记流水号=" + info.DengJiLsh + "，" + result.Message);
            return result;
        }

        /// <summary>是否撤销收费（shouTuiBz=2）</summary>
        private static bool IsRefund(FeeInfo info)
        {
            string strShouTuiBz = string.IsNullOrEmpty(info.ShouTuiBz) ? "1" : info.ShouTuiBz;
            return strShouTuiBz == "2";
        }

        /// <summary>
        /// 撤销收费时补全费用ID/费用明细ID。
        /// 体检撤销时传回的仍是收费时的 settleCode（= dengJiLsh = HIS 的 waibujklsh），
        /// 据此到 HIS 库反查 feiYongId / feiYongMxId。
        /// 体检已传入的不覆盖；明细为空时按 HIS 库查询结果生成明细。
        /// </summary>
        private static void FillRefundFeeId(FeeInfo info)
        {
            if (info.Items != null && info.Items.Count > 0)
            {
                bool blnNeed = false;
                foreach (FeeItem it in info.Items)
                {
                    if (string.IsNullOrEmpty(it.FeiYongId) || string.IsNullOrEmpty(it.FeiYongMxId))
                    {
                        blnNeed = true;
                        break;
                    }
                }
                if (!blnNeed) return;
            }

            List<HisFeeQuery.HisFeeId> feeIds;
            try
            {
                feeIds = HisFeeQuery.GetFeeIdList(info.DengJiLsh);
            }
            catch (Exception ex)
            {
                LogHelper.LogInfo("退费查询HIS费用信息失败，登记流水号=" + info.DengJiLsh + "，错误：" + ex.Message);
                return;
            }

            if (feeIds == null || feeIds.Count == 0)
            {
                LogHelper.LogInfo("退费未查询到HIS费用信息，登记流水号=" + info.DengJiLsh);
                return;
            }

            if (info.Items == null || info.Items.Count == 0)
            {
                info.Items = new List<FeeItem>();
                foreach (HisFeeQuery.HisFeeId id in feeIds)
                {
                    info.Items.Add(new FeeItem
                    {
                        FeiYongId = id.FeiYongId,
                        FeiYongMxId = id.FeiYongMxId,
                        ShuLiang = "-1"
                    });
                }
                return;
            }

            for (int i = 0; i < info.Items.Count; i++)
            {
                FeeItem it = info.Items[i];
                if (!string.IsNullOrEmpty(it.FeiYongId) && !string.IsNullOrEmpty(it.FeiYongMxId)) continue;

                if (i >= feeIds.Count)
                {
                    LogHelper.LogInfo("退费明细与HIS费用明细数量不匹配，登记流水号=" + info.DengJiLsh);
                    continue;
                }

                it.FeiYongId = feeIds[i].FeiYongId;
                it.FeiYongMxId = feeIds[i].FeiYongMxId;
            }
        }

        /// <summary>组装 createMenZhenFy 请求报文（沿用原 ClsSyncFee 字段，数据改为来自API入参）</summary>
        private static string BuildCreateFeeJson(FeeInfo info)
        {
            var jo = new JObject();
            jo["bingRenId"] = info.BingRenId ?? "";
            jo["jiuZhenKh"] = info.JiuZhenKh ?? "";
            //操作员：体检传入（调鼎 settleDctCode）优先，未传则取配置
            jo["caoZuoYuan"] = ValueOrConfig(info.CaoZuoYuan, "caoZuoYuan");
            jo["yuanQuId"] = zlConfiguration.GetAppSetting("yuanQuId");
            jo["yingYongId"] = zlConfiguration.GetAppSetting("yingYongId");
            jo["kaiDanKs"] = zlConfiguration.GetAppSetting("kaiDanKs");
            jo["dengJiLsh"] = info.DengJiLsh ?? "";
            jo["shouTuiBz"] = string.IsNullOrEmpty(info.ShouTuiBz) ? "1" : info.ShouTuiBz;

            var arr = new JArray();
            if (info.Items != null)
            {
                foreach (var it in info.Items)
                {
                    var d = new JObject();
                    d["feiYongMxId"] = it.FeiYongMxId ?? "";
                    d["feiYongId"] = it.FeiYongId ?? "";
                    d["shouFeiXmId"] = zlConfiguration.GetAppSetting("shouFeiXmId");
                    d["shouFeiXmMc"] = zlConfiguration.GetAppSetting("shouFeiXmMc");
                    d["shuLiang"] = string.IsNullOrEmpty(it.ShuLiang) ? "1" : it.ShuLiang;
                    //d["danJia"] = it.DanJia ?? "";
                    d["danJia"] = it.JieSuanJe ?? "";
                    d["jieSuanJe"] = it.JieSuanJe ?? "";
                    d["zhiXingKs"] = zlConfiguration.GetAppSetting("zhiXingKs");
                    d["zhiXingKsMc"] =  "";
                    arr.Add(d);
                }
            }
            jo["feiYongMxList"] = arr;
            return jo.ToString(Formatting.None);
        }

        /// <summary>XML传入值优先，为空时回退到配置文件</summary>
        private static string ValueOrConfig(string value, string configKey)
        {
            if (!string.IsNullOrEmpty(value)) return value;
            return zlConfiguration.GetAppSetting(configKey) ?? "";
        }

        /// <summary>获取东昉HIS的OAuth Token（client_credentials）</summary>
        private static string GetDfToken(string dfHttp)
        {
            string url = dfHttp + "/oauth/token?grant_type=client_credentials&client_id=" +
                         zlConfiguration.GetAppSetting("client_id") + "&client_secret=" +
                         zlConfiguration.GetAppSetting("client_secret");
            string outStr = HttpService.Post("", url, "", "POST");
            if (!string.IsNullOrEmpty(outStr) && outStr.IndexOf("access_token") > -1)
            {
                JObject jo = JObject.Parse(outStr);
                return jo["access_token"].Value<string>();
            }
            return "";
        }
    }
}
