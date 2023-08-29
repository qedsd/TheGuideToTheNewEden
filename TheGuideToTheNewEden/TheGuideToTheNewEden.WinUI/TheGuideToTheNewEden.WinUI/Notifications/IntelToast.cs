using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System;
using TheGuideToTheNewEden.Core.Models;

namespace TheGuideToTheNewEden.WinUI.Notifications
{
    internal class IntelToast
    {
        public static bool SendToast(string chanel, EarlyWarningContent earlyWarningContent)
        {
            var appNotification = new AppNotificationBuilder()
                .AddText($"频道预警：{earlyWarningContent.SolarSystemName} {earlyWarningContent.Jumps}跳")
                .AddText($"{chanel}: {earlyWarningContent.Content}")
                .BuildNotification();
            appNotification.Expiration = DateTime.Now.AddMinutes(1);
            AppNotificationManager.Default.Show(appNotification);

            return appNotification.Id != 0;
        }

        public static void NotificationReceived(AppNotificationActivatedEventArgs notificationActivatedEventArgs)
        {
        }
    }
}
