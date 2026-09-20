using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using zlSoft.Common;

namespace zlSoft.Business
{
    /// <summary>
    /// 二次审核检验报告重新回传：查已回传过(DAOCHUBZ=1)且近期检测的报告，
    /// 如果样本存在二次审核记录(LCB_YANGBENXXQXSHJL)，则重新获取结果、组装XML并回传体检。
    /// </summary>
    public class ClsGetLisResultAgain
    {
        /// <summary>
        /// 从LIS系统重新回传二次审核的检验报告
        /// </summary>
        /// <param name="cmd">中间库连接(保留兼容，本流程不再使用)</param>
        /// <param name="strMDDataType">中间库类型</param>
        /// <param name="strToken">业务Token</param>
        public static void GetLisResult(zlCommand cmd, string strMDDataType, string strToken)
        {
            #region 定义变量
            string strSql;
            string strOut;
            string ResultFileUrl = "";
            string strLisType = zlConfiguration.GetAppSetting("LisType");
            string strLisConn = zlConfiguration.GetAppSetting("LisConn");
            zlCommand cmdLis = new zlCommand(strLisType, strLisConn, "");
            zlDataReader drLis = new zlDataReader();
            zlCommand cmdPacs = new zlCommand(strLisType, strLisConn, "");
            zlDataReader drPacs = new zlDataReader();
            JObject jobjResult;
            string pdfCodesCfg = zlConfiguration.GetAppSetting("LisReportPdfItemCodes");
            if (string.IsNullOrEmpty(pdfCodesCfg)) pdfCodesCfg = "4721039,4721040,4721041";
            HashSet<string> pdfCodes = new HashSet<string>(pdfCodesCfg.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            string fileHost = zlConfiguration.GetAppSetting("LisReportFileHost");
            if (string.IsNullOrEmpty(fileHost)) fileHost = "http://10.112.1.124:8891/";
            #endregion

            try
            {
                #region 从LIS库查询待重新回传的检验报告
                strSql = zlConfiguration.GetAppSetting("LisResultAgainSql");
                cmdLis.SetSQL(strLisType, strSql);
                drLis.SetReader(strLisType, cmdLis.ExecuteReader(strLisType));
                if (!drLis.HasRows(strLisType))
                {
                    drLis.Close(strLisType);
                    return;
                }

                DataTable dt = new DataTable();
                DataRow drList;
                dt.Columns.Add("样本条码");
                dt.Columns.Add("申请单号");
                dt.Columns.Add("申请时间");
                dt.Columns.Add("项目代码");
                dt.Columns.Add("患者ID");
                dt.Columns.Add("患者姓名");
                dt.Columns.Add("性别代码");
                dt.Columns.Add("性别名称");
                dt.Columns.Add("证件号");
                dt.Columns.Add("出生日期");
                dt.Columns.Add("年龄");
                dt.Columns.Add("体检号");
                while (drLis.Read(strLisType))
                {
                    drList = dt.NewRow();
                    drList["样本条码"] = SafeGet(drLis, strLisType, "样本条码");
                    drList["申请单号"] = SafeGet(drLis, strLisType, "申请单号");
                    drList["申请时间"] = SafeGet(drLis, strLisType, "申请时间");
                    drList["项目代码"] = SafeGet(drLis, strLisType, "项目代码");
                    drList["患者ID"] = SafeGet(drLis, strLisType, "患者ID");
                    drList["患者姓名"] = SafeGet(drLis, strLisType, "患者姓名");
                    drList["性别代码"] = SafeGet(drLis, strLisType, "性别代码");
                    drList["性别名称"] = SafeGet(drLis, strLisType, "性别名称");
                    drList["证件号"] = SafeGet(drLis, strLisType, "证件号");
                    drList["出生日期"] = SafeGet(drLis, strLisType, "出生日期");
                    drList["年龄"] = SafeGet(drLis, strLisType, "年龄");
                    drList["体检号"] = SafeGet(drLis, strLisType, "体检号");
                    dt.Rows.Add(drList);
                }
                drLis.Close(strLisType);
                #endregion

                #region 逐条判断二次审核、重新获取结果并回传体检
                string strUrl = zlConfiguration.GetAppSetting("LisResultUrl");

                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    try
                    {
                        //二次审核及去重已由LisResultAgainSql的 exists(LCB_YANGBENXXQXSHJL.SHENHESJ > nvl(ERCISHJ,jiancesj)) 完成
                        //重新获取LIS结果
                        string strGetUrl = strUrl + "?ChaXunLX=1&ChaXunMa=" + dt.Rows[i]["样本条码"].ToString() + "&kaiShiSJ=" + dt.Rows[i]["申请时间"].ToString() + "&jieShuSJ=" + System.DateTime.Now.ToString("yyyy-MM-dd") + "&JiGouID=" + zlConfiguration.GetAppSetting("LisOrgID");
                        strOut = HttpService.Post("", strGetUrl, strToken, "GET");

                        jobjResult = JObject.Parse(strOut);
                        if (jobjResult["code"].ToString() == "0")
                        {
                            JArray jarData = JArray.Parse(jobjResult["data"].ToString());
                            bool sent = false;
                            foreach (var varData in jarData)
                            {
                                JObject jobjData = JObject.Parse(varData.ToString());
                                JArray jarJieGuo = JArray.Parse(jobjData["jieGuoXX"].ToString());

                                List<JObject> jieGuoItems = new List<JObject>();
                                foreach (var varJieGuo in jarJieGuo)
                                {
                                    JObject jobjJieGuo = JObject.Parse(varJieGuo.ToString());
                                    string sqdId = jobjJieGuo["shenQingDanID"] == null ? "" : jobjJieGuo["shenQingDanID"].ToString();
                                    if (sqdId == dt.Rows[i]["申请单号"].ToString() || string.IsNullOrEmpty(sqdId))
                                    {
                                        jieGuoItems.Add(jobjJieGuo);
                                    }
                                }

                                if (jieGuoItems.Count == 0) continue;

                                ResultFileUrl = "";
                                if (pdfCodes.Contains(dt.Rows[i]["项目代码"].ToString()))
                                {
                                    string pdfSql = "select * from adk_pdfreportfile where tiaomahao= '" + dt.Rows[i]["样本条码"].ToString() + "'";
                                    cmdPacs.SetSQL(strLisType, pdfSql);
                                    drPacs.SetReader(strLisType, cmdPacs.ExecuteReader(strLisType));
                                    if (drPacs.HasRows(strLisType) && drPacs.Read(strLisType))
                                    {
                                        string result = drPacs.Get(strLisType, "PATH").ToString();
                                        if (!string.IsNullOrEmpty(result))
                                        {
                                            ResultFileUrl = fileHost + result.Replace('\\', '/');
                                        }
                                    }
                                    drPacs.Close(strLisType);
                                }

                                bool ok = ClsSendTjLisReport.SendReport(jobjData, jieGuoItems, dt.Rows[i], ResultFileUrl);
                                if (ok) sent = true;
                            }

                            //二次审核回传成功，改表状态(从LIS表改) + 记录重传时间用于下次去重
                            if (sent)
                            {
                                //strSql = "Update Lcb_Jianyansqd Set DAOCHUBZ = 1, ERCISHJ = to_date('"+ System.DateTime.Now + "','YYYY-MM-DD HH24:MI:SS') Where shenqingdanid = '" + dt.Rows[i]["申请单号"].ToString() + "'";
                                strSql = "Update Lcb_Jianyansqd Set DAOCHUBZ = 1, ERCISHJ = sysdate Where shenqingdanid = '" + dt.Rows[i]["申请单号"].ToString() + "'";
                                cmdLis.SetSQL(strLisType, strSql);
                                cmdLis.ExecuteNonQuery(strLisType);
                                LogHelper.LogInfo("申请单【" + dt.Rows[i]["申请单号"] + "】二次审核报告重新回传成功");
                            }
                        }
                        else
                        {
                            if (!jobjResult["message"].ToString().Contains("未找到报告数据") && !jobjResult["message"].ToString().Contains("未找到申请单号对应数据"))
                            {
                                LogHelper.LogInfo("申请单号【" + dt.Rows[i]["申请单号"].ToString() + "】的LIS结果获取失败，" + jobjResult["message"].ToString());
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.LogInfo("申请单号【" + dt.Rows[i]["申请单号"].ToString() + "】的二次审核报告回传异常，" + ex.Message);
                    }
                }
                #endregion
            }
            catch (Exception ex)
            {
                drLis.Close(strLisType);
                LogHelper.LogInfo("LIS二次审核报告回传异常，" + ex.Message);
            }
        }

        private static string SafeGet(zlDataReader dr, string strType, string col)
        {
            try
            {
                object v = dr.Get(strType, col);
                return v == null ? "" : v.ToString();
            }
            catch
            {
                return "";
            }
        }
    }
}
