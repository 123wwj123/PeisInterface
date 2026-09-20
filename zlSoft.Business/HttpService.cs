using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Net;
using System.IO;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using zlSoft.Common;
namespace zlSoft.Business
{
    /// <summary>
    /// http连接基础类，负责底层的http通信
    /// </summary>
    public class HttpService
    {

        public static bool CheckValidationResult(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors)
        {
            //直接确认，否则打不开    
            return true;
        }

        public static string Post(string req_data, string url, string strToken,string strType)
        {
            System.GC.Collect();//垃圾回收，回收没有正常关闭的http连接

            string result = "";//返回结果


            byte[] data = Encoding.UTF8.GetBytes(req_data);
            
            HttpWebRequest request = null;
            HttpWebResponse response = null;

            //clsPublic.WriteLog("【" + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "】调用功能：" + url + "\r" + "入参：" + req_data);

            try
            {
                //设置最大连接数
                ServicePointManager.DefaultConnectionLimit = 200;
                //设置https验证方式
                if (url.StartsWith("https", StringComparison.OrdinalIgnoreCase))
                {
                    ServicePointManager.ServerCertificateValidationCallback =
                            new RemoteCertificateValidationCallback(CheckValidationResult);
                }

                /***************************************************************
                * 下面设置HttpWebRequest的相关属性
                * ************************************************************/
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls;

                request = (HttpWebRequest)WebRequest.Create(url);

                request.Method = strType;
                request.Timeout = 30000;
                request.ContentType = "application/json; charset=UTF-8";
                if (!string.IsNullOrEmpty(strToken))
                {
                    //添加Token
                    request.Headers.Add("Authorization", "Bearer " + strToken);
                }
                else
                {
                    //Token为空时用form-data方式提交数据获取Token
                    request.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
                }
                if (!string.IsNullOrEmpty(req_data))
                {
                    request.ContentLength = data.Length;
                    request.Proxy = null;
                    Stream newStream = request.GetRequestStream();
                    newStream.Write(data, 0, data.Length);//写入参数
                }
                //获取服务端返回
                response = (HttpWebResponse)request.GetResponse();

                //获取服务端返回数据
                StreamReader sr = new StreamReader(response.GetResponseStream(), Encoding.UTF8);
                result = sr.ReadToEnd().Trim();
                sr.Close();
                //clsPublic.WriteLog("【" + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")  + "】出参：" + result);
            }
            catch (System.Threading.ThreadAbortException)
            {
                System.Threading.Thread.ResetAbort();
            }
            catch (WebException e)
            {

                LogHelper.LogInfo(e.Message);
                if (e.Status == WebExceptionStatus.ProtocolError)
                {
                }
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                //关闭连接和流
                //if (response != null)
                //{
                //    response.Close();
                //}
                response.Close();
                //if (request != null)
                //{
                //    request.Abort();
                //}
                request.Abort();
            }
            return result;
        }
        /// <summary>
        /// 以 XML 方式 POST 请求体（Content-Type: application/xml），用于回传检查报告到体检接口。
        /// </summary>
        public static string PostXml(string req_data, string url)
        {
            System.GC.Collect();

            string result = "";
            byte[] data = Encoding.UTF8.GetBytes(req_data);

            HttpWebRequest request = null;
            HttpWebResponse response = null;

            try
            {
                ServicePointManager.DefaultConnectionLimit = 200;
                if (url.StartsWith("https", StringComparison.OrdinalIgnoreCase))
                {
                    ServicePointManager.ServerCertificateValidationCallback =
                            new RemoteCertificateValidationCallback(CheckValidationResult);
                }
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls;

                request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "POST";
                request.Timeout = 30000;
                request.ContentType = "application/xml; charset=UTF-8";

                if (!string.IsNullOrEmpty(req_data))
                {
                    request.ContentLength = data.Length;
                    request.Proxy = null;
                    Stream newStream = request.GetRequestStream();
                    newStream.Write(data, 0, data.Length);
                }

                response = (HttpWebResponse)request.GetResponse();
                StreamReader sr = new StreamReader(response.GetResponseStream(), Encoding.UTF8);
                result = sr.ReadToEnd().Trim();
                sr.Close();
            }
            catch (System.Threading.ThreadAbortException)
            {
                System.Threading.Thread.ResetAbort();
            }
            catch (WebException e)
            {
                LogHelper.LogInfo(e.Message);
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                if (response != null)
                {
                    response.Close();
                }
                if (request != null)
                {
                    request.Abort();
                }
            }
            return result;
        }
    }
}
