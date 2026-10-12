using System;
using System.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using zlSoft.Common;

namespace zlSoft.Business
{
    /// <summary>
    /// PACS 项目状态回传（调鼎 PACS 接口 3. 状态回传）。
    ///
    /// 定时任务：用 PacsStatusSql 查询 PACS 库已审核（OrderStatus in 1170/1180 且队列未回传）的报告，
    /// 逐条调用体检系统 checkStatusInfoUpdate 通知"已审核"（state=8），
    /// 调用成功后更新 T_Report_Queue.TJ_Report_Flag='Y' 避免下次重复回传。
    /// </summary>
    public class ClsSendPacsStatus
    {
        /// <summary>状态码：8-审核</summary>
        private const string STATUS_AUDITED = "8";

        /// <summary>三方成功返回码</summary>
        private const string CODE_SUCCESS = "1001";

        /// <summary>状态回传：已审核（首次审核）报告，使用 PacsStatusSql</summary>
        public static void SendPacsStatus()
        {
            SendStatusBySql("PacsStatusSql");
        }

        /// <summary>状态回传：二次审核（重新审核）报告，使用 PacsStatusAgainSql</summary>
        public static void SendPacsStatusAgain()
        {
            SendStatusBySql("PacsStatusAgainSql");
        }

        /// <summary>
        /// 按指定配置的查询Sql，逐条回传"已审核"（state=8）状态到体检系统。
        /// </summary>
        private static void SendStatusBySql(string strSqlKey)
        {
            string strOrderSeq = "";
            try
            {
                string strUrl = GetConfig("TjPacsStatusUrl");
                if (string.IsNullOrEmpty(strUrl))
                {
                    LogHelper.LogInfo("PACS状态回传跳过：未配置 TjPacsStatusUrl");
                    return;
                }

                string strSql = GetConfig(strSqlKey);
                if (string.IsNullOrEmpty(strSql))
                {
                    LogHelper.LogInfo("PACS状态回传跳过：未配置 " + strSqlKey);
                    return;
                }

                string strPacsType = zlConfiguration.GetAppSetting("PacsType");
                string strPacsConn = zlConfiguration.GetAppSetting("PacsConn");
                string strHospCode = GetConfig("TjHospCode");

                DataTable dt = QueryPacsStatus(strPacsType, strPacsConn, strSql);
                if (dt == null || dt.Rows.Count == 0) return;

                zlCommand cmdPacs = new zlCommand(strPacsType, strPacsConn, "");
                try
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        strOrderSeq = Fld(row, "OrderID");
                        string strApplyNo = Fld(row, "申请单号");
                        string strCheckCode = Fld(row, "体检号");

                        if (string.IsNullOrEmpty(strCheckCode))
                        {
                            LogHelper.LogInfo("PACS状态回传跳过：体检号为空，申请单号=" + strApplyNo);
                            continue;
                        }

                        try
                        {
                            string strIn = BuildStatusJson(strHospCode, strCheckCode, strApplyNo);
                            string strOut = HttpService.PostJson(strIn, strUrl);

                            if (IsSuccess(strOut))
                            {
                                //回传成功：标记队列，避免下次重复回传
                                UpdateQueueFlag(cmdPacs, strPacsType, strOrderSeq);
                            }
                            else
                            {
                                LogHelper.LogInfo("PACS状态回传失败，体检号=" + strCheckCode +
                                                  "，申请单号=" + strApplyNo + "，返回：" + strOut);
                            }
                        }
                        catch (Exception ex)
                        {
                            LogHelper.LogInfo("PACS状态回传异常，体检号=" + strCheckCode +
                                              "，申请单号=" + strApplyNo + "，" + ex.Message);
                        }
                    }
                }
                finally
                {
                    try { cmdPacs.Close(strPacsType); }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogInfo("PACS状态回传任务异常，OrderID=" + strOrderSeq + "，" + ex.Message);
            }
        }

        /// <summary>查询 PACS 库待回传状态的报告</summary>
        private static DataTable QueryPacsStatus(string strPacsType, string strPacsConn, string strSql)
        {
            DataTable dt = new DataTable();

            zlCommand cmdPacs = new zlCommand(strPacsType, strPacsConn, "");
            try
            {
                cmdPacs.SetSQL(strPacsType, strSql);

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
            }
            finally
            {
                try { cmdPacs.Close(strPacsType); }
                catch { }
            }

            return dt;
        }

        /// <summary>组装 checkStatusInfoUpdate 报文</summary>
        private static string BuildStatusJson(string strHospCode, string strCheckCode, string strApplyNo)
        {
            JObject jo = new JObject();
            jo["hospCode"] = strHospCode ?? "";
            jo["checkCode"] = strCheckCode ?? "";
            jo["applyCode"] = strApplyNo ?? "";
            jo["state"] = STATUS_AUDITED;
            return jo.ToString(Formatting.None);
        }

        /// <summary>三方返回 code=1001 视为成功</summary>
        private static bool IsSuccess(string strOut)
        {
            if (string.IsNullOrEmpty(strOut)) return false;

            try
            {
                JObject jobj = JObject.Parse(strOut);
                JToken code = jobj["code"];
                if (code == null || code.Type == JTokenType.Null) return false;
                return code.ToString().Trim() == CODE_SUCCESS;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 回传成功后更新 PACS 端队列标志，避免下次重复回传。
        /// </summary>
        private static void UpdateQueueFlag(zlCommand cmdPacs, string strPacsType, string orderSeq)
        {
            if (string.IsNullOrEmpty(orderSeq)) return;
            string strSql = "Update T_Report_Queue Set TJ_Report_Flag = 'Y' Where OrderID = '" + orderSeq + "'";
            cmdPacs.SetSQL(strPacsType, strSql);
            cmdPacs.ExecuteNonQuery(strPacsType);
        }

        private static string GetConfig(string key)
        {
            try
            {
                return zlConfiguration.GetAppSetting(key) ?? "";
            }
            catch
            {
                return "";
            }
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
