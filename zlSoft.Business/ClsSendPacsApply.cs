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
            zlDataReader drPacs = new zlDataReader();
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
                dt.Columns.Add("手机号");
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
                    drList[12] = drMd.Get(strMDDataType, "手机号").ToString();
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
                        string examItemIds = dt.Rows[i]["项目ID"].ToString();
                        string examItemNames = dt.Rows[i]["项目名称"].ToString(); // 默认使用原始项目名称

                  //无痛胃肠镜
                  // 如果项目ID不为空，从PACS数据库查询项目名称
                        if (examItemIds == "4720815")
                        {

                            //if (!string.IsNullOrEmpty(examItemIds))
                            //{
                            //    // 构建查询SQL - 使用IN查询多个项目ID
                            //    string querySql = "SELECT LISTAGG(EXAMITEM, ',') WITHIN GROUP (ORDER BY EXAMITEMCODE) AS EXAMITEMS FROM t_examitem WHERE EXAMITEMCODE IN (" + examItemIds + ")";

                            //    cmdPacs.SetSQL(strPacsType, querySql);
                            //    drPacs.SetReader(strPacsType, cmdPacs.ExecuteReader(strPacsType));

                            //    if (drPacs.HasRows(strPacsType) && drPacs.Read(strPacsType))
                            //    {
                            //        string result = drPacs.Get(strPacsType, "EXAMITEMS").ToString();
                            //        if (!string.IsNullOrEmpty(result))
                            //        {
                            //            examItemNames = result;
                            //        }
                            //    }

                            //    drPacs.Close(strPacsType);
                            //}

                            //写入PACS肠镜申请 8848 无痛肠镜
                            strSql = "Insert Into T_HISORDER (HIS_ID,PATTYPE,PATIENTID,PATNAME,PATSEX,PATBIRTHDAY,NATIONALITY,COUNTRY,IDCARD,CLINICID,CLINIC,ORDERDT,MODALITY,ORDERDRID," +
                                "                             ORDERDR,EXAMITEM,EXAMITEMID,PHYPATIENTID,CHARGEFLAG,OUTPATIENTID,VISITNUM,TELEPHONENO)" +
                                "     Values ([1],[2],[3],[4],[5],[6],[7],[8],[9],[10],[11],[12],[13],[14],[15],[16],[17],[18],[19],[20],[21],[22])";
                            cmdPacs.SetSQL(strPacsType, strSql, dt.Rows[i]["申请单号"].ToString(), "P", dt.Rows[i]["体检号"].ToString(), dt.Rows[i]["姓名"].ToString(), dt.Rows[i]["性别"].ToString(),
                                           dt.Rows[i]["出生日期"].ToString(), "汉族", "中国", dt.Rows[i]["身份证号"].ToString(), zlConfiguration.GetAppSetting("PDeptID"), zlConfiguration.GetAppSetting("PDeptName"),
                                           dt.Rows[i]["开单时间"].ToString(), dt.Rows[i]["类型"].ToString().Replace("DR", "DX"), dt.Rows[i]["医生工号"].ToString(), dt.Rows[i]["医生姓名"].ToString(),
                                           "无痛肠镜", "8848", dt.Rows[i]["体检号"].ToString(), 1, dt.Rows[i]["体检号"].ToString(), dt.Rows[i]["体检号"].ToString(), dt.Rows[i]["手机号"].ToString());
                            cmdPacs.ExecuteNonQuery(strPacsType);
                            //写入PACS肠镜申请 8859 无痛胃镜
                            strSql = "Insert Into T_HISORDER (HIS_ID,PATTYPE,PATIENTID,PATNAME,PATSEX,PATBIRTHDAY,NATIONALITY,COUNTRY,IDCARD,CLINICID,CLINIC,ORDERDT,MODALITY,ORDERDRID," +
                                "                             ORDERDR,EXAMITEM,EXAMITEMID,PHYPATIENTID,CHARGEFLAG,OUTPATIENTID,VISITNUM,TELEPHONENO)" +
                                "     Values ([1],[2],[3],[4],[5],[6],[7],[8],[9],[10],[11],[12],[13],[14],[15],[16],[17],[18],[19],[20],[21],[22])";
                            cmdPacs.SetSQL(strPacsType, strSql, dt.Rows[i]["申请单号"].ToString(), "P", dt.Rows[i]["体检号"].ToString(), dt.Rows[i]["姓名"].ToString(), dt.Rows[i]["性别"].ToString(),
                                           dt.Rows[i]["出生日期"].ToString(), "汉族", "中国", dt.Rows[i]["身份证号"].ToString(), zlConfiguration.GetAppSetting("PDeptID"), zlConfiguration.GetAppSetting("PDeptName"),
                                           dt.Rows[i]["开单时间"].ToString(), dt.Rows[i]["类型"].ToString().Replace("DR", "DX"), dt.Rows[i]["医生工号"].ToString(), dt.Rows[i]["医生姓名"].ToString(),
                                           "无痛胃镜", "8859", dt.Rows[i]["体检号"].ToString(), 1, dt.Rows[i]["体检号"].ToString(), dt.Rows[i]["体检号"].ToString(), dt.Rows[i]["手机号"].ToString());
                            cmdPacs.ExecuteNonQuery(strPacsType);



                        }//非无痛胃肠镜
                        else {

                            if (!string.IsNullOrEmpty(examItemIds))
                            {
                                // 构建查询SQL - 使用IN查询多个项目ID
                                string querySql = "SELECT LISTAGG(EXAMITEM, ',') WITHIN GROUP (ORDER BY EXAMITEMCODE) AS EXAMITEMS FROM t_examitem WHERE EXAMITEMCODE IN (" + examItemIds + ")";

                                cmdPacs.SetSQL(strPacsType, querySql);
                                drPacs.SetReader(strPacsType, cmdPacs.ExecuteReader(strPacsType));

                                if (drPacs.HasRows(strPacsType) && drPacs.Read(strPacsType))
                                {
                                    string result = drPacs.Get(strPacsType, "EXAMITEMS").ToString();
                                    if (!string.IsNullOrEmpty(result))
                                    {
                                        examItemNames = result;
                                    }
                                }

                                drPacs.Close(strPacsType);
                            }

                            //写入PACS申请
                            strSql = "Insert Into T_HISORDER (HIS_ID,PATTYPE,PATIENTID,PATNAME,PATSEX,PATBIRTHDAY,NATIONALITY,COUNTRY,IDCARD,CLINICID,CLINIC,ORDERDT,MODALITY,ORDERDRID," +
                                "                             ORDERDR,EXAMITEM,EXAMITEMID,PHYPATIENTID,CHARGEFLAG,OUTPATIENTID,VISITNUM,TELEPHONENO)" +
                                "     Values ([1],[2],[3],[4],[5],[6],[7],[8],[9],[10],[11],[12],[13],[14],[15],[16],[17],[18],[19],[20],[21],[22])";
                            cmdPacs.SetSQL(strPacsType, strSql, dt.Rows[i]["申请单号"].ToString(), "P", dt.Rows[i]["体检号"].ToString(), dt.Rows[i]["姓名"].ToString(), dt.Rows[i]["性别"].ToString(),
                                           dt.Rows[i]["出生日期"].ToString(), "汉族", "中国", dt.Rows[i]["身份证号"].ToString(), zlConfiguration.GetAppSetting("PDeptID"), zlConfiguration.GetAppSetting("PDeptName"),
                                           dt.Rows[i]["开单时间"].ToString(), dt.Rows[i]["类型"].ToString().Replace("DR", "DX"), dt.Rows[i]["医生工号"].ToString(), dt.Rows[i]["医生姓名"].ToString(),
                                           examItemNames, dt.Rows[i]["项目ID"].ToString(), dt.Rows[i]["体检号"].ToString(), 1, dt.Rows[i]["体检号"].ToString(), dt.Rows[i]["体检号"].ToString(), dt.Rows[i]["手机号"].ToString());
                            cmdPacs.ExecuteNonQuery(strPacsType);                        
                        
                        }


                        //更新中间库状态
                        strSql = "Update PacApply Set IsSend = 2 Where PicDetail<>'XD' and ReqNO = '" + dt.Rows[i]["申请单号"].ToString()  + "'";
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

        /// <summary>
        /// 从身份证号提取出生日期
        /// </summary>
        /// <param name="idCard">身份证号</param>
        /// <returns>出生日期字符串，格式为yyyy-MM-dd</returns>
        private static string GetBirthdayFromIDCard(string idCard)
        {
            if (string.IsNullOrEmpty(idCard))
                return string.Empty;

            // 移除身份证号中的空格和特殊字符
            idCard = idCard.Trim().Replace(" ", "");

            try
            {
                // 15位身份证号：第7-12位是出生日期（YYMMDD）
                // 18位身份证号：第7-14位是出生日期（YYYYMMDD）
                if (idCard.Length == 15)
                {
                    // 15位身份证：提取7-12位，格式为YYMMDD
                    string year = "19" + idCard.Substring(6, 2); // 前两位是19
                    string month = idCard.Substring(8, 2);
                    string day = idCard.Substring(10, 2);

                    // 验证日期是否有效
                    if (IsValidDate(year, month, day))
                    {
                        return year + "-" + month + "-" + day; // 使用字符串拼接
                    }
                }
                else if (idCard.Length == 18)
                {
                    // 18位身份证：提取7-14位，格式为YYYYMMDD
                    string year = idCard.Substring(6, 4);
                    string month = idCard.Substring(10, 2);
                    string day = idCard.Substring(12, 2);

                    // 验证日期是否有效
                    if (IsValidDate(year, month, day))
                    {
                        return year + "-" + month + "-" + day; // 使用字符串拼接
                    }
                }
                else
                {
                    // 不是标准身份证号，尝试查找数字序列
                    // 查找连续的8位数字（可能包含在更长字符串中）
                    var matches = System.Text.RegularExpressions.Regex.Matches(idCard, @"\d{8}");
                    if (matches.Count > 0)
                    {
                        string dateStr = matches[0].Value;
                        string year = dateStr.Substring(0, 4);
                        string month = dateStr.Substring(4, 2);
                        string day = dateStr.Substring(6, 2);

                        if (IsValidDate(year, month, day))
                        {
                            return year + "-" + month + "-" + day; // 使用字符串拼接
                        }
                    }
                }
            }
            catch (Exception)
            {
                // 忽略解析错误，返回空字符串
            }

            return string.Empty;
        }

        /// <summary>
        /// 验证日期是否有效
        /// </summary>
        private static bool IsValidDate(string year, string month, string day)
        {
            int y = 0, m = 0, d = 0;
            try
            {
                if (int.TryParse(year, out y) &&
                    int.TryParse(month, out m) &&
                    int.TryParse(day, out d))
                {
                    if (y >= 1900 && y <= DateTime.Now.Year + 10 && // 合理年份范围
                        m >= 1 && m <= 12 &&
                        d >= 1 && d <= DateTime.DaysInMonth(y, m))
                    {
                        return true;
                    }
                }
            }
            catch (Exception)
            {
                // 忽略验证错误
            }

            return false;
        }





    }
}
