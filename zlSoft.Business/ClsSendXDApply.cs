using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using zlSoft.Common;

namespace zlSoft.Business
{
    public class ClsSendPacsApply
    {
        /// <summary>
        /// 将PACS申请推送到PACS数据库
        /// </summary>
        /// <param name="cmd">中间库连接串</param>
        /// <param name="strMDDataType">中间库类型</param>
        public static void SendPacsApply(zlCommand cmd, string strMDDataType)
        {
            #region 定义变量
            string strSql;
            zlDataReader drMd = new zlDataReader();

            string strPacsType = zlConfiguration.GetAppSetting("PacsType");
            string strPacsConn = zlConfiguration.GetAppSetting("PacsConn");
            
            #endregion

            try
            {
                #region 从中间库获取待发送PACS申请
                strSql = zlConfiguration.GetAppSetting("PacsApplySql");
                cmd.SetSQL(strMDDataType, strSql, "");
                drMd.SetReader(strMDDataType, cmd.ExecuteReader(strMDDataType));
                if (!drMd.HasRows(strMDDataType))
                {
                    //没有需要推送的申请信息，退出
                    drMd.Close(strMDDataType);
                    return;
                }

                //将dateread数据放到datatable中,关闭DataRead对象
                DataTable dt = new DataTable();
                DataRow drList;
                dt.Columns.Add("申请单号");
                dt.Columns.Add("体检号");
                dt.Columns.Add("姓名");
                dt.Columns.Add("性别");
                dt.Columns.Add("出生日期");
                dt.Columns.Add("身份证号");
                dt.Columns.Add("类型");
                dt.Columns.Add("开单时间");
                dt.Columns.Add("医生工号");
                dt.Columns.Add("医生姓名");
                dt.Columns.Add("项目名称");
                dt.Columns.Add("项目ID");
                while (drMd.Read(strMDDataType))
                {
                    drList = dt.NewRow();
                    drList[0] = drMd.Get(strMDDataType, "申请单号").ToString();
                    drList[1] = drMd.Get(strMDDataType, "体检号").ToString();
                    drList[2] = drMd.Get(strMDDataType, "姓名").ToString();
                    drList[3] = drMd.Get(strMDDataType, "性别").ToString();
                    drList[4] = drMd.Get(strMDDataType, "出生日期").ToString();
                    drList[5] = drMd.Get(strMDDataType, "身份证号").ToString();
                    drList[6] = drMd.Get(strMDDataType, "类型").ToString();
                    drList[7] = drMd.Get(strMDDataType, "开单时间").ToString();
                    drList[8] = drMd.Get(strMDDataType, "医生工号").ToString();
                    drList[9] = drMd.Get(strMDDataType, "医生姓名").ToString();
                    drList[10] = drMd.Get(strMDDataType, "项目名称").ToString();
                    drList[11] = drMd.Get(strMDDataType, "项目ID").ToString();

                    dt.Rows.Add(drList);
                }

                drMd.Close(strMDDataType);
                #endregion

                #region 将PACS申请写入到PACS中间库
                zlCommand cmdPacs = new zlCommand(strPacsType, strPacsConn, "");
                for (int i = 0;i < dt.Rows.Count;i++)
                {
                    try
                    {
                        //写入PACS申请
                        strSql = "Insert Into T_HISORDER (HIS_ID,PATTYPE,PATIENTID,PATNAME,PATSEX,PATBIRTHDAY,NATIONALITY,COUNTRY,IDCARD,CLINICID,CLINIC,ORDERDT,MODALITY,ORDERDRID," +
                            "                             ORDERDR,EXAMITEM,EXAMITEMID,PHYPATIENTID,CHARGEFLAG)" +
                            "     Values ([1],[2],[3],[4],[5],[6],[7],[8],[9],[10],[11],[12],[13],[14],[15],[16],[17],[18],[19])";
                        cmdPacs.SetSQL(strPacsType, strSql, dt.Rows[i]["申请单号"].ToString(), "P", dt.Rows[i]["体检号"].ToString(), dt.Rows[i]["姓名"].ToString(), dt.Rows[i]["性别"].ToString(),
                                       dt.Rows[i]["出生日期"].ToString(), "汉族", "中国", dt.Rows[i]["身份证号"].ToString(), zlConfiguration.GetAppSetting("PDeptID"), zlConfiguration.GetAppSetting("PDeptName"),
                                       dt.Rows[i]["开单时间"].ToString(), dt.Rows[i]["类型"].ToString(), dt.Rows[i]["医生工号"].ToString(), dt.Rows[i]["医生姓名"].ToString(),
                                       dt.Rows[i]["项目名称"].ToString(), dt.Rows[i]["项目ID"].ToString(), dt.Rows[i]["体检号"].ToString(), 1);
                        cmdPacs.ExecuteNonQuery(strPacsType);

                        //更新中间库状态
                        strSql = "Update PacApply Set IsSend = 2 Where ReqNO = '" + dt.Rows[i]["申请单号"].ToString()  + "'";
                        cmd.SetSQL(strMDDataType, strSql);
                        cmd.ExecuteNonQuery(strMDDataType);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.LogInfo("申请单号【" + dt.Rows[i]["申请单号"].ToString() + "】的PACS申请同步异常，" + ex.Message);
                    }
                }
                #endregion

                cmdPacs.Close(strPacsType);
            }
            catch (Exception ex)
            {
                drMd.Close(strMDDataType);
                LogHelper.LogInfo("PACS申请同步异常，" + ex.Message);
            }
        }
    }
}
