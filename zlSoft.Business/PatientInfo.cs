using System;

namespace zlSoft.Business
{
    /// <summary>
    /// 体检通过API传入的患者建档信息（从请求XML的 body/patiInfo 解析而来）
    /// </summary>
    public class PatientInfo
    {
        public string PatId { get; set; }          // pat_id  患者业务系统唯一标识（体检侧）
        public string PhysicalNo { get; set; }      // physical_no 体检号
        public string VisitCardNo { get; set; }     // visit_card_no 就诊卡号
        public string PatTypeCode { get; set; }     // pat_type_code 患者来源类型

        public string CertiTypeCode { get; set; }   // certi_type_code 证件类型编码
        public string CertiType { get; set; }       // certi_type 证件类型名称
        public string IdNumber { get; set; }        // id_number 身份证件号码

        public string PatName { get; set; }         // pat_name 姓名
        public string GenderCode { get; set; }      // gender_code 性别代码
        public string GenderName { get; set; }      // gender_name 性别名称
        public string DateBirth { get; set; }       // date_birth 出生日期
        public string EthnicCode { get; set; }      // ethnic_code 民族代码
        public string EthnicName { get; set; }      // ethnic_name 民族名称
        public string MaritalCode { get; set; }     // marital_code 婚姻状况代码
        public string MaritalName { get; set; }     // marital_name 婚姻状况名称
        public string PhoneNo { get; set; }         // phone_no 联系电话

        public string AddrDesc { get; set; }        // pre_addr/addr_desc 现住址（非结构化）

        public string OccupCategCode { get; set; }  // occup_categ_code 职业类别代码
        public string OccupCategName { get; set; }  // occup_categ_name 职业类别名称
        public string CompanyName { get; set; }     // company_name 工作单位名称

        public string RecordTime { get; set; }      // record_time 患者登记时间
        public string RegWorkerCode { get; set; }   // author1/reg_worker_code 录入职工代码
        public string RegWorkerName { get; set; }   // author1/reg_worker_name 录入职工姓名
    }

    /// <summary>
    /// HIS建档处理结果
    /// </summary>
    public class RegisterResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }      // 处理说明 -> res_msg
        public string BingRenId { get; set; }    // HIS病人ID -> 返回 body/pat_id
        public string JiuZhenKh { get; set; }    // HIS就诊卡号
        public string PhysicalNo { get; set; }   // 体检号 -> 返回 body/physical_no（原样回传）
    }
}
