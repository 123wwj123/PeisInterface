using System;
using System.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using zlSoft.Common;

namespace zlSoft.Business
{
    /// <summary>
    /// 三方（调鼎）"2.1 体检系统主动查询" JSON 接口处理（PACS）。
    ///
    /// 处理流程：
    /// 1. 入参 checkCode（体检号）、checkType（检查类型）必填，startTime/endTime 为报告时间范围；
    /// 2. 用 PacsResultQuerySql 按"体检号 + 检查类型"到 PACS 库查询报告；
    /// 3. 每一行查询结果直接作为一条三方记录返回：
    ///    检查所见(STUDYRESULT) -> resultContent，诊断意见(DIAGRESULT) -> result；
    /// 4. 胃镜、肠镜不再合并，各自单独一条返回。
    /// </summary>
    public class PacsResultJson
    {
        // 三方返回码
        private const string CODE_SUCCESS = "1001";
        private const string CODE_ERROR = "9999";

        // startTime 为空时的默认起始时间
        private const string DEFAULT_START_TIME = "1900-01-01 00:00:00";

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

                string strCheckType = GetString(request, "checkType");
                if (string.IsNullOrEmpty(strCheckType))
                {
                    return BuildErrorResponse("checkType不能为空");
                }

                string strStartTime = GetString(request, "startTime");
                if (string.IsNullOrEmpty(strStartTime)) strStartTime = DEFAULT_START_TIME;

                string strEndTime = GetString(request, "endTime");
                if (string.IsNullOrEmpty(strEndTime)) strEndTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                //REPORTDT 为字符类型，字符串比较时需补全时间部分，
                //否则"2026-09-23"会把"2026-09-23 09:00:00"排除掉
                strStartTime = PadTime(strStartTime, " 00:00:00");
                strEndTime = PadTime(strEndTime, " 23:59:59");

                //1. 按体检号 + 检查类型查询 PACS 报告
                DataTable dt = QueryPacsReport(strCheckCode, strCheckType, strStartTime, strEndTime);

                //2. PACS 查询结果 -> 三方出参（逐条返回，不做胃镜/肠镜合并）
                JArray dataArr = BuildData(request, dt);

