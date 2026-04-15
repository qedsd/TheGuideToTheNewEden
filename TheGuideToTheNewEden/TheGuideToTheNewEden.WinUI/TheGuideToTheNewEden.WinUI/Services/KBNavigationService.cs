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
        private TabView _tabView;
        private TabViewItem _homeTabViewItem;
        private Dictionary<long, TabViewItem> _instances = new Dictionary<long, TabViewItem>();
        public void Init(TabView tabView)
        {
            _tabView = tabView;
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

        public async Task NavigationTo(int id, ZKB.NET.EntityType entityType, string header, bool newWindow = false)
        {
            if (!newWindow)
            {
                ClientServiceHelper.GetRequiredService<PageNavigationService>().NavigateToZKB();
            }
            var statistic = await GetEntityStatisticAsync(id, entityType);
            if (statistic != null)
            {
                Helpers.WindowHelper.MainWindow.DispatcherQueue.SafelyTryEnqueue(() =>
                {
                    if (_tabView != null && !newWindow)
                    {
                        TabViewItem content = null;
                        if (!_instances.TryGetValue(id, out content))
                        {
                            content = new TabViewItem()
                            {
                                Content = new EntityStatistPage(statistic, this),//KBNavigationService懒得改成IOC了，就这样传吧
                                Header = header,
                                IsClosable = true,
                                IsSelected = true,
                                Tag = (long)id,
                            };
                            _instances.Add(id, content);
                            _tabView.TabItems.Add(content);
                        }
                        PageChanged?.Invoke(id, header);
                    }
                    else
                    {
                        ToolWindow toolWindow = new ToolWindow(header, header,new EntityStatistPage(statistic, this), WindowTitleStyle.Default, true, true, true, true, false, 1000, 800);
                        toolWindow.Activate();
                    }
                });
            }
        }
        public async Task NavigationTo(IdName idName, bool newWindow = false)
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
            await NavigationTo(idName.Id, entityType, idName.Name,newWindow);
        }

        public void NavigateToKM(Core.Models.KB.KBItemInfo info, bool newWindow = false)
        {
            Helpers.WindowHelper.MainWindow.DispatcherQueue.SafelyTryEnqueue(() =>
            {
                long id = info.SKBDetail.KillmailId;
                string name = info.Victim == null ? id.ToString() : info.Victim.Name;
                string header = $"KB-{name}";
                if (_tabView == null || newWindow)
                {
                    ToolWindow toolWindow = new ToolWindow(header, header, new KBDetailPage(info), WindowTitleStyle.Default, true, true, true, true, false, 1000, 800);
                    toolWindow.Activate();
                }
                else
                {
                    TabViewItem content = null;
                    if (!_instances.TryGetValue(id, out content))
                    {
                        content = new TabViewItem()
                        {
                            Header = header,
                            Content = new KBDetailPage(info),
                            IsClosable = true,
                            IsSelected = true,
                            Tag = id,
                        };
                        _instances.Add(id, content);
                        _tabView.TabItems.Add(content);
                    }
                    PageChanged?.Invoke(id, $"KB-{name}");
                }
            });
        }
        public void NavigateToInstance(long id)
        {
            Helpers.WindowHelper.MainWindow.DispatcherQueue.SafelyTryEnqueue(() =>
            {
                if (_instances.TryGetValue(id, out var content))
                {
                    content.IsSelected = true;
                }
            });
        }

        public void NavigateToHome()
        {
            if(_homeTabViewItem == null)
            {
                _homeTabViewItem = new TabViewItem()
                {
                    Header = Helpers.ResourcesHelper.GetString("ZKBHomePage_KillStream"),
                    Content = new KillStreamPage(),
                    IsSelected = true,
                    IsClosable = false,
                    Tag = -1L
                };
                _tabView.TabItems.Add(_homeTabViewItem);
            }
            _homeTabViewItem.IsSelected = true;
        }

        public void RemoveInstance(long id)
        {
            if(id == -1)//Home
            {
                (_homeTabViewItem.Content as KillStreamPage).Close();
                _homeTabViewItem.Content = null;
                _homeTabViewItem.Content = new KillStreamPage(false);
            }
            if (_instances.TryGetValue(id, out var content))
            {
                _tabView.TabItems.Remove(content);
                _instances.Remove(id);
            }
        }
        public void Reset()
        {
            foreach (var instance in _instances)
            {
                _tabView.TabItems?.Remove(instance);
            }
            _instances.Clear();
            _homeTabViewItem = null;
            _tabView = null;
        }

        public delegate void KBPageDelegate(long id, string name);
        public event KBPageDelegate PageChanged;
    }
}
