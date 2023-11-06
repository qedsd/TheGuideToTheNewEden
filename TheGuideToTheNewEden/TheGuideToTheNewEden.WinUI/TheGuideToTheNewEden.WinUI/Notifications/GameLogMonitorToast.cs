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
                .AddArgument(Common.scenarioTag, ScenarioId.ToString())
                //.AddButton(new AppNotificationButton("显示游戏窗口")
                //.AddArgument("action", "ShowGame")
                //.AddArgument(Common.scenarioTag, ScenarioId.ToString()))
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
                var hwnd = Helpers.WindowHelper.GetGameHwndByCharacterName(listener);
                if (hwnd != IntPtr.Zero)
                {
                    Helpers.WindowHelper.SetForegroundWindow_Click(hwnd);
                }
            }
        }
    }
}
