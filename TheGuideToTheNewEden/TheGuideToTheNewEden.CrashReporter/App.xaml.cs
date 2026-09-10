using System.Configuration;
using System.Data;
using System.Diagnostics.Eventing.Reader;
using System.Text;
using System.Windows;

namespace TheGuideToTheNewEden.CrashReporter
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static string MSG;
        public static bool FromCrash = false;
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 解析命令行参数
            var args = e.Args;
            if (args.Length > 0)
            {
                MSG = string.Join(",", args);
                FromCrash = true;
            }
            else
            {
                // 没有传入消息时从事件查看器获取与软件相关的异常信息
                MSG = GetEventViewerCrashInfo();
            }
        }

        /// <summary>
        /// 从 Windows 事件查看器获取与 TheGuideToTheNewEden 相关的异常信息
        /// </summary>
        private static string GetEventViewerCrashInfo()
        {
            try
            {
                var sb = new StringBuilder();

                // 查询 Application 日志：Event ID 1000 (Application Error) 和 1026 (.NET Runtime)
                const string query = "*[System[(EventID=1000 or EventID=1026)]]";
                var eventLogQuery = new EventLogQuery("Application", PathType.LogName, query)
                {
                    ReverseDirection = true // 最新的优先
                };

                using var reader = new EventLogReader(eventLogQuery);
                EventRecord eventRecord;
                int count = 0;

                while ((eventRecord = reader.ReadEvent()) != null && count < 10)
                {
                    string description = eventRecord.FormatDescription() ?? string.Empty;
                    if (description.Contains("TheGuideToTheNewEden", StringComparison.OrdinalIgnoreCase))
                    {
                        sb.AppendLine($"═══════════════════════════════════════════");
                        sb.AppendLine($"Event ID: {eventRecord.Id}");
                        sb.AppendLine($"Time: {eventRecord.TimeCreated?.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
                        sb.AppendLine($"Level: {eventRecord.LevelDisplayName}");
                        sb.AppendLine($"Provider: {eventRecord.ProviderName}");
                        sb.AppendLine($"───────────────────────────────────────────");
                        sb.AppendLine(description);
                        sb.AppendLine();
                        count++;
                    }
                }

                if (count == 0)
                {
                    return "未在事件查看器中发现与 TheGuideToTheNewEden 软件相关的异常信息。\r\n" +
                           "No crash information related to TheGuideToTheNewEden found in Event Viewer.";
                }

                sb.AppendLine($"共找到 {count} 条相关记录 (仅显示最近10条)");
                sb.AppendLine($"Found {count} related record(s) (showing up to 10 most recent)");
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"读取事件查看器时出错：{ex.Message}\r\n" +
                       $"Error reading Event Viewer: {ex.Message}";
            }
        }
    }
}
