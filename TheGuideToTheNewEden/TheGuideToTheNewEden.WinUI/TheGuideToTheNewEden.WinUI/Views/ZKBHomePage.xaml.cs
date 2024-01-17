using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.WinUI.Extensions;
using TheGuideToTheNewEden.WinUI.Views.KB;
using Windows.Foundation;
using Windows.Foundation.Collections;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class ZKBHomePage : Page, IPage
    {
        public ZKBHomePage()
        {
            this.InitializeComponent();
            Loaded += ZKBHomePage_Loaded;
        }

        private async void ZKBHomePage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= ZKBHomePage_Loaded;
            this.GetBaseWindow()?.ShowWaiting(Helpers.ResourcesHelper.GetString("ZKBHomePage_ConnectingToWSS"));
            await VM.InitAsync();
            this.GetBaseWindow()?.HideWaiting();
        }

        private void KBListControl_OnItemClicked(Core.Models.KB.KBItemInfo itemInfo)
        {
            KBDetailPage detailPage = new KBDetailPage(itemInfo);
            string name = itemInfo.Victim == null ? itemInfo.SKBDetail.KillmailId.ToString() : itemInfo.Victim.Name;
            TabViewItem item = new TabViewItem()
            {
                Header = $"KB - {name}",
                IsSelected = true,
                Content = detailPage
            };
            ContentTabView.TabItems.Add(item);
        }

        private void ContentTabView_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            sender.TabItems.Remove(args.Item);
        }

        public void Close()
        {
            VM?.Dispose();
        }

        private async void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (_searchItem?.Name == sender.Text)
            {
                return;
            }
            if (string.IsNullOrEmpty(sender.Text))
            {
                sender.ItemsSource = null;
            }
            else
            {
                string searchName = sender.Text;
                sender.ItemsSource =  await Task.Run(() =>
                {
                    List<IdName> result = new List<IdName>();
                    var names = Core.Services.IDNameService.SerachByName(searchName);
                    var typesSearch = Core.Services.DB.InvTypeService.Search(searchName);
                    List<Core.DBModels.InvType> ships = null;
                    if(typesSearch.NotNullOrEmpty())
                    {
                        var types = Core.Services.DB.InvTypeService.QueryTypes(typesSearch.Select(p=>p.ID).ToList());
                        var typeGroups = Core.Services.DB.InvGroupService.QueryGroupIdOfCategory(new List<int>()
                        {
                            3,6,22,23,65,87,
                        });
                        ships = types.Where(p => typeGroups.Contains(p.GroupID)).ToList();
                    }
                    var groups = Core.Services.DB.InvGroupService.QueryGroups(searchName);
                    var systems = Core.Services.DB.MapSolarSystemService.Search(searchName);
                    var regions = Core.Services.DB.MapRegionService.Search(searchName);
                    if(ships.NotNullOrEmpty())
                    {
                        foreach(var ship in ships)
                        {
                            result.Add(new IdName()
                            {
                                Id = ship.TypeID,
                                Name = ship.TypeName,
                                Category = (int)IdName.CategoryEnum.InventoryType
                            });
                        }
                    }
                    if (groups.NotNullOrEmpty())
                    {
                        foreach (var group in groups)
                        {
                            result.Add(new IdName()
                            {
                                Id = group.GroupID,
                                Name = group.GroupName,
                                Category = (int)IdName.CategoryEnum.Group
                            });
                        }
                    }
                    if (systems.NotNullOrEmpty())
                    {
                        foreach (var system in systems)
                        {
                            result.Add(new IdName()
                            {
                                Id = system.ID,
                                Name = system.Name,
                                Category = (int)IdName.CategoryEnum.SolarSystem
                            });
                        }
                    }
                    if (regions.NotNullOrEmpty())
                    {
                        foreach (var region in regions)
                        {
                            result.Add(new IdName()
                            {
                                Id = region.ID,
                                Name = region.Name,
                                Category = (int)IdName.CategoryEnum.Region
                            });
                        }
                    }
                    if (names.NotNullOrEmpty())
                    {
                        var hashSet = result.Select(p => p.Id).ToHashSet2();
                        foreach (var name in names)
                        {
                            if(!hashSet.Contains(name.Id))
                            {
                                result.Add(name);
                            }
                        }
                    }
                    return result;
                });
            }
        }
        private IdName _searchItem;
        private void SearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            _searchItem = args.SelectedItem as IdName;
            ShowDetail(_searchItem);
        }
        private async void ShowDetail(IdName idName)
        {
            ZKB.NET.EntityType entityType;
            switch(idName.GetCategory())
            {
                case IdName.CategoryEnum.Character:entityType = ZKB.NET.EntityType.CharacterID; break;
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
            try
            {
                var statist = await ZKB.NET.ZKB.GetStatisticAsync(entityType, idName.Id);
            }
            catch (Exception e)
            {
                this.GetBaseWindow()?.ShowError(e.Message);
                Core.Log.Error(e);
            }
        }
    }
}
