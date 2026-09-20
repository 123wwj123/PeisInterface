using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace zlSoft.Business
{
    /// <summary>费用明细（请求），对应 createMenZhenFyReq/feiYongMxList 中的一条</summary>
    public class FeeItem
    {
        public string FeiYongMxId { get; set; }  // 费用明细Id（退费必传）
        public string FeiYongId { get; set; }    // 费用Id（退费必传）
        public string ShouFeiXmId { get; set; }  // 收费项目Id
        public string ShouFeiXmMc { get; set; }  // 收费项目名称
        public string ShuLiang { get; set; }     // 数量（退费为负）
        public string DanJia { get; set; }       // 单价
        public string JieSuanJe { get; set; }    // 结算金额（退费为负）
        public string ZhiXingKs { get; set; }    // 执行科室
        public string ZhiXingKsMc { get; set; }  // 执行科室名称
    }

    /// <summary>
    /// 生成费用（收费/退费）请求信息，从 body/createMenZhenFyReq 解析而来。
    /// </summary>
    public class FeeInfo
    {
        public string BingRenId { get; set; }    // 病人Id（建档时返回）
        public string JiuZhenKh { get; set; }    // 就诊卡号
        public string CaoZuoYuan { get; set; }   // 操作员
        public string YuanQuId { get; set; }     // 院区Id
        public string YingYongId { get; set; }   // 应用Id
        public string KaiDanKs { get; set; }     // 开单科室
        public string DengJiLsh { get; set; }    // 登记流水号（第三方唯一）
        public string ShouTuiBz { get; set; }    // 收退标志 1:生成待收费 2:生成待退费
        public List<FeeItem> Items { get; set; }

        public FeeInfo()
        {
            Items = new List<FeeItem>();
        }
    }

    /// <summary>生成费用处理结果。ReturnData 直接保留 HIS 返回的 DTO_MZ_CreateMzFy 原始对象，用于透传全部出参。</summary>
    public class FeeResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public JObject ReturnData { get; set; }  // HIS returnData（createMenZhenFy 的完整返回）
    }
}
