using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using zlSoft.Common;

namespace zlSoft.Business
{
    /// <summary>
    /// 三方（调鼎）"收费申请和撤销收费申请" JSON 接口处理。
    ///
    /// 协议要点：
    /// 1. settleCode 作为 HIS 的登记流水号（dengJiLsh / waibujklsh），每笔交易唯一；
    /// 2. applyType：1-申请（收费）  2-撤销；
    /// 3. 撤销时体检侧传回的仍是收费时的 settleCode，HIS 需要的 feiYongId
    ///    由 HisFeeService 按 waibujklsh=settleCode 到 HIS 库反查后调用撤销接口；
    /// 4. 患者定位沿用现有 HisPatientService.Register()（查到即补卡，查不到即建档），
    ///    与原中间表同步逻辑保持一致；
    /// 5. HIS 侧体检费只生成一条明细（shouFeiXmId/shouFeiXmMc 由配置提供），
    ///    金额取实收金额 payAmount，故 examitems 仅接收不参与 HIS 计费。
    /// </summary>
    public class FeeChargeJson
    {
        // 三方返回码
        private const string CODE_SUCCESS = "1001";
        private const string CODE_ERROR = "9999";

        public static string Process(string requestJson)
        {
            string strSettleCode = "";
            try
            {
                if (string.IsNullOrWhiteSpace(requestJson))
                {
                    return BuildErrorResponse("请求报文不能为空");
                }

                JObject request = JObject.Parse(requestJson);

                strSettleCode = GetString(request, "settleCode");
                string strCheckCode = GetString(request, "checkCode");
                string strApplyType = GetString(request, "applyType");

                if (string.IsNullOrEmpty(strSettleCode))
                {
                    return BuildErrorResponse("settleCode不能为空");
                }
                if (string.IsNullOrEmpty(strCheckCode))
                {
                    return BuildErrorResponse("checkCode不能为空");
                }
                if (strApplyType != "1" && strApplyType != "2")
                {
                    return BuildErrorResponse("applyType取值错误，1-申请 2-撤销");
                }

                //1. 患者定位：沿用现有建档逻辑（已建档补卡/未建档建档），取得 bingRenId、jiuZhenKh
                PatientInfo patInfo = ParsePatient(request, strCheckCode);
                RegisterResult regResult = HisPatientService.Register(patInfo);
                if (regResult == null || !regResult.Success)
                {
                    return BuildErrorResponse("获取HIS患者信息失败：" + (regResult == null ? "" : regResult.Message));
                }

                //2. 三方收退费 -> HIS 生成门诊费用
                FeeInfo feeInfo = BuildFeeInfo(request, strSettleCode, strApplyType, regResult);
                FeeResult feeResult = HisFeeService.CreateFee(feeInfo);

                if (feeResult == null || !feeResult.Success)
                {
                    return BuildErrorResponse(feeResult == null ? "生成费用失败" : feeResult.Message);
                }

                //3. 交易流水号取 HIS 返回的费用ID
                string strSerialNo = "";
                if (feeResult.ReturnData != null)
                {
                    JToken token = feeResult.ReturnData["feiYongId"];
                    if (token != null && token.Type != JTokenType.Null) strSerialNo = token.ToString();
                }

                return BuildResponse(CODE_SUCCESS, strApplyType == "1" ? "发送成功" : "操作成功", strSerialNo);
            }
            catch (JsonReaderException ex)
            {
                LogHelper.LogInfo("三方收费申请JSON解析异常：" + ex.Message);
                return BuildErrorResponse("请求报文格式错误：" + ex.Message);
            }
            catch (Exception ex)
            {
                LogHelper.LogInfo("三方收费申请接口异常，settleCode=" + strSettleCode + "，错误：" + ex.Message);
                return BuildErrorResponse("系统异常：" + ex.Message);
            }
        }

        /// <summary>三方患者字段 -> PatientInfo（供现有建档逻辑使用）</summary>
        private static PatientInfo ParsePatient(JObject request, string checkCode)
        {
            PatientInfo info = new PatientInfo();

            //体检号作为 HIS 的就诊卡号/卡号
            info.PhysicalNo = checkCode;
            info.PatName = GetString(request, "name");
            info.GenderCode = GetString(request, "sex");      // 0-女 1-男，与 HisPatientService 映射一致
            info.DateBirth = GetString(request, "birth");
            info.MaritalCode = GetString(request, "marriage");
            info.CertiTypeCode = GetString(request, "idcardType");
            info.IdNumber = GetString(request, "idcard");     // 建档逻辑按身份证号查询HIS
            info.PhoneNo = GetString(request, "phone");
            info.AddrDesc = GetString(request, "address");

            return info;
        }

        /// <summary>三方收退费请求 -> HIS 生成费用入参</summary>
        private static FeeInfo BuildFeeInfo(JObject request, string settleCode, string applyType, RegisterResult regResult)
        {
            FeeInfo info = new FeeInfo();
            info.BingRenId = regResult.BingRenId;
            info.JiuZhenKh = regResult.JiuZhenKh;

            //settleCode 作为 HIS 登记流水号（dengJiLsh），收费与退费均传同一 settleCode
            info.DengJiLsh = settleCode;

            //operator：调鼎 settleDctCode（操作人员编码）
            info.CaoZuoYuan = zlConfiguration.GetAppSetting("caoZuoYuan"); //GetString(request, "settleDctCode");

            bool blnCancel = applyType == "2";
            info.ShouTuiBz = blnCancel ? "2" : "1";

            FeeItem item = new FeeItem();
            if (blnCancel)
            {
                //撤销：调用 HIS 撤销费用接口，金额/数量不参与；
                //feiYongId 留空，由 HisFeeService 按 settleCode 到 HIS 库反查
                item.ShuLiang = "-1";
            }
            else
            {
                //收费：金额取实收金额 payAmount（为空时取应收金额 totalAmount）
                string strAmount = GetString(request, "payAmount");
                if (string.IsNullOrEmpty(strAmount)) strAmount = GetString(request, "totalAmount");

                item.ShuLiang = "1";
                item.DanJia = strAmount;
                item.JieSuanJe = strAmount;
            }
            info.Items.Add(item);

            return info;
        }

        private static string BuildResponse(string code, string msg, string serialNo)
        {
            JObject response = new JObject();
            response["code"] = code;
            response["msg"] = msg ?? "";
            response["serialNo"] = serialNo ?? "";
            return response.ToString(Formatting.None);
        }

        private static string BuildErrorResponse(string message)
        {
            return BuildResponse(CODE_ERROR, message, "");
        }

        private static string GetString(JObject obj, string name)
        {
            if (obj == null || string.IsNullOrEmpty(name)) return "";
            JToken token = obj[name];
            if (token == null || token.Type == JTokenType.Null) return "";
            return token.ToString().Trim();
        }
    }
}
