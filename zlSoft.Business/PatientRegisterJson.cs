using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using zlSoft.Common;

namespace zlSoft.Business
{
    /// <summary>
    /// 三方 HIS 建档接口 JSON 报文处理。
    ///
    /// 说明：
    /// 1. 本类只负责三方 JSON 入参/出参转换；
    /// 2. 不修改、不复制 HIS 建档业务逻辑；
    /// 3. HIS 查询、已建档补卡、未建档建档全部继续调用
    ///    HisPatientService.Register()；
    /// 4. alternateNo 本次仅接收，不参与 HIS 查询及建档逻辑。
    /// </summary>
    public class PatientRegisterJson
    {
        /// <summary>
        /// 处理三方 JSON 建档请求。
        ///
        /// 三方请求：
        /// {
        ///     "hospCode": "xazxsd",
        ///     "alternateNo": "30008792",
        ///     "hisNo": "10000001",
        ///     "name": "张家辉",
        ///     "isPersonal": "1",
        ///     "sex": "1",
        ///     "birth": "1978-09-12",
        ///     "marriage": "1",
        ///     "idcardType": "1",
        ///     "idcard": "330724197809120337",
        ///     "phone": "13588170962",
        ///     "address": "杭州市余杭区仓前街道昌邑路3号"
        /// }
        ///
        /// 返回：
        /// {
        ///     "code": "1001",
        ///     "msg": "成功",
        ///     "hisNo": "10000001",
        ///     "alternateNo": "30008792"
        /// }
        /// </summary>
        public static string Process(string requestJson)
        {
            string alternateNo = "";
            string hisNo = "";

            try
            {
                if (string.IsNullOrWhiteSpace(requestJson))
                {
                    return BuildErrorResponse("请求报文不能为空");
                }

                JObject request = JObject.Parse(requestJson);

                // 三方字段，仅用于返回及协议转换。
                // 不参与现有 HIS 建档逻辑。
                alternateNo = GetString(request, "alternateNo");
                hisNo = GetString(request, "hisNo");

                // 1. 三方 JSON -> 当前系统 PatientInfo
                PatientInfo info = ParseRequest(request);

                // 2. 沿用现有 HIS 建档逻辑
                //
                // 这里非常重要：
                // 不在这里重新实现“查HIS -> 补卡 -> 建档”。
                // 统一调用现有 HisPatientService.Register()。
                RegisterResult result = HisPatientService.Register(info);

                // 3. 当前系统 RegisterResult -> 三方 JSON
                return BuildResponse(result);
            }
            catch (JsonReaderException ex)
            {
                LogHelper.LogInfo(
                    "三方患者建档JSON解析异常：" + ex.Message);

                return BuildErrorResponse(
                    "请求报文格式错误：" + ex.Message);
            }
            catch (Exception ex)
            {
                LogHelper.LogInfo(
                    "三方患者建档接口异常：" + ex.Message);

                return BuildErrorResponse(
                    "系统异常：" + ex.Message);
            }
        }

