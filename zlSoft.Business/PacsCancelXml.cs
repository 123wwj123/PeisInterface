using System;
using System.Text;
using System.Xml.Linq;
using zlSoft.Common;

namespace zlSoft.Business
{
    /// <summary>
    /// 检查撤销（取消）的 XML 报文解析与组装。
    /// 入参与检查申请完全一致，仅 apply_state=3 表示取消（1 表示申请）。
    /// </summary>
    public class PacsCancelXml
    {
        public static string Process(string requestXml)
        {
            string msgId = "", eventId = "", sender = "", receiver = "";
            try
            {
                XDocument doc = XDocument.Parse(requestXml);
                XElement root = doc.Root;

                XElement request = root.Element("request");
                if (request != null)
                {
                    msgId = GetVal(request, "msg_id");
                    eventId = GetVal(request, "event_id");
                    sender = GetVal(request, "sender");
                    receiver = GetVal(request, "receiver");
                }

                XElement body = root.Element("body");
                XElement patiVisit = body != null ? body.Element("patiVisit") : null;
                XElement testApply = body != null ? body.Element("testApply") : null;
                if (patiVisit == null || testApply == null)
                {
                    return BuildResponse(msgId, eventId, sender, receiver,
                        new PacsApplyResult { Success = false, Message = "请求报文缺少 body/patiVisit 或 body/testApply 节点" });
                }

                // apply_state=1 申请，apply_state=3 取消；本接口仅处理取消
                string applyState = GetVal(testApply, "apply_state");
                if (applyState != "3")
                {
                    return BuildResponse(msgId, eventId, sender, receiver,
                        new PacsApplyResult { Success = false, Message = "apply_state=" + applyState + " 非撤销请求（应为3），未执行撤销" });
                }

                PacsApplyInfo info = ParseCancel(patiVisit, testApply);
                PacsApplyResult result = HisPacsService.CancelApply(info);
                return BuildResponse(msgId, eventId, sender, receiver, result);
            }
            catch (Exception ex)
            {
                LogHelper.LogInfo("PACS检查撤销请求报文解析异常：" + ex.Message);
                return BuildResponse(msgId, eventId, sender, receiver,
                    new PacsApplyResult { Success = false, Message = "请求报文解析异常：" + ex.Message });
            }
        }

        private static PacsApplyInfo ParseCancel(XElement patiVisit, XElement testApply)
        {
            PacsApplyInfo info = new PacsApplyInfo();
            info.PhysicalNo = GetVal(patiVisit, "physical_no");
            info.ApplyNo = GetVal(testApply, "apply_no");
            info.ApplyState = GetVal(testApply, "apply_state");

            XElement testComps = testApply.Element("test_comps");
            if (testComps != null)
            {
                foreach (XElement comp in testComps.Elements("test_comp"))
                {
                    string orderNo = GetVal(comp, "order_no");
                    XElement items = comp.Element("items");
                    if (items != null)
                    {
                        foreach (XElement item in items.Elements("item"))
                        {
                            PacsApplyItem pi = new PacsApplyItem();
                            pi.OrderNo = orderNo;
                            pi.TestItemCode = GetVal(item, "test_item_code");
                            pi.TestItemName = GetVal(item, "test_item_name");
                            info.Items.Add(pi);
                        }
                    }
                }
            }
            return info;
        }

        private static string BuildResponse(string msgId, string eventId, string sender, string receiver, PacsApplyResult result)
        {
            XElement message = new XElement("message",
                new XElement("response",
                    new XElement("msg_id", msgId ?? ""),
                    new XElement("event_id", eventId ?? ""),
                    new XElement("creat_time", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")),
                    new XElement("sender", sender ?? ""),
                    new XElement("receiver", receiver ?? ""),
                    new XElement("res_code", result.Success ? "1" : "0"),
                    new XElement("res_msg", result.Message ?? "")));

            XDocument doc = new XDocument(new XDeclaration("1.0", "UTF-8", null), message);
            using (var sw = new Utf8StringWriter())
            {
                doc.Save(sw);
                return sw.ToString();
            }
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
