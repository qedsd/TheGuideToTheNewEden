using Microsoft.Windows.AppNotifications.Builder;
using Microsoft.Windows.AppNotifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models;
using System.Diagnostics;
using TheGuideToTheNewEden.WinUI.Services;

namespace TheGuideToTheNewEden.WinUI.Notifications
{
    internal class GameLogMonitorToast
    {
        public const int ScenarioId = 2;
        public static bool SendToast(int id, string listener, string content)
        {
            var appNotification = new AppNotificationBuilder()
                .AddArgument("action", "ToastClick")
                .AddArgument(Common.scenarioTag, (new Random()).Next().ToString())
                .AddArgument("id", id.ToString())
                .AddArgument("listener", listener)
                .AddText($"日志监控 - {listener}")
                .AddText(content)
                .BuildNotification();

            AppNotificationManager.Default.Show(appNotification);

            return appNotification.Id != 0;
        }
        public static void NotificationReceived(AppNotificationActivatedEventArgs notificationActivatedEventArgs)
        {
            if(notificationActivatedEventArgs.Arguments.TryGetValue("id", out string id))
            {
                GameLogMonitorNotifyService.Current.Stop(int.Parse(id));
            }
            if (notificationActivatedEventArgs.Arguments.TryGetValue("listener", out string listener))
            {
                var process = Process.GetProcessesByName("exefile");
                if (process != null && process.Any())
                {
                    var targetProc = process.FirstOrDefault(p => p.MainWindowTitle.Contains(listener));
                    if (targetProc != null)
                    {
                        if (targetProc.MainWindowHandle != IntPtr.Zero)
                        {
                            Helpers.WindowHelper.SetForegroundWindow_Click(targetProc.MainWindowHandle);
                        }
                    }
                    else
                    {
                        Core.Log.Error($"无法找到{listener}的游戏窗口");
                    }
                }
                else
                {
                    Core.Log.Error("无法找到exefile进程");
                }
            }
        }
    }
}
