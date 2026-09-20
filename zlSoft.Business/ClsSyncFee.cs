using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Web.Services.Description;
using zlSoft.Common;

namespace zlSoft.Business
{
    public class ClsSyncFee
    {
        /// <summary>
        /// 同步体检人员收费信息至HIS系统
        /// </summary>


        public static void SyncFee(zlCommand cmd,string strMDDataType)
        {
            #region  定义变量
            //string strMDDataType;
            string strSql;
            DateTime dtNow;

            int intDay;
            //string strFeeItemID;
            string strPatID;
            string strJzk;

            string strUrl;
            string strDFHttp;
            string strIn = "";
            string strOut = "";
            string strToken = "";
            JObject jobjJson;

            zlDataReader dr = new zlDataReader();
            #endregion

          

            #region 业务内容
            //strMDDataType = zlConfiguration.GetAppSetting("MDDataType");
            intDay = Convert.ToInt32(zlConfiguration.GetAppSetting("SyncDay"));

            //获取中间库当前时间
            strSql = "Select Getdate() 时间";
            cmd.SetSQL(strMDDataType,strSql);
            dr.SetReader(strMDDataType,cmd.ExecuteReader(strMDDataType));
            dr.Read(strMDDataType);
            dtNow = DateTime.Parse(dr.Get(strMDDataType,"时间").ToString());
            dr.Close(strMDDataType);

            //获取东昉服务地址
            strDFHttp = zlConfiguration.GetAppSetting("DFHttp");

            try
            {
                #region 更新已同步的体检费用状态
                //弃用，从东昉服务平台走
                ////查询已经同步费用信息，还未完成缴费的记录
                //strSql = @"Select '1' 来源,ChargeNO 单据号,registerID 体检号,Name 姓名
                //             From TJRegisters 
                //            Where FeeState = 0 And ChargeNO Is Not Null And ReqDate > getdate() - [1]
                //            Union All
                //           Select '2' 来源,ChargeNO 单据号,registerID 体检号,Name 姓名
                //             From TJRegistersAddFee 
                //            Where FeeState = 0 And ChargeNO Is Not Null And ReqDate > getdate() - [1]";
                //cmd.SetSQL(strMDDataType,strSql,intDay);
                //dr.SetReader(strMDDataType,cmd.ExecuteReader(strMDDataType));
                //while (dr.Read(strMDDataType))
                //{
                //    try
                //    {
                //        //查询缴费状态，并更新中间库中缴费状态
                //        if (dr.Get(strMDDataType, "来源").ToString() == "1")
                //        {
                //            strSql = "Update TJRegisters Set FeeState = 1,IsSend = 0 Where ChargeNO = [1]";
                //        }
                //        else
                //        {
                //            strSql = "Update TJRegistersAddFee Set FeeState = 1,IsSend = 0 Where ChargeNO = [1]";
                //        }
                //        cmd.SetSQL(strMDDataType, strSql, dr.Get(strMDDataType, "单据号").ToString());
                //        cmd.ExecuteNonQuery(strMDDataType);
                //    }
                //    catch (Exception ex)
                //    {
                //        zlLogService.WriteErrorLog("更新体检人员【" + dr.Get(strMDDataType, "体检号").ToString() + "--" + dr.Get(strMDDataType, "姓名").ToString() + "】的费用【" + dr.Get(strMDDataType, "单据号").ToString() + "】状态异常，错误内容：" + ex.Message);
                //    }
                //}
                #endregion

                #region  同步体检费用撤销申请
                //查询待同步体检费用
                strSql = zlConfiguration.GetAppSetting("CancelFeeSql");
                cmd.SetSQL(strMDDataType, strSql);
                dr.SetReader(strMDDataType, cmd.ExecuteReader(strMDDataType));

                if (!dr.HasRows(strMDDataType))
                {
                    dr.Close(strMDDataType);
                }
                else
                {
                    //将dateread数据放到datatable中,关闭DataRead对象
                    DataTable dtCancel = new DataTable();
                    DataRow drCancelList;
                    dtCancel.Columns.Add("来源");
                    dtCancel.Columns.Add("体检号");
                    dtCancel.Columns.Add("HIS流水号");
                    while (dr.Read(strMDDataType))
                    {
                        drCancelList = dtCancel.NewRow();
                        drCancelList[0] = dr.Get(strMDDataType, "来源").ToString();
                        drCancelList[1] = dr.Get(strMDDataType, "体检号").ToString();
                        drCancelList[2] = dr.Get(strMDDataType, "HIS流水号").ToString();

                        dtCancel.Rows.Add(drCancelList);
                    }

                    dr.Close(strMDDataType);

                    if (string.IsNullOrEmpty(strToken))
                    {
                        strUrl = strDFHttp + "/oauth/token?grant_type=client_credentials&client_id=" + zlConfiguration.GetAppSetting("client_id") + "&client_secret=" + zlConfiguration.GetAppSetting("client_secret");
                        strToken = HttpService.Post("", strUrl, "", "POST");
                        if (strToken.IndexOf("access_token") > -1)
                        {
                            jobjJson = JObject.Parse(strToken);
                            strToken = jobjJson["access_token"].Value<string>();
                        }
                        else
                        {
                            LogHelper.LogInfo("撤销患者费用失败，获取东昉Token失败！");
                            dr.Close(strMDDataType);
                            return;
                        }
                    }

                    strUrl = strDFHttp + "/shouFei/cheXiaoMzFy";
                    for (int intR = 0; intR < dtCancel.Rows.Count; intR++)
                    {
                        try
                        {
                            strIn = "{\"jiuZhenKh\":\"" + dtCancel.Rows[intR]["体检号"].ToString() + "\",\"bingRenId\":\"\",\"feiYongIdList\":[{\"feiYongId\":\"" + dtCancel.Rows[intR]["HIS流水号"].ToString() + "\"}]}";
                            strOut = HttpService.Post(strIn, strUrl, strToken, "POST");
                            jobjJson = JObject.Parse(strOut);
                            if (jobjJson["returnCode"].Value<string>() == "1")
                            {
                                //费用同步成功，更新中间表信息
                                if (dtCancel.Rows[intR]["来源"].ToString() == "1")
                                {
                                    strSql = "Update TJRegisters Set FeeState = 3 Where ChargeNo = '" + dtCancel.Rows[intR]["HIS流水号"].ToString() + "'";
                                }
                                else
                                {
                                    strSql = "Update TJRegistersAddFee Set FeeState = 3 Where ChargeNo = '" + dtCancel.Rows[intR]["HIS流水号"].ToString() + "'";
                                }
                                cmd.SetSQL(strMDDataType, strSql);
                                cmd.ExecuteNonQuery(strMDDataType);
                            }
                            else
                            {
                                LogHelper.LogInfo("体检人员【" + dtCancel.Rows[intR]["体检号"].ToString() + "】的费用【" + dtCancel.Rows[intR]["HIS流水号"].ToString() + "】撤销失败，" + jobjJson["returnMessage"].Value<string>());
                            }
                        }
                        catch (Exception ex)
                        {
                            LogHelper.LogInfo("体检人员【" + dtCancel.Rows[intR]["体检号"].ToString() + "】的费用【" + dtCancel.Rows[intR]["HIS流水号"].ToString() + "】撤销失败，" + ex.Message);
                        }
                    }
                }
                #endregion


                #region  同步体检费用至HIS

                //查询待同步体检费用
                strSql = zlConfiguration.GetAppSetting("SendFeeSql");
                cmd.SetSQL(strMDDataType, strSql);
                dr.SetReader(strMDDataType, cmd.ExecuteReader(strMDDataType));

                if (!dr.HasRows(strMDDataType))
                {
                    dr.Close(strMDDataType);
                    return;
                }

                //将dateread数据放到datatable中,关闭DataRead对象
                DataTable dt = new DataTable();
                DataRow drList;
                dt.Columns.Add("来源");
                dt.Columns.Add("流水号");
                dt.Columns.Add("体检号"); 
                dt.Columns.Add("姓名");
                dt.Columns.Add("身份证号");
                dt.Columns.Add("联系电话");
                dt.Columns.Add("金额");
                dt.Columns.Add("性别");
                while (dr.Read(strMDDataType))
                {
                    drList = dt.NewRow();
                    drList[0] = dr.Get(strMDDataType, "来源").ToString();
                    drList[1] = dr.Get(strMDDataType, "流水号").ToString();
                    drList[2] = dr.Get(strMDDataType, "体检号").ToString();
                    drList[3] = dr.Get(strMDDataType, "姓名").ToString();
                    drList[4] = dr.Get(strMDDataType, "身份证号").ToString();
                    drList[5] = dr.Get(strMDDataType, "联系电话").ToString();
                    drList[6] = dr.Get(strMDDataType, "金额").ToString();
                    drList[7] = dr.Get(strMDDataType, "性别").ToString();
                    dt.Rows.Add(drList);
                }

                dr.Close(strMDDataType);

                if (string.IsNullOrEmpty(strToken))
                {
                    strUrl = strDFHttp + "/oauth/token?grant_type=client_credentials&client_id=" + zlConfiguration.GetAppSetting("client_id") + "&client_secret=" + zlConfiguration.GetAppSetting("client_secret");
                    strToken = HttpService.Post("", strUrl, "", "POST");
                    if (strToken.IndexOf("access_token") > -1)
                    {
                        jobjJson = JObject.Parse(strToken);
                        strToken = jobjJson["access_token"].Value<string>();
                    }
                    else
                    {
                        LogHelper.LogInfo("同步患者费用失败，获取东昉Token失败！");
                        dr.Close(strMDDataType);
                        return;
                    }
                }

                for (int intR = 0; intR < dt.Rows.Count;intR++)
                {
                    try
                    {
                        strPatID = "";
                        strJzk = "";
                        //体检人员信息建档
                        //判断当前身份证号是否已经建档
                        string strIdCard = dt.Rows[intR]["身份证号"].ToString();
                        strUrl = strDFHttp + "/bingRenXx/getJkBingRenXxByConditions";
                        strIn = "{\"shenFenZh\":\"" + strIdCard + "\"}";
                        strOut = HttpService.Post(strIn,strUrl,strToken, "POST");
                        jobjJson = JObject.Parse(strOut);
                        if (jobjJson["returnCode"].Value<string>() == "1")
                        {
                            jobjJson = JObject.Parse(jobjJson["returnData"].ToString());
                            if (!string.IsNullOrEmpty(jobjJson["bingRenId"].Value<string>()))
                            {
                                if (!dt.Rows[intR]["姓名"].ToString().Equals(jobjJson["xingMing"].Value<string>()))
                                {
                                    LogHelper.LogInfo("体检号【" + dt.Rows[intR]["体检号"].ToString() + "】的身份证号【" + dt.Rows[intR]["身份证号"].ToString() + "】和HIS中相同身份证号的人员的姓名不一致！");
                                    continue;
                                }
                                strPatID = jobjJson["bingRenId"].Value<string>();
                                strJzk = jobjJson["jiuZhenKh"].Value<string>();

                                //将体检号作为卡号 自助补卡 到HIS系统
                                strUrl = strDFHttp + "/menZhenJz/buKa?bingRenId=" + strPatID + "&kaiLeiXing=2&xinKaHao=" + dt.Rows[intR]["体检号"].ToString() + "&jiuZhenKh=&caoZuoYuan=" + zlConfiguration.GetAppSetting("caoZuoYuan") + "&geRenBh=";
                                strOut = HttpService.Post("", strUrl, strToken, "POST");
                            }
                        }

                        if (string.IsNullOrEmpty(strPatID))
                        {
                            //建档
                            if (strIdCard != "" && strIdCard.Length==18)
                            {
                                strOut = zlFunc.GetIdCardInfo(strIdCard, dtNow);
                                strIn = "{\"jiuZhenKh\":\"" + dt.Rows[intR]["体检号"].ToString() + "\"," +
                                         "\"kaiLeiXing\":\"4\"," +
                                         "\"xingMing\":\"" + dt.Rows[intR]["姓名"].ToString() + "\"," +
                                         "\"xingBie\":\"" + strOut.Split('|')[1] + "\"," +
                                         "\"shenFenZh\":\"" + strIdCard + "\"," +
                                         "\"danWeiBh\":\"\"," +
                                         "\"chuShengRq\":\"" + strOut.Split('|')[0] + "\"," +
                                         "\"lianXiDz\":\"\"," +
                                         "\"lianXiDh\":\"" + dt.Rows[intR]["联系电话"].ToString() + "\"," +
                                         "\"feiYongLb\":\"\"," +
                                         "\"feiYongXz\":\"\"," +
                                         "\"jiLuLy\":\"3\"," +
                                         "\"caoZuoYuan\":\"\"," +
                                         "\"chongZhiJe\":\"\"," +
                                         "\"yiBaoKh\":\"\"," +
                                         "\"geRenBh\":\"\"," +
                                         "\"yiBaoBrXx\":\"\"," +
                                         "\"gongZuoDw\":\"\"," +
                                         "\"canBaoXzMc\":\"\"," +
                                         "\"muLuBlLb\":\"\"," +
                                         "\"kunNanJzDj\":\"\"," +
                                         "\"yiLiaoLb\":\"\"," +
                                         "\"minZuDm\":\"\"," +
                                         "\"minZuMc\":\"\"}";
                            }
                            else
                            {
                                strOut = "" + "|" + dt.Rows[intR]["性别"].ToString() + "|" + "";
                                strIn = "{\"jiuZhenKh\":\"" + dt.Rows[intR]["体检号"].ToString() + "\"," +
                                       "\"kaiLeiXing\":\"4\"," +
                                       "\"xingMing\":\"" + dt.Rows[intR]["姓名"].ToString() + "\"," +
                                       "\"xingBie\":\"" + strOut.Split('|')[1] + "\"," +
                                    //   "\"shenFenZh\":\"" + "" + "\"," +
                                       "\"shenFenZh\":\"" + strIdCard + "\"," +
                                       "\"danWeiBh\":\"\"," +
                                       "\"chuShengRq\":\"" + strOut.Split('|')[0] + "\"," +
                                       "\"lianXiDz\":\"\"," +
                                       "\"lianXiDh\":\"" + dt.Rows[intR]["联系电话"].ToString() + "\"," +
                                       "\"feiYongLb\":\"\"," +
                                       "\"feiYongXz\":\"\"," +
                                       "\"jiLuLy\":\"3\"," +
                                       "\"caoZuoYuan\":\"\"," +
                                       "\"chongZhiJe\":\"\"," +
                                       "\"yiBaoKh\":\"\"," +
                                       "\"geRenBh\":\"\"," +
                                       "\"yiBaoBrXx\":\"\"," +
                                       "\"gongZuoDw\":\"\"," +
                                       "\"canBaoXzMc\":\"\"," +
                                       "\"muLuBlLb\":\"\"," +
                                       "\"kunNanJzDj\":\"\"," +
                                       "\"yiLiaoLb\":\"\"," +
                                       "\"minZuDm\":\"\"," +
                                       "\"minZuMc\":\"\"}";
                            }

                            strUrl = strDFHttp + "/menZhenJz/jianDang";
                            strOut = HttpService.Post(strIn,strUrl,strToken, "POST");
                            jobjJson = JObject.Parse(strOut);
                            if (jobjJson["returnCode"].Value<string>() == "1")
                            {
                                jobjJson = JObject.Parse(jobjJson["returnData"].ToString());
                                strPatID = jobjJson["bingRenId"].Value<string>();
                                strJzk = jobjJson["jiuZhenKh"].Value<string>();
                            }
                        }

                        if (!string.IsNullOrEmpty(strPatID))
                        {
                            //同步费用
                            //strIn = "{\"TransCode \":\"196\",\"PatientID\"" + strPatID + "\",\"PayName\":\"\",\"List\":[{\"ID\":\"" + strFeeItemID + "\",\"DeptID\":\"" + zlConfiguration.GetAppSetting("FeeAppDept") +
                            //        "\",\"ExecDeptID\":\"" + zlConfiguration.GetAppSetting("FeeExcDept") + ",\"Price\":\"" + dr.Get(strMDDataType, "金额").ToString() + "\",\"Num\":\"1\"}]}";

                            strIn = "{\"bingRenId\":\"" + strPatID + "\"," +
                                     "\"jiuZhenKh\":\"" + strJzk + "\"," +
                                     "\"caoZuoYuan\":\"" + zlConfiguration.GetAppSetting("caoZuoYuan") + "\"," +
                                     "\"yuanQuId\":\"" + zlConfiguration.GetAppSetting("yuanQuId") + "\"," +
                                     "\"yingYongId\":\"" + zlConfiguration.GetAppSetting("yingYongId") + "\"," +
                                     "\"kaiDanKs\":\"" + zlConfiguration.GetAppSetting("kaiDanKs") + "\"," +
                                     "\"dengJiLsh\":\"" + dt.Rows[intR]["流水号"].ToString() + "\"," +
                                     "\"shouTuiBz\":\"1\"," +
                                     "\"feiYongMxList\":[{" +
                                         "\"feiYongMxId\":\"\"," +
                                         "\"feiYongId\":\"\"," +
                                         "\"shouFeiXmId\":\"" + zlConfiguration.GetAppSetting("shouFeiXmId") + "\"," +
                                         "\"shouFeiXmMc\":\"" + zlConfiguration.GetAppSetting("shouFeiXmMc") + "\"," +
                                         "\"shuLiang\":\"1\"," +
                                         "\"danJia\":\"" + dt.Rows[intR]["金额"].ToString() + "\"," +
                                         "\"jieSuanJe\":\"" + dt.Rows[intR]["金额"].ToString() + "\"," +
                                         "\"zhiXingKs\":\"" + zlConfiguration.GetAppSetting("zhiXingKs") + "\"," +
                                         "\"zhiXingKsMc\":\"\"}]}";
                            strUrl = strDFHttp + "/shouFei/createMenZhenFy";
                            strOut = HttpService.Post(strIn, strUrl, strToken, "POST");

                            if (!string.IsNullOrEmpty(strOut))
                            {
                                jobjJson = JObject.Parse(strOut);
                                if (jobjJson["returnCode"].Value<string>() == "1")
                                {
                                    jobjJson = JObject.Parse(jobjJson["returnData"].ToString());
                                    //费用同步成功，更新中间表信息
                                    if (dt.Rows[intR]["来源"].ToString() == "1")
                                    {
                                        strSql = "Update TJRegisters Set IsSend = 0,ChargeDate = GetDate(),ChargeNO = '" + jobjJson["feiYongId"].Value<string>() + "' Where RegisterID = '" + dt.Rows[intR]["流水号"].ToString() + "'";
                                    }
                                    else
                                    {
                                        strSql = "Update TJRegistersAddFee Set IsSend = 0,ChargeDate = GetDate(),ChargeNO = '" + jobjJson["feiYongId"].Value<string>() + "' Where ID = '" + dt.Rows[intR]["流水号"].ToString() + "'";
                                    }
                                    cmd.SetSQL(strMDDataType, strSql);
                                    cmd.ExecuteNonQuery(strMDDataType);
                                }
                                else
                                {
                                    LogHelper.LogInfo("体检人员【" + dt.Rows[intR]["体检号"].ToString() + "--" + dt.Rows[intR]["姓名"].ToString() + "】费用同步失败，" + jobjJson["returnMessage"].Value<string>());
                                }
                            }
                        }
                        else
                        {
                            LogHelper.LogInfo("体检人员【" + dt.Rows[intR]["体检号"].ToString() + "--" + dt.Rows[intR]["姓名"].ToString() + "】信息建档失败，" + jobjJson["returnMessage"].Value<string>());
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.LogInfo("同步体检人员【" + dt.Rows[intR]["体检号"].ToString() + "--" + dt.Rows[intR]["姓名"].ToString() + "】费用异常，错误内容：" + ex.Message + strIn + strOut);
                    }

                }
                #endregion

            }
            catch (Exception ex)
            {
                dr.Close(strMDDataType);
                LogHelper.LogInfo("同步患者费用异常，错误内容：" +ex.Message);
            }
            #endregion
        }
    }
}
