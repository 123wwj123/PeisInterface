using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Org.BouncyCastle.Utilities.Encoders;

namespace zlSoft.Common
{
    public class MainSm4
    {
        /// <summary>
        /// 加密ECB模式
        /// </summary>
        /// <param name="secretKey">密钥</param>
        /// <param name="hexString">明文是否是十六进制</param>
        /// <param name="plainText">明文</param>
        /// <returns>返回密文</returns>
        public static String Encrypt_ECB(String secretKey, bool hexString, String plainText)
        {
            Sm4Context ctx = new Sm4Context();
            ctx.isPadding = true;
            ctx.mode = Sm4.SM4_ENCRYPT;
            byte[] keyBytes;
            if (hexString)
            {
                keyBytes = Hex.Decode(secretKey);
            }
            else
            {
                keyBytes = Encoding.Default.GetBytes(secretKey);
            }
            Sm4 sm4 = new Sm4();
            sm4.sm4_setkey_enc(ctx, keyBytes);
            byte[] encrypted = sm4.sm4_crypt_ecb(ctx, Encoding.UTF8.GetBytes(plainText));
            String cipherText = Encoding.UTF8.GetString(Hex.Encode(encrypted));
            return cipherText;
        }

        /// <summary>
        /// 解密ECB模式
        /// </summary>
        /// <param name="secretKey">密钥</param>
        /// <param name="hexString">明文是否是十六进制</param>
        /// <param name="cipherText">密文</param>
        /// <returns>返回明文</returns>
        public static String Decrypt_ECB(String secretKey, bool hexString, String cipherText)
        {
            Sm4Context ctx = new Sm4Context();
            ctx.isPadding = true;
            ctx.mode = Sm4.SM4_DECRYPT;

            byte[] keyBytes;
            if (hexString)
            {
                keyBytes = Hex.Decode(secretKey);
            }
            else
            {
                keyBytes = Encoding.UTF8.GetBytes(secretKey);
            }

            Sm4 sm4 = new Sm4();
            sm4.sm4_setkey_dec(ctx, keyBytes);
            byte[] decrypted = sm4.sm4_crypt_ecb(ctx, Hex.Decode(cipherText));
            return Encoding.UTF8.GetString(decrypted).Replace("\0","");

        }

        /// <summary>
        /// 加密CBC模式
        /// </summary>
        /// <param name="secretKey">密钥</param>
        /// <param name="hexString">明文是否是十六进制</param>
        /// <param name="iv"></param>
        /// <param name="plainText">明文</param>
        /// <returns>返回密文</returns>
        public String Encrypt_CBC(String secretKey, bool hexString, string iv, String plainText)
        {
            Sm4Context ctx = new Sm4Context();
            ctx.isPadding = true;
            ctx.mode = Sm4.SM4_ENCRYPT;
            byte[] keyBytes;
            byte[] ivBytes;
            if (hexString)
            {
                keyBytes = Hex.Decode(secretKey);
                ivBytes = Hex.Decode(iv);
            }
            else
            {
                keyBytes = Encoding.Default.GetBytes(secretKey);
                ivBytes = Encoding.Default.GetBytes(iv);
            }
            Sm4 sm4 = new Sm4();
            sm4.sm4_setkey_enc(ctx, keyBytes);
            byte[] encrypted = sm4.sm4_crypt_cbc(ctx, ivBytes, Encoding.Default.GetBytes(plainText));
            String cipherText = Encoding.Default.GetString(Hex.Encode(encrypted));
            return cipherText;
        }
        /// <summary>
        /// 解密CBC模式
        /// </summary>
        /// <param name="secretKey">密钥</param>
        /// <param name="hexString">明文是否是十六进制</param>
        /// <param name="iv"></param>
        /// <param name="cipherText">密文</param>
        /// <returns>返回明文</returns>
        public String Decrypt_CBC(String secretKey, bool hexString, string iv, String cipherText)
        {
            Sm4Context ctx = new Sm4Context();
            ctx.isPadding = true;
            ctx.mode = Sm4.SM4_DECRYPT;
            byte[] keyBytes;
            byte[] ivBytes;
            if (hexString)
            {
                keyBytes = Hex.Decode(secretKey);
                ivBytes = Hex.Decode(iv);
            }
            else
            {
                keyBytes = Encoding.Default.GetBytes(secretKey);
                ivBytes = Encoding.Default.GetBytes(iv);
            }
            Sm4 sm4 = new Sm4();
            sm4.sm4_setkey_dec(ctx, keyBytes);
            byte[] decrypted = sm4.sm4_crypt_cbc(ctx, ivBytes, Hex.Decode(cipherText));
            return Encoding.Default.GetString(decrypted);
        }

        //[STAThread]
        //public static string SM3QM(string strIn)
        //{
        //    byte[] md = new byte[32];
        //    byte[] msg1 = Encoding.Default.GetBytes(strIn);
        //    SM3Class.SM3Digest sm3 = new SM3Class.SM3Digest();
        //    sm3.BlockUpdate(msg1, 0, msg1.Length);
        //    sm3.DoFinal(md, 0);
        //    System.String s = new UTF8Encoding().GetString(Hex.Encode(md));
        //    return s.ToUpper();

        //}
    }
}
