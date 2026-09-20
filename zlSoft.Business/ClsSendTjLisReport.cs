using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using zlSoft.Common;

namespace zlSoft.Business
{
    /// <summary>
    /// 组装检验报告(HIP0406)XML并回传体检提供的 TestResultInfoAdd 接口。
    /// </summary>
    public class ClsSendTjLisReport
    {
        /// <summary>
        /// 回传一个申请单的检验报告到体检。
        /// </summary>
        /// <param name="jobjData">LIS结果接口返回的单个报告对象(data数组的一项)</param>
        /// <param name="jieGuoItems">属于该申请单的结果明细项</param>
        /// <param name="patRow">从Lcb_Jianyansqd查询得到的患者/申请行</param>
        /// <param name="repPrintIp">报告打印/文件地址(放入rep_print_ip)</param>
        /// <returns>体检返回成功返回true</returns>
        public static bool SendReport(JObject jobjData, List<JObject> jieGuoItems, System.Data.DataRow patRow, string repPrintIp)
        {
            try
            {
                string url = zlConfiguration.GetAppSetting("TjLisReportUrl");
                if (string.IsNullOrEmpty(url))
                {
                    LogHelper.LogInfo("检验报告回传地址TjLisReportUrl未配置，跳过");
                    return false;
                }

                string xml = BuildReportXml(jobjData, jieGuoItems, patRow, repPrintIp);
                string resp = HttpService.PostXml(xml, url);

                string successCode = zlConfiguration.GetAppSetting("LisReportSuccessCode");
                if (string.IsNullOrEmpty(successCode)) successCode = "1";

                string resCode = ExtractTag(resp, "res_code");
                string resMsg = ExtractTag(resp, "res_msg");
                bool reportOk = (resCode == successCode);
                if (!reportOk)
                {
                    LogHelper.LogInfo("申请单【" + Cell(patRow, "申请单号") + "】检验报告回传失败，res_code=" + resCode + "，res_msg=" + resMsg + "，返回原文：" + resp);
                }

                //危急值：明细中存在weiJiZT=1时，额外调用体检危急值接口(不影响报告回传结果)
                try
                {
                    List<JObject> critiItems = new List<JObject>();
                    foreach (JObject jg in jieGuoItems)
                    {
                        if (Fld(jg, "weiJiZT") == "1") critiItems.Add(jg);
                    }
                    if (critiItems.Count > 0)
                    {
                        ClsSendTjLisCriti.SendCriti(jobjData, critiItems, patRow);
                    }
                }
                catch (Exception exc)
                {
                    LogHelper.LogInfo("申请单【" + Cell(patRow, "申请单号") + "】危急值处理异常，" + exc.Message);
                }

                return reportOk;
            }
            catch (Exception ex)
            {
                LogHelper.LogInfo("申请单【" + Cell(patRow, "申请单号") + "】检验报告回传异常，" + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 按HIP0406检验报告新增格式组装XML。
        /// </summary>
        public static string BuildReportXml(JObject jobjData, List<JObject> jieGuoItems, System.Data.DataRow patRow, string repPrintIp)
        {
            string sender = zlConfiguration.GetAppSetting("LisReportSender");
            if (string.IsNullOrEmpty(sender)) sender = "LIS";
            string receiver = zlConfiguration.GetAppSetting("LisReportReceiver");
            if (string.IsNullOrEmpty(receiver)) receiver = "HIS";
            string eventId = zlConfiguration.GetAppSetting("LisReportEventId");
            if (string.IsNullOrEmpty(eventId)) eventId = "TestResultInfoUpdate";
            string orgCode = zlConfiguration.GetAppSetting("LisOrgID");
            string orgName = zlConfiguration.GetAppSetting("LisOrgName");
            string deptCode = zlConfiguration.GetAppSetting("LDeptID");
            string deptName = zlConfiguration.GetAppSetting("LDeptName");

            string applyNo = Fld(jobjData, "yangBenHao");
            string orderNo = Cell(patRow, "申请单号");
            string barCode = Cell(patRow, "样本条码");
            string itemCode = Cell(patRow, "项目代码");

            // 报告/审核/检验人及时间
            string baoGaoID = Fld(jobjData, "baoGaoID");
            string jianCeRID = Fld(jobjData, "jianCeRID");
            string jianCeRXM = Fld(jobjData, "jianCeRXM");
            string shenHeRID = Fld(jobjData, "shenHeRID");
            string shenHeRXM = Fld(jobjData, "shenHeRXM");
            string shenHeSJ = FmtDateTime(Fld(jobjData, "shenHeSJ"));
            string jianYanJGID = Fld(jobjData, "jianYanJGID");
            string jianYanJGMC = Fld(jobjData, "jianYanJGMC");
            string jianYanXMDM = Fld(jobjData, "jianYanXMDM");
            string jianYanXMMC = Fld(jobjData, "jianYanXMMC");
            string yangBenLXID = Fld(jobjData, "yangBenLXID");
            string yangBenLXMC = Fld(jobjData, "yangBenLXMC");

            // 检验时间取明细中最早/任一测试时间
            string ceShiSJ = "";
            if (jieGuoItems.Count > 0) ceShiSJ = FmtDateTime(Fld(jieGuoItems[0], "ceShiSJ"));

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
            sb.Append("<visit_card_no></visit_card_no>");
            sb.Append("<mr_no></mr_no>");
            sb.Append("<pat_type_code>4</pat_type_code>");
            sb.Append("<pat_type_name>体检</pat_type_name>");
            sb.Append("<outhosp_no></outhosp_no>");
            sb.Append("<visit_no>0</visit_no>");
            sb.Append("<inhosp_no></inhosp_no>");
            sb.Append("<admit_date></admit_date>");
            sb.Append("<id_number>").Append(X(Cell(patRow, "证件号"))).Append("</id_number>");
            sb.Append("<medi_insur_no></medi_insur_no>");
            sb.Append("<pat_name>").Append(X(Cell(patRow, "患者姓名"))).Append("</pat_name>");
            sb.Append("<phone_no></phone_no>");
            sb.Append("<gender_code>").Append(X(Cell(patRow, "性别代码"))).Append("</gender_code>");
            sb.Append("<gender_name>").Append(X(Cell(patRow, "性别名称"))).Append("</gender_name>");
            sb.Append("<date_birth>").Append(X(Cell(patRow, "出生日期"))).Append("</date_birth>");
            sb.Append("<age>").Append(X(Cell(patRow, "年龄"))).Append("</age>");
            sb.Append("<dept_code>").Append(X(deptCode)).Append("</dept_code>");
            sb.Append("<dept_name>").Append(X(deptName)).Append("</dept_name>");
            sb.Append("<ward_code></ward_code><ward_name></ward_name>");
            sb.Append("<sickroom_code></sickroom_code><sickroom_name></sickroom_name>");
            sb.Append("<bed_code></bed_code><bed_name></bed_name>");
            sb.Append("<diags><diag><diag_code></diag_code><diag_name></diag_name><diag_date></diag_date></diag></diags>");
            sb.Append("</patiVisit>");

            // testReport
            sb.Append("<testReport>");
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
            sb.Append("<testDr>");
            sb.Append("<test_dr_code>").Append(X(jianCeRID)).Append("</test_dr_code>");
            sb.Append("<test_dr_name>").Append(X(jianCeRXM)).Append("</test_dr_name>");
            sb.Append("<test_date>").Append(X(ceShiSJ)).Append("</test_date>");
            sb.Append("</testDr>");
            sb.Append("<apply><apply_dr_code></apply_dr_code><apply_dr_name></apply_dr_name></apply>");
            sb.Append("<apply_dept_code>").Append(X(deptCode)).Append("</apply_dept_code>");
            sb.Append("<apply_dept_name>").Append(X(deptName)).Append("</apply_dept_name>");
            sb.Append("<apply_org_code>").Append(X(orgCode)).Append("</apply_org_code>");
            sb.Append("<apply_org_name>").Append(X(orgName)).Append("</apply_org_name>");
            sb.Append("<rep_dept_code>").Append(X(deptCode)).Append("</rep_dept_code>");
            sb.Append("<rep_dept_name>").Append(X(deptName)).Append("</rep_dept_name>");
            sb.Append("<rep_org_code>").Append(X(string.IsNullOrEmpty(jianYanJGID) ? orgCode : jianYanJGID)).Append("</rep_org_code>");
            sb.Append("<rep_org_name>").Append(X(string.IsNullOrEmpty(jianYanJGMC) ? orgName : jianYanJGMC)).Append("</rep_org_name>");
            sb.Append("<test_type_code>").Append(X(jianYanXMDM)).Append("</test_type_code>");
            sb.Append("<test_type_name>").Append(X(jianYanXMMC)).Append("</test_type_name>");

            // test_comps -> 单个医嘱项目
            sb.Append("<test_comps><test_comp>");
            sb.Append("<order_no>").Append(X(orderNo)).Append("</order_no>");
            sb.Append("<bar_code_order>").Append(X(barCode)).Append("</bar_code_order>");
            string test_categ_code = "";
            string test_categ_name = "";
            foreach (JObject jg in jieGuoItems)
            {
                test_categ_code = Fld(jg, "jianYanXMDM");
                test_categ_name = Fld(jg, "jianYanXMMC");
                break;
            }
            sb.Append("<test_categ_code>").Append(X(test_categ_code)).Append("</test_categ_code>");
            sb.Append("<test_categ_name>").Append(X(test_categ_name)).Append("</test_categ_name>");
            sb.Append("<test_method_code></test_method_code><test_method_name></test_method_name>");
            sb.Append("<itemRess>");
            foreach (JObject jg in jieGuoItems)
            {
                sb.Append("<itemRes>");
                sb.Append("<test_item_code>").Append(X(Fld(jg, "shiYanXM"))).Append("</test_item_code>");
                sb.Append("<test_item_name>").Append(X(Fld(jg, "shiYanXMMC"))).Append("</test_item_name>");
                sb.Append("<test_qual_result></test_qual_result>");
                sb.Append("<test_qual_result_code>0</test_qual_result_code>");
                sb.Append("<test_result_value>").Append(X(Fld(jg, "shiYanJG"))).Append("</test_result_value>");
                sb.Append("<test_result_value_unit>").Append(X(Fld(jg, "danWei"))).Append("</test_result_value_unit>");
                sb.Append("<ref_value_up>").Append(X(Fld(jg, "canKaoGZ"))).Append("</ref_value_up>");
                sb.Append("<ref_value_low>").Append(X(Fld(jg, "canKaoDZ"))).Append("</ref_value_low>");
                sb.Append("<ref_value>").Append(X(Fld(jg, "canKaoFW"))).Append("</ref_value>");
                sb.Append("<result_description>").Append(X(GetFlag(Fld(jg, "canKaoZT")))).Append("</result_description>");
                sb.Append("</itemRes>");
            }
            sb.Append("</itemRess>");
            sb.Append("</test_comp></test_comps>");

            // specimen
            sb.Append("<specimen>");
            sb.Append("<spec_date></spec_date><receive_date></receive_date>");
            sb.Append("<spec_staff_code></spec_staff_code><spec_staff_name></spec_staff_name>");
            sb.Append("<bar_code>").Append(X(barCode)).Append("</bar_code>");
            sb.Append("<spec_type_code>").Append(X(yangBenLXID)).Append("</spec_type_code>");
            sb.Append("<spec_type_name>").Append(X(yangBenLXMC)).Append("</spec_type_name>");
            sb.Append("<spec_status_name></spec_status_name><spec_status_desc></spec_status_desc>");
            sb.Append("</specimen>");

            sb.Append("<rep_print_ip>").Append(X(repPrintIp)).Append("</rep_print_ip>");
            sb.Append("<test_rep_note></test_rep_note>");
            sb.Append("</testReport>");

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

        /// <summary>"yyyy-MM-dd HH:mm:ss" -> "yyyyMMddHHmmss"，无法解析原样返回</summary>
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

        /// <summary>根据参考状态返回结果说明：正常空、异常偏高↑、异常偏低↓</summary>
        private static string GetFlag(string strFlag)
        {
            if (strFlag == "H" || strFlag == "HH" || strFlag == "B") return "↑";
            if (strFlag == "L" || strFlag == "LL" || strFlag == "C") return "↓";
            return "";
        }
        #endregion
    }
}
