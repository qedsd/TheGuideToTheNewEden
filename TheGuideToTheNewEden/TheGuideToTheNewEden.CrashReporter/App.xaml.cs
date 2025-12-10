using System.Configuration;
using System.Data;
using System.Windows;

namespace TheGuideToTheNewEden.CrashReporter
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static string MSG;
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 解析命令行参数
            var args = e.Args;
            if (args.Length > 0)
            {
                MSG = string.Join(",", args);
            }
        }
    }
}
