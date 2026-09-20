using Microsoft.VisualBasic;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using zlSoft.Common;
using Newtonsoft.Json.Linq;
using System.Data;
using System.IO;
using System.Security.Policy;

namespace zlSoft.Business
{
    public class ClsSendLisApply
    {
        private static string mstrSql;
        /// <summary>
        /// 向LIS系统发送LIS申请
        /// </summary>
        /// <param name="cmd">中间库连接</param>
        /// <param name="strMDDataType">中间库类型</param>
        /// <param name="strToken">调用羚云服务Token</param>
        public static void SendLisApply(zlCommand cmd, string strMDDataType,string strToken)
        {
            #region 定义变量
            string strIn;
            string strPatInfo = "";
            string strItemList = "";
            string strPeisNo;
            zlDataReader drMd = new zlDataReader();
            #endregion


            try
            {
                #region 从中间库获取待发送LIS申请
                mstrSql = zlConfiguration.GetAppSetting("LisApplySql");
                cmd.SetSQL(strMDDataType, mstrSql);
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
                dt.Columns.Add("联系电话");
                dt.Columns.Add("项目名称");
                dt.Columns.Add("项目代码");
                dt.Columns.Add("开单医生");
                dt.Columns.Add("开单医生ID");
                dt.Columns.Add("样本条码");
                dt.Columns.Add("开单时间");
                dt.Columns.Add("申请时间");
                while (drMd.Read(strMDDataType))
                {
                    drList = dt.NewRow();
                    drList[0] = drMd.Get(strMDDataType, "申请单号").ToString();
                    drList[1] = drMd.Get(strMDDataType, "体检号").ToString();
                    drList[2] = drMd.Get(strMDDataType, "姓名").ToString();
                    drList[3] = drMd.Get(strMDDataType, "性别").ToString();
                    drList[4] = drMd.Get(strMDDataType, "出生日期").ToString();
                    drList[5] = drMd.Get(strMDDataType, "身份证号").ToString();
                    drList[6] = drMd.Get(strMDDataType, "联系电话").ToString();
                    drList[7] = drMd.Get(strMDDataType, "项目名称").ToString();
                    drList[8] = drMd.Get(strMDDataType, "项目代码").ToString();
                    drList[9] = drMd.Get(strMDDataType, "开单医生").ToString();
                    drList[10] = drMd.Get(strMDDataType, "开单医生ID").ToString();
                    drList[11] = drMd.Get(strMDDataType, "样本条码").ToString();
                    drList[12] = drMd.Get(strMDDataType, "开单时间").ToString();
                    drList[13] = drMd.Get(strMDDataType, "申请时间").ToString();

                    dt.Rows.Add(drList);
                }

                drMd.Close(strMDDataType);

                #endregion

                #region 将LIS申请写入到LIS系统
                strPeisNo = "*";
                for (int i = 0;i < dt.Rows.Count;i++)
                {
                    try
                    {
                        //写入LIS申请
                        //每个体检病人发送
                        if (!strPeisNo.Equals(dt.Rows[i]["体检号"].ToString()))
                        {
                            if (!string.IsNullOrEmpty(strPatInfo) && !string.IsNullOrEmpty(strItemList))
                            {
                                //推送上一个体检人员LIS申请
                                strIn = strPatInfo + strItemList.Substring(0,strItemList.Length - 1) + "]}]}";
                                SendApplyInfo(cmd,strMDDataType,strIn,strToken, strPeisNo);


                                //将上一个体检人员申请内容清空
                                strPatInfo = "";
                                strItemList = "";
                            }

                            strPeisNo = dt.Rows[i]["体检号"].ToString();

                            strPatInfo = "{\"kaiDanJGID\":\"" + zlConfiguration.GetAppSetting("LisOrgID") + "\"," +
                                         "\"kaiDanJGMC\":\"" + zlConfiguration.GetAppSetting("LisOrgName") + "\"," +
                                         "\"tiaoMaDYBZ\":1," +
                                         "\"data\":[{" +
                                            "\"bingRenID\":\"" + dt.Rows[i]["体检号"].ToString() + "\"," +
                                            "\"BingAnHao\":\"" + dt.Rows[i]["体检号"].ToString() + "\"," +
                                            "\"MPI\":\"" + dt.Rows[i]["体检号"].ToString() + "\"," +
                                            "\"XingMing\":\"" + dt.Rows[i]["姓名"].ToString() + "\"," +
                                            "\"XingBie\":\"" + dt.Rows[i]["性别"].ToString() + "\"," +
                                            //"\"NianLing\":null," +
                                            //"\"NianLingDW\":null," +
                                            //"\"nianLingMS\":null," +
                                            "\"ChuShengRQ\":\"" + dt.Rows[i]["出生日期"].ToString() + "\"," +
                                            "\"ZhengJianLX\":1," +
                                            "\"ZhengJianHM\":\"" + dt.Rows[i]["身份证号"].ToString() + "\"," +
                                            "\"DianHuaHM\":\"" + dt.Rows[i]["联系电话"].ToString() + "\"," +
                                            //"\"BingRenZZ\":\"\"," +
                                            "\"JiuZhenHao\":\"" + dt.Rows[i]["体检号"].ToString() + "\"," +
                                            "\"JiuZhenLX\":3," +
                                            "\"jiZhenBZ\":0," +
                                            //"\"zhenDuanDM\":\"\"," +
                                            //"\"zhenDuanMC\":\"\"," +
                                            //"\"zhuYuanID\":\"\"," +
                                            //"\"yingErID\":\"\"," +
                                            //"\"chuangHao\":\"\"," +
                                            //"\"chuangHaoPX\":null," +
                                            //"\"bingQuID\":\"\"," +
                                            //"\"bingQuMC\":\"\"," +
                                            "\"shouFeiZT\":1," +
                                            "\"shiShouFY\":0," +
                                            "\"ziFeiBZ\":1," +
                                            "\"feiYongLB\":\"全自费\"," +
                                            //"\"shengLiZQMD\":\"\"," +
                                            //"\"shengLiZQMC\":\"\"," +
                                            "\"LuSeTD\":0," +
                                            "\"ShengQingDXX\":[";
                        }

                        strItemList += "{\"JianYanXMMC\":\"" + dt.Rows[i]["项目名称"].ToString() + "\"," +
                                        "\"JianYanXMDM\":\"" + dt.Rows[i]["项目代码"].ToString() + "\"," +
                                        "\"XiangMuJE\":0," +
                                        //"\"YangBenLXID\":\"\"," +
                                        //"\"YangBenLXMC\":\"\"," +
                                        "\"ShenQingID\":\"" + dt.Rows[i]["申请单号"].ToString() + "\"," +
                                        "\"YiZhuID\":\"" + dt.Rows[i]["申请单号"].ToString() + "\"," +
                                        "\"TiaoMaHao\":\"" + dt.Rows[i]["样本条码"].ToString() + "\"," +
                                        "\"jiZhenBZ\":0," +
                                        "\"kaiDanKSID\":\"" + zlConfiguration.GetAppSetting("LDeptID") + "\"," +
                                        "\"kaiDanKSMC\":\"" + zlConfiguration.GetAppSetting("LDeptName") + "\"," +
                                        "\"kaiDanYSID\":\"" + dt.Rows[i]["开单医生ID"].ToString() + "\"," +
                                        "\"kaiDanYSMC\":\"" + dt.Rows[i]["开单医生"].ToString() + "\"," +
                                        "\"kaiDanSJ\":\"" + dt.Rows[i]["开单时间"].ToString() + "\"," +
                                        "\"daYinRID\":\"" + dt.Rows[i]["开单医生ID"].ToString() + "\"," +
                                        "\"daYinRXM\":\"" + dt.Rows[i]["开单医生"].ToString() + "\"," +
                                        "\"daYinKSID\":\"" + zlConfiguration.GetAppSetting("LDeptID") + "\"," +
                                        "\"daYinKSMC\":\"" + zlConfiguration.GetAppSetting("LDeptName") + "\"," +
                                        "\"daYinSJ\":\"" + dt.Rows[i]["开单时间"].ToString() + "\"" +
                                        //"\"CaiJiRID\":\"\"," +
                                        //"\"CaiJiRXM\":\"\"," +
                                        //"\"CaiJiKSID\":\"\"," +
                                        //"\"CaiJiKSMC\":\"\"," +
                                        //"\"caiJiSJ\":null," +
                                        //"\"songJianRID\":\"\"," +
                                        //"\"songJianRXM\":\"\"," +
                                        //"\"songJianKSID\":\"\"," +
                                        //"\"songJianKDMC\":\"\"," +
                                        //"\"songJianSJ\":null" +
                                        "},";
                    }
                    catch (Exception ex)
                    {
                        LogHelper.LogInfo("申请单号【" + dt.Rows[i]["申请单号"].ToString() + "】的LIS申请同步异常，" + ex.Message);
                    }
                }
                //推送最后一个体检人员的LIS申请
                strIn = strPatInfo + strItemList.Substring(0, strItemList.Length - 1) + "]}]}";
                SendApplyInfo(cmd, strMDDataType, strIn, strToken, strPeisNo);
                #endregion

            }
            catch (Exception ex)
            {
                drMd.Close(strMDDataType);
                LogHelper.LogInfo("LIS申请同步异常，" + ex.Message);
            }
        }

