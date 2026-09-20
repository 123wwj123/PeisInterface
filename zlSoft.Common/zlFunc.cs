using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

//功能：将字符串格式化为标准的日期类型
//程序：陈玉强
//日期：2013-04-01

namespace zlSoft.Common
{
    public class zlFunc
    {
        /// <summary>
        /// 将字符串格式化为标准的日期类型
        /// </summary>
        /// <param name="strDate">需要转换的字符串</param>
        /// <param name="strFormat">需要转换的格式</param>
        /// <returns></returns>
        public static string GetDate(string strDate, string strFormat = "yyyy-MM-dd HH:mm:ss")
        {
            string strReturn = "";
            try
            {
                DateTime dt = Convert.ToDateTime(strDate);
                strReturn = string.Format("{0:" + strFormat + "}", dt);
                return strReturn;
            }
            catch (Exception)
            {
                return strReturn;
            }
        }

        public static string GetSysDate(string strDataType,string strConn,string strFormat = "yyyy-MM-dd HH:mm:ss")
        {
            string strSQL = "";
            switch (strDataType)
            {
                case "SqlServer":
                    strSQL = "Select Getdate()";
                    break;
                case "Oracle":
                    strSQL = "select sysdate from dual";
                    break;
            }
            zlCommand cmd = new zlCommand(strDataType,strConn,strSQL);

            string strReturn = "";
            try
            {
                DateTime dt = Convert.ToDateTime(cmd.ExecuteScalar(strDataType));
                strReturn = string.Format("{0:" + strFormat + "}", dt);
                cmd.Close(strDataType);
                return strReturn;
            }
            catch (Exception)
            {
                cmd.Close(strDataType);
                return strReturn;
            }
        }

        /// <summary>
        /// 将字符串标准化为数字类型
        /// </summary>
        /// <param name="strNum">需要转换的字符串</param>
        /// <param name="intNum">保留小数位数</param>
        /// <returns></returns>
        public static string GetNum(string strNum, int intNum = 0)
        {
            string strReturn = "0";
            try
            {
                double db = Convert.ToDouble(strNum);
                strReturn = db.ToString("N" + intNum.ToString());
                return strReturn;
            }
            catch (Exception)
            {
                return strReturn;
            }
        }

