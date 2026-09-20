using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace zlSoft.Common
{
    public class zlLogService
    {

        public zlLogService()
        {
            //
            // TODO: 在此处添加构造函数逻辑
            //
        }

        /// <summary>
        /// 向系统的错误日志中写一条新的日志记录。
        /// </summary>
        /// <param name="log">日志的内容。</param>
        public static void WriteErrorLog(string log)
        {
            try
            {
                //string strPath = zlLogService.GetLogFile();
                //StreamWriter sw = new StreamWriter(strPath + "//Error//" + DateTime.Now.ToString("yyyy-MM-dd") + ".log", true);
                //sw.WriteLine(DateTime.Now.ToString() + ":    " + log);
                //sw.Close();
                //通过log4net写日志
                LogHelper.LogInfo(log);
            }
            catch (Exception se)
            {
                LogHelper.errInfo("WriteErrorLog", se);
            }
        }

        public static void WriteErrorLog(string log, Exception ex)
        {
            try
            {
                //string strPath = zlLogService.GetLogFile();
                //StreamWriter sw = new StreamWriter(strPath + "//Error//" + DateTime.Now.ToString("yyyy-MM-dd") + ".log", true);
                //sw.WriteLine(DateTime.Now.ToString() + ":    " + log);
                //sw.Close();
                //通过log4net写日志
                LogHelper.errInfo(log, ex);
            }
            catch (Exception se)
            {
                LogHelper.errInfo("WriteErrorLog", se);
            }
        }

        /// <summary>
        /// 向系统的交易日志中写一条新的日志记录。
        /// </summary>
        /// <param name="log">日志的内容。</param>
        private static void WriteBusinessLog(string log,string strName)
        {
            string strState = zlConfiguration.GetAppSetting("LOGSTATE");
            if (strState == "TRUE")
            {
                try
                {
                    string strPath = zlLogService.GetLogFile();
                    if (!System.IO.Directory.Exists(strPath + "Business\\" + DateTime.Now.ToString("yyyy-MM-dd") + "\\"))
                    {
                        System.IO.Directory.CreateDirectory(strPath + "Business\\" + DateTime.Now.ToString("yyyy-MM-dd") + "\\");
                    }
                    StreamWriter sw = new StreamWriter(strPath + "//Business//" + DateTime.Now.ToString("yyyy-MM-dd") + "//" + strName + ".log", true);
                    sw.WriteLine(log);
                    sw.Close();
                }
                catch (Exception)
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// 向系统的错误日志中写一条新的日志记录。
        /// </summary>
        /// <param name="log">日志的内容。</param>
        public static void Write(string log)
        {
            zlLogService.WriteErrorLog(log);
        }

        /// <summary>
        /// 获得系统的错误日志文件。日志文件的完整路径存放在应用程序配置文件中的logfile配置项下。
        /// </summary>
        /// <returns></returns>
        public static string GetLogFile()
        {
            string filepath = zlConfiguration.GetAppSetting("LOGFILE");
            try
            {
                if (!System.IO.Directory.Exists(filepath))
                {
                    System.IO.Directory.CreateDirectory(filepath);
                }
                if (!System.IO.Directory.Exists(filepath + "Error\\"))
                {
                    System.IO.Directory.CreateDirectory(filepath + "Error\\");
                }
                if (!System.IO.Directory.Exists(filepath + "Business\\"))
                {
                    System.IO.Directory.CreateDirectory(filepath + "Business\\");
                }
                //删除有效期日期以前的日志
                //if (zlInitiation.LOGDAY > 0)
                //{
                //    string strFileName = DateTime.Now.AddDays(-(zlInitiation.LOGDAY)).ToShortDateString() + ".log";
                //    if (System.IO.File.Exists(filepath + "Error\\" + strFileName))
                //    {
                //        System.IO.File.Delete(filepath + "Error\\" + strFileName);
                //    }
                //    if (System.IO.File.Exists(filepath + "Business\\" + strFileName))
                //    {
                //        System.IO.File.Delete(filepath + "Business\\" + strFileName);
                //    }
                //}
            }
            catch (Exception e)
            {
                zlLogService.Write(e.Message);
            }
            return filepath;
        }

        /// <summary>
        /// 向系统的访问日志中写入一条新的记录。系统的访问日志位于系统的DATA文件夹下的LOG文件夹中。
        /// </summary>
        /// <param name="log">日志的内容。</param>
        public static void WriteAccessLog(string log)
        {
            try
            {
                string filepath = zlConfiguration.GetAppSetting("datadir");
                filepath += "log\\";

                if (!System.IO.Directory.Exists(filepath))
                {
                    System.IO.Directory.CreateDirectory(filepath);
                }
                StreamWriter sw = new StreamWriter(filepath + "ACCESSLOG" + DateTime.Today.ToString("yyyyMMdd") + ".txt", true);
                sw.WriteLine(DateTime.Now.ToString() + ":   " + log);
                sw.Close();
            }
            catch (Exception e)
            {
                zlLogService.Write(e.Message);
            }
        }

        /// <summary>
        /// 向系统的管理员日志中写入一条新的记录。系统的管理员日志位于系统的DATA文件夹下的LOG文件夹中。
        /// </summary>
        /// <param name="log"></param>
        public static void WriteAdminLog(string log)
        {
            try
            {
                string filepath = zlConfiguration.GetAppSetting("logfile");
                filepath += "log\\";

                if (!System.IO.Directory.Exists(filepath))
                {
                    System.IO.Directory.CreateDirectory(filepath);
                }
                StreamWriter sw = new StreamWriter(filepath + "ADMINLOG" + DateTime.Today.ToString("yyyyMMdd") + ".txt", true);
                sw.WriteLine(DateTime.Now.ToString() + ":   " + log);
                sw.Close();
            }
            catch (Exception e)
            {
                zlLogService.Write(e.Message);
            }
        }

        /// <summary>
        /// 向Windows的应用程序日志中写一条新的记录
        /// </summary>
        /// <param name="log"></param>
        /// <param name="error"></param>
        static public void WriteEvent(string app, string log, bool error)
        {
            if (!System.Diagnostics.EventLog.SourceExists(app))
            {
                System.Diagnostics.EventLog.CreateEventSource(app, "");
            }
            System.Diagnostics.EventLog eventlog = new System.Diagnostics.EventLog();
            eventlog.Source = app;
            eventlog.Log = "";
            if (error)
                eventlog.WriteEntry(log, System.Diagnostics.EventLogEntryType.Error);
            else
                eventlog.WriteEntry(log, System.Diagnostics.EventLogEntryType.Information);
        }
    }
}