        /// <summary>
        /// 向LIS系统推送申请信息并更新体检中间表状态
        /// </summary>
        /// <param name="cmd">中间表数据库连接对象</param>
        /// <param name="strMDDataType">数据库类型</param>
        /// <param name="strIn">推送的内容</param>
        /// <param name="strToken">LIS服务Token</param>
        /// <param name="strPeisNO">体检号</param>
        private static void SendApplyInfo(zlCommand cmd, string strMDDataType, string strIn,string strToken,string strPeisNO)
        {
            JObject jobjResult;
            string strOut;
            string strUrl = zlConfiguration.GetAppSetting("LisApplyUrl");
            strOut = HttpService.Post(strIn, strUrl, strToken, "POST");

            jobjResult = JObject.Parse(strOut);
            if (jobjResult["code"].ToString() == "0")
            {
                //更新中间库状态
                mstrSql = "Update LabReqItem Set State = 1 Where ReqNO In (Select ReqNO From LabReqMaster Where PatNO = '" + strPeisNO + "')";
                cmd.SetSQL(strMDDataType, mstrSql);
                cmd.ExecuteNonQuery(strMDDataType);
                mstrSql = "Update LabReqMaster Set State = 3 Where PatNO = '" + strPeisNO + "'";
                cmd.SetSQL(strMDDataType, mstrSql);
                cmd.ExecuteNonQuery(strMDDataType);
            }
            else
            {
                if (!jobjResult["message"].ToString().Contains("已存在"))
                {
                    LogHelper.LogInfo("体检号【" + strPeisNO + "】的LIS申请推送失败，" + jobjResult["message"].ToString());
                }
                else
                {
                    //更新中间库状态
                    mstrSql = "Update LabReqItem Set State = 1 Where ReqNO In (Select ReqNO From LabReqMaster Where PatNO = '" + strPeisNO + "')";
                    cmd.SetSQL(strMDDataType, mstrSql);
                    cmd.ExecuteNonQuery(strMDDataType);
                    mstrSql = "Update LabReqMaster Set State = 3 Where PatNO = '" + strPeisNO + "'";
                    cmd.SetSQL(strMDDataType, mstrSql);
                    cmd.ExecuteNonQuery(strMDDataType);
                }
            }
        }
    }
}