        /// <summary>
        /// 检测XML节点是否存在
        /// </summary>
        /// <param name="xd">XML对象</param>
        /// <param name="strFindNode">查询节点</param>
        /// <param name="intNode">是否为检测子节点</param>
        /// <returns>True.存在节点 False.不存在节点</returns>
        public static bool CheckNode(XmlDocument xd, string strFindNode, Int16 intNode = -1)
        {
            try
            {
                XmlNode no = null;
                if (intNode != -1)
                    no = xd.SelectNodes(strFindNode).Item(intNode);
                else
                    no = xd.SelectSingleNode(strFindNode);
                return (no != null);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 检测字符串是否可以转换为数字
        /// </summary>
        /// <param name="strNumber">检测字符串</param>
        /// <returns>True.可以转换 False.无法转换</returns>
        public static bool IsNumeric(String strNumber)
        {
            Regex objNotNumberPattern = new Regex("[^0-9.-]");
            Regex objTwoDotPattern = new Regex("[0-9]*[.][0-9]*[.][0-9]*");
            Regex objTwoMinusPattern = new Regex("[0-9]*[-][0-9]*[-][0-9]*");
            String strValidRealPattern = "^([-]|[.]|[-.]|[0-9])[0-9]*[.]*[0-9]+$";
            String strValidIntegerPattern = "^([-]|[0-9])[0-9]*$";
            Regex objNumberPattern = new Regex("(" + strValidRealPattern + ")|(" + strValidIntegerPattern + ")");
            return !objNotNumberPattern.IsMatch(strNumber) &&
            !objTwoDotPattern.IsMatch(strNumber) &&
            !objTwoMinusPattern.IsMatch(strNumber) &&
            objNumberPattern.IsMatch(strNumber);
        }

        /// <summary>  
        /// 截取字符串  
        /// </summary>  
        /// <param name="inputString">原始字符串</param>  
        /// <param name="length">截取长度</param>  
        /// <returns>截取后的字符串</returns>   

        public static string zlSubstr(string inputString, int length)
        {
            ///
            ///格式化字符串长度，超出部分显示省略号,区分汉字跟字母。汉字2个字节，字母数字一个字节
            ///
            if (length >= 5)
                length = length - 3;
            string temp = string.Empty;
            if (System.Text.Encoding.Default.GetByteCount(inputString) <= length)//如果长度比需要的长度n小,返回原字符串
            {
                return inputString;
            }
            else
            {
                int t = 0;
                char[] q = inputString.ToCharArray();
                for (int i = 0; i < q.Length; i++)
                {
                    if ((int)q[i] >= 0x4E00 && (int)q[i] <= 0x9FA5)//是否汉字
                    {
                        temp += q[i];
                        t += 2;
                    }
                    else
                    {
                        temp += q[i];
                        t += 1;
                    }
                    if (t >= length)
                    {
                        break;
                    }
                }
                return (temp + "...");
            }
        }

        /// <summary>
        /// 根据传入的序列名称获取ID
        /// </summary>
        /// <param name="sequenceName"></param>
        /// <returns></returns>
        public static long zlsequenceID(string strDataType,string sequenceName)
        {
            string strSQL = "";
            strSQL = "select " + sequenceName + ".nextval from dual";
            zlCommand cmd = new zlCommand(strDataType,zlConfiguration.GetAppSetting("MDDataConn"),strSQL);
            long lngReturn = 0;
            try
            {
                lngReturn = Convert.ToInt32(cmd.ExecuteScalar(strDataType));
                cmd.Close(strDataType);
                return lngReturn;
            }
            catch (Exception)
            {
                cmd.Close(strDataType);
                return lngReturn;
            }
        }

        /// <summary>
        /// 注册码校验
        /// </summary>
        /// <param name="strInfo">返回注册内容</param>
        /// <param name="dtNow">中间库当前时间</param>
        /// <returns></returns>
        public static Boolean CheckKey(out string strInfo,DateTime dtNow)
        {
            string strKey = "akdif2s92js3h4b7xi9wk2v7f3j0ds3h";
            string strLicense;
            string strHospName;
            

            //获取注册码
            strLicense = zlConfiguration.GetAppSetting("LICENSE");
            strHospName = zlConfiguration.GetAppSetting("HOSPNAME");

            try
            {
                strLicense = MainSm4.Decrypt_ECB(strKey, false, strLicense);
                if (!strHospName.Equals(strLicense.Split('|')[1]))
                {
                    strInfo = "当前注册码不是有效的注册码！";
                    LogHelper.LogInfo("注册码医院信息错误！");
                    return false;
                }

                if (DateTime.Parse(strLicense.Split('|')[2]) < dtNow)
                {
                    strInfo = "当前注册码已经过期！";
                    LogHelper.LogInfo("当前注册码已经过期！");
                    return false;
                }


                strInfo = strLicense.Split('|')[0] + " 有效期：" + (strLicense.Split('|')[2].Equals("3000-01-01")?"长期有效": strLicense.Split('|')[2]);
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.LogInfo("系统运行错误，注册码信息解析失败。" + ex.Message);
                strInfo = "系统运行错误，注册码信息解析失败。";
                return false;
            }
        }

        /// <summary>
        /// 根据身份证号获取出生日期、年龄、性别等信息
        /// </summary>
        /// <param name="strIdCard">身份证号</param>
        /// <param name="dtNow">中间库当前时间</param>
        /// <returns>返回信息  出生日期|性别|年龄 </returns>
        public static string GetIdCardInfo(string strIdCard,DateTime dtNow)
        {
            string strBirthDay;
            DateTime dtBirthDay;
            string strSex;
            string strInfo;

            string strAge;

            int intMonth;

            strBirthDay = strIdCard.Substring(6, 4) + "-" + strIdCard.Substring(10, 2) + "-" + strIdCard.Substring(12, 2);
            dtBirthDay = DateTime.Parse(strBirthDay);

            strSex = (Convert.ToInt16(strIdCard.Substring(16, 1)) % 2) == 0 ? "女" : "男";

            //计算年龄
            //计算当前年龄月数
            intMonth = (dtNow.Year - dtBirthDay.Year) * 12 + (dtNow.Month - 1 - dtBirthDay.Month) + ((dtNow.Day - dtBirthDay.Day) >= 0 ? 1 : 0);
            //大于12岁只取岁
            if (intMonth >= 144)
            {
                strAge = (intMonth / 12) + "岁";
            }
            else
            {
                //小于12岁，大于1岁  取  几岁几月
                if (intMonth >= 12)
                {
                    strAge = (intMonth / 12) + "岁" + ((intMonth % 12) == 0 ? "" : (intMonth % 12) + "月");
                }
                else
                {
                    //小于1岁，大于1月，取
                    if (intMonth >= 1)
                    {
                        strAge = intMonth + "月";
                    }
                    else
                    {
                        strAge = (dtNow - dtBirthDay).Days + "天";
                    }
                }
            }

            strInfo = strBirthDay + "|" + strSex + "|" + strAge;
            return strInfo;
        }
    }
}
