using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using zlSoft.Common;

namespace zlSoft.Business
{
    /// <summary>
    /// 组装检验危急值(TestCritiValueNoti)XML并回传体检危急值接口。
    /// 当LIS结果明细 weiJiZT=1 时触发。
    /// </summary>
    public class ClsSendTjLisCriti
    {
        /// <summary>
        /// 回传危急值。
        /// </summary>
        /// <param name="jobjData">LIS结果接口返回的单个报告对象</param>
        /// <param name="critiItems">危急值明细项(weiJiZT=1)</param>
        /// <param name="patRow">从Lcb_Jianyansqd查询得到的患者/申请行</param>
        /// <returns>体检返回成功返回true</returns>
        public static bool SendCriti(JObject jobjData, List<JObject> critiItems, System.Data.DataRow patRow)
        {
            try
            {
                if (critiItems == null || critiItems.Count == 0) return false;

                string url = zlConfiguration.GetAppSetting("TjLisCritiUrl");
                if (string.IsNullOrEmpty(url))
                {
                    LogHelper.LogInfo("危急值回传地址TjLisCritiUrl未配置，跳过");
                    return false;
                }

                string xml = BuildCritiXml(jobjData, critiItems, patRow);
                string resp = HttpService.PostXml(xml, url);

                string successCode = zlConfiguration.GetAppSetting("LisReportSuccessCode");
                if (string.IsNullOrEmpty(successCode)) successCode = "1";

                string resCode = ExtractTag(resp, "res_code");
                string resMsg = ExtractTag(resp, "res_msg");

                if (resCode == successCode)
                {
                    LogHelper.LogInfo("申请单【" + Cell(patRow, "申请单号") + "】危急值回传成功，危急值项数=" + critiItems.Count);
                    return true;
                }
                else
                {
                    LogHelper.LogInfo("申请单【" + Cell(patRow, "申请单号") + "】危急值回传失败，res_code=" + resCode + "，res_msg=" + resMsg + "，返回原文：" + resp);
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogInfo("申请单【" + Cell(patRow, "申请单号") + "】危急值回传异常，" + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 按危急值(TestCritiValueNoti)格式组装XML。
        /// </summary>
        public static string BuildCritiXml(JObject jobjData, List<JObject> critiItems, System.Data.DataRow patRow)
        {
            string sender = zlConfiguration.GetAppSetting("LisReportSender");
            if (string.IsNullOrEmpty(sender)) sender = "LIS";
            string receiver = zlConfiguration.GetAppSetting("LisReportReceiver");
            if (string.IsNullOrEmpty(receiver)) receiver = "HIS";
            string eventId = zlConfiguration.GetAppSetting("LisCritiEventId");
            if (string.IsNullOrEmpty(eventId)) eventId = "TestCritiValueNoti";
            string orgCode = zlConfiguration.GetAppSetting("LisOrgID");
            string orgName = zlConfiguration.GetAppSetting("LisOrgName");
            string deptCode = zlConfiguration.GetAppSetting("LDeptID");
            string deptName = zlConfiguration.GetAppSetting("LDeptName");
            //体检号拼接条码号
            string applyNo = Cell(patRow, "体检号")+ Cell(patRow, "样本条码");

            string baoGaoID = Fld(jobjData, "baoGaoID");
            string jianCeRID = Fld(jobjData, "jianCeRID");
            string jianCeRXM = Fld(jobjData, "jianCeRXM");
            string shenHeRID = Fld(jobjData, "shenHeRID");
            string shenHeRXM = Fld(jobjData, "shenHeRXM");
            string shenHeSJ = FmtDateTime(Fld(jobjData, "shenHeSJ"));
            string jianYanXMDM = Fld(jobjData, "jianYanXMDM");
            string jianYanXMMC = Fld(jobjData, "jianYanXMMC");

            // 备注：拼接危急值项名称+结果
            StringBuilder remarks = new StringBuilder();
            foreach (JObject jg in critiItems)
            {
                if (remarks.Length > 0) remarks.Append("；");
                remarks.Append(Fld(jg, "shiYanXMMC")).Append("=").Append(Fld(jg, "shiYanJG"));
            }

            StringBuilder sb = new StringBuilder();
            sb.Append("<message>");

            // ---- request ----
            sb.Append("<request>");
            sb.Append("<msg_id>").Append(Guid.NewGuid().ToString("N").ToUpper()).Append("</msg_id>");
            sb.Append("<event_id>").Append(X(eventId)).Append("</event_id>");
            sb.Append("<creat_time>").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append("</creat_time>");
            sb.Append("<sender>").Append(X(sender)).Append("</sender>");
            sb.Append("<receiver>").Append(X(receiver)).Append("</receiver>");
            sb.Append("</request>");

            // ---- body ----
            sb.Append("<body>");

            // patiVisit
            sb.Append("<patiVisit>");
            sb.Append("<pat_id>").Append(X(Cell(patRow, "患者ID"))).Append("</pat_id>");
            sb.Append("<visit_card_no>").Append(X(Cell(patRow, "体检号"))).Append("</visit_card_no>");
            sb.Append("<mr_no></mr_no>");
            sb.Append("<pat_type_code>4</pat_type_code>");
            sb.Append("<pat_type_name>体检</pat_type_name>");
            sb.Append("<outhosp_no></outhosp_no>");
            sb.Append("<visit_no>0</visit_no>");
            sb.Append("<inhosp_no></inhosp_no>");
            sb.Append("<admit_date></admit_date>");
            sb.Append("<pat_name>").Append(X(Cell(patRow, "患者姓名"))).Append("</pat_name>");
            sb.Append("</patiVisit>");

            // critiValues
            sb.Append("<critiValues>");
            sb.Append("<report_no>").Append(X(baoGaoID)).Append("</report_no>");
            sb.Append("<apply_no>").Append(X(applyNo)).Append("</apply_no>");
            sb.Append("<reporter>");
            sb.Append("<rep_dr_code>").Append(X(jianCeRID)).Append("</rep_dr_code>");
            sb.Append("<rep_dr_name>").Append(X(jianCeRXM)).Append("</rep_dr_name>");
            sb.Append("<report_date>").Append(X(shenHeSJ)).Append("</report_date>");
            sb.Append("</reporter>");
            sb.Append("<auditor>");
            sb.Append("<audit_dr_code>").Append(X(shenHeRID)).Append("</audit_dr_code>");
            sb.Append("<audit_dr_name>").Append(X(shenHeRXM)).Append("</audit_dr_name>");
            sb.Append("<audit_date>").Append(X(shenHeSJ)).Append("</audit_date>");
            sb.Append("</auditor>");
            sb.Append("<last_test_dr_code>").Append(X(jianCeRID)).Append("</last_test_dr_code>");
            sb.Append("<last_test_dr_name>").Append(X(jianCeRXM)).Append("</last_test_dr_name>");
            sb.Append("<apply><apply_date></apply_date><apply_dr_code></apply_dr_code><apply_dr_name></apply_dr_name></apply>");
            sb.Append("<apply_dept_code>").Append(X(deptCode)).Append("</apply_dept_code>");
            sb.Append("<apply_dept_name>").Append(X(deptName)).Append("</apply_dept_name>");
            sb.Append("<apply_org_code>").Append(X(orgCode)).Append("</apply_org_code>");
            sb.Append("<apply_org_name>").Append(X(orgName)).Append("</apply_org_name>");

            // test_comps -> 单个医嘱项目
            sb.Append("<test_comps>");
            //sb.Append("<test_categ_code>").Append(X(jianYanXMDM)).Append("</test_categ_code>");
            //sb.Append("<test_categ_name>").Append(X(jianYanXMMC)).Append("</test_categ_name>");
            //sb.Append("<itemRess>");
            foreach (JObject jg in critiItems)
            {
                sb.Append("<test_comp>");
                sb.Append("<test_categ_code>").Append(X(Fld(jg, "jianYanXMDM"))).Append("</test_categ_code>");
                sb.Append("<test_categ_name>").Append(X(Fld(jg, "jianYanXMMC"))).Append("</test_categ_name>");
                sb.Append("<itemRes>");
                // LIS结果接口未提供独立的危急值上下限，如后续有字段可在此补充
                sb.Append("<criti_value_low></criti_value_low>");
                sb.Append("<criti_value_up></criti_value_up>");
                sb.Append("<ref_value_low>").Append(X(Fld(jg, "canKaoDZ"))).Append("</ref_value_low>");
                sb.Append("<ref_value_up>").Append(X(Fld(jg, "canKaoGZ"))).Append("</ref_value_up>");       
                sb.Append("<test_item_code>").Append(X(Fld(jg, "shiYanXM"))).Append("</test_item_code>");
                sb.Append("<test_item_name>").Append(X(Fld(jg, "shiYanXMMC"))).Append("</test_item_name>");
                sb.Append("<test_qual_result></test_qual_result>");
                sb.Append("<test_qual_result_code>0</test_qual_result_code>");
                sb.Append("<test_result_value>").Append(X(Fld(jg, "shiYanJG"))).Append("</test_result_value>");
                sb.Append("<test_result_value_unit>").Append(X(Fld(jg, "danWei"))).Append("</test_result_value_unit>");
                sb.Append("</itemRes>");
                sb.Append("<criti_value>").Append(X(Fld(jg, "shiYanJG"))).Append("</criti_value>");
                sb.Append("</test_comp>");
            }
            //sb.Append("</itemRess>");
            sb.Append("</test_comps>");

            // 发送/通知
            sb.Append("<notice_dr_code>").Append(X(jianCeRID)).Append("</notice_dr_code>");
            sb.Append("<notice_dr_name>").Append(X(jianCeRXM)).Append("</notice_dr_name>");
            sb.Append("<notice_time>").Append(DateTime.Now.ToString("yyyyMMddHHmmss")).Append("</notice_time>");
            sb.Append("<remarks>").Append(X(remarks.ToString())).Append("</remarks>");
            sb.Append("</critiValues>");

            sb.Append("</body>");
            sb.Append("</message>");
            return sb.ToString();
        }

        #region 辅助方法
        private static string Cell(System.Data.DataRow row, string col)
        {
            if (row == null || !row.Table.Columns.Contains(col) || row[col] == null) return "";
            return row[col].ToString();
        }

        private static string Fld(JObject o, string key)
        {
            if (o == null) return "";
            JToken t = o[key];
            if (t == null || t.Type == JTokenType.Null) return "";
            return t.ToString();
        }

        private static string X(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
        }

        private static string FmtDateTime(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            DateTime dt;
            if (DateTime.TryParse(s, out dt)) return dt.ToString("yyyyMMddHHmmss");
            return s;
        }

        private static string ExtractTag(string xml, string tag)
        {
            if (string.IsNullOrEmpty(xml)) return "";
            string open = "<" + tag + ">";
            string close = "</" + tag + ">";
            int i = xml.IndexOf(open, StringComparison.OrdinalIgnoreCase);
            if (i < 0) return "";
            i += open.Length;
            int j = xml.IndexOf(close, i, StringComparison.OrdinalIgnoreCase);
            if (j < 0) return "";
            return xml.Substring(i, j - i).Trim();
        }
        #endregion
    }
}
