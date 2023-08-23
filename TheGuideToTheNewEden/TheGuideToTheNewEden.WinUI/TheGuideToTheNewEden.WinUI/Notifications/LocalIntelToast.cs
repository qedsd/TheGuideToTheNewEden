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
        public const int ScenarioId = 1;

        public static bool SendToast(string title, string msg)
        {
            var appNotification = new AppNotificationBuilder()
                .AddArgument("action", "ToastClick")
                .AddArgument(Common.scenarioTag, ScenarioId.ToString())
                .SetAppLogoOverride(new System.Uri("file://" + App.GetFullPathToAsset("Square150x150Logo.png")), AppNotificationImageCrop.Circle)
                .AddText($"本地预警-{title}")
                .AddText(msg)
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
