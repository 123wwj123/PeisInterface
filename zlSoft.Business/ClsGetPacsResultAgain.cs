using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using zlSoft.Common;

namespace zlSoft.Business
{
    public class ClsGetPacsResultAgain
    {
        // 无痛肠镜和无痛胃镜的 examitemid
        private const string COLONOSCOPY_ID = "1a0e13af-0029-43b1-9a73-6aea2e4d9099";
        private const string GASTROSCOPY_ID = "39db19e7-0c95-4f32-92df-78cf0a57a5e1";

        /// <summary>
        /// 定时任务：通过 GetPacsResultSqlAgain 查询审核时间变动（修正/追加）的检查报告，
        /// 组装 HIP0506 XML 后调用体检 CheckResultInfoAdd 接口回传更新（不写中间表）。
        /// 注意：变动报告按原逻辑不更新队列标志，会在有效期内每轮重新回传，由体检端做覆盖更新。
        /// </summary>
        public static void GetPacsResult(zlCommand cmd, string strMDDataType)
        {
            #region 定义变量
            string strSql;
            string strPacsType = zlConfiguration.GetAppSetting("PacsType");
            string strPacsConn = zlConfiguration.GetAppSetting("PacsConn");
            string reqNo = "";
            #endregion

            try
            {
                #region 从PACS库获取需要回传的报告（使用DataTable）
                zlCommand cmdPacs = new zlCommand(strPacsType, strPacsConn, "");
                strSql = zlConfiguration.GetAppSetting("GetPacsResultSqlAgain");
                cmdPacs.SetSQL(strPacsType, strSql);
                zlDataReader drPacs = new zlDataReader();
                drPacs.SetReader(strPacsType, cmdPacs.ExecuteReader(strPacsType));

                // 获取原始DbDataReader
                System.Data.Common.DbDataReader rawReader = null;
                if (strPacsType == "Oracle")
                    rawReader = drPacs.GetOracleDataReader();
                else if (strPacsType == "SqlServer")
                    rawReader = drPacs.GetSqlDataReader();
                else
                    return;

                if (rawReader == null || !rawReader.HasRows)
                {
                    drPacs.Close(strPacsType);
                    cmdPacs.Close(strPacsType);
                    return;
                }

                // 将数据加载到DataTable中
                DataTable dt = new DataTable();
                dt.Load(rawReader);
                drPacs.Close(strPacsType);
                #endregion

                #region 按申请单号分组，组装XML回传体检
                var groups = dt.AsEnumerable().GroupBy(r => r.Field<string>("申请单号"));
                foreach (var group in groups)
                {
                    reqNo = group.Key;
                    var items = group.ToList();

                    bool hasColonoscopy = items.Any(r => r.Field<string>("examitemid") == COLONOSCOPY_ID);
                    bool hasGastroscopy = items.Any(r => r.Field<string>("examitemid") == GASTROSCOPY_ID);

                    if (hasColonoscopy || hasGastroscopy)
                    {
                        // 无痛肠镜+无痛胃镜：合并为一条报告后回传
                        var merged = MergeDataRows(items);
                        ClsSendTjReport.SendReport(merged);
                    }
                    else
                    {
                        // 非合并：逐条回传
                        foreach (var row in items)
                        {
                            bool ok = ClsSendTjReport.SendReport(row);
                            if (ok)
                                UpdateQueueFlag(cmdPacs, strPacsType, row.Field<string>("orderseq"));
                        }
                    }
                }
                #endregion

                cmdPacs.Close(strPacsType);
            }
            catch (Exception ex)
            {
                LogHelper.LogInfo("PACS变动结果回传异常，申请单号：" + reqNo + ex.Message);
            }
        }


        /// <summary>
        /// 回传成功后更新 PACS 端队列标志，避免下次重复回传。
        /// </summary>
        private static void UpdateQueueFlag(zlCommand cmdPacs, string strPacsType, string orderSeq)
        {
            if (string.IsNullOrEmpty(orderSeq)) return;
            string strSql = "Update T_Report_Queue Set TJ_Report_Flag = 'Y' Where OrderID = '" + orderSeq + "'";
            cmdPacs.SetSQL(strPacsType, strSql);
            cmdPacs.ExecuteNonQuery(strPacsType);
        }
        /// <summary>
        /// 合并多条DataRow（用于无痛肠镜+无痛胃镜）
        /// </summary>
        private static DataRow MergeDataRows(List<DataRow> rows)
        {
            // 以第一条为模板
            DataRow merged = rows[0].Table.NewRow();
            merged.ItemArray = rows[0].ItemArray.Clone() as object[];

            // 合并指定字段
            string[] mergeFields = { "诊断意见", "检查所见", "报告图片" };
            foreach (string field in mergeFields)
            {
                var values = rows.Select(r => r.Field<string>(field) ?? "")
                                 .Where(v => !string.IsNullOrWhiteSpace(v))
                                 .ToList();
                merged[field] = string.Join(";", values);
            }
            // 其他字段保持原样（已从rows[0]复制）
            return merged;
        }
    }
}
