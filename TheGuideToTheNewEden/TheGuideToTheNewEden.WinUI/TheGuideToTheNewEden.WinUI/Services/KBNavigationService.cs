using Azure;
using ESI.NET.Models.PlanetaryInteraction;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
        private Page _homePage;
        private Frame _contentFrame;
        private Dictionary<long, object> _instances = new Dictionary<long, object>();
        public void Init(Frame contentFrame)
        {
            _contentFrame = contentFrame;
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
            ClientServiceHelper.GetRequiredService<PageNavigationService>().NavigateToZKB();
            var statistic = await GetEntityStatisticAsync(id, entityType);
            if (statistic != null)
            {
                Helpers.WindowHelper.MainWindow.DispatcherQueue.SafelyTryEnqueue(() =>
                {
                    object content = null;
                    if (!_instances.TryGetValue(id, out content))
                    {
                        content = new EntityStatistPage(statistic, this);//KBNavigationService懒得改成IOC了，就这样传吧
                        _instances.Add(id, content);
                    }
                    _contentFrame.Content = content;
                    PageChanged?.Invoke(id, header);
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
            Helpers.WindowHelper.MainWindow.DispatcherQueue.SafelyTryEnqueue(() =>
            {
                long id = info.SKBDetail.KillmailId;
                object content = null;
                if (!_instances.TryGetValue(id, out content))
                {
                    content = new KBDetailPage(info);
                    _instances.Add(id, content);
                }
                string name = info.Victim == null ? id.ToString() : info.Victim.Name;
                _contentFrame.Content = content;
                PageChanged?.Invoke(id, $"KB-{name}");
            });
        }
        public void NavigateToInstance(long id)
        {
            Helpers.WindowHelper.MainWindow.DispatcherQueue.SafelyTryEnqueue(() =>
            {
                if (_instances.TryGetValue(id, out var content))
                {
                    _contentFrame.Content = content;
                }
            });
        }

        public void NavigateToHome()
        {
            if(_homePage == null)
            {
                _homePage = new KillStreamPage();
            }
            _contentFrame.Content = _homePage;
        }

        public void RemoveInstance(long id)
        {
            _instances.Remove(id);
        }

        public delegate void KBPageDelegate(long id, string name);
        public event KBPageDelegate PageChanged;
    }
}
