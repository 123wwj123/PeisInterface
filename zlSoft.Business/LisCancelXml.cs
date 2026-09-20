using System;
using System.Text;
using System.Xml.Linq;
using zlSoft.Common;

namespace zlSoft.Business
{
    /// <summary>
    /// 检验撤销（取消）的 XML 报文解析与组装。
    /// 入参与检验申请完全一致，仅 apply_state=3 表示取消（1 表示申请）。
    /// </summary>
    public class LisCancelXml
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
                        new LisApplyResult { Success = false, Message = "请求报文缺少 body/patiVisit 或 body/testApply 节点" });
                }

                // apply_state=1 申请，apply_state=2 取消；本接口仅处理取消
                string applyState = GetVal(testApply, "apply_state");
                if (applyState != "2")
                {
                    return BuildResponse(msgId, eventId, sender, receiver,
                        new LisApplyResult { Success = false, Message = "apply_state=" + applyState + " 非撤销请求（应为3），未执行撤销" });
                }

                LisApplyInfo info = ParseCancel(patiVisit, testApply);
                LisApplyResult result = HisLisService.CancelApply(info);
                return BuildResponse(msgId, eventId, sender, receiver, result);
            }
            catch (Exception ex)
            {
                LogHelper.LogInfo("检验撤销请求报文解析异常：" + ex.Message);
                return BuildResponse(msgId, eventId, sender, receiver,
                    new LisApplyResult { Success = false, Message = "请求报文解析异常：" + ex.Message });
            }
        }

        private static LisApplyInfo ParseCancel(XElement patiVisit, XElement testApply)
        {
            LisApplyInfo info = new LisApplyInfo();
            info.PhysicalNo = GetVal(patiVisit, "physical_no");
            info.ApplyNo = GetVal(testApply, "apply_no");
            info.ApplyState = GetVal(testApply, "apply_state");

            XElement apply = testApply.Element("apply");
            if (apply != null)
            {
                info.ApplyDate = GetVal(apply, "apply_date");
                info.ApplyDrCode = GetVal(apply, "apply_dr_code");
                info.ApplyDrName = GetVal(apply, "apply_dr_name");
            }

            XElement applyOrg = testApply.Element("apply_org");
            if (applyOrg != null)
            {
                info.ApplyDeptCode = GetVal(applyOrg, "apply_dept_code");
                info.ApplyDeptName = GetVal(applyOrg, "apply_dept_name");
                info.ApplyOrgCode = GetVal(applyOrg, "apply_org_code");
                info.ApplyOrgName = GetVal(applyOrg, "apply_org_name");
            }

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
                            LisApplyItem li = new LisApplyItem();
                            li.OrderNo = orderNo;
                            li.TestItemCode = GetVal(item, "test_item_code");
                            li.TestItemName = GetVal(item, "test_item_name");
                            info.Items.Add(li);
                        }
                    }
                }
            }
            return info;
        }

        private static string BuildResponse(string msgId, string eventId, string sender, string receiver, LisApplyResult result)
        {
            XElement message = new XElement("message",
                new XElement("response",
                    new XElement("msg_id", msgId ?? ""),
                    new XElement("event_id", eventId ?? ""),
                    new XElement("creat_time", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")),
                    new XElement("sender", sender ?? ""),
                    new XElement("receiver", receiver ?? ""),
                    new XElement("res_code", result.Success ? "1" : "0"),
                    new XElement("res_msg", result.Message ?? "")),
                new XElement("body",
                    new XElement("apply_no", result.ApplyNo ?? "")));

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
