using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using zlSoft.Common;


//功能：服务初始化业务实现
//程序：陈玉强
//日期：2013-04-01

namespace zlSoft.Business
{
    /// <summary>
    /// 服务初始化业务实现
    /// </summary>
   public class execInit
    {
        #region Init 服务初始化

        /// <summary>
        /// 实现接口初始化及验证信息
        /// </summary>
        /// <returns></returns>

        public static string Init()
        {
            string strResponse = "";
            try
            {
                //检测授权信息[取消检测服务将不能使用]
                zlLicense.VerifyLicense();
                if (zlLicense.ServerChecked)
                {
                    //注册文件正确！
                    //返回值
                    strResponse = ""+
                        "<Response>\n" +
                        "    <ResultCode>0</ResultCode>\n" +
                        "    <ErrorMsg>初始化成功！</ErrorMsg>\n" +
                        "    <HisDateTime>" + zlFunc.GetDate(zlLicense.HisDateTime.ToString()) + "</HisDateTime>\n" +
                        "    <UseDateTime>" + zlFunc.GetDate(zlLicense.UseDateTime.ToString(), "yyyy-MM-dd") + "</UseDateTime>\n" +
                        "    <RegInfo>" + zlLicense.RegInfo + "</RegInfo>\n" +
                        "    <ProgCode>XieRong 2013-05-12</ProgCode>\n" +
                        "</Response>";
                }
                else
                {
                    //服务日期到期，请检测注册文件！
                    //返回值
                    strResponse =""+
                        "<Response>\n" +
                        "    <ResultCode>-1</ResultCode>\n" +
                        "    <ErrorMsg>" + zlLicense.ErrMsg + "</ErrorMsg>\n" +
                        "    <HisDateTime>" + zlFunc.GetDate(zlLicense.HisDateTime.ToString()) + "</HisDateTime>\n" +
                        "    <UseDateTime>" + zlFunc.GetDate(zlLicense.UseDateTime.ToString(), "yyyy-MM-dd") + "</UseDateTime>\n" +
                        "    <RegInfo>" + zlLicense.RegInfo + "</RegInfo>\n" +
                        "    <ProgCode>XieRong 2013-05-12</ProgCode>\n" +
                       "</Response>";
                }
                return strResponse;
            }
            catch (Exception se)
            {
            zlLogService.WriteErrorLog("Init", se);
                strResponse =""+
                    "<Response>\n" +
                    "    <ResultCode>-1</ResultCode>\n" +
                    "    <ErrorMsg>" + se.Message + "</ErrorMsg>\n" +
                    "    <ProgCode>XieRong 2013-05-12</ProgCode>\n" +
                   "</Response>";
                return strResponse;
            }
        }
        #endregion Init 服务初始化
    }

}
