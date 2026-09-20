using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using zlSoft.Common;

namespace zlSoft.Business
{

    public class ClsGetLisResult
    {
        /// <summary>
        /// 从LIS系统下载检验报告，组装XML后回传体检接口
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
            // LIS库连接：新SQL、adk_pdfreportfile、DAOCHUBZ回写均在LIS库执行
            zlCommand cmdLis = new zlCommand(strLisType, strLisConn, "");
            zlDataReader drLis = new zlDataReader();
            zlCommand cmdPacs = new zlCommand(strLisType, strLisConn, "");
            zlDataReader drPacs = new zlDataReader();
            JObject jobjResult;
            // 需要取报告文件地址的项目代码（可配置）
            string pdfCodesCfg = zlConfiguration.GetAppSetting("LisReportPdfItemCodes");
            if (string.IsNullOrEmpty(pdfCodesCfg)) pdfCodesCfg = "4721039,4721040,4721041";
            HashSet<string> pdfCodes = new HashSet<string>(pdfCodesCfg.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            string fileHost = zlConfiguration.GetAppSetting("LisReportFileHost");
            if (string.IsNullOrEmpty(fileHost)) fileHost = "http://10.112.1.124:8891/";
            #endregion

            try
            {
                #region 从LIS库查询待回传的检验报告
                strSql = zlConfiguration.GetAppSetting("LisResultSql");
                cmdLis.SetSQL(strLisType, strSql);
                drLis.SetReader(strLisType, cmdLis.ExecuteReader(strLisType));
                if (!drLis.HasRows(strLisType))
                {
                    //没有需要回传的报告，退出
                    drLis.Close(strLisType);
                    return;
                }

                //将DataReader数据放入DataTable，关闭DataReader对象
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

                #region 逐条获取LIS结果、组装XML并回传体检
                string strUrl = zlConfiguration.GetAppSetting("LisResultUrl");

                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    try
                    {
                        //获取LIS结果
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

                                //筛选属于当前申请单的结果明细（含外送shenQingDanID为空的情况）
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

                                //报告文件地址（指定项目代码才取）→ rep_print_ip
                                ResultFileUrl = "";
                                if (pdfCodes.Contains(dt.Rows[i]["项目代码"].ToString()))
                                {
                                    string querySql = "select * from adk_pdfreportfile where tiaomahao= '" + dt.Rows[i]["样本条码"].ToString() + "'";
                                    cmdPacs.SetSQL(strLisType, querySql);
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

                                //组装XML并回传体检
                                bool ok = ClsSendTjLisReport.SendReport(jobjData, jieGuoItems, dt.Rows[i], ResultFileUrl);
                                if (ok)
                                {
                                    sent = true;
                                }
                            }

                            //回传成功：将Lcb_Jianyansqd的DAOCHUBZ改为1，避免重复上传
                            if (sent)
                            {
                                strSql = "Update Lcb_Jianyansqd Set DAOCHUBZ = 1 Where shenqingdanid = '" + dt.Rows[i]["申请单号"].ToString() + "' And jianyanxmdm = '" + dt.Rows[i]["项目代码"].ToString() + "'";
                                cmdLis.SetSQL(strLisType, strSql);
                                cmdLis.ExecuteNonQuery(strLisType);
                                LogHelper.LogInfo("申请单【" + dt.Rows[i]["申请单号"] + "】检验报告回传成功，DAOCHUBZ已置1");
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
                        LogHelper.LogInfo("申请单号【" + dt.Rows[i]["申请单号"].ToString() + "】的检验报告回传异常，" + ex.Message);
                    }
                }
                #endregion
            }
            catch (Exception ex)
            {
                drLis.Close(strLisType);
                LogHelper.LogInfo("LIS检验报告回传异常，" + ex.Message);
            }
        }

        /// <summary>安全取列值，列不存在时返回空串</summary>
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
