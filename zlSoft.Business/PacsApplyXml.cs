using System;
using System.Globalization;
using System.Text;
using System.Xml.Linq;
using zlSoft.Common;

namespace zlSoft.Business
{
    /// <summary>
    /// PACS 检查申请（新增）的 XML 报文解析与组装。入参/出参均为 XML。
    /// </summary>
    public class PacsApplyXml
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

                PacsApplyInfo info = ParseApply(patiVisit, testApply);
                PacsApplyResult result = HisPacsService.SendApply(info);
                return BuildResponse(msgId, eventId, sender, receiver, result);
            }
            catch (Exception ex)
            {
                LogHelper.LogInfo("PACS检查申请请求报文解析异常：" + ex.Message);
                return BuildResponse(msgId, eventId, sender, receiver,
                    new PacsApplyResult { Success = false, Message = "请求报文解析异常：" + ex.Message });
            }
        }

        private static PacsApplyInfo ParseApply(XElement patiVisit, XElement testApply)
        {
            PacsApplyInfo info = new PacsApplyInfo();

            // 患者信息
            info.PatId = GetVal(patiVisit, "pat_id");
            info.PhysicalNo = GetVal(patiVisit, "physical_no");
            info.PatName = GetVal(patiVisit, "pat_name");
            info.GenderCode = GetVal(patiVisit, "gender_name");
            info.DateBirth = FormatDate(GetVal(patiVisit, "date_birth"));
            info.IdNumber = GetVal(patiVisit, "id_number");
            info.PhoneNo = GetVal(patiVisit, "phone_no");

            // 申请信息
            info.ApplyNo = GetVal(testApply, "apply_no");
            info.Modality = GetVal(testApply, "test_type_code"); // MODALITY = test_type_code

            XElement apply = testApply.Element("apply");
            if (apply != null)
            {
                info.ApplyDate = FormatDateTime(GetVal(apply, "apply_date"));
                info.ApplyDrCode = GetVal(apply, "apply_dr_code");
                info.ApplyDrName = GetVal(apply, "apply_dr_name");
            }

            // 医嘱项目：每个 test_comp 为一组（order_no = 申请单号），items 正常只有一个 item
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

        /// <summary>组装返回XML：res_code 成功=1/失败=0</summary>
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

        private static string FormatDateTime(string dtStr)
        {
            if (string.IsNullOrWhiteSpace(dtStr))
                return dtStr;

            // 尝试解析 yyyyMMddHHmmss
            if (DateTime.TryParseExact(dtStr, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
                return dt.ToString("yyyy-MM-dd HH:mm:ss");

            // 尝试解析 yyyy-MM-dd HH:mm:ss（已是目标格式）
            if (DateTime.TryParseExact(dtStr, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                return dtStr;

            // 其他通用解析（如带毫秒等）
            if (DateTime.TryParse(dtStr, out dt))
                return dt.ToString("yyyy-MM-dd HH:mm:ss");

            return dtStr; // 无法解析，原样返回
        }
        private static string FormatDate(string dateStr)
        {
            if (string.IsNullOrWhiteSpace(dateStr))
                return dateStr;

            // 尝试解析 yyyyMMdd
            if (DateTime.TryParseExact(dateStr, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
                return dt.ToString("yyyy-MM-dd");

            // 尝试解析 yyyy-MM-dd（已格式化）
            if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                return dateStr; // 已是正确格式

            // 其他格式可尝试通用解析
            if (DateTime.TryParse(dateStr, out dt))
                return dt.ToString("yyyy-MM-dd");

            // 无法解析，原样返回
            return dateStr;
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
