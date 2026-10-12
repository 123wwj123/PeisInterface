using System;
using System.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using zlSoft.Common;

namespace zlSoft.Business
{
    /// <summary>
    /// 检验医嘱状态回传（调鼎 LIS 接口 3. 医嘱状态回传）。
    ///
    /// 定时任务：用 LisStatusSql 查询 LIS 库已审核（JIANYANZT&gt;=150 且未回传）的检验申请，
    /// 逐条调用体检系统 examStatusInfoUpdate 通知"结果完成"（state=3），
    /// 调用成功后将 Lcb_Jianyansqd.DAOCHUBZ 置 1，避免重复上传。
    /// </summary>
    public class ClsSendLisStatus
    {
        /// <summary>状态码：3-结果完成（审核过）</summary>
        private const string STATUS_FINISHED = "3";

        /// <summary>状态码：4-结果二次更改（重新审核）</summary>
        private const string STATUS_RECHECK = "3";

        /// <summary>接口类型：LIS</summary>
        private const string APPLY_TYPE_LIS = "LIS";

        /// <summary>三方成功返回码</summary>
        private const string CODE_SUCCESS = "1001";

        /// <summary>状态回传：已审核（首次）检验申请，使用 LisStatusSql / LisStatusState</summary>
        public static void SendLisStatus()
        {
            SendStatusBySql("LisStatusSql", "LisStatusState", STATUS_FINISHED, false);
        }

        /// <summary>状态回传：二次审核（重新审核）检验申请，使用 LisStatusAgainSql / LisStatusAgainState</summary>
        public static void SendLisStatusAgain()
        {
            SendStatusBySql("LisStatusAgainSql", "LisStatusAgainState", STATUS_RECHECK, true);
        }

        /// <summary>
        /// 按指定配置查询并回传检验医嘱状态。
        /// </summary>
        /// <param name="strSqlKey">查询Sql配置键</param>
        /// <param name="strStateKey">状态码配置键</param>
        /// <param name="strDefaultState">缺省状态码</param>
        /// <param name="blnAgain">是否二次审核（更新时写 ERCISHJ 用于下次去重）</param>
        private static void SendStatusBySql(string strSqlKey, string strStateKey, string strDefaultState, bool blnAgain)
        {
            string strApplyNo = "";
            try
            {
                string strUrl = GetConfig("TjLisStatusUrl");
                if (string.IsNullOrEmpty(strUrl))
                {
                    LogHelper.LogInfo("检验状态回传跳过：未配置 TjLisStatusUrl");
                    return;
                }

                string strSql = GetConfig(strSqlKey);
                if (string.IsNullOrEmpty(strSql))
                {
                    LogHelper.LogInfo("检验状态回传跳过：未配置 " + strSqlKey);
                    return;
                }

                //状态码可配置，未配置时使用缺省值
                string strState = GetConfig(strStateKey);
                if (string.IsNullOrEmpty(strState)) strState = strDefaultState;

                string strLisType = zlConfiguration.GetAppSetting("LisType");
                string strLisConn = zlConfiguration.GetAppSetting("LisConn");
                string strHospCode = GetConfig("TjHospCode");

                DataTable dt = QueryLisStatus(strLisType, strLisConn, strSql);
                if (dt == null || dt.Rows.Count == 0) return;

                zlCommand cmdLis = new zlCommand(strLisType, strLisConn, "");
                try
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        strApplyNo = Fld(row, "申请单号");
                        string strItemCode = Fld(row, "项目代码");
                        string strCheckCode = Fld(row, "体检号");
                        string strBarCode = Fld(row, "样本条码");
                        string strPatId = Fld(row, "患者ID");
                        string strOperaTime = Fld(row, "操作时间");

                        //checkCode 必填；条码号与体检号二选一，此处两者都传
                        if (string.IsNullOrEmpty(strCheckCode))
                        {
                            LogHelper.LogInfo("检验状态回传跳过：体检号为空，申请单号=" + strApplyNo);
                            continue;
                        }

                        try
                        {
                            string strIn = BuildStatusJson(strHospCode, strCheckCode, strBarCode,
                                                           strApplyNo, strPatId, strOperaTime, strState);
                            string strOut = HttpService.PostJson(strIn, strUrl);

                            if (IsSuccess(strOut))
                            {
                                //回传成功：DAOCHUBZ 置 1，避免重复上传
                                UpdateDaoChuBz(cmdLis, strLisType, strApplyNo, strItemCode, blnAgain);
                            }
                            else
                            {
                                LogHelper.LogInfo("检验状态回传失败，申请单号=" + strApplyNo +
                                                  "，项目代码=" + strItemCode + "，返回：" + strOut);
                            }
                        }
                        catch (Exception ex)
                        {
                            LogHelper.LogInfo("检验状态回传异常，申请单号=" + strApplyNo +
                                              "，项目代码=" + strItemCode + "，" + ex.Message);
                        }
                    }
                }
                finally
                {
                    try { cmdLis.Close(strLisType); }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogInfo("检验状态回传任务异常，申请单号=" + strApplyNo + "，" + ex.Message);
            }
        }

