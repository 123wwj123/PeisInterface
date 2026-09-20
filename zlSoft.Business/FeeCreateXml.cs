using System;
using System.Text;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using zlSoft.Common;

namespace zlSoft.Business
{
    /// <summary>
    /// 生成费用（同时支持收费和退费）的 XML 报文解析与组装。入参/出参均为 XML。
    /// 返回时将 HIS createMenZhenFy 的完整出参（DTO_MZ_CreateMzFy）透传给体检（包含原代码未取的字段）。
    /// </summary>
    public class FeeCreateXml
    {
        // 返回主记录（DTO_MZ_CreateMzFy）字段（按HIS文档顺序）
        private static readonly string[] MainFields = new string[]
        {
            "feiYongId", "feiYongLx", "kaiDanRq", "kaiDanKs", "kaiDanKsMc",
            "kaiDanYs", "kaiDanYsXm", "zhiXingKs", "zhiXingKsMc", "bingRenId"
        };

        // 返回明细（feiYongMxList）字段（按HIS文档顺序）
        private static readonly string[] DetailFields = new string[]
        {
            "feiYongMxId", "feiYongId", "shunXuHao", "feiYongLx", "xiangMuId",
            "xiangMuMc", "xiangMuLx", "yaoPinGg", "jiLiang", "jiLiangDw",
            "pinCi", "geiYaoFs", "shuLiang", "jieSuanJia", "jieSuanJe"
        };

        public static string Process(string requestXml)
        {
            string msgId = "", eventId = "CreateCharge", sender = "", receiver = "";
            try
            {
                XDocument doc = XDocument.Parse(requestXml);
                XElement root = doc.Root;

                XElement request = root.Element("request");
                if (request != null)
                {
                    msgId = GetVal(request, "msg_id");
                    if (!string.IsNullOrEmpty(GetVal(request, "event_id"))) eventId = GetVal(request, "event_id");
                    sender = GetVal(request, "sender");
                    receiver = GetVal(request, "receiver");
                }

                XElement body = root.Element("body");
                XElement req = body != null ? body.Element("createMenZhenFyReq") : null;
                if (req == null)
                {
                    return BuildResponse(msgId, eventId, sender, receiver,
                        new FeeResult { Success = false, Message = "请求报文缺少 body/createMenZhenFyReq 节点" });
                }

                FeeInfo info = ParseFeeReq(req);
                FeeResult result = HisFeeService.CreateFee(info);
                return BuildResponse(msgId, eventId, sender, receiver, result);
            }
            catch (Exception ex)
            {
                LogHelper.LogInfo("生成费用请求报文解析异常：" + ex.Message);
                return BuildResponse(msgId, eventId, sender, receiver,
                    new FeeResult { Success = false, Message = "请求报文解析异常：" + ex.Message });
            }
        }

        private static FeeInfo ParseFeeReq(XElement req)
        {
            FeeInfo info = new FeeInfo();
            info.BingRenId = GetVal(req, "bingRenId");
            info.JiuZhenKh = GetVal(req, "jiuZhenKh");
            info.CaoZuoYuan = GetVal(req, "caoZuoYuan");
            info.YuanQuId = GetVal(req, "yuanQuId");
            info.YingYongId = GetVal(req, "yingYongId");
            info.KaiDanKs = GetVal(req, "kaiDanKs");
            info.DengJiLsh = GetVal(req, "dengJiLsh");
            info.ShouTuiBz = GetVal(req, "shouTuiBz");

            // 每个 <feiYongMxList> 节点为一条费用明细（可循环多条）
            foreach (XElement mx in req.Elements("feiYongMxList"))
            {
                FeeItem it = new FeeItem();
                it.FeiYongMxId = GetVal(mx, "feiYongMxId");
                it.FeiYongId = GetVal(mx, "feiYongId");
                it.ShouFeiXmId = GetVal(mx, "shouFeiXmId");
                it.ShouFeiXmMc = GetVal(mx, "shouFeiXmMc");
                it.ShuLiang = GetVal(mx, "shuLiang");
                it.DanJia = GetVal(mx, "danJia");
                it.JieSuanJe = GetVal(mx, "jieSuanJe");
                it.ZhiXingKs = GetVal(mx, "zhiXingKs");
                it.ZhiXingKsMc = GetVal(mx, "zhiXingKsMc");
                info.Items.Add(it);
            }
            return info;
        }

        /// <summary>组装返回XML：res_code 成功=1/失败=0；body/DTO_MZ_CreateMzFy 透传HIS出参</summary>
        private static string BuildResponse(string msgId, string eventId, string sender, string receiver, FeeResult result)
        {
            XElement response = new XElement("response",
                new XElement("msg_id", msgId ?? ""),
                new XElement("event_id", eventId ?? ""),
                new XElement("creat_time", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")),
                new XElement("sender", sender ?? ""),
                new XElement("receiver", receiver ?? ""),
                new XElement("res_code", result.Success ? "1" : "0"),
                new XElement("res_msg", result.Message ?? ""));

            XElement dto = new XElement("DTO_MZ_CreateMzFy");
            JObject rd = result.ReturnData;
            if (rd != null)
            {
                foreach (string f in MainFields)
                {
                    dto.Add(new XElement(f, GetJson(rd, f)));
                }

                JToken list = rd["feiYongMxList"];
                if (list != null && list.Type == JTokenType.Array)
                {
                    foreach (JToken item in (JArray)list)
                    {
                        JObject io = item as JObject;
                        if (io == null) continue;
                        XElement mx = new XElement("feiYongMxList");
                        foreach (string f in DetailFields)
                        {
                            mx.Add(new XElement(f, GetJson(io, f)));
                        }
                        dto.Add(mx);
                    }
                }
            }

            XElement message = new XElement("message", response, new XElement("body", dto));
            XDocument doc = new XDocument(new XDeclaration("1.0", "UTF-8", null), message);
            using (var sw = new Utf8StringWriter())
            {
                doc.Save(sw);
                return sw.ToString();
            }
        }

        private static string GetJson(JObject obj, string name)
        {
            if (obj == null) return "";
            JToken t = obj[name];
            if (t == null || t.Type == JTokenType.Null) return "";
            return t.ToString();
        }

        private static string GetVal(XElement parent, string name)
        {
            if (parent == null) return "";
            XElement e = parent.Element(name);
            return e != null ? e.Value.Trim() : "";
        }

        private class Utf8StringWriter : System.IO.StringWriter
        {
            public override Encoding Encoding { get { return Encoding.UTF8; } }
        }
    }
}
