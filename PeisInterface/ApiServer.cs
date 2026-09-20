using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using zlSoft.Business;
using zlSoft.Common;

namespace SpdService
{
    /// <summary>
    /// 进程内自托管的 HTTP API 服务（基于 HttpListener），
    /// 用于接收体检系统主动调用（XML入参/出参）。
    /// 与定时器 tmrRun 在同一进程内共存，互不干扰。
    /// LIS/PACS 仍走 tmrRun 的中间表轮询，本服务只负责 HIS 侧的 API。
    /// </summary>
    public class ApiServer
    {
        private HttpListener _listener;
        private volatile bool _running;

        // 统一接收路径（所有请求发往此地址）
        private const string PATH_UNIFIED = "/api/his";   // 可根据实际情况调整

        // 路由路径（不区分大小写）
        private const string PATH_PATIENT_REGISTER = "/api/patient/register";
        private const string PATH_FEE_CHARGE = "/api/fee/charge";
        private const string PATH_FEE_CREATE = "/api/fee/create";
        private const string PATH_LIS_APPLY = "/api/lis/apply";
        private const string PATH_PACS_APPLY = "/api/pacs/apply";
        private const string PATH_LIS_CANCEL = "/api/lis/cancel";
        private const string PATH_PACS_CANCEL = "/api/pacs/cancel";

        // event_id 与处理类的映射（也可用字典或配置文件）
        private const string EVENT_PATIENT_QUERY = "ArchiveQuery";
        private const string EVENT_PATIENT_REGISTER = "PesPatientInfoAdd";
        private const string EVENT_FEE_CREATE = "CreateCharge";
        private const string EVENT_LIS_APPLY = "TestAppInfoAdd";
        private const string EVENT_LIS_CANCEL = "TestAppInfoUpdate";
        private const string EVENT_PACS_APPLY = "CheckAppInfoAdd";
        private const string EVENT_PACS_CANCEL = "CheckAppInfoUpdate";

        public void Start()
        {
            // 例：http://+:8090/   （+ 表示监听本机所有IP，需管理员权限或 netsh 授权）
            string prefix = zlConfiguration.GetAppSetting("ApiListenPrefix");
            if (string.IsNullOrEmpty(prefix))
            {
                prefix = "http://+:8090/";
            }

            _listener = new HttpListener();
            _listener.Prefixes.Add(prefix);
            _listener.Start();
            _running = true;

            Task.Factory.StartNew(ListenLoop, TaskCreationOptions.LongRunning);
            LogHelper.LogInfo("体检API服务已启动，监听：" + prefix);
        }

        public void Stop()
        {
            _running = false;
            try
            {
                if (_listener != null)
                {
                    _listener.Stop();
                    _listener.Close();
                }
            }
            catch { }
        }

