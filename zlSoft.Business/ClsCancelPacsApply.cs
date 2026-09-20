using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using zlSoft.Common;

namespace zlSoft.Business
{
    public class ClsCancelPacsApply
    {
        /// <summary>
        /// 从PACS数据库撤销PACS申请
        /// </summary>
        /// <param name="cmd">中间库连接串</param>
        /// <param name="strMDDataType">中间库类型</param>
        public static void CancelPacsApply(zlCommand cmd, string strMDDataType)
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
                strSql = zlConfiguration.GetAppSetting("PacsCancelSql");
                cmd.SetSQL(strMDDataType, strSql, "");
                drMd.SetReader(strMDDataType, cmd.ExecuteReader(strMDDataType));
                if (!drMd.HasRows(strMDDataType))
                {
                    //没有需要撤销的申请信息，退出
                    drMd.Close(strMDDataType);
                    return;
                }

                //将dateread数据放到datatable中,关闭DataRead对象
                DataTable dt = new DataTable();
                DataRow drList;
                dt.Columns.Add("体检号");
                dt.Columns.Add("申请单号");
                dt.Columns.Add("状态");
                while (drMd.Read(strMDDataType))
                {
                    drList = dt.NewRow();
                    drList[0] = drMd.Get(strMDDataType, "体检号").ToString();
                    drList[1] = drMd.Get(strMDDataType, "申请单号").ToString();
                    drList[2] = drMd.Get(strMDDataType, "状态").ToString();

                    dt.Rows.Add(drList);
                }

                drMd.Close(strMDDataType);
                #endregion

                #region 将PACS申请撤销信息写入到PACS中间库
                zlCommand cmdPacs = new zlCommand(strPacsType, strPacsConn, "");
                for (int i = 0;i < dt.Rows.Count;i++)
                {
                    try
                    {
                        if (dt.Rows[i]["状态"].ToString() == "2")
                        {
                            //将撤销信息写入到PACS中间库，不判断是否已经登记
                            strSql = "Update T_HISORDER Set ISDELETED = 1 Where HIS_ID = '" + dt.Rows[i]["申请单号"].ToString()  + "'";
                            cmdPacs.SetSQL(strPacsType, strSql);
                            cmdPacs.ExecuteNonQuery(strPacsType);
                        }
                        //更新中间库状态
                        strSql = "Update PacApply Set IsSend = 3 Where ReqNO = '" + dt.Rows[i]["申请单号"].ToString()  + "'";
                        cmd.SetSQL(strMDDataType, strSql);
                        cmd.ExecuteNonQuery(strMDDataType);

                    }
                    catch (Exception ex)
                    {
                        LogHelper.LogInfo("申请单号【" + dt.Rows[i]["申请单号"].ToString() + "】的PACS申请撤销异常，" + ex.Message);
                    }

                    strSql = "Update TJRegisters Set FeeState = 4 Where RegisterID = '" + dt.Rows[i]["体检号"].ToString() + "'";
                    cmd.SetSQL(strMDDataType, strSql);
                    cmd.ExecuteNonQuery(strMDDataType);
                }
                #endregion

                cmdPacs.Close(strPacsType);
            }
            catch (Exception ex)
            {
                drMd.Close(strMDDataType);
                LogHelper.LogInfo("PACS申请撤销异常，" + ex.Message);
            }
        }
    }
}