        /// <summary>查询 LIS 库待回传状态的检验申请</summary>
        private static DataTable QueryLisStatus(string strLisType, string strLisConn, string strSql)
        {
            DataTable dt = new DataTable();

            zlCommand cmdLis = new zlCommand(strLisType, strLisConn, "");
            try
            {
                cmdLis.SetSQL(strLisType, strSql);

                zlDataReader drLis = new zlDataReader();
                drLis.SetReader(strLisType, cmdLis.ExecuteReader(strLisType));

                System.Data.Common.DbDataReader rawReader = null;
                if (strLisType == "Oracle")
                    rawReader = drLis.GetOracleDataReader();
                else if (strLisType == "SqlServer")
                    rawReader = drLis.GetSqlDataReader();

                if (rawReader != null && rawReader.HasRows)
                {
                    dt.Load(rawReader);
                }
                drLis.Close(strLisType);
            }
            finally
            {
                try { cmdLis.Close(strLisType); }
                catch { }
            }

            return dt;
        }

        /// <summary>组装 examStatusInfoUpdate 报文（applyCode 为数组）</summary>
        private static string BuildStatusJson(string strHospCode, string strCheckCode, string strBarCode,
                                              string strApplyNo, string strPatId, string strOperaTime, string strState)
        {
            if (string.IsNullOrEmpty(strOperaTime))
            {
                strOperaTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            }

            JArray arrApplyCode = new JArray();
            if (!string.IsNullOrEmpty(strApplyNo)) arrApplyCode.Add(strApplyNo);

            JObject jo = new JObject();
            jo["hospCode"] = strHospCode ?? "";
            jo["applyCode"] = arrApplyCode;
            jo["checkCode"] = strCheckCode ?? "";
            jo["barcode"] = strBarCode ?? "";
            jo["alternateNo"] = strPatId ?? "";
            jo["operaTime"] = strOperaTime ?? "";
            jo["actDctName"] = "";
            jo["state"] = strState ?? "";
            jo["applyType"] = APPLY_TYPE_LIS;
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
        /// 回传成功：将 Lcb_Jianyansqd 的 DAOCHUBZ 改为 1，避免重复上传。
        /// </summary>
        private static void UpdateDaoChuBz(zlCommand cmdLis, string strLisType, string strApplyNo,
                                           string strItemCode, bool blnAgain)
        {
            if (string.IsNullOrEmpty(strApplyNo)) return;

            string strSql;
            if (blnAgain)
            {
                //二次审核回传成功：改表状态并记录重传时间，用于下次去重
                strSql = "Update Lcb_Jianyansqd Set DAOCHUBZ = 1, ERCISHJ = sysdate Where shenqingdanid = '" + strApplyNo + "'";
            }
            else
            {
                strSql = "Update Lcb_Jianyansqd Set DAOCHUBZ = 1 Where shenqingdanid = '" + strApplyNo +
                         "' And jianyanxmdm = '" + strItemCode + "'";
            }
            cmdLis.SetSQL(strLisType, strSql);
            cmdLis.ExecuteNonQuery(strLisType);

            LogHelper.LogInfo("申请单【" + strApplyNo + "】检验状态回传成功" +
                              (blnAgain ? "（二次审核），ERCISHJ已置sysdate" : "，DAOCHUBZ已置1"));
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
