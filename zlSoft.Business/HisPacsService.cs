using System;
using zlSoft.Common;

namespace zlSoft.Business
{
    /// <summary>
    /// PACS 检查申请（API 模式）。
    /// 数据来源改为体检通过 API 推送；写入 PACS 库 T_HISORDER 的方式与原 ClsSendPacsApply 保持一致。
    /// 不再读写体检中间库。
    /// </summary>
    public class HisPacsService
    {
        public static PacsApplyResult SendApply(PacsApplyInfo info)
        {
            PacsApplyResult result = new PacsApplyResult();
            result.ApplyNo = !string.IsNullOrEmpty(info.ApplyNo)
                ? info.ApplyNo
                : (info.Items.Count > 0 ? info.Items[0].OrderNo : "");

            if (info.Items == null || info.Items.Count == 0)
            {
                result.Success = false;
                result.Message = "检查申请明细为空";
                return result;
            }

            string strPacsType = zlConfiguration.GetAppSetting("PacsType");
            string strPacsConn = zlConfiguration.GetAppSetting("PacsConn");
            string strPDeptID = zlConfiguration.GetAppSetting("PDeptID");
            string strPDeptName = zlConfiguration.GetAppSetting("PDeptName");

            // MODALITY：类型 = test_type_code，与原逻辑一致将 DR 替换为 DX
            string strModality = (info.Modality ?? "").Replace("DR", "DX");

            zlCommand cmdPacs = null;
            zlDataReader drPacs = new zlDataReader();
            try
            {
                cmdPacs = new zlCommand(strPacsType, strPacsConn, "");

                foreach (PacsApplyItem item in info.Items)
                {   //VISITNUM1 字段目前没有用，暂时用来存放体检发送过来的电子申请单编号ApplyNo
                    string examItemIds = item.TestItemCode ?? "";
                    string examItemNames = item.TestItemName ?? ""; // 默认使用传入的项目名称
                    string strHisId = item.OrderNo ?? "";            // 申请单号 = order_no
                    string strSql;

                    // 无痛胃肠镜
                    if (examItemIds == "4720815")
                    {
                        // 写入 PACS肠镜申请 8848 无痛肠镜
                        strSql = "Insert Into T_HISORDER (HIS_ID,PATTYPE,PATIENTID,PATNAME,PATSEX,PATBIRTHDAY,NATIONALITY,COUNTRY,IDCARD,CLINICID,CLINIC,ORDERDT,MODALITY,ORDERDRID," +
                            "                             ORDERDR,EXAMITEM,EXAMITEMID,PHYPATIENTID,CHARGEFLAG,OUTPATIENTID,VISITNUM,TELEPHONENO,VISITNUM1)" +
                            "     Values ([1],[2],[3],[4],[5],[6],[7],[8],[9],[10],[11],[12],[13],[14],[15],[16],[17],[18],[19],[20],[21],[22],[23])";
                        cmdPacs.SetSQL(strPacsType, strSql, strHisId, "P", info.PatId, info.PatName, info.GenderCode,
                                       info.DateBirth, "汉族", "中国", info.IdNumber, strPDeptID, strPDeptName,
                                       info.ApplyDate, strModality, info.ApplyDrCode, info.ApplyDrName,
                                       "无痛肠镜", "8848", info.PhysicalNo, 1, info.PhysicalNo, info.PhysicalNo, info.PhoneNo, result.ApplyNo);
                        cmdPacs.ExecuteNonQuery(strPacsType);

                        // 写入 PACS胃镜申请 8859 无痛胃镜
                        strSql = "Insert Into T_HISORDER (HIS_ID,PATTYPE,PATIENTID,PATNAME,PATSEX,PATBIRTHDAY,NATIONALITY,COUNTRY,IDCARD,CLINICID,CLINIC,ORDERDT,MODALITY,ORDERDRID," +
                            "                             ORDERDR,EXAMITEM,EXAMITEMID,PHYPATIENTID,CHARGEFLAG,OUTPATIENTID,VISITNUM,TELEPHONENO,VISITNUM1)" +
                            "     Values ([1],[2],[3],[4],[5],[6],[7],[8],[9],[10],[11],[12],[13],[14],[15],[16],[17],[18],[19],[20],[21],[22],[23])";
                        cmdPacs.SetSQL(strPacsType, strSql, strHisId, "P", info.PatId, info.PatName, info.GenderCode,
                                       info.DateBirth, "汉族", "中国", info.IdNumber, strPDeptID, strPDeptName,
                                       info.ApplyDate, strModality, info.ApplyDrCode, info.ApplyDrName,
                                       "无痛胃镜", "8859", info.PhysicalNo, 1, info.PhysicalNo, info.PhysicalNo, info.PhoneNo, result.ApplyNo);
                        cmdPacs.ExecuteNonQuery(strPacsType);
                    }
                    else // 非无痛胃肠镜
                    {
                        // 如果项目ID不为空，从 PACS 库查询项目名称
                        if (!string.IsNullOrEmpty(examItemIds))
                        {
                            string querySql = "SELECT LISTAGG(EXAMITEM, ',') WITHIN GROUP (ORDER BY EXAMITEMCODE) AS EXAMITEMS FROM t_examitem WHERE EXAMITEMCODE IN (" + examItemIds + ")";
                            cmdPacs.SetSQL(strPacsType, querySql);
                            drPacs.SetReader(strPacsType, cmdPacs.ExecuteReader(strPacsType));
                            if (drPacs.HasRows(strPacsType) && drPacs.Read(strPacsType))
                            {
                                string queryResult = drPacs.Get(strPacsType, "EXAMITEMS").ToString();
                                if (!string.IsNullOrEmpty(queryResult))
                                {
                                    examItemNames = queryResult;
                                }
                            }
                            drPacs.Close(strPacsType);
                        }

                        // 写入 PACS 申请
                        strSql = "Insert Into T_HISORDER (HIS_ID,PATTYPE,PATIENTID,PATNAME,PATSEX,PATBIRTHDAY,NATIONALITY,COUNTRY,IDCARD,CLINICID,CLINIC,ORDERDT,MODALITY,ORDERDRID," +
                            "                             ORDERDR,EXAMITEM,EXAMITEMID,PHYPATIENTID,CHARGEFLAG,OUTPATIENTID,VISITNUM,TELEPHONENO,VISITNUM1)" +
                            "     Values ([1],[2],[3],[4],[5],[6],[7],[8],[9],[10],[11],[12],[13],[14],[15],[16],[17],[18],[19],[20],[21],[22],[23])";
                        cmdPacs.SetSQL(strPacsType, strSql, strHisId, "P", info.PatId, info.PatName, info.GenderCode,
                                       info.DateBirth, "汉族", "中国", info.IdNumber, strPDeptID, strPDeptName,
                                       info.ApplyDate, strModality, info.ApplyDrCode, info.ApplyDrName,
                                       examItemNames, examItemIds, info.PhysicalNo, 1, info.PhysicalNo, info.PhysicalNo, info.PhoneNo, result.ApplyNo);
                        cmdPacs.ExecuteNonQuery(strPacsType);
                    }
                }

                cmdPacs.Close(strPacsType);
                result.Success = true;
                result.Message = "成功";
            }
            catch (Exception ex)
            {
                try { drPacs.Close(strPacsType); } catch { }
                try { if (cmdPacs != null) cmdPacs.Close(strPacsType); } catch { }
                LogHelper.LogInfo("PACS申请同步异常，" + ex.Message);
                result.Success = false;
                result.Message = "PACS申请同步异常：" + ex.Message;
            }
            return result;
        }


