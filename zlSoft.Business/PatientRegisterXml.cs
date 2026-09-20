using System;
using System.Text;
using System.Xml.Linq;
using zlSoft.Common;

namespace zlSoft.Business
{
    /// <summary>
    /// 患者建档(患者注册V1.0) 的 XML 报文解析与组装。
    /// 入参/出参均为 XML。
    /// </summary>
    public class PatientRegisterXml
    {
        /// <summary>
        /// 处理一次建档请求：解析请求XML -> 调用HIS建档 -> 返回响应XML字符串
        /// </summary>
        public static string Process(string requestXml)
        {
            string msgId = "";
            string physicalNo = "";
            try
            {
                XDocument doc = XDocument.Parse(requestXml);
                XElement root = doc.Root;
                XElement request = root.Element("request");
                if (request != null)
                {
                    msgId = GetVal(request, "msg_id");
                }

                XElement body = root.Element("body");
                XElement pati = body != null ? body.Element("patiInfo") : null;
                if (pati == null)
                {
                    return BuildResponse(msgId, new RegisterResult
                    {
                        Success = false,
                        Message = "请求报文缺少 body/patiInfo 节点",
                        PhysicalNo = ""
                    });
                }

                PatientInfo info = ParsePatiInfo(pati, body);
                physicalNo = info.PhysicalNo;

                RegisterResult result = HisPatientService.Register(info);
                return BuildResponse(msgId, result);
            }
            catch (Exception ex)
            {
                LogHelper.LogInfo("患者建档请求报文解析异常：" + ex.Message);
                return BuildResponse(msgId, new RegisterResult
                {
                    Success = false,
                    Message = "请求报文解析异常：" + ex.Message,
                    PhysicalNo = physicalNo
                });
            }
        }

        private static PatientInfo ParsePatiInfo(XElement pati, XElement body)
        {
            PatientInfo info = new PatientInfo();
            info.PatId = GetVal(pati, "pat_id");
            info.PhysicalNo = GetVal(pati, "physical_no");
            info.VisitCardNo = GetVal(pati, "visit_card_no");
            info.PatTypeCode = GetVal(pati, "pat_type_code");

            XElement certi = pati.Element("certi");
            if (certi != null)
            {
                info.CertiTypeCode = GetVal(certi, "certi_type_code");
                info.CertiType = GetVal(certi, "certi_type");
                info.IdNumber = GetVal(certi, "id_number");
            }

            info.PatName = GetVal(pati, "pat_name");
            info.GenderCode = GetVal(pati, "gender_code");
            info.GenderName = GetVal(pati, "gender_name");
            info.DateBirth = GetVal(pati, "date_birth");
            info.EthnicCode = GetVal(pati, "ethnic_code");
            info.EthnicName = GetVal(pati, "ethnic_name");
            info.MaritalCode = GetVal(pati, "marital_code");
            info.MaritalName = GetVal(pati, "marital_name");
            info.PhoneNo = GetVal(pati, "phone_no");

            XElement preAddr = pati.Element("pre_addr");
            if (preAddr != null)
            {
                info.AddrDesc = GetVal(preAddr, "addr_desc");
            }

            info.OccupCategCode = GetVal(pati, "occup_categ_code");
            info.OccupCategName = GetVal(pati, "occup_categ_name");
            info.CompanyName = GetVal(pati, "company_name");
            info.RecordTime = GetVal(pati, "record_time");

            XElement author1 = body != null ? body.Element("author1") : null;
            if (author1 != null)
            {
                info.RegWorkerCode = GetVal(author1, "reg_worker_code");
                info.RegWorkerName = GetVal(author1, "reg_worker_name");
            }
            return info;
        }

        /// <summary>组装返回XML：res_code 成功=1/失败=0；body/pat_id 填HIS的bingRenId</summary>
        private static string BuildResponse(string msgId, RegisterResult result)
        {
            XElement message = new XElement("message",
                new XElement("response",
                    new XElement("msg_id", msgId ?? ""),
                    new XElement("creat_time", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")),
                    new XElement("res_code", result.Success ? "1" : "0"),
                    new XElement("res_msg", result.Message ?? "")),
                new XElement("body",
                    new XElement("pat_id", result.BingRenId ?? ""),
                    new XElement("physical_no", result.PhysicalNo ?? "")));

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

        /// <summary>让 XDocument.Save 输出 UTF-8 声明</summary>
        private class Utf8StringWriter : System.IO.StringWriter
        {
            public override Encoding Encoding { get { return Encoding.UTF8; } }
        }
    }
}
