using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using zlSoft.Common;
using zlSoft.Business;
using Newtonsoft.Json.Linq;
//using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;
//using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.Collections.Specialized;

namespace SpdService
{
    public partial class frmMain : Form
    {
        //体检API自托管服务（HIS侧改为API模式）
        private ApiServer _apiServer;

        public frmMain()
        {
            InitializeComponent();
        }

        private void frmMain_Load(object sender, EventArgs e)
        {
            string strTime;
            //string strInfo;
            string strMDDataType;
            string strMDDataConn;

            try
            {
                strMDDataType = zlConfiguration.GetAppSetting("MDDataType");
                strMDDataConn = zlConfiguration.GetAppSetting("MDDataConn");
                //打开数据库连接，获取当前时间
                strTime = zlFunc.GetSysDate(strMDDataType, strMDDataConn);

                //校验注册码
                //if (!zlFunc.CheckKey(out strInfo, DateTime.Parse(strTime)))
                //{
                //    MessageBox.Show("服务启动失败，注册码有误！", "体检接口");
                //}

                //lblInfo.Text = strInfo;

                //启动定时器，开始循环作业
                tmrRun.Interval = 1000 * Convert.ToInt16(zlConfiguration.GetAppSetting("TIME"));
                tmrRun.Enabled = true;

                tmrRun_Tick(null, null);

                //启动体检API服务（接收体检主动调用，如患者建档）
                _apiServer = new ApiServer();
                _apiServer.Start();

            }
            catch (Exception ex)
            {
                MessageBox.Show("服务启动失败。" + ex.Message, "体检接口");
            }
        }

        private void tmrRun_Tick(object sender, EventArgs e)
        {
            string strMDDataType;
            string strMDDataConn;

            strMDDataType = zlConfiguration.GetAppSetting("MDDataType");
            strMDDataConn = zlConfiguration.GetAppSetting("MDDataConn");
            zlCommand cmd = new zlCommand(strMDDataType, strMDDataConn, "");
            btnQuit.Enabled = false;

            try
            {
                // string strIdCard = "340103200009108018";
                //string strIn1 = "{\"shenFenZh\":\"" + strIdCard + "\"}";

                // HttpService.Post(strIn1, "http://10.112.1.11:9000/OAPI/bingRenXx/getJkBingRenXxByConditions", "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzY29wZSI6WyI2MjY0MTYyNTk2MTY5OTczNzYiXSwiZXhwIjoxNzU3NjIwMjE5LCJqdGkiOiJkYjU4OWQ4Mi1hNjFiLTRjY2UtYjllMi1iNzNmNTZkZDY2ZGMiLCJjbGllbnRfaWQiOiJYRlpaUUVmWFRaN2V4aGhpIn0.7_QrEIRFhptvWCzqeMKM6_ncctWtw3fOb6nxfp7t9Vg", "POST");
                //同步计费信息到HIS
                //ClsSyncFee.SyncFee(cmd, strMDDataType);

                //同步体检PACS申请撤销信息到PACS系统
                //ClsCancelPacsApply.CancelPacsApply(cmd, strMDDataType);

                //同步PACS申请到PACS系统
                //ClsSendPacsApply.SendPacsApply(cmd, strMDDataType);

                //同步PACS结果到中间库
                //ClsGetPacsResult.GetPacsResult(cmd, strMDDataType);


                //同步PACS变动结果到中间库
                //ClsGetPacsResultAgain.GetPacsResult(cmd, strMDDataType);

                //处理羚云Token
                if (Program.gdtToken < System.DateTime.Now.AddMinutes(5))
                {
                    //获取新的Token
                    string strIn = "grant_type=client_credentials&client_id=" + zlConfiguration.GetAppSetting("ClientID") + "&client_secret=" + zlConfiguration.GetAppSetting("ClientSecret");
                    string strOut = HttpService.Post(strIn, zlConfiguration.GetAppSetting("LisTokenUrl"), "", "POST");
                    if (strOut.Contains("access_token"))
                    {
                        JObject jobjResult = JObject.Parse(strOut);
                        Program.gstrToken = jobjResult["access_token"].ToString();
                        long lngSecond = Convert.ToInt64(jobjResult["expires_in"].ToString());
                        Program.gdtToken = System.DateTime.Now.AddSeconds(lngSecond);
                    }
                    else
                    {
                        LogHelper.LogInfo("【" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "】获取Token异常，返回内容：" + strOut);
                        return;
                    }
                }

                ////同步体检LIS申请撤销信息到LIS系统
                //ClsCancelLisApply.CancelLisApply(cmd, strMDDataType, Program.gstrToken);

                ////同步LIS申请信息到LIS系统
                //ClsSendLisApply.SendLisApply(cmd, strMDDataType, Program.gstrToken);

                ////同步LIS结果到中间库
                //ClsGetLisResult.GetLisResult(cmd, strMDDataType, Program.gstrToken);
                ////同步LIS变动结果到中间库
                ClsGetLisResultAgain.GetLisResult(cmd, strMDDataType, Program.gstrToken);
            }
            catch (Exception ex)
            {
                LogHelper.LogInfo(ex.Message);
            }

            cmd.Close("SqlServer");
            btnQuit.Enabled = true;
        }

        private void btnQuit_Click(object sender, EventArgs e)
        {
            tmrRun.Enabled = false;
            if (_apiServer != null)
            {
                _apiServer.Stop();
            }
            System.Environment.Exit(0);
        }
    }
}
