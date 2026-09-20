using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using zlSoft.Common;

namespace zlSoft.Business
{
    public class ClsCancelLisApply
    {
        /// <summary>
        /// 同步LIS系统撤销检验申请
        /// </summary>
        /// <param name="cmd">中间库连接</param>
        /// <param name="strMDDataType">中间库类型</param>
        /// <param name="strToken">调用羚云服务Token</param>
        public static void CancelLisApply(zlCommand cmd, string strMDDataType, string strToken)
        {
            #region 定义变量
            string strSql;
            string strIn;
            string strOut;
            JObject jobjResult;
            zlDataReader drMd = new zlDataReader();
            #endregion

            try
            {
                #region 从中间库获取待撤销LIS申请
                strSql = zlConfiguration.GetAppSetting("LisCancelSql");
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
                dt.Columns.Add("申请单号");
                dt.Columns.Add("状态");
                dt.Columns.Add("操作时间");
                dt.Columns.Add("操作医生");
                dt.Columns.Add("操作医生ID");
                dt.Columns.Add("体检号");
                while (drMd.Read(strMDDataType))
                {
                    drList = dt.NewRow();
                    drList[0] = drMd.Get(strMDDataType, "申请单号").ToString();
                    drList[1] = drMd.Get(strMDDataType, "状态").ToString();
                    drList[2] = drMd.Get(strMDDataType, "操作时间").ToString();
                    drList[3] = drMd.Get(strMDDataType, "操作医生").ToString();
                    drList[4] = drMd.Get(strMDDataType, "操作医生ID").ToString();
                    drList[5] = drMd.Get(strMDDataType, "体检号").ToString();

                    dt.Rows.Add(drList);
                }

                drMd.Close(strMDDataType);
                #endregion

                #region 从LIS系统撤销LIS申请
                string strUrl = zlConfiguration.GetAppSetting("LisCancelUrl");
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    try
                    {
                        if (dt.Rows[i]["状态"].ToString() == "3")
                        {
                            //每条申请单独撤销
                            strIn = "{\"caoZuoRID\":\"" + dt.Rows[i]["操作医生ID"].ToString() + "\"," +
                                     "\"caoZuoRXM\":\"" + dt.Rows[i]["操作医生"].ToString() + "\"," +
                                     "\"caoZuoJGID\":\"" + zlConfiguration.GetAppSetting("LisOrgID") + "\"," +
                                     "\"caoZuoJGMC\":\"" + zlConfiguration.GetAppSetting("LisOrgName") + "\"," +
                                     "\"caoZuoKSID\":\"" + zlConfiguration.GetAppSetting("LDeptID") + "\"," +
                                     "\"caoZuoKSMC\":\"" + zlConfiguration.GetAppSetting("LDeptName") + "\"," +
                                     "\"caoZuoSJ\":\"" + dt.Rows[i]["操作时间"].ToString() + "\"," +
                                     "\"data\":[{" +
                                        "\"ShenQingDanID\":\"" + dt.Rows[i]["申请单号"].ToString() + "\"," +
                                        "\"kaiDanJGID\":\"" + zlConfiguration.GetAppSetting("LisOrgID") + "\"," +
                                        "\"kaiDanJGMC\":\"" + zlConfiguration.GetAppSetting("LisOrgName") + "\"}]}";
                            strOut = HttpService.Post(strIn, strUrl, strToken, "POST");

                            jobjResult = JObject.Parse(strOut);
                            if (jobjResult["code"].ToString() == "0")
                            {
                                //更新中间库状态
                                strSql = "Update LabReqMaster Set State = 2 Where ReqNO = '" + dt.Rows[i]["申请单号"].ToString()  + "'";
                                cmd.SetSQL(strMDDataType, strSql);
                                cmd.ExecuteNonQuery(strMDDataType);
                            }
                            else
                            {
                                LogHelper.LogInfo("申请单号【" + dt.Rows[i]["申请单号"].ToString() + "】的LIS申请撤销失败，" + jobjResult["message"].ToString());
                            }
                        }
                        else
                        {
                            //更新中间库状态
                            strSql = "Update LabReqMaster Set State = 2 Where ReqNO = '" + dt.Rows[i]["申请单号"].ToString()  + "'";
                            cmd.SetSQL(strMDDataType, strSql);
                            cmd.ExecuteNonQuery(strMDDataType);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.LogInfo("申请单号【" + dt.Rows[i]["申请单号"].ToString() + "】的LIS申请撤销异常，" + ex.Message);
                    }

                    strSql = "Update TJRegisters Set FeeState = 5 Where RegisterID = '" + dt.Rows[i]["体检号"].ToString() + "'";
                    cmd.SetSQL(strMDDataType, strSql);
                    cmd.ExecuteNonQuery(strMDDataType);
                }
                #endregion
            }
            catch (Exception ex)
            {
                drMd.Close(strMDDataType);
                LogHelper.LogInfo("LIS申请撤销异常，" + ex.Message);
            }
        }
    }
}
