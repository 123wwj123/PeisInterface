using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Xml;
using System.Collections;
using System.Configuration;

namespace zlSoft.Business
{
    public class clsGetSign
    {
        //生成sign使用到的key
        static string strKey = ConfigurationSettings.AppSettings["key"].ToString();

        /// <summary>
        /// 微信线下扫码付sign获取
        /// 规则：签名生成的通用步骤如下：
        ///     第一步，设所有发送或者接收到的数据为集合M，将集合M内非空参数值的参数按照参数名ASCII码从小到大排序（字典序），
        ///     使用URL键值对的格式（即key1=value1&key2=value2…&key=pay_md5_key,其中pay_md5_key为睿博统一提供）拼接成字符串stringA。
        ///     特别注意以下重要规则：
        ///         ◆ 参数名ASCII码从小到大排序（字典序）；
        ///         ◆ 如果参数的值为空不参与签名；
        ///         ◆ 参数名区分大小写；
        ///         ◆ 验证调用返回或微信主动通知签名时，传送的sign参数不参与签名，将生成的签名与该sign值作校验。
        ///     第二步，在stringA最后拼接上key得到stringSignTemp字符串，并对stringSignTemp进行MD5运算，再将得到的字符串所有字符转换为大写，得到sign值signValue。
        /// </summary>
        /// <param name="strrequest">业务入参</param>
        /// <returns>生成的sign码</returns>
        public static string GetOutSign(string strrequest)
        {
            //定义变量
            string strSign = "";
            XmlDocument xml = new XmlDocument();
            XmlNode xn;
            XmlNodeList xnl;
            //加载XML，获取XML中所有节点进行循环，只取参数值不为空，并且参数名不为patname、sign
            ArrayList aryItem = new ArrayList();
            xml.LoadXml(strrequest);
            xn = xml.DocumentElement.SelectSingleNode("//request");
            xnl = xn.ChildNodes;
            for (int intN = 0; intN < xnl.Count; intN++)
            {
                if (xnl[intN].InnerText != "" && xnl[intN].Name != "patname" && xnl[intN].Name != "sign")
                    aryItem.Add(xnl[intN].Name);
            }

            //按照参数名排序，并进行MD5加密
            aryItem.Sort();
            for (int intX = 0; intX < aryItem.Count; intX++)
            {
                strSign = strSign + aryItem[intX].ToString() + "=" + xml.DocumentElement.SelectSingleNode("//request//" + aryItem[intX].ToString()).InnerText + "&";
            }
            return UserMd5(strSign + "key=" + strKey).ToUpper();
        }

        /// <summary>
        /// 将字符串进行MD5加密
        /// </summary>
        /// <param name="strText">要加密的字符串</param>
        /// <returns>加密后的字符串</returns>
        public static string UserMd5(string str)
        {
            string cl = str;
            string pwd = "";
            System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create();//实例化一个md5对像
            // 加密后是一个字节类型的数组，这里要注意编码UTF8/Unicode等的选择　
            byte[] s = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(cl));
            // 通过使用循环，将字节类型的数组转换为字符串，此字符串是常规字符格式化所得
            for (int i = 0; i < s.Length; i++)
            {
                // 将得到的字符串使用十六进制类型格式。格式后的字符是小写的字母，如果使用大写（X）则格式后的字符是大写字符 

                pwd = pwd + s[i].ToString("X2");

            }
            return pwd;
        }

        /// <summary>
        /// 校验睿博返回的Xml串sign
        /// </summary>
        /// <param name="strResponse">睿博返回的Xml串</param>
        /// <returns>校验结果</returns>
        public static Boolean CheckSign(string strResponse)
        {
            string strInSign;
            string strGetSign;
            XmlDocument xml = new XmlDocument();
            xml.LoadXml(strResponse);

            //获取返回的xml串中的sign
            strInSign = xml.SelectSingleNode("//request//sign").InnerText;
            //根据返回的xml串自己生成sign
            strGetSign = GetInSign(strResponse);
            //对两个sign进行对比，如果不一致，返回校验失败信息
            if (strInSign != strGetSign)
                return false;
            return true;
        }

        /// <summary>
        /// 计算睿博返回的xml串口sign值
        /// </summary>
        /// <param name="strResponse">睿博返回的Xml串</param>
        /// <returns></returns>
        private static string GetInSign(string strResponse)
        {
            //定义变量
            string strSign = "";
            XmlDocument xml = new XmlDocument();
            XmlNode xn;
            XmlNodeList xnl;
            //加载XML，获取XML中所有节点进行循环，只取参数值不为空，并且参数名不为patname、sign
            ArrayList aryItem = new ArrayList();
            xml.LoadXml(strResponse);
            xn = xml.DocumentElement.SelectSingleNode("//request");
            xnl = xn.ChildNodes;
            for (int intN = 0; intN < xnl.Count; intN++)
            {
                if (xnl[intN].InnerText != "" && xnl[intN].Name != "sign")
                    aryItem.Add(xnl[intN].Name);
            }

            //按照参数名排序，并进行MD5加密
            aryItem.Sort();
            for (int intX = 0; intX < aryItem.Count; intX++)
            {
                strSign = strSign + aryItem[intX].ToString() + "=" + xml.DocumentElement.SelectSingleNode("//request//" + aryItem[intX].ToString()).InnerText + "&";
            }
            return UserMd5(strSign + "key=" + strKey).ToUpper();
        }
    }
}
