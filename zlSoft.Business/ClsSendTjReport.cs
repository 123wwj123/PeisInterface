using System;
using System.Data;
using System.IO;
using System.Net;
using System.Text;
using zlSoft.Common;
using static System.Net.WebRequestMethods;

namespace zlSoft.Business
{
    /// <summary>
    /// 检查报告回传体检系统。
    /// 将 PACS 报告结果（GetPacsResultSql 查询结果的一行 DataRow）组装成 HIP0506
    /// 检查报告新增 XML，调用体检提供的 CheckResultInfoAdd 接口。
    /// </summary>
    public class ClsSendTjReport
    {
        /// <summary>
        /// 发送一条（或合并后一条）检查报告到体检系统。成功返回 true。
        /// </summary>
        public static bool SendReport(DataRow row)
        {
            string reqNo = Fld(row, "申请单号");
            try
            {
                string url = zlConfiguration.GetAppSetting("TjReportUrl");
                if (string.IsNullOrEmpty(url))
                {
                    LogHelper.LogInfo("检查报告回传失败：未配置 TjReportUrl，申请单号：" + reqNo);
                    return false;
                }

                // 获取相对路径（即 view_picture_ip 的值）
                //string relativePath = Fld(row, "报告图片");

                //// 如果相对路径不为空，尝试从 FTP 下载并保存到本地
                //if (!string.IsNullOrEmpty(relativePath))
                //{
                //    // 构造 FTP 完整 URL（假设 FTP 基础地址 + 相对路径，并将反斜杠转为正斜杠）
                //    string ftpBase = zlConfiguration.GetAppSetting("FtpBaseUrl");
                //    if (!string.IsNullOrEmpty(ftpBase))
                //    {
                //        if (!ftpBase.EndsWith("/")) ftpBase += "/";
                //        string ftpUrl = ftpBase + relativePath.Replace('\\', '/');

                //        string tempFile = DownloadFromFtp(ftpUrl);
                //        if (!string.IsNullOrEmpty(tempFile))
                //        {
                //            bool saved = SaveToPhysicalPath(tempFile, relativePath);
                //            if (!saved)
                //            {
                //                LogHelper.LogInfo($"文件保存失败，但继续发送报告，申请单号：{reqNo}");
                //            }
                //        }
                //        else
                //        {
                //            LogHelper.LogInfo($"FTP 下载失败，继续发送报告，申请单号：{reqNo}");
                //        }
                //    }
                //    else
                //    {
                //        LogHelper.LogInfo("未配置 FtpBaseUrl，跳过文件迁移");
                //    }
                //}


                string xml = BuildReportXml(row);
                string resp = HttpService.PostXml(xml, url);
                LogHelper.LogInfo("检查报告回传，申请单号：" + reqNo + "，请求：" + xml + "，返回：" + resp);

                string successCode = zlConfiguration.GetAppSetting("ReportSuccessCode");
                if (string.IsNullOrEmpty(successCode)) successCode = "1";

                string resCode = ExtractTag(resp, "res_code");
                if (resCode == successCode)
                    return true;

                LogHelper.LogInfo("检查报告回传未成功，申请单号：" + reqNo + "，res_code=" + resCode + "，res_msg=" + ExtractTag(resp, "res_msg"));
                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogInfo("检查报告回传异常，申请单号：" + reqNo + "，" + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 按 HIP0506 检查报告新增请求消息格式组装 XML。
        /// </summary>
        private static string BuildReportXml(DataRow row)
        {
            string patId        = Fld(row, "病人id");
            string physicalNo   = Fld(row, "体检号");
            string reqNo        = Fld(row, "申请单号");
            string applyNo      = Fld(row, "电子申请单号");
            string reportId     = Fld(row, "报告ID");
            string result       = MapReult(Fld(row, "检查结果"));
            string diagnosis    = Fld(row, "诊断意见");
            string reportDate   = Fld(row, "检查时间");
            string reportDrId   = Fld(row, "检查人ID");
            string reportDrName = Fld(row, "检查人");
            string auditDate    = Fld(row, "审核时间");
            string auditDrId    = Fld(row, "审核人ID");
            string auditDrName  = Fld(row, "审核人");
            string studyResult  = Fld(row, "检查所见");
            string pictureUrl   = "http://10.112.1.131:8899/"+ Fld(row, "报告图片").Replace("\\","/");
            string examItemId   = Fld(row, "examitemId");

            string sender    = zlConfiguration.GetAppSetting("ReportSender");
            string receiver  = zlConfiguration.GetAppSetting("ReportReceiver");
            string deptCode  = zlConfiguration.GetAppSetting("PDeptID");
            string deptName  = zlConfiguration.GetAppSetting("PDeptName");
            string orgCode   = zlConfiguration.GetAppSetting("ReportOrgCode");
            string orgName   = zlConfiguration.GetAppSetting("ReportOrgName");
            string msgId     = Guid.NewGuid().ToString("N");
            string createTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            StringBuilder sb = new StringBuilder();
            sb.Append("<message>");
            sb.Append("<request>");
            sb.Append("<msg_id>").Append(X(msgId)).Append("</msg_id>");
            sb.Append("<event_id>CheckResultInfoAdd</event_id>");
            sb.Append("<creat_time>").Append(X(createTime)).Append("</creat_time>");
            sb.Append("<sender>").Append(X(sender)).Append("</sender>");
            sb.Append("<receiver>").Append(X(receiver)).Append("</receiver>");
            sb.Append("</request>");
            sb.Append("<body>");
            sb.Append("<patiVisit>");
            sb.Append("<physical_no>").Append(X(physicalNo)).Append("</physical_no>");
            sb.Append("<pat_id>").Append(X(patId)).Append("</pat_id>");
            sb.Append("<pat_type_code>4</pat_type_code>");
            sb.Append("<pat_type_name>体检</pat_type_name>");
            sb.Append("</patiVisit>");
            sb.Append("<testReport>");
            sb.Append("<report_no>").Append(X(reportId)).Append("</report_no>");
            sb.Append("<apply_no>").Append(X(reqNo)).Append("</apply_no>");
            sb.Append("<report_state>2</report_state>");
            sb.Append("<reporter>");
            sb.Append("<rep_dr_code>").Append(X(reportDrId)).Append("</rep_dr_code>");
            sb.Append("<rep_dr_name>").Append(X(reportDrName)).Append("</rep_dr_name>");
            sb.Append("<report_date>").Append(X(reportDate)).Append("</report_date>");
            sb.Append("</reporter>");
            sb.Append("<auditor>");
            sb.Append("<audit_dr_code>").Append(X(auditDrId)).Append("</audit_dr_code>");
            sb.Append("<audit_dr_name>").Append(X(auditDrName)).Append("</audit_dr_name>");
            sb.Append("<audit_date>").Append(X(auditDate)).Append("</audit_date>");
            sb.Append("</auditor>");
            sb.Append("<last_test_dr_code>").Append(X(reportDrId)).Append("</last_test_dr_code>");
            sb.Append("<last_test_dr_name>").Append(X(reportDrName)).Append("</last_test_dr_name>");
            sb.Append("<testDr>");
            sb.Append("<test_dr_code>").Append(X(reportDrId)).Append("</test_dr_code>");
            sb.Append("<test_dr_name>").Append(X(reportDrName)).Append("</test_dr_name>");
            sb.Append("<test_date>").Append(X(reportDate)).Append("</test_date>");
            sb.Append("</testDr>");
            sb.Append("<apply_dept_code>").Append(X(deptCode)).Append("</apply_dept_code>");
            sb.Append("<apply_dept_name>").Append(X(deptName)).Append("</apply_dept_name>");
            sb.Append("<apply_org_code>").Append(X(orgCode)).Append("</apply_org_code>");
            sb.Append("<apply_org_name>").Append(X(orgName)).Append("</apply_org_name>");
            sb.Append("<rep_dept_code>").Append(X(deptCode)).Append("</rep_dept_code>");
            sb.Append("<rep_dept_name>").Append(X(deptName)).Append("</rep_dept_name>");
            sb.Append("<rep_org_code>").Append(X(orgCode)).Append("</rep_org_code>");
            sb.Append("<rep_org_name>").Append(X(orgName)).Append("</rep_org_name>");
            sb.Append("<test_comps>");
            sb.Append("<test_comp>");
            sb.Append("<order_no>").Append(X(reqNo)).Append("</order_no>");
            sb.Append("<itemRess>");
            sb.Append("<itemRes>");
            sb.Append("<test_item_code>").Append(X(examItemId)).Append("</test_item_code>");
            sb.Append("<test_item_name></test_item_name>");
            sb.Append("</itemRes>");
            sb.Append("</itemRess>");
            sb.Append("<test_result_obj>").Append(X(studyResult)).Append("</test_result_obj>");
            sb.Append("<test_result_subj>").Append(X(diagnosis)).Append("</test_result_subj>");
            sb.Append("<test_result_gender>").Append(X(result)).Append("</test_result_gender>");
            sb.Append("</test_comp>");
            sb.Append("</test_comps>");
            sb.Append("<rep_print_ip>").Append(X(pictureUrl)).Append("</rep_print_ip>");
            sb.Append("<view_picture_ip>").Append(X(pictureUrl)).Append("</view_picture_ip>");
            sb.Append("<test_rep_note>").Append(X(result)).Append("</test_rep_note>");
            sb.Append("</testReport>");
            sb.Append("</body>");
            sb.Append("</message>");
            return sb.ToString();
        }
        private static string MapReult(string genderCode)
        {
            if (string.IsNullOrEmpty(genderCode)) return "";
            switch (genderCode.Trim())
            {
                case "阴性": return "0"; 
                case "阳性": return "1"; 
                default: return genderCode.Trim();
            }
        }
        /// <summary>安全读取 DataRow 字段（列不存在或为 NULL 时返回空串）。</summary>
        private static string Fld(DataRow row, string col)
        {
            if (row == null || !row.Table.Columns.Contains(col)) return "";
            object v = row[col];
            return (v == null || v == DBNull.Value) ? "" : v.ToString();
        }

        /// <summary>XML 文本转义。</summary>
        private static string X(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }

        /// <summary>从返回 XML 中提取指定标签的文本值。</summary>
        private static string ExtractTag(string xml, string tag)
        {
            if (string.IsNullOrEmpty(xml)) return "";
            string open = "<" + tag + ">";
            int i = xml.IndexOf(open, StringComparison.OrdinalIgnoreCase);
            if (i < 0) return "";
            i += open.Length;
            int j = xml.IndexOf("</" + tag + ">", i, StringComparison.OrdinalIgnoreCase);
            if (j < 0) return "";
            return xml.Substring(i, j - i).Trim();
        }

        private static string DownloadFromFtp(string ftpUrl)
        {
            if (string.IsNullOrEmpty(ftpUrl))
                return null;

            try
            {
                Uri uri = new Uri(ftpUrl);
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(uri);
                request.Method = WebRequestMethods.Ftp.DownloadFile;
                request.UseBinary = true;
                request.Timeout = int.Parse(zlConfiguration.GetAppSetting("FtpTimeout") ?? "30000");

                string ftpUser = zlConfiguration.GetAppSetting("FtpUserName");
                string ftpPass = zlConfiguration.GetAppSetting("FtpPassword");
                if (!string.IsNullOrEmpty(ftpUser))
                    request.Credentials = new NetworkCredential(ftpUser, ftpPass);

                using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
                using (Stream responseStream = response.GetResponseStream())
                {
                    string tempFile = Path.GetTempFileName();
                    using (FileStream fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write))
                    {
                        responseStream.CopyTo(fs);
                    }
                    return tempFile;
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogInfo($"FTP 下载失败：{ftpUrl}，错误：{ex.Message}");
                return null;
            }
        }
        //private static bool SaveToPhysicalPath(string tempFilePath, string relativePath)
        //{
        //    try
        //    {
        //        string targetRoot = zlConfiguration.GetAppSetting("TargetPhysicalPath");
        //        if (string.IsNullOrEmpty(targetRoot))
        //        {
        //            LogHelper.LogInfo("未配置 TargetPhysicalPath，无法保存文件");
        //            return false;
        //        }

        //        // 确保根目录以路径分隔符结尾
        //        if (!targetRoot.EndsWith(Path.DirectorySeparatorChar.ToString()))
        //            targetRoot += Path.DirectorySeparatorChar;

        //        // 组合完整物理路径
        //        string fullPath = Path.Combine(targetRoot, relativePath);
        //        string directory = Path.GetDirectoryName(fullPath);
        //        if (!Directory.Exists(directory))
        //            Directory.CreateDirectory(directory);

        //        // 复制文件（若目标已存在则覆盖，视业务而定）
        //        File.Copy(tempFilePath, fullPath, true);
        //        LogHelper.LogInfo($"文件已保存至：{fullPath}");
        //        return true;
        //    }
        //    catch (Exception ex)
        //    {
        //        LogHelper.LogInfo($"保存文件失败：{ex.Message}");
        //        return false;
        //    }
        //    finally
        //    {
        //        // 删除临时文件
        //        if (File.Exists(tempFilePath))
        //            File.Delete(tempFilePath);
        //    }
        //}

    }
}
