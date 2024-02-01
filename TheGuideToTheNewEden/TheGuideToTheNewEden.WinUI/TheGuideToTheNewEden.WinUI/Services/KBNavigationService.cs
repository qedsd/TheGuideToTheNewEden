using Microsoft.UI.Xaml.Controls;
using Octokit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Models.KB;
using TheGuideToTheNewEden.WinUI.Extensions;
using TheGuideToTheNewEden.WinUI.Views;
using TheGuideToTheNewEden.WinUI.Views.KB;
using ZKB.NET.Models.Statistics;

namespace TheGuideToTheNewEden.WinUI.Services
{
    public class KBNavigationService
    {
        private static KBNavigationService _default;
        public static KBNavigationService Default
        {
            get
            {
                _default ??= new KBNavigationService();
                return _default;
            }
        }
        private readonly Action<string, Microsoft.UI.Xaml.Controls.Page> _addTabAction;
        private readonly BaseWindow _window;
        public KBNavigationService(ZKBHomePage page)
        {
            _addTabAction = new Action<string, Microsoft.UI.Xaml.Controls.Page>((h,p) =>
            {
                page.AddTab(h, p);
            });
            _window = page.GetBaseWindow();
        }
        public KBNavigationService()
        {
            _addTabAction = new Action<string, Microsoft.UI.Xaml.Controls.Page>((h, p) =>
            {
                Services.NavigationService.NavigateTo(p,h);
            });
            _window = Helpers.WindowHelper.MainWindow as BaseWindow;
        }

        public static async Task<EntityStatistic> GetEntityStatisticAsync(int id, ZKB.NET.EntityType entityType)
        {
            return await ZKB.NET.ZKB.GetStatisticAsync(entityType, id);
        }

        public static async Task<EntityStatistic> GetEntityStatisticAsync(IdName idName)
        {
            ZKB.NET.EntityType entityType;
            switch (idName.GetCategory())
            {
                case IdName.CategoryEnum.Character: entityType = ZKB.NET.EntityType.CharacterID; break;
                case IdName.CategoryEnum.Corporation: entityType = ZKB.NET.EntityType.CorporationID; break;
                case IdName.CategoryEnum.Alliance: entityType = ZKB.NET.EntityType.AllianceID; break;
                case IdName.CategoryEnum.Faction: entityType = ZKB.NET.EntityType.FactionID; break;
                case IdName.CategoryEnum.InventoryType: entityType = ZKB.NET.EntityType.ShipTypeID; break;
                case IdName.CategoryEnum.Group: entityType = ZKB.NET.EntityType.GroupID; break;
                case IdName.CategoryEnum.SolarSystem: entityType = ZKB.NET.EntityType.SolarSystemID; break;
                case IdName.CategoryEnum.Region: entityType = ZKB.NET.EntityType.RegionID; break;
                default:
                    {
                        Core.Log.Error($"Unknown category:{idName.GetCategory()}");
                        return null;
                    }
            }
            return await ZKB.NET.ZKB.GetStatisticAsync(entityType, idName.Id);
        }

        public async Task NavigationTo(int id, ZKB.NET.EntityType entityType, string header)
        {
            var statistic = await GetEntityStatisticAsync(id, entityType);
            if (statistic != null)
            {
                EntityStatistPage page = new EntityStatistPage(statistic,this);
                _window.DispatcherQueue.TryEnqueue(() =>
                {
                    _addTabAction(header, page);
                });
            }
        }
        public async Task NavigationTo(IdName idName)
        {
            ZKB.NET.EntityType entityType;
            switch (idName.GetCategory())
            {
                case IdName.CategoryEnum.Character: entityType = ZKB.NET.EntityType.CharacterID; break;
                case IdName.CategoryEnum.Corporation: entityType = ZKB.NET.EntityType.CorporationID; break;
                case IdName.CategoryEnum.Alliance: entityType = ZKB.NET.EntityType.AllianceID; break;
                case IdName.CategoryEnum.Faction: entityType = ZKB.NET.EntityType.FactionID; break;
                case IdName.CategoryEnum.InventoryType: entityType = ZKB.NET.EntityType.ShipTypeID; break;
                case IdName.CategoryEnum.Group: entityType = ZKB.NET.EntityType.GroupID; break;
                case IdName.CategoryEnum.SolarSystem: entityType = ZKB.NET.EntityType.SolarSystemID; break;
                case IdName.CategoryEnum.Region: entityType = ZKB.NET.EntityType.RegionID; break;
                default:
                    {
                        Core.Log.Error($"Unknown category:{idName.GetCategory()}");
                        return;
                    }
            }
            await NavigationTo(idName.Id, entityType, idName.Name);
        }

        public void NavigateToKM(Core.Models.KB.KBItemInfo info)
        {
            _window.DispatcherQueue.TryEnqueue(() =>
            {
                KBDetailPage detailPage = new KBDetailPage(info, this);
                string name = info.Victim == null ? info.SKBDetail.KillmailId.ToString() : info.Victim.Name;
                _addTabAction($"KB - {name}", detailPage);
            });
        }

    }
}