        /// <summary>
        /// 撤销检查申请（API 模式）。写 PACS 库的方式与原 ClsCancelPacsApply 一致：
        /// 将 T_HISORDER.ISDELETED 置 1（按 HIS_ID = 申请单号 order_no），不读写体检中间库。
        /// </summary>
        public static PacsApplyResult CancelApply(PacsApplyInfo info)
        {
            PacsApplyResult result = new PacsApplyResult();
            result.ApplyNo = !string.IsNullOrEmpty(info.ApplyNo)
                ? info.ApplyNo
                : (info.Items.Count > 0 ? info.Items[0].OrderNo : "");

            if (info.Items == null || info.Items.Count == 0)
            {
                result.Success = false;
                result.Message = "撤销明细为空";
                return result;
            }

            string strPacsType = zlConfiguration.GetAppSetting("PacsType");
            string strPacsConn = zlConfiguration.GetAppSetting("PacsConn");

            zlCommand cmdPacs = null;
            try
            {
                cmdPacs = new zlCommand(strPacsType, strPacsConn, "");
                var handled = new System.Collections.Generic.HashSet<string>();
                foreach (PacsApplyItem item in info.Items)
                {
                    string reqNo = item.OrderNo ?? "";
                    if (string.IsNullOrEmpty(reqNo) || handled.Contains(reqNo)) continue;
                    handled.Add(reqNo);

                    // 撤销：将 T_HISORDER 标记删除（写表方式与原 ClsCancelPacsApply 一致）
                    string strSql = "Update T_HISORDER Set ISDELETED = 1 Where HIS_ID = '" + reqNo + "'";
                    cmdPacs.SetSQL(strPacsType, strSql);
                    cmdPacs.ExecuteNonQuery(strPacsType);
                }
                cmdPacs.Close(strPacsType);
                result.Success = true;
                result.Message = "成功";
            }
            catch (Exception ex)
            {
                try { if (cmdPacs != null) cmdPacs.Close(strPacsType); } catch { }
                LogHelper.LogInfo("PACS申请撤销异常，" + ex.Message);
                result.Success = false;
                result.Message = "PACS申请撤销异常：" + ex.Message;
            }
            return result;
        }
    }
}
