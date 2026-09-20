using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;


//功能：提供常用的数据加密和解密的方法
//程序：陈玉强
//日期：2013-04-01

namespace zlSoft.Common
{
    /// <summary>
    /// 提供常用的数据加密和解密的方法。
    /// </summary>
    public class zlEncryption
    {
        public zlEncryption()
        {
        }

        /// <summary>
        /// 对字符串进行MD5加密
        /// </summary>
        /// <param name="strPwd">加密明文</param>
        /// <returns>加密密文</returns>
        public static string MD5(string strPwd)
        {
            MD5 md5 = new MD5CryptoServiceProvider();
            byte[] data = System.Text.Encoding.Default.GetBytes(strPwd);//将字符编码为一个字节序列 
            byte[] md5data = md5.ComputeHash(data);//计算data字节数组的哈希值 
            md5.Clear();
            string str = "";
            for (int i = 0; i < md5data.Length; i++)
            {
                str += md5data[i].ToString("x").PadLeft(2, '0');
            }
            str = str.ToUpper();
            return str;
        }

        /// <summary>
        /// 对字符串进行加密。
        /// </summary>
        /// <param name="src">要加密的字符串。</param>
        /// <returns>返回加密后的字符串。</returns>
        public static string Encrypt(string src)
        {
            if (src == "") return src;

            Encoding enc = Encoding.Unicode;
            byte[] input = enc.GetBytes(src);
            byte[] output = zlEncryption.Encrypt(input);

            string result = System.Convert.ToBase64String(output);
            return result;
        }

        /// <summary>
        /// 对字节数组进行加密。
        /// </summary>
        /// <param name="input">要加密的字节数组。</param>
        /// <returns>返回加密后的字节数组。</returns>
        public static byte[] Encrypt(byte[] input)
        {
            byte[] key = { 0x13, 0x90, 0x11, 0x99, 0x93, 0x13, 0x80, 0x12 };
            byte[] iv = { 0x08, 0x01, 0x41, 0x39, 0x01, 0x19, 0x99, 0x31 };

            DESCryptoServiceProvider desp = new DESCryptoServiceProvider();
            ICryptoTransform enf = desp.CreateEncryptor(key, iv);

            byte[] output = enf.TransformFinalBlock(input, 0, input.Length);

            return output;
        }

        /// <summary>
        /// 对字节数组进行解密。
        /// </summary>
        /// <param name="input">要解密的字节数组。</param>
        /// <returns>返回解密后的字节数组。</returns>
        public static byte[] Decrypt(byte[] input)
        {
            byte[] key = { 0x13, 0x90, 0x11, 0x99, 0x93, 0x13, 0x80, 0x12 };
            byte[] iv = { 0x08, 0x01, 0x41, 0x39, 0x01, 0x19, 0x99, 0x31 };

            DESCryptoServiceProvider desp = new DESCryptoServiceProvider();
            ICryptoTransform enf = desp.CreateDecryptor(key, iv);

            byte[] output = enf.TransformFinalBlock(input, 0, input.Length);

            return output;
        }

        /// <summary>
        /// 对字符串进行解密。
        /// </summary>
        /// <param name="src">要解密的字符串。</param>
        /// <returns>返回解密后的字符串。</returns>
        public static string Decrypt(string src)
        {
            if (src == "") return src;

            Encoding enc = Encoding.Unicode;

            byte[] input = System.Convert.FromBase64String(src);
            byte[] output = zlEncryption.Decrypt(input);

            string result = enc.GetString(output);
            return result;
        }
    }
}
