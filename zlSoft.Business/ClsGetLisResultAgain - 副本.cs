using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using zlSoft.Common;

namespace zlSoft.Business
{

    public class ClsGetLisResultAgain
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

            #endregion

            try
            {
                #region 从LIS库查询待回传的检验报告
                strSql = zlConfiguration.GetAppSetting("LisResultAgainSql");
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
                while (drLis.Read(strLisType))
                {
                    drList = dt.NewRow();
                    drList["样本条码"] = SafeGet1(drLis, strLisType, "样本条码");
                    drList["申请单号"] = SafeGet1(drLis, strLisType, "申请单号");
                    drList["申请时间"] = SafeGet1(drLis, strLisType, "申请时间");
                    drList["项目代码"] = SafeGet1(drLis, strLisType, "项目代码");
                    drList["患者ID"] = SafeGet1(drLis, strLisType, "患者ID");
                    drList["患者姓名"] = SafeGet1(drLis, strLisType, "患者姓名");
                    drList["性别代码"] = SafeGet1(drLis, strLisType, "性别代码");
                    drList["性别名称"] = SafeGet1(drLis, strLisType, "性别名称");
                    drList["证件号"] = SafeGet1(drLis, strLisType, "证件号");
                    drList["出生日期"] = SafeGet1(drLis, strLisType, "出生日期");
                    drList["年龄"] = SafeGet1(drLis, strLisType, "年龄");
                    dt.Rows.Add(drList);
                }
                drLis.Close(strLisType);
                #endregion

                #region 将LIS申请写入到LIS系统

                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    try
                    {
                        //判断条码是否二次审核
                        string querySql = "select *from LCB_YANGBENXXQXSHJL where tiaomahao= '" + dt.Rows[i]["样本条码"].ToString() + "'";
                        cmdPacs.SetSQL(strLisType, querySql);
                        drPacs.SetReader(strLisType, cmdPacs.ExecuteReader(strLisType));

                        if (drPacs.HasRows(strLisType) && drPacs.Read(strLisType))
                        {
                            //string result = drPacs.Get(strLisType, "PATH").ToString();
                            //if (!string.IsNullOrEmpty(result))
                            //{
                            //    ResultFileUrl = "http://10.112.1.124:8891/" + result.Replace('\\', '/');
                            //}
                            //二次审核修改状态变为1，然后会自动调用上一步接口下载结果
                            strSql = "Update Lcb_Jianyansqd Set DAOCHUBZ = 0 Where shenqingdanid = '" + dt.Rows[i]["申请单号"].ToString() + "'";
                            cmdLis.SetSQL(strLisType, strSql);
                            cmdLis.ExecuteNonQuery(strLisType);
                            LogHelper.LogInfo("申请单【" + dt.Rows[i]["申请单号"] + "】检验报告二次回传成功，DAOCHUBZ已置0");

                        }

                        drPacs.Close(strLisType);

                    }
                    catch (Exception ex)
                    {
                        LogHelper.LogInfo("申请单号【" + dt.Rows[i]["申请单号"].ToString() + "】的LIS结果获取异常，" + ex.Message);
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
        private static string SafeGet1(zlDataReader dr, string strType, string col)
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
