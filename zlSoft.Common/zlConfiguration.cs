using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;

//功能：获取配置文件Config的值
//程序：陈玉强
//日期：2013-04-01

namespace zlSoft.Common
{
    /// <summary>
    /// 取得Config的值
    /// </summary>
    public class zlConfiguration
    {
        public zlConfiguration()
        {
        }
        /// <summary>
        /// 获取Config的值
        /// </summary>
        /// <param name="AppSettingName">获取字段名</param>
        /// <returns></returns>
        public static string GetAppSetting(string AppSettingName)
        {
            return ConfigurationManager.ConnectionStrings[AppSettingName].ConnectionString;
        }
    }
}
