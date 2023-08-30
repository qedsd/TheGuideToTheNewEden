using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System;
using System.Diagnostics;
using System.Linq;
using TheGuideToTheNewEden.Core.Models;

namespace TheGuideToTheNewEden.WinUI.Notifications
{
    internal class IntelToast
    {
        public const int ScenarioId = 1;
        public static bool SendToast(string listener, string chanel, EarlyWarningContent earlyWarningContent)
        {
            var appNotification = new AppNotificationBuilder()
                .AddArgument("action", "ToastClick")
                .AddArgument(Common.scenarioTag, ScenarioId.ToString())
                .AddArgument("listener", listener)
                .AddText($"频道预警：{earlyWarningContent.SolarSystemName} {earlyWarningContent.Jumps}跳")
                .AddText($"{chanel}: {earlyWarningContent.Content}")
                .BuildNotification();
            appNotification.Expiration = DateTime.Now.AddMinutes(1);
            AppNotificationManager.Default.Show(appNotification);

            return appNotification.Id != 0;
        }

        public static void NotificationReceived(AppNotificationActivatedEventArgs notificationActivatedEventArgs)
        {
            if (notificationActivatedEventArgs.Arguments.TryGetValue("listener", out string listener))
            {
                var process = Process.GetProcessesByName("exefile");
                if (process != null && process.Any())
                {
                    var targetProc = process.FirstOrDefault(p => p.MainWindowTitle.Contains(listener));
                    if(targetProc != null)
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
