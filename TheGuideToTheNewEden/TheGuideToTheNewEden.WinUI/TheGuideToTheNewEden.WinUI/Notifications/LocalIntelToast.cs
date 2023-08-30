using Microsoft.Windows.AppNotifications.Builder;
using Microsoft.Windows.AppNotifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Notifications
{
    internal class LocalIntelToast
    {
        public const int ScenarioId = 2;
        public static async Task<bool> SendToast(IntPtr hwnd, string title, string changedMsg, string remainMsg)
        {
            await AppNotificationManager.Default.RemoveByGroupAsync(title);
            var appNotification = new AppNotificationBuilder()
                .AddArgument("action", "ToastClick")
                .AddArgument(Common.scenarioTag, ScenarioId.ToString())
                .AddArgument("HWnd", hwnd.ToString())
                .AddText($"本地预警 - {title.Trim()}")
                .AddText(changedMsg)
                .AddText(remainMsg)
                .SetGroup(title)
                .BuildNotification();
            AppNotificationManager.Default.Show(appNotification);
            return appNotification.Id != 0;
        }

        public static void NotificationReceived(AppNotificationActivatedEventArgs notificationActivatedEventArgs)
        {
            if(notificationActivatedEventArgs.Arguments.TryGetValue("HWnd", out string hwndString))
            {
                var hwnd = IntPtr.Parse(hwndString);
                if(hwnd != IntPtr.Zero)
                {
                    Helpers.WindowHelper.SetForegroundWindow_Click(hwnd);
                }
            }
        }
    }
}