        /// <summary>
        /// 将三方 JSON 参数转换成现有 PatientInfo。
        ///
        /// 注意：
        /// 这里只做字段映射，不做 HIS 业务处理。
        /// </summary>
        private static PatientInfo ParseRequest(JObject request)
        {
            PatientInfo info = new PatientInfo();

            /*
             * 三方：
             * name
             * ->
             * 现有：
             * PatName
             */
            info.PatName = GetString(request, "name");

            /*
             * 三方：
             * sex
             * ->
             * GenderCode
             *
             * 三方文档：
             * 0-女
             * 1-男
             *
             * 当前 HisPatientService.BuildJianDangJson()
             * 已经负责 GenderCode -> HIS 性别值的转换，
             * 所以这里不要再次转换。
             */
            info.GenderCode = GetString(request, "sex");

            /*
             * 出生日期：
             * birth -> DateBirth
             */
            info.DateBirth = GetString(request, "birth");

            /*
             * 婚姻：
             * marriage -> MaritalCode
             */
            info.MaritalCode = GetString(request, "marriage");

            /*
             * 证件类型：
             * idcardType -> CertiTypeCode
             */
            info.CertiTypeCode = GetString(request, "idcardType");

            /*
             * 证件号：
             *
             * 这是当前 HIS 建档逻辑最重要的字段。
             *
             * HisPatientService.Register()
             * 当前就是根据 info.IdNumber 查询 HIS。
             */
            info.IdNumber = GetString(request, "idcard");

            /*
             * 电话：
             * phone -> PhoneNo
             */
            info.PhoneNo = GetString(request, "phone");

            /*
             * 地址：
             * address -> AddrDesc
             */
            info.AddrDesc = GetString(request, "address");

            /*
             * 个人/单位：
             * isPersonal -> PatTypeCode
             *
             * 当前 HIS 建档逻辑暂未使用该字段，
             * 但先完整接收并映射，避免协议字段丢失。
             */
            info.PatTypeCode = GetString(request, "isPersonal");

            /*
             * HIS 就诊号：
             * hisNo -> VisitCardNo
             *
             * 注意：
             * 当前 HisPatientService.Register()
             * 的实际建档/补卡逻辑仍按照原代码执行。
             *
             * 这里仅完成内部模型映射。
             */
            info.VisitCardNo = GetString(request, "hisNo");

            /*
             * hospCode：
             *
             * 三方协议存在该字段，但是当前 PatientInfo
             * 没有对应属性，而且现有 HisPatientService
             * 也没有使用医院编码。
             *
             * 因此本次不处理。
             */
            string hospCode = GetString(request, "hospCode");

            /*
             * alternateNo：
             *
             * 三方定义：
             * HIS 档案号，非空表示更新。
             *
             * 但根据本次需求：
             * HIS 查询、建档、补卡逻辑全部原样不动。
             *
             * 因此这里不能把 alternateNo 塞进
             * PhysicalNo，也不能拿它替换 IdNumber。
             *
             * 仅接收，不参与 HIS 业务。
             */
            string alternateNo = GetString(request, "alternateNo");

            return info;
        }

        /// <summary>
        /// 将现有 RegisterResult 转换成三方要求的 JSON 返回格式。
        /// </summary>
        private static string BuildResponse(RegisterResult result)
        {
            if (result == null)
            {
                return BuildErrorResponse("HIS建档返回结果为空");
            }

            JObject response = new JObject();

            /*
             * 三方：
             * 1001 = 成功
             * 9999 = 错误
             */
            response["code"] = result.Success ? "1001" : "9999";

            response["msg"] = result.Message ?? "";

            /*
             * 当前系统：
             * JiuZhenKh = HIS 就诊卡号
             *
             * 三方：
             * hisNo = 就诊号
             */
            if (result.Success)
            {
                response["hisNo"] = result.JiuZhenKh ?? "";

                /*
                 * 当前系统：
                 * BingRenId = HIS 病人ID
                 *
                 * 三方：
                 * alternateNo = HIS 档案号
                 */
                response["alternateNo"] = result.BingRenId ?? "";
            }

            return response.ToString(Formatting.None);
        }

        /// <summary>
        /// 生成三方错误返回。
        /// </summary>
        private static string BuildErrorResponse(string message)
        {
            JObject response = new JObject();

            response["code"] = "9999";
            response["msg"] = message ?? "";

            return response.ToString(Formatting.None);
        }

        /// <summary>
        /// 安全读取 JSON 字符串字段。
        ///
        /// 字段不存在、null 均返回空字符串。
        /// </summary>
        private static string GetString(JObject obj, string name)
        {
            if (obj == null || string.IsNullOrEmpty(name))
            {
                return "";
            }

            JToken token = obj[name];

            if (token == null || token.Type == JTokenType.Null)
            {
                return "";
            }

            return token.ToString().Trim();
        }
    }
}