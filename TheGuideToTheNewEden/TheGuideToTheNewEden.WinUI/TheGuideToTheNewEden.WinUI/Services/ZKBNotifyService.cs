using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Models.KB;
using TheGuideToTheNewEden.WinUI.Notifications;

namespace TheGuideToTheNewEden.WinUI.Services
{
    internal static class ZKBNotifyService
    {
        public static void TryNotify(Core.Models.KB.KBItemInfo kBItemInfo)
        {
            try
            {
                var setting = Settings.ZKBSettingService.Setting;
                if (kBItemInfo != null && setting.Notify)
                {
                    if (kBItemInfo.SKBDetail.Zkb.TotalValue >= setting.MinNotifyValue)
                    {
                        bool match;
                        match = Match(setting.Types, kBItemInfo.SKBDetail.Victim.ShipTypeId);
                        match &= Match(setting.Systems, kBItemInfo.SKBDetail.SolarSystemId);
                        match &= Match(setting.Regions, kBItemInfo.Region?.RegionID);
                        match &= Match(setting.Characters, kBItemInfo.SKBDetail.Victim.CharacterId);
                        match &= Match(setting.Corps, kBItemInfo.SKBDetail.Victim.CorporationId);
                        match &= Match(setting.Alliances, kBItemInfo.SKBDetail.Victim.AllianceId);
                        if (match)
                        {
                            ZKBToast.SendToast(kBItemInfo);
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                Core.Log.Error(ex);
            }
        }
        private static bool Match(HashSet<int> ids, int? targetId)
        {
            if (ids.NotNullOrEmpty() && targetId != null && (int)targetId > 0)
            {
                return ids.Contains((int)targetId);
            }
            else
            {
                return true;
            }
        }
    }
}
