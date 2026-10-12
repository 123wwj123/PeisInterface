using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using zlSoft.Common;

namespace zlSoft.Business
{
    /// <summary>
    /// 三方（调鼎）"2.1 体检系统查询检验结果" JSON 接口处理。
    ///
    /// 处理流程：
    /// 1. 入参 barcode（条码号）必填，startTime/endTime 为报告（样本登记）时间范围；
    /// 2. 按条码号调用羚云 LIS listChangGuiJG（ChaXunLX=1）查询常规检验结果；
    /// 3. 羚云每条结果明细（jieGuoXX）转换为一条三方记录；
    /// 4. 羚云未返回而三方又需要的字段（患者姓名、申请科室、标本时间等）一律留空。
    /// </summary>
    public class LisResultJson
    {
        // 三方返回码
        private const string CODE_SUCCESS = "1001";
        private const string CODE_ERROR = "9999";

        // startTime 为空时的默认查询跨度（天）
        private const int DEFAULT_QUERY_DAYS = 7;

        public static string Process(string requestJson)
        {
            string strBarCode = "";
            try
            {
                if (string.IsNullOrWhiteSpace(requestJson))
                {
                    return BuildErrorResponse("请求报文不能为空");
                }

                JObject request = JObject.Parse(requestJson);

                strBarCode = GetString(request, "barcode");
                if (string.IsNullOrEmpty(strBarCode))
                {
                    return BuildErrorResponse("barcode不能为空");
                }

                string strStartTime = GetString(request, "startTime");
                if (string.IsNullOrEmpty(strStartTime))
                {
                    strStartTime = DateTime.Now.AddDays(-DEFAULT_QUERY_DAYS).ToString("yyyy-MM-dd");
                }

                string strEndTime = GetString(request, "endTime");
                if (string.IsNullOrEmpty(strEndTime))
                {
                    strEndTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                }

                //1. 按条码号查询 LIS 结果
                string strMessage;
                JArray lisData = HisLisService.QueryResult(strBarCode, strStartTime, strEndTime, out strMessage);
                if (lisData == null)
                {
                    return BuildErrorResponse(string.IsNullOrEmpty(strMessage) ? "LIS结果查询失败" : strMessage);
                }

                //2. 羚云结果 -> 三方出参
                JArray dataArr = BuildData(request, lisData);

                return BuildResponse(CODE_SUCCESS, "成功", dataArr);
            }
            catch (JsonReaderException ex)
            {
                LogHelper.LogInfo("三方查询结果JSON解析异常：" + ex.Message);
                return BuildErrorResponse("请求报文格式错误：" + ex.Message);
            }
            catch (Exception ex)
            {
                LogHelper.LogInfo("三方检验结果查询接口异常，条码号=" + strBarCode + "，错误：" + ex.Message);
                return BuildErrorResponse("系统异常：" + ex.Message);
            }
        }