                return BuildResponse(CODE_SUCCESS, "成功", dataArr);
            }
            catch (JsonReaderException ex)
            {
                LogHelper.LogInfo("三方PACS结果查询JSON解析异常：" + ex.Message);
                return BuildErrorResponse("请求报文格式错误：" + ex.Message);
            }
            catch (Exception ex)
            {
                LogHelper.LogInfo("三方PACS结果查询接口异常，体检号=" + strCheckCode + "，错误：" + ex.Message);
                return BuildErrorResponse("系统异常：" + ex.Message);
            }
        }

        /// <summary>时间字符串不足 19 位（只有日期）时补全时间部分，保证字符比较正确</summary>
        private static string PadTime(string strTime, string strSuffix)
        {
            if (string.IsNullOrEmpty(strTime)) return strTime;
            strTime = strTime.Trim();
            if (strTime.Length >= 19) return strTime;
            if (strTime.Contains(" ")) return strTime;   // 已带时间
            return strTime + strSuffix;
        }

        /// <summary>按体检号 + 检查类型查询 PACS 库报告</summary>
        private static DataTable QueryPacsReport(string strCheckCode, string strCheckType, string strStartTime, string strEndTime)
        {
            DataTable dt = new DataTable();

            string strPacsType = zlConfiguration.GetAppSetting("PacsType");
            string strPacsConn = zlConfiguration.GetAppSetting("PacsConn");
            string strSql = zlConfiguration.GetAppSetting("PacsResultQuerySql");

            if (string.IsNullOrEmpty(strSql))
            {
                throw new Exception("未配置PACS结果查询语句【PacsResultQuerySql】");
            }

            //与 HisPacsService 保持一致：PACS 的 DR 以 DX 存储
            string strModality = (strCheckType ?? "").Replace("DR", "DX");

            zlCommand cmdPacs = new zlCommand(strPacsType, strPacsConn, "");
            try
            {
                cmdPacs.SetSQL(strPacsType, strSql, strCheckCode, strModality, strStartTime, strEndTime);

                //记录最终下发的 SQL（占位符已替换为 :PARn，参数值见后面日志），便于排查
                string strLogSql = strPacsType == "Oracle"
                    ? cmdPacs.GetCommandText()
                    : cmdPacs.GetSqlCommand().CommandText;
                LogHelper.LogInfo("PACS结果查询，体检号=" + strCheckCode +
                                  "，检查类型=" + strModality +
                                  "，开始时间=" + strStartTime +
                                  "，结束时间=" + strEndTime +
                                  "，SQL=" + strLogSql);

                zlDataReader drPacs = new zlDataReader();
                drPacs.SetReader(strPacsType, cmdPacs.ExecuteReader(strPacsType));

                System.Data.Common.DbDataReader rawReader = null;
                if (strPacsType == "Oracle")
                    rawReader = drPacs.GetOracleDataReader();
                else if (strPacsType == "SqlServer")
                    rawReader = drPacs.GetSqlDataReader();

                if (rawReader != null && rawReader.HasRows)
                {
                    dt.Load(rawReader);
                }
                drPacs.Close(strPacsType);

                LogHelper.LogInfo("PACS结果查询完成，体检号=" + strCheckCode +
                                  "，检查类型=" + strModality +
                                  "，返回 " + dt.Rows.Count + " 条报告");
            }
            finally
            {
                try { cmdPacs.Close(strPacsType); }
                catch { }
            }

            return dt;
        }

        /// <summary>
        /// PACS 查询结果 -> 三方 data（每行一条，胃镜/肠镜不再合并）。
        /// 检查所见 -> resultContent；诊断意见 -> result。
        /// </summary>
        private static JArray BuildData(JObject request, DataTable dt)
        {
            JArray arr = new JArray();
            if (dt == null) return arr;

            string strDeptCode = GetConfig("PDeptID");
            string strDeptName = GetConfig("PDeptName");

            foreach (DataRow row in dt.Rows)
            {
                JObject jo = new JObject();

                jo["hospCode"] = GetString(request, "hospCode");
                jo["applyCode"] = Fld(row, "申请单号");
                jo["alternateNo"] = Fld(row, "病人id");
                jo["checkCode"] = Fld(row, "体检号");
                jo["name"] = Fld(row, "姓名");
                jo["checkType"] = Fld(row, "检查方式编码");

                jo["applyDepartCode"] = strDeptCode;
                jo["applyDepartName"] = strDeptName;
                //PACS 未返回执行科室，沿用体检科室配置
                jo["actDepartCode"] = strDeptCode;
                jo["actDepartName"] = strDeptName;

                jo["examitemCode"] = Fld(row, "examitemId");
                jo["examitemName"] = Fld(row, "项目名称");
                jo["interfaceCode"] = Fld(row, "电子申请单号");
                //无明细项目时使用体检项目编码/名称
                jo["itemCode"] = Fld(row, "examitemId");
                jo["itemName"] = Fld(row, "项目名称");

                //诊断意见 -> result；检查所见 -> resultContent
                jo["result"] = Fld(row, "诊断意见");
                jo["resultContent"] = Fld(row, "检查所见");
                jo["resultImagePath"] = Fld(row, "报告图片");

                //异常提示：阳性 1，阴性 0
                jo["tips"] = MapTips(Fld(row, "检查结果"));
                jo["bodyPartName"] = "";   // PACS 未返回检查部位

                jo["chkDctName"] = Fld(row, "检查人");
                jo["chkTime"] = Fld(row, "检查时间");
                jo["auditDctName"] = Fld(row, "审核人");
                jo["auditTime"] = Fld(row, "审核时间");
                jo["reportDctName"] = Fld(row, "检查人");
                jo["reportTime"] = Fld(row, "检查时间");
                jo["updateTime"] = Fld(row, "审核时间");

                arr.Add(jo);
            }

            return arr;
        }

        /// <summary>异常提示：检查结果（阴性/阳性）-> tips（0/1）</summary>
        private static string MapTips(string strResult)
        {
            return (strResult ?? "").Trim() == "阳性" ? "1" : "0";
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

        private static string GetConfig(string key)
        {
            try
            {
                string v = zlConfiguration.GetAppSetting(key);
                return v ?? "";
            }
            catch
            {
                return "";
            }
        }

        private static string GetString(JObject obj, string name)
        {
            if (obj == null || string.IsNullOrEmpty(name)) return "";
            JToken token = obj[name];
            if (token == null || token.Type == JTokenType.Null) return "";
            return token.ToString().Trim();
        }

        /// <summary>安全读取 DataRow 字段（列不存在或为 NULL 时返回空串）</summary>
        private static string Fld(DataRow row, string col)
        {
            if (row == null || !row.Table.Columns.Contains(col)) return "";
            object v = row[col];
            return (v == null || v == DBNull.Value) ? "" : v.ToString();
        }
    }
}
