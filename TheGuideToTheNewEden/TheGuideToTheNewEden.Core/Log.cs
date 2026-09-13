using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Layout;
using System;
using System.IO;
using System.Linq;

namespace TheGuideToTheNewEden.Core
{
    /// <summary>
    /// 全局日志。**任何一处写日志都不应该抛异常**：早期版本里 <see cref="Error"/> 直接用
    /// <c>log.Error(...)</c>，一旦在 <see cref="Init"/> 之前有人记日志（例如首次运行
    /// <c>Configs/ESILicense.txt</c> 不存在时的那条提示）就会抛 NullReferenceException，
    /// **把真正的错误盖掉、界面直接崩**。现在改成：懒初始化 + 全程 try/catch + 兜底输出。
    /// </summary>
    public static class Log
    {
        private const string ConfigFileName = "log4net.config";

        private static long infoCount;
        private static long errorCount;
        private static ILog log;
        private static readonly object _locker = new object();
        private static object _lastError;

        /// <summary>初始化 log4net。可重复调用（幂等），并且不会抛异常。</summary>
        public static void Init()
        {
            lock (_locker)
            {
                if (log != null)
                {
                    return;
                }

                try
                {
                    // 日志目录可能还不存在（首次运行）：先建出来，否则 rolling file appender 写不进去
                    var logDirectory = GetLogPath();
                    if (!string.IsNullOrEmpty(logDirectory))
                    {
                        Directory.CreateDirectory(logDirectory);
                    }

                    var configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
                    if (File.Exists(configFile))
                    {
                        XmlConfigurator.Configure(new FileInfo(configFile));
                    }
                    else
                    {
                        // 配置文件丢了也不能变成"没有日志"：挂一个最小的文件 appender 兜底
                        var fallback = new RollingFileAppender();
                        fallback.File = Path.Combine(
                            string.IsNullOrEmpty(logDirectory) ? AppDomain.CurrentDomain.BaseDirectory : logDirectory,
                            "fallback.log");
                        fallback.AppendToFile = true;
                        fallback.Layout = new PatternLayout("%date [%thread] %-5level %message%newline");
                        fallback.ActivateOptions();
                        BasicConfigurator.Configure(fallback);
                    }

                    log = LogManager.GetLogger(typeof(Log));
                }
                catch (Exception ex)
                {
                    // 配置坏了也要有个能用的 logger（没有 appender 时只是不落盘，但绝不抛）
                    Fallback("Log.Init 失败：" + ex);
                    try
                    {
                        log = LogManager.GetLogger(typeof(Log));
                    }
                    catch
                    {
                        log = null;
                    }
                }
            }
        }

        /// <summary>真正写日志时用的实例：没初始化过就先初始化（调用顺序问题不再致命）。</summary>
        private static ILog Logger
        {
            get
            {
                var current = log;
                if (current != null)
                {
                    return current;
                }

                Init();
                return log;
            }
        }

        private static void Write(Action<ILog> write, object message)
        {
            try
            {
                var logger = Logger;
                if (logger == null)
                {
                    Fallback(Format(message));
                    return;
                }

                write(logger);
            }
            catch (Exception ex)
            {
                Fallback(Format(message) + "（写日志失败：" + ex.Message + "）");
            }
        }

        private static void Raise(LogMsgEvent handler, object message)
        {
            if (handler == null)
            {
                return;
            }

            try
            {
                handler(message);
            }
            catch (Exception ex)
            {
                Fallback("日志事件订阅者抛异常：" + ex.Message);
            }
        }

        private static string Format(object message)
        {
            if (message == null)
            {
                return string.Empty;
            }

            var exception = message as Exception;
            return exception != null ? exception.ToString() : message.ToString();
        }

        private static string FallbackFile
            => Path.Combine(GetLogPath(), "fallback.log");

        private static void Fallback(string text)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine(text);
                Console.WriteLine(text);
            }
            catch
            {
                // 忽略
            }

            // 日志配置坏了/没有 appender 时，至少还留下一份兜底文件，免得"什么都看不到"
            try
            {
                File.AppendAllText(FallbackFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + text + Environment.NewLine);
            }
            catch
            {
                // 连兜底都失败就算了：日志永远不该把程序带崩
            }
        }

        public static void Info(object info)
        {
            System.Threading.Interlocked.Increment(ref infoCount);
            Write(l => l.Info(info), info);
            Raise(OnInfo, info);
        }

        public static void Error(object error)
        {
            lock (_locker)
            {
                _lastError = error;
            }

            System.Threading.Interlocked.Increment(ref errorCount);
            Write(l => l.Error(error), error);
            Raise(OnError, error);
        }

        public static void Fatal(object error)
        {
            Write(l => l.Fatal(error), error);
        }

        public static void Warn(object warn)
        {
            Write(l => l.Warn(warn), warn);
            Raise(OnWarn, warn);
        }

        public static void Debug(object debug)
        {
            Write(l => l.Debug(debug), debug);
            Raise(OnDebug, debug);
        }

        /// <summary>
        /// 日志目录。注意 <see cref="Config.AppDataPath"/> 在 <c>CoreInitializer</c> 里是**晚于**日志初始化才赋值的，
        /// 所以这里不能直接 <c>Path.Combine</c>（null 会抛异常、日志就没了），得给一个稳妥的兜底目录。
        /// </summary>
        public static string GetLogPath()
        {
            var root = Config.AppDataPath;
            if (string.IsNullOrEmpty(root))
            {
                root = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "TheGuideToTheNewEden");
            }

            return System.IO.Path.Combine(root, "Logs");
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

        public static ILog GetInstance()
        {
            return log;
        }

        public static string GetLogFile()
        {
            try
            {
                var logger = Logger;
                if (logger == null || logger.Logger == null || logger.Logger.Repository == null)
                {
                    return null;
                }

                var appender = logger.Logger.Repository.GetAppenders()
                    .FirstOrDefault(p => p is RollingFileAppender) as RollingFileAppender;
                return appender == null ? null : appender.File;
            }
            catch
            {
                return null;
            }
        }

        public delegate void LogMsgEvent(object msg);
        public static event LogMsgEvent OnError;
        public static event LogMsgEvent OnInfo;
        public static event LogMsgEvent OnWarn;
        public static event LogMsgEvent OnDebug;
    }
}
