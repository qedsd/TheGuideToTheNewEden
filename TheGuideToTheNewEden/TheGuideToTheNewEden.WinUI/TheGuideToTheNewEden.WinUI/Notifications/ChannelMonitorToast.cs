using Microsoft.Windows.AppNotifications.Builder;
using Microsoft.Windows.AppNotifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.WinUI.Services;

namespace TheGuideToTheNewEden.WinUI.Notifications
{
    internal class ChannelMonitorToast
    {
        public const int ScenarioId = 4;
        public static bool SendToast(string listener, string content)
        {
            var appNotification = new AppNotificationBuilder()
                .AddArgument("action", "ToastClick")
                .AddArgument(Common.scenarioTag, ScenarioId.ToString())
                .AddArgument("listener", listener)
                .AddText($"频道监控 - {listener}")
                .AddText(content)
                .SetGroup(listener)
                .BuildNotification();

            AppNotificationManager.Default.Show(appNotification);
            return appNotification.Id != 0;
        }
        public static void NotificationReceived(AppNotificationActivatedEventArgs notificationActivatedEventArgs)
        {
            if (notificationActivatedEventArgs.Arguments.TryGetValue("listener", out string listener))
            {
                ChannelMonitorNotifyService.Current.Stop(listener);
                var hwnd = Helpers.WindowHelper.GetGameHwndByCharacterName(listener);
                if (hwnd != IntPtr.Zero)
                {
                    Helpers.WindowHelper.SetForegroundWindow_Click(hwnd);
                }
            }
        }
    }
}
