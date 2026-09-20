using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using zlSoft.Common;

namespace zlSoft.Business
{
    /// <summary>
    /// HIS患者建档业务服务（从中间表模式解耦出来的可复用逻辑）。
    /// 入参是强类型 PatientInfo（可来自API请求，也可来自其它来源），
    /// 内部完成：查是否已建档 -> 已建档补卡 / 未建档建档 -> 返回结果。
    /// 本方法不读写体检中间库。
    /// </summary>
    public class HisPatientService
    {
        public static RegisterResult Register(PatientInfo info)
        {
            var result = new RegisterResult
            {
                PhysicalNo = info != null ? info.PhysicalNo : "",
                Success = false
            };

            if (info == null)
            {
                result.Message = "患者信息为空";
                return result;
            }

            try
            {
                string dfHttp = zlConfiguration.GetAppSetting("DFHttp");
                string token = GetDfToken(dfHttp);
                if (string.IsNullOrEmpty(token))
                {
                    result.Message = "获取东昉Token失败";
                    LogHelper.LogInfo("患者建档失败：获取东昉Token失败！体检号=" + info.PhysicalNo);
                    return result;
                }

                string bingRenId = "";
                string jiuZhenKh = "";
                string strUrl, strIn, strOut;
                JObject jobj;

                #region 1. 按身份证号查询是否已建档
                if (!string.IsNullOrEmpty(info.IdNumber))
                {
                    strUrl = dfHttp + "/bingRenXx/getJkBingRenXxByConditions";
                    strIn = "{\"shenFenZh\":\"" + info.IdNumber + "\"}";
                    strOut = HttpService.Post(strIn, strUrl, token, "POST");
                    jobj = JObject.Parse(strOut);
                    if (jobj["returnCode"] != null && jobj["returnCode"].Value<string>() == "1")
                    {
                        JObject data = JObject.Parse(jobj["returnData"].ToString());
                        string existId = data["bingRenId"] != null ? data["bingRenId"].Value<string>() : "";
                        if (!string.IsNullOrEmpty(existId))
                        {
                            // 姓名一致性校验（沿用原逻辑）
                            string hisName = data["xingMing"] != null ? data["xingMing"].Value<string>() : "";
                            if (!string.IsNullOrEmpty(info.PatName) && !info.PatName.Equals(hisName))
                            {
                                result.Message = "身份证号【" + info.IdNumber + "】在HIS中对应姓名【" + hisName +
                                                 "】与传入姓名【" + info.PatName + "】不一致";
                                LogHelper.LogInfo("体检号【" + info.PhysicalNo + "】" + result.Message);
                                return result;
                            }

                            bingRenId = existId;
                            jiuZhenKh = data["jiuZhenKh"] != null ? data["jiuZhenKh"].Value<string>() : "";

                            // 已建档 -> 用体检号做新卡号补卡（skip项：如不需要可删除本段）
                            if (!string.IsNullOrEmpty(info.PhysicalNo))
                            {
                                jiuZhenKh = info.PhysicalNo;
                                strUrl = dfHttp + "/menZhenJz/buKa?bingRenId=" + bingRenId +
                                 "&kaiLeiXing=2&xinKaHao=" + info.PhysicalNo +
                                 "&jiuZhenKh=&caoZuoYuan=" + zlConfiguration.GetAppSetting("caoZuoYuan") + "&geRenBh=";
                                HttpService.Post("", strUrl, token, "POST");
                            }
                        }
                    }
                }
                #endregion

                #region 2. 未建档 -> 建档
                if (string.IsNullOrEmpty(bingRenId))
                {
                    strIn = BuildJianDangJson(info);
                    strUrl = dfHttp + "/menZhenJz/jianDang";
                    strOut = HttpService.Post(strIn, strUrl, token, "POST");
                    jobj = JObject.Parse(strOut);
                    if (jobj["returnCode"] != null && jobj["returnCode"].Value<string>() == "1")
                    {
                        JObject data = JObject.Parse(jobj["returnData"].ToString());
                        bingRenId = data["bingRenId"] != null ? data["bingRenId"].Value<string>() : "";
                        jiuZhenKh = data["jiuZhenKh"] != null ? data["jiuZhenKh"].Value<string>() : "";
                    }
                    else
                    {
                        result.Message = "建档失败：" + (jobj["returnMessage"] != null ? jobj["returnMessage"].Value<string>() : strOut);
                        LogHelper.LogInfo("体检号【" + info.PhysicalNo + "--" + info.PatName + "】" + result.Message);
                        return result;
                    }
                }
                #endregion

                if (string.IsNullOrEmpty(bingRenId))
                {
                    result.Message = "建档失败：未获取到bingRenId";
                    return result;
                }

                result.Success = true;
                result.Message = "成功";
                result.BingRenId = bingRenId;
                result.JiuZhenKh = jiuZhenKh;
                return result;
            }
            catch (Exception ex)
            {
                LogHelper.LogInfo("患者建档API异常，体检号=" + info.PhysicalNo + "，错误：" + ex.Message);
                result.Success = false;
                result.Message = "系统异常：" + ex.Message;
                return result;
            }
        }

        /// <summary>组装HIS建档报文（沿用原 ClsSyncFee 的字段结构，数据改为来自API入参）</summary>
        private static string BuildJianDangJson(PatientInfo info)
        {
            var jo = new JObject();
            jo["jiuZhenKh"] = info.PhysicalNo ?? "";     // 体检号当就诊卡号
            jo["kaiLeiXing"] = "4";
            jo["xingMing"] = info.PatName ?? "";
            jo["xingBie"] = MapGender(info.GenderCode); // 性别代码映射到HIS
            jo["shenFenZh"] = info.IdNumber ?? "";
            jo["danWeiBh"] = "";
            jo["chuShengRq"] = info.DateBirth ?? "";     // 体检直接传出生日期，无需再从身份证解析
            jo["lianXiDz"] = info.AddrDesc ?? "";
            jo["lianXiDh"] = info.PhoneNo ?? "";
            jo["feiYongLb"] = "";
            jo["feiYongXz"] = "";
            jo["jiLuLy"] = "3";
            jo["caoZuoYuan"] = zlConfiguration.GetAppSetting("caoZuoYuan"); // 用配置文件里的操作员
            jo["chongZhiJe"] = "";
            jo["yiBaoKh"] = "";
            jo["geRenBh"] = "";
            jo["yiBaoBrXx"] = "";
            jo["gongZuoDw"] = info.CompanyName ?? "";
            jo["canBaoXzMc"] = "";
            jo["muLuBlLb"] = "";
            jo["kunNanJzDj"] = "";
            jo["yiLiaoLb"] = "";
            jo["minZuDm"] =  "";
            jo["minZuMc"] =  "";
            return jo.ToString(Formatting.None);
        }

        /// <summary>
        /// 性别代码映射：体检 gender_code(GB/T 2261.1: 1男/2女) -> HIS性别码。
        /// 现两侧默认一致，直接透传；若HIS性别码不同，在此调整映射。
        /// </summary>
        private static string MapGender(string genderCode)
        {
            if (string.IsNullOrEmpty(genderCode)) return "";
            switch (genderCode.Trim())
            {
                case "1": return "男"; // 男
                case "2": return "女"; // 女
                case "0": return "女"; // 女
                default: return genderCode.Trim();
            }
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
