using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace zlSoft.Common
{
    public class LogHelper
    {
        //private static readonly log4net.ILog log_err = log4net.LogManager.GetLogger("logerror");
        //private static readonly log4net.ILog log_debug = log4net.LogManager.GetLogger("logdebug");
        private static readonly log4net.ILog log_info = log4net.LogManager.GetLogger("Loging");
        private static readonly log4net.ILog err_info = log4net.LogManager.GetLogger("Logerr");

        /// <summary>
        /// 错误信息记录
        /// </summary>
        /// <param name="description">简单描述</param>
        /// <param name="ex">错误信息</param>
        //public static void LogError(string description, Exception ex)
        //    {
        //    if (log_err.IsErrorEnabled) log_err.Error(description, ex);
        //    }

        //public static void LogError(object message)
        //    {
        //    if (log_err.IsErrorEnabled) log_err.Error(message);
        //    }

        /// <summary>
        /// 调试信息记录
        /// </summary>
        /// <param name="message">调试信息</param>
        //public static void LogDebug(object message)
        //    {
        //    if (log_debug.IsDebugEnabled) log_debug.Debug(message);
        //    }

        //public static void LogDebug(object message, Exception ex)
        //    {
        //    if (log_debug.IsDebugEnabled) log_debug.Debug(message, ex);
        //    }

        /// <summary>
        /// 一般信息记录
        /// </summary>
        /// <param name="message">一般信息</param>
        /// 


        public static void LogInfo(object message)
        {
            if (log_info.IsInfoEnabled) log_info.Info(message);
        }

        public static void LogInfo(object message, Exception ex)
        {

            if (log_info.IsInfoEnabled) log_info.Info(message, ex);
        }
        /// <summary>
        /// 出参日志
        /// </summary>
        /// <param name="message"></param>
        public static void LogOutInfo(object message)
        {
            object objmsg = null;
            string msg = "";
            try
            {
                msg = "[" + (new StackTrace()).GetFrame(1).GetMethod().Name + "] ";
            }
            catch (Exception)
            {
                msg = "";
            }
            objmsg += msg + message;
            if (log_info.IsInfoEnabled) log_info.Info("出参:" + objmsg);
        }
        /// <summary>
        /// 入参日志
        /// </summary>
        /// <param name="message"></param>
        public static void LogInInfo(object message)
        {
            object objmsg = null;
            string msg = "";
            try
            {
                msg = "[" + (new StackTrace()).GetFrame(1).GetMethod().Name + "] ";
            }
            catch (Exception)
            {
                msg = "";
            }
            objmsg += msg + message;
            if (log_info.IsInfoEnabled) log_info.Info("入参:" + objmsg);
        }
        public static void errInfo(object message, Exception ex)
        {

            if (err_info.IsInfoEnabled) err_info.Info(message, ex);
        }

        //(new StackTrace()).GetFrame(1) // 0为本身的方法；1为调用方法
        //(new StackTrace()).GetFrame(1).GetMethod().Name; // 方法名
        //(new StackTrace()).GetFrame(1).GetMethod().ReflectedType.Name; // 类名
    }


}
