using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;


namespace TheGuideToTheNewEden.WinUI.Notifications
{
    internal class ToastWithAvatar
    {
        public const int ScenarioId = 1;

        public static bool SendToast(string Title = "新伊甸漫游指南",string content = "Toast通知")
        {
            //AddArgument为用户点击通知后返回的参数标识
            var appNotification = new AppNotificationBuilder()
                .AddArgument("action", "ToastClick")
                .AddArgument(Common.scenarioTag, ScenarioId.ToString())
                .SetAppLogoOverride(new System.Uri("file://" + App.GetFullPathToAsset("Square150x150Logo.png")), AppNotificationImageCrop.Circle)
                .AddText(Title)
                .AddText(content)
                .BuildNotification();

            AppNotificationManager.Default.Show(appNotification);

            return appNotification.Id != 0; // return true (indicating success) if the toast was sent (if it has an Id)
        }

        public static void NotificationReceived(AppNotificationActivatedEventArgs notificationActivatedEventArgs)
        {
            //var notification = new MainPage.Notification();
            //notification.Originator = ScenarioName;
            //notification.Action = notificationActivatedEventArgs.Arguments["action"];
            //MainPage.Current.NotificationReceived(notification);
            //App.ToForeground();
        }
    }
}
