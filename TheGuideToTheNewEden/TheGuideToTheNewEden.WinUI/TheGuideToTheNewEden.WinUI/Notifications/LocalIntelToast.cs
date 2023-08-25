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
        public const string GroupId = "LocalIntel";
        public const int ScenarioId = 1;

        public static async Task<bool> SendToast(string title, string msg)
        {
            await AppNotificationManager.Default.RemoveByGroupAsync(title);
            var appNotification = new AppNotificationBuilder()
                .AddText($"本地预警-{title}")
                .AddText(msg)
                .SetGroup(title)
                .BuildNotification();
            AppNotificationManager.Default.Show(appNotification);
            return appNotification.Id != 0;
        }

        public static void NotificationReceived(AppNotificationActivatedEventArgs notificationActivatedEventArgs)
        {
        }
    }
}
