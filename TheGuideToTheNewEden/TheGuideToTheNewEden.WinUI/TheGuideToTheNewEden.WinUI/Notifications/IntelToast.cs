using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System;
using TheGuideToTheNewEden.Core.Models;

namespace TheGuideToTheNewEden.WinUI.Notifications
{
    internal class IntelToast
    {
        public const int ScenarioId = 1;

        public static bool SendToast(EarlyWarningContent earlyWarningContent)
        {
            var appNotification = new AppNotificationBuilder()
                .AddArgument("action", "ToastClick")
                .AddArgument(Common.scenarioTag, ScenarioId.ToString())
                .SetAppLogoOverride(new System.Uri("file://" + App.GetFullPathToAsset("Square150x150Logo.png")), AppNotificationImageCrop.Circle)
                .AddText($"频道预警：{earlyWarningContent.SolarSystemName} {earlyWarningContent.Jumps}跳")
                .AddText(earlyWarningContent.Content)
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