        private void ListenLoop()
        {
            while (_running)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = _listener.GetContext(); // 阻塞等待请求
                }
                catch (Exception)
                {
                    if (!_running) break;   // 正常停止
                    continue;
                }
                // 每个请求独立线程处理，避免相互阻塞
                ThreadPool.QueueUserWorkItem(o => HandleRequest(ctx));
            }
        }

        //private void HandleRequest(HttpListenerContext ctx)
        //{
        //    string responseXml;
        //    try
        //    {
        //        string path = ctx.Request.Url.AbsolutePath.TrimEnd('/');
        //        if (path.Equals(PATH_PATIENT_REGISTER, StringComparison.OrdinalIgnoreCase))
        //        {
        //            string requestXml = ReadRequestBody(ctx.Request);
        //            responseXml = PatientRegisterXml.Process(requestXml);
        //            WriteResponse(ctx, 200, responseXml);
        //        }
        //        else if (path.Equals(PATH_FEE_CREATE, StringComparison.OrdinalIgnoreCase))
        //        {
        //            string requestXml = ReadRequestBody(ctx.Request);
        //            responseXml = FeeCreateXml.Process(requestXml);
        //            WriteResponse(ctx, 200, responseXml);
        //        }
        //        else if (path.Equals(PATH_LIS_APPLY, StringComparison.OrdinalIgnoreCase))
        //        {
        //            string requestXml = ReadRequestBody(ctx.Request);
        //            responseXml = LisApplyXml.Process(requestXml);
        //            WriteResponse(ctx, 200, responseXml);
        //        }
        //        else if (path.Equals(PATH_PACS_APPLY, StringComparison.OrdinalIgnoreCase))
        //        {
        //            string requestXml = ReadRequestBody(ctx.Request);
        //            responseXml = PacsApplyXml.Process(requestXml);
        //            WriteResponse(ctx, 200, responseXml);
        //        }
        //        else if (path.Equals(PATH_LIS_CANCEL, StringComparison.OrdinalIgnoreCase))
        //        {
        //            string requestXml = ReadRequestBody(ctx.Request);
        //            responseXml = LisCancelXml.Process(requestXml);
        //            WriteResponse(ctx, 200, responseXml);
        //        }
        //        else if (path.Equals(PATH_PACS_CANCEL, StringComparison.OrdinalIgnoreCase))
        //        {
        //            string requestXml = ReadRequestBody(ctx.Request);
        //            responseXml = PacsCancelXml.Process(requestXml);
        //            WriteResponse(ctx, 200, responseXml);
        //        }
        //        else
        //        {
        //            WriteResponse(ctx, 404, "<message><response><res_code>0</res_code><res_msg>未知的接口路径</res_msg></response></message>");
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        LogHelper.LogInfo("API请求处理异常：" + ex.Message);
        //        try
        //        {
        //            WriteResponse(ctx, 500, "<message><response><res_code>0</res_code><res_msg>服务端异常：" +
        //                System.Security.SecurityElement.Escape(ex.Message) + "</res_msg></response></message>");
        //        }
        //        catch { }
        //    }
        //}

        private void HandleRequest(HttpListenerContext ctx)
        {
            string responseXml;
            try
            {
                // 1. 检查路径是否匹配统一入口
                string path = ctx.Request.Url.AbsolutePath.TrimEnd('/');

                // 新三方 JSON 建档接口
                if (path.Equals(PATH_PATIENT_REGISTER, StringComparison.OrdinalIgnoreCase))
                {
                    string requestJson = ReadRequestBody(ctx.Request);

                    string responseJson =
                        PatientRegisterJson.Process(requestJson);

                    WriteJsonResponse(ctx, 200, responseJson);

                    return;
                }

                // 三方 JSON 收费申请/撤销收费申请（调鼎收退费接口）
                if (path.Equals(PATH_FEE_CHARGE, StringComparison.OrdinalIgnoreCase))
                {
                    string requestJson = ReadRequestBody(ctx.Request);

                    string responseJson = FeeChargeJson.Process(requestJson);

                    WriteJsonResponse(ctx, 200, responseJson);

                    return;
                }


                // 三方 JSON 检验申请/撤销申请（调鼎LIS接口，按条码逐次调用）
                if (path.Equals(PATH_LIS_APPLY, StringComparison.OrdinalIgnoreCase))
                {
                    string requestJson = ReadRequestBody(ctx.Request);

                    string responseJson = LisApplyJson.Process(requestJson);

                    WriteJsonResponse(ctx, 200, responseJson);

                    return;
                }


                // 三方 JSON 检查申请/撤销申请（调鼎PACS接口，ECG已过滤）
                if (path.Equals(PATH_PACS_APPLY, StringComparison.OrdinalIgnoreCase))
                {
                    string requestJson = ReadRequestBody(ctx.Request);

                    string responseJson = PacsApplyJson.Process(requestJson);

                    WriteJsonResponse(ctx, 200, responseJson);

                    return;
                }

                if (!path.Equals(PATH_UNIFIED, StringComparison.OrdinalIgnoreCase))
                {
                    WriteResponse(ctx, 404, "<message><response><res_code>0</res_code><res_msg>未知的接口路径</res_msg></response></message>");
                    return;
                }
                
                // 2. 读取请求XML
                string requestXml = ReadRequestBody(ctx.Request);

                // 3. 提取 event_id
                string eventId = ExtractEventId(requestXml);
                string msgid = ExtractMsgId(requestXml);
                if (string.IsNullOrEmpty(eventId))
                {
                    WriteResponse(ctx, 400, "<message><response><res_code>0</res_code><res_msg>缺少 event_id 或解析失败</res_msg></response></message>");
                    return;
                }

                // 4. 根据 event_id 分发
                switch (eventId)
                {
                    case EVENT_PATIENT_QUERY://中智先调用档案查询，没有就会建档，我们这边默认返回都是失败,让他们之间建档
                        responseXml = $"<message><response><sender>HIS</sender><receiver>体检</receiver><res_code>0</res_code><event_id>{eventId}</event_id><msg_id>{msgid}</msg_id><res_msg>请调用建档接口</res_msg></response></message>";
                        break;
                    case EVENT_PATIENT_REGISTER:
                        responseXml = PatientRegisterXml.Process(requestXml);
                        break;
                    case EVENT_FEE_CREATE:
                        responseXml = FeeCreateXml.Process(requestXml);
                        break;
                    case EVENT_LIS_APPLY:
                        responseXml = LisApplyXml.Process(requestXml);
                        break;
                    case EVENT_LIS_CANCEL:
                        responseXml = LisCancelXml.Process(requestXml);
                        break;
                    case EVENT_PACS_APPLY:
                        responseXml = PacsApplyXml.Process(requestXml);
                        break;
                    case EVENT_PACS_CANCEL:
                        responseXml = PacsCancelXml.Process(requestXml);
                        break;
                    default:
                        WriteResponse(ctx, 400, $"<message><response><res_code>0</res_code><res_msg>未知的 event_id: {eventId}</res_msg></response></message>");
                        return;
                }

                WriteResponse(ctx, 200, responseXml);
            }
            catch (Exception ex)
            {
                LogHelper.LogInfo("API请求处理异常：" + ex.Message);
                try
                {
                    WriteResponse(ctx, 500, "<message><response><res_code>0</res_code><res_msg>服务端异常：" +
                        System.Security.SecurityElement.Escape(ex.Message) + "</res_msg></response></message>");
                }
                catch { }
            }
        }

        /// <summary>
        /// 从XML中提取 <event_id> 节点的值
        /// </summary>
        private string ExtractEventId(string xml)
        {
            try
            {
                XDocument doc = XDocument.Parse(xml);
                XElement eventNode = doc.Descendants("event_id").FirstOrDefault();
                return eventNode?.Value?.Trim();
            }
            catch
            {
                return null;
            }
        }
        /// <summary>
        /// 从XML中提取 <msg_id> 节点的值
        /// </summary>
        private string ExtractMsgId(string xml)
        {
            try
            {
                XDocument doc = XDocument.Parse(xml);
                XElement eventNode = doc.Descendants("msg_id").FirstOrDefault();
                return eventNode?.Value?.Trim();
            }
            catch
            {
                return null;
            }
        }
        //private static string ReadRequestBody(HttpListenerRequest request)
        //{
        //    Encoding enc = request.ContentEncoding ?? Encoding.UTF8;
        //    using (var sr = new StreamReader(request.InputStream, enc))
        //    {
        //        return sr.ReadToEnd();
        //    }
        //}
        //private static string ReadRequestBody(HttpListenerRequest request)
        //{
        //    // 打印请求头信息，便于排查
        //    string contentType = request.Headers["Content-Type"];
        //    LogHelper.LogInfo($"Request Content-Type: {contentType}");

        //    // 优先使用请求指定的编码，否则默认 UTF-8
        //    Encoding enc = request.ContentEncoding ?? Encoding.UTF8;
        //    LogHelper.LogInfo($"Using encoding: {enc.WebName}");

        //    using (var sr = new StreamReader(request.InputStream, enc))
        //    {
        //        return sr.ReadToEnd();
        //    }
        //}
        private static string ReadRequestBody(HttpListenerRequest request)
        {
            // 强制 UTF-8 解码，确保中文不乱码
            using (var sr = new StreamReader(request.InputStream, Encoding.UTF8))
            {
                return sr.ReadToEnd();
            }
        }


        private static void WriteResponse(HttpListenerContext ctx, int statusCode, string xml)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(xml ?? "");
            ctx.Response.StatusCode = statusCode;
            ctx.Response.ContentType = "text/xml; charset=utf-8";
            ctx.Response.ContentEncoding = Encoding.UTF8;
            ctx.Response.ContentLength64 = buffer.Length;
            using (Stream os = ctx.Response.OutputStream)
            {
                os.Write(buffer, 0, buffer.Length);
            }
        }

        private static void WriteJsonResponse(
                        HttpListenerContext ctx,
                        int statusCode,
                        string json)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(json ?? "");

            ctx.Response.StatusCode = statusCode;
            ctx.Response.ContentType = "application/json; charset=utf-8";
            ctx.Response.ContentEncoding = Encoding.UTF8;
            ctx.Response.ContentLength64 = buffer.Length;

            using (Stream os = ctx.Response.OutputStream)
            {
                os.Write(buffer, 0, buffer.Length);
            }
        }
    }
}
