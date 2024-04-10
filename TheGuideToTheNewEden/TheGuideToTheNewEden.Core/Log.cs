using log4net;
using log4net.Config;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.Core
{
    public static class Log
    {
        private static long infoCount;
        private static long errorCount;
        private static ILog log;
        private static object _locker = new object();
        private static object _lastError;
        public static void Init()
        {
            XmlConfigurator.Configure(new FileInfo(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log4net.config")));
            log = LogManager.GetLogger(typeof(Log));
        }
        public static void Info(object info)
        {
            System.Threading.Interlocked.Increment(ref infoCount);
            log.Info(info);
            OnInfo?.Invoke(info);
        }
        public static void Error(object error)
        {
            lock(_locker)
            {
                _lastError = error;
            }
            System.Threading.Interlocked.Increment(ref errorCount);
            log.Error(error);
            OnError?.Invoke(error);
        }
        public static void Fatal(object error)
        {
            log.Fatal(error);
        }
        public static void Warn(object warn)
        {
            log.Warn(warn);
            OnWarn?.Invoke(warn);
        }
        public static void Debug(object debug)
        {
            log.Debug(debug);
        }

        public static string GetLogPath()
        {
            return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Log");
        }

        public static long GetErrorCount()
        {
            return System.Threading.Interlocked.Read(ref errorCount);
        }

        public static long GetInfoCount()
        {
            return System.Threading.Interlocked.Read(ref infoCount);
        }

        public static object GetLastError()
        {
            return _lastError;
        }

        public delegate void LogMsgEvent(object msg);
        public static event LogMsgEvent OnError;
        public static event LogMsgEvent OnInfo;
        public static event LogMsgEvent OnWarn;
    }
}
