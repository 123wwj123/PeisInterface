using System;
using System.Collections.Generic;

namespace zlSoft.Business
{
    /// <summary>检查申请明细项（每个 test_comp 一组，order_no = 申请单号）</summary>
    public class PacsApplyItem
    {
        /// <summary>医嘱号/申请单号 order_no</summary>
        public string OrderNo { get; set; }
        /// <summary>项目代码 test_item_code（写入 EXAMITEMID）</summary>
        public string TestItemCode { get; set; }
        /// <summary>项目名称 test_item_name（写入 EXAMITEM）</summary>
        public string TestItemName { get; set; }
    }

    /// <summary>检查申请信息（PACS）</summary>
    public class PacsApplyInfo
    {
        // 患者信息
        public string PatId { get; set; }   // 病人id pat_id
        public string PhysicalNo { get; set; }   // 体检号 physical_no
        public string PatName { get; set; }      // 姓名 pat_name
        public string GenderCode { get; set; }   // 性别代码 gender_code
        public string DateBirth { get; set; }    // 出生日期 date_birth
        public string IdNumber { get; set; }     // 身份证号 id_number
        public string PhoneNo { get; set; }      // 手机号 phone_no

        // 申请信息
        public string ApplyNo { get; set; }      // 电子申请单编号 apply_no
        public string ApplyState { get; set; }   // 申请单状态 apply_state：1申请 3取消
        public string ApplyDate { get; set; }    // 申请/开单时间 apply_date
        public string ApplyDrCode { get; set; }  // 开单医生工号 apply_dr_code
        public string ApplyDrName { get; set; }  // 开单医生姓名 apply_dr_name
        public string Modality { get; set; }     // 类型/MODALITY = test_type_code

        public List<PacsApplyItem> Items { get; set; }

        public PacsApplyInfo()
        {
            Items = new List<PacsApplyItem>();
        }
    }

    /// <summary>检查申请处理结果</summary>
    public class PacsApplyResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string ApplyNo { get; set; }
    }
}
