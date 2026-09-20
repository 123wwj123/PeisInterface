using System.Collections.Generic;

namespace zlSoft.Business
{
    /// <summary>检验申请明细项（一个 test_comp/item 对应一条 LIS 申请项）</summary>
    public class LisApplyItem
    {
        public string OrderNo { get; set; }       // 医嘱号 = 申请单号 -> ShenQingID/YiZhuID
        public string TestItemCode { get; set; }   // 项目代码（原项目代码） -> JianYanXMDM
        public string TestItemName { get; set; }   // 项目名称 -> JianYanXMMC
    }

    /// <summary>
    /// 检验申请（新增）信息，从 body/patiVisit + body/testApply 解析而来。
    /// 组装为羚云 LIS insertShengQingDan 报文后调用 LIS（调用方式与原来一致）。
    /// </summary>
    public class LisApplyInfo
    {
        // 患者信息（patiVisit）
        public string PatId { get; set; }   // 病人id pat_id
        public string PhysicalNo { get; set; }     // 体检号 -> bingRenID/BingAnHao/MPI/JiuZhenHao
        public string PatName { get; set; }        // 姓名 -> XingMing
        public string GenderCode { get; set; }     // 性别代码 -> XingBie
        public string DateBirth { get; set; }      // 出生日期 -> ChuShengRQ
        public string IdNumber { get; set; }       // 身份证号 -> ZhengJianHM
        public string PhoneNo { get; set; }        // 联系电话 -> DianHuaHM

        // 申请信息（testApply）
        public string ApplyNo { get; set; }        // 电子申请单编号（返回回传）
        public string ApplyState { get; set; }     // 申请单状态 apply_state：1申请 3取消
        public string BarCode { get; set; }        // 样本条码 -> TiaoMaHao
        public string ApplyDrCode { get; set; }    // 开单医生ID -> kaiDanYSID/daYinRID
        public string ApplyDrName { get; set; }    // 开单医生 -> kaiDanYSMC/daYinRXM
        public string ApplyDate { get; set; }      // 开单时间 -> kaiDanSJ/daYinSJ
        public string ApplyDeptCode { get; set; }  // 开单科室ID（回退配置 LDeptID）
        public string ApplyDeptName { get; set; }  // 开单科室名称（回退配置 LDeptName）
        public string ApplyOrgCode { get; set; }   // 机构编码（回退配置 LisOrgID）
        public string ApplyOrgName { get; set; }   // 机构名称（回退配置 LisOrgName）

        public List<LisApplyItem> Items { get; set; }

        public LisApplyInfo()
        {
            Items = new List<LisApplyItem>();
        }
    }

    /// <summary>检验申请处理结果</summary>
    public class LisApplyResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string ApplyNo { get; set; }   // 回传的申请单号
    }
}
