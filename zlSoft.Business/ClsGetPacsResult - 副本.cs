using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using zlSoft.Common;

namespace zlSoft.Business
{
    public class ClsGetPacsResult
    {
        /// <summary>
        /// 从PACS系统下载检验报告
        /// </summary>
        /// <param name="cmd">中间库连接</param>
        /// <param name="strMDDataType">中间库类型</param>
        public static void GetPacsResult(zlCommand cmd, string strMDDataType)
        {
            #region 定义变量
            string strSql;
            zlDataReader drMd = new zlDataReader();
            zlDataReader drPacs = new zlDataReader();

            string strPacsType = zlConfiguration.GetAppSetting("PacsType");
            string strPacsConn = zlConfiguration.GetAppSetting("PacsConn");
            #endregion
      
            try
            {
                #region 从PACS库获取需要下载的报告
                //获取PACS结果
                zlCommand cmdPacs = new zlCommand(strPacsType, strPacsConn, "");
                strSql = zlConfiguration.GetAppSetting("GetPacsResultSql");
                cmdPacs.SetSQL(strPacsType, strSql);
                drPacs.SetReader(strPacsType, cmdPacs.ExecuteReader(strPacsType));
                if (!drPacs.HasRows(strPacsType))
                {
                    //没有需要下载的报告，直接退出
                    drMd.Close(strMDDataType);
                    drPacs.Close(strPacsType);
                    cmdPacs.Close(strPacsType);
                    return;
                }
                #endregion

                #region 从中间库获取待下载报告的PACS申请
                //strSql = zlConfiguration.GetAppSetting("PacsResultSql");
                //cmd.SetSQL(strMDDataType, strSql);
                //drMd.SetReader(strMDDataType, cmd.ExecuteReader(strMDDataType));
                //if (!drMd.HasRows(strMDDataType))
                //{
                //    //没有需要下载报告的申请，退出
                //    drMd.Close(strMDDataType);
                //    return;
                //}

                ////将dateread数据放到datatable中,关闭DataRead对象
                //DataTable dt = new DataTable();
                //DataRow drList;
                //dt.Columns.Add("申请单号");
                //dt.Columns.Add("序号");
                //while (drMd.Read(strMDDataType))
                //{
                //    drList = dt.NewRow();
                //    drList[0] = drMd.Get(strMDDataType, "申请单号").ToString();
                //    drList[1] = drMd.Get(strMDDataType, "序号").ToString();
                //    dt.Rows.Add(drList);
                //}

                //drMd.Close(strMDDataType);
                #endregion

                #region 将PACS结果写入到中间库
                while (drPacs.Read(strPacsType))
                {
                    try
                    {
                        
                        //先删除历史结果，再写入新结果
                        strSql = "Delete From PacsResult Where ReqNO = '" + drPacs.Get(strPacsType,"申请单号").ToString() + "'";
                        cmd.SetSQL(strMDDataType, strSql);
                        cmd.ExecuteNonQuery(strMDDataType);

                        strSql = @"Insert Into PacsResult (SEQNO,REQNO,REPORTNUMBER,ULTRASOUNDPOSITIVE,DESCRIPTION,REPORTDATE,REPORTID,REPORTNAME,AUDITDATE,AUDITID,AUDITNAME,PICTUREFTPURL,SHOWINFO) 
                                        Values ('" + drPacs.Get(strPacsType, "申请单号").ToString() + "','" + drPacs.Get(strPacsType, "申请单号").ToString() + "','" + drPacs.Get(strPacsType, "报告ID").ToString() + "','" + drPacs.Get(strPacsType, "检查结果").ToString() + "','" + drPacs.Get(strPacsType, "诊断意见").ToString() + "',Cast('" +
                                                drPacs.Get(strPacsType, "检查时间").ToString() + "' As DateTime),'" + drPacs.Get(strPacsType, "检查人").ToString() + "','" + drPacs.Get(strPacsType, "检查人").ToString() + "',Cast('" + drPacs.Get(strPacsType, "审核时间").ToString() + "' As DateTime),'" +
                                                drPacs.Get(strPacsType, "审核人").ToString() + "','" + drPacs.Get(strPacsType, "审核人").ToString() + "','" + drPacs.Get(strPacsType, "报告图片").ToString() + "','" + drPacs.Get(strPacsType, "检查所见").ToString() + "')";
                        cmd.SetSQL(strMDDataType, strSql);
                        cmd.ExecuteNonQuery(strMDDataType);
                            

                        //结果写入完成，修改申请状态
                        strSql = "Update PacApply Set IsSend = 3 Where ReqNO = '" + drPacs.Get(strPacsType, "申请单号").ToString() + "'";
                        cmd.SetSQL(strMDDataType, strSql);
                        cmd.ExecuteNonQuery(strMDDataType);

                        //修改PACS库报告提取状态
                        strSql = "Update T_Report_Queue Set TJ_Report_Flag = 'Y' Where OrderID = '" + drPacs.Get(strPacsType, "申请单号").ToString() + "'";
                        cmdPacs.SetSQL(strPacsType,strSql);
                        cmdPacs.ExecuteNonQuery(strPacsType);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.LogInfo("申请单号【" + drPacs.Get(strPacsType, "申请单号").ToString() + "】的PACS结果写入异常，" + ex.Message);
                    }
                }
                #endregion

                drPacs.Close(strPacsType);
            }
            catch (Exception ex)
            {
                drMd.Close(strMDDataType);
                drPacs.Close(strPacsType);
                LogHelper.LogInfo("PACS结果获取异常，" + ex.Message);
            }
        }
    }
}
