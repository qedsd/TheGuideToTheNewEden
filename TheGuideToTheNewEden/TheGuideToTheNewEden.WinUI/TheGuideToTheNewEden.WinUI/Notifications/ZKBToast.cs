using Microsoft.Windows.AppNotifications.Builder;
using Microsoft.Windows.AppNotifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.WinUI.Services;
using Newtonsoft.Json;
using TheGuideToTheNewEden.Core.Helpers;
using NetTaste;
using WinUIEx;

namespace TheGuideToTheNewEden.WinUI.Notifications
{
    internal class ZKBToast
    {
        public const int ScenarioId = 5;
        public static bool SendToast(Core.Models.KB.KBItemInfo kBItemInfo)
        {
            string faction = kBItemInfo.VictimAllianceName == null ? kBItemInfo.VictimCorporationIdName?.Name : kBItemInfo.VictimAllianceName.Name;
            var appNotification = new AppNotificationBuilder()
                .AddArgument("action", "ToastClick")
                .AddArgument(Common.scenarioTag, ScenarioId.ToString())
                .AddArgument("id", kBItemInfo.SKBDetail.KillmailId.ToString())
                .AddText($"{kBItemInfo.Type?.TypeName} {Converters.ISKNormalizeConverter.Normalize(kBItemInfo.SKBDetail.Zkb.TotalValue)}")
                .AddText(kBItemInfo.Victim?.Name)
                .AddText(faction)
                //.SetAppLogoOverride(new Uri(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"logo_32.png")))
                .BuildNotification();

            AppNotificationManager.Default.Show(appNotification);
            return appNotification.Id != 0;
        }
        public static async void NotificationReceived(AppNotificationActivatedEventArgs notificationActivatedEventArgs)
        {
            if (notificationActivatedEventArgs.Arguments.TryGetValue("id", out string idStr))
            {
                var info = await KBHelpers.CreateKBItemInfoAsync(int.Parse(idStr));
                Services.KBNavigationService.Default.NavigateToKM(info);
                Helpers.WindowHelper.MainWindow.SetForegroundWindow();
            }
        }
    }
}