        /// <summary>
        /// 羚云 data（报告主体列表）-> 三方 data（每条结果明细一条记录）。
        /// 羚云字段与三方字段的对应关系见各赋值处；取不到的字段留空。
        /// </summary>
        private static JArray BuildData(JObject request, JArray lisData)
        {
            JArray arr = new JArray();

            foreach (JToken tReport in lisData)
            {
                if (tReport == null || tReport.Type != JTokenType.Object) continue;

                JObject report = (JObject)tReport;
                JToken jieGuoXX = report["jieGuoXX"];
                if (jieGuoXX == null || jieGuoXX.Type != JTokenType.Array) continue;

                foreach (JToken tItem in (JArray)jieGuoXX)
                {
                    if (tItem == null || tItem.Type != JTokenType.Object) continue;

                    JObject jieGuo = (JObject)tItem;
                    JObject row = new JObject();

                    //基础信息：回传入参或留空（羚云结果中无患者基本信息）
                    row["hospCode"] = GetString(request, "hospCode");
                    row["applyCode"] = GetStr(jieGuo, "shenQingDanID");   // 申请单号
                    row["alternateNo"] = GetString(request, "alternateNo");
                    row["checkCode"] = GetString(request, "checkCode");
                    row["name"] = "";           // 羚云未返回患者姓名
                    row["sex"] = "";
                    row["birth"] = "";
                    row["marriage"] = "";
                    row["idcardType"] = "";
                    row["idcard"] = "";
                    row["phone"] = "";
                    row["address"] = "";
                    row["applyDctCode"] = "";   // 羚云未返回开单医生
                    row["applyDctName"] = "";

                    //报告主体信息
                    row["barcode"] = GetStr(report, "tiaoMaHao");
                    row["applyDepartCode"] = "";                           // 申请科室：羚云未返回
                    row["applyDepartName"] = "";
                    //row["actDepartCode"] = GetStr(report, "jianYanJGID");  // 执行科室取检验机构
                    //row["actDepartName"] = GetStr(report, "jianYanJGMC");
                    row["actDepartCode"] = "001";  // 执行科室取检验机构
                    row["actDepartName"] = "检验科";

                    //项目信息
                    row["examitemCode"] = GetStr(jieGuo, "jianYanXMDM");
                    row["examitemName"] = GetStr(jieGuo, "jianYanXMMC");
                    row["itemCode"] = GetStr(jieGuo, "shiYanXM");
                    row["itemName"] = GetStr(jieGuo, "shiYanXMMC");

                    //标本时间：羚云未返回
                    row["sampleTime"] = "";
                    row["receiveSampleTime"] = "";

                    //结果信息
                    row["result"] = GetStr(jieGuo, "shiYanJG");
                    row["tips"] = MapTips(GetStr(jieGuo, "canKaoZT"), GetStr(jieGuo, "jingGaoZT"));
                    row["itemUnit"] = GetStr(jieGuo, "danWei");
                    row["refInterval"] = GetStr(jieGuo, "canKaoFW");
                    row["crisis"] = GetStr(jieGuo, "weiJiZT");

                    //检查/审核/报告：羚云无"报告医生"，以审核医生、审核时间代替
                    row["chkDctName"] = GetStr(jieGuo, "jianCeRXM");
                    row["chkTime"] = GetStr(jieGuo, "ceShiSJ");
                    row["auditDctName"] = GetStr(report, "shenHeRXM");
                    row["auditTime"] = GetStr(report, "shenHeSJ");
                    row["reportDctName"] = GetStr(report, "shenHeRXM");
                    row["reportTime"] = GetStr(report, "shenHeSJ");
                    row["updateTime"] = GetStr(report, "shenHeSJ");

                    arr.Add(row);
                }
            }

            return arr;
        }

        /// <summary>
        /// 异常提示：羚云参考状态 -> 三方 tips 标志。
        /// 三方标志：M 正常、H 偏高、HH 偏高报警、L 偏低、LL 偏低报警、P 阳性、Q 弱阳性、E 错误。
        /// 羚云 canKaoZT：N 正常、H 偏高、L 偏低、HH 高高、LL 底底、+ 阳性、- 阴性、
        ///                A 正常、B 偏高、C 偏低、D 阴性、E 阳性；jingGaoZT：1 警告 0 非警告。
        /// 若羚云直接返回的已是三方标志（H/HH/L/LL/P/Q/M），原样透传；E 一律按阳性处理转 P。
        /// </summary>
        private static string MapTips(string canKaoZT, string jingGaoZT)
        {
            string strZt = (canKaoZT ?? "").Trim().ToUpper();
            bool blnWarn = (jingGaoZT ?? "").Trim() == "1";   // 羚云警告状态

            switch (strZt)
            {
                //正常 / 阴性
                case "":
                case "N":
                case "A":
                case "D":
                case "-":
                case "M":
                    return "M";

                //偏高（警告时为偏高报警）
                case "H":
                case "B":
                    return blnWarn ? "HH" : "H";

                //偏低（警告时为偏低报警）
                case "L":
                case "C":
                    return blnWarn ? "LL" : "L";

                //已在报警区间的直接透传
                case "HH":
                    return "HH";
                case "LL":
                    return "LL";

                //阳性 / 弱阳性（羚云 E 表示阳性，一并转为 P）
                case "+":
                case "P":
                case "E":
                    return "P";
                case "Q":
                    return "Q";

                default:
                    return "M";
            }
        }

        private static string BuildResponse(string code, string msg, JArray data)
        {
            JObject response = new JObject();
            response["code"] = code;
            response["msg"] = msg ?? "";
            response["data"] = data ?? new JArray();
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

        /// <summary>取羚云返回字段（不 Trim，null 返回空串）</summary>
        private static string GetStr(JObject obj, string name)
        {
            if (obj == null || string.IsNullOrEmpty(name)) return "";
            JToken token = obj[name];
            if (token == null || token.Type == JTokenType.Null) return "";
            return token.ToString();
        }
    }
}
