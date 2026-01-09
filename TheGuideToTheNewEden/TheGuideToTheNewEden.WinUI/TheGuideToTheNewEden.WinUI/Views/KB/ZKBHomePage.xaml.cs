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
using System.Xml.Linq;
using TheGuideToTheNewEden.WinUI.Services;
using System.Threading;
using TheGuideToTheNewEden.Core.Models.Universe;

namespace TheGuideToTheNewEden.WinUI.Views.KB
{
    public sealed partial class ZKBHomePage : Page, IPage
    {
        public ZKBHomePage()
        {
            this.InitializeComponent();
            
            ClientServiceHelper.GetRequiredService<KBNavigationService>().Init(KBTabView);
            ClientServiceHelper.GetRequiredService<KBNavigationService>().NavigateToHome();
        }

        public void Close()
        {
            ClientServiceHelper.GetRequiredService<KBNavigationService>().Reset();
        }

        private CancellationTokenSource _cancellationTokenSource;
        private async void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            _cancellationTokenSource?.Cancel();
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            _cancellationTokenSource = cancellationTokenSource;
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
                var items =  await Task.Run(() =>
                {
                    return Serach(searchName, cancellationTokenSource.Token);
                });
                if(!cancellationTokenSource.IsCancellationRequested)
                {
                    sender.ItemsSource = items;
                    Debug.WriteLine($"{sender.Text} : {items.Count}");
                }
                else
                {
                    Debug.WriteLine("ÒÑÈ¡Ïû");
                }
            }
        }
        private List<IdName> Serach(string searchName, CancellationToken cancellationToken)
        {
            List<IdName> result = new List<IdName>();
            var names = Core.Services.IDNameService.SerachByName(searchName);
            if(cancellationToken.IsCancellationRequested)
            {
                return null;
            }
            var typesSearch = Core.Services.DB.InvTypeService.Search(searchName);
            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }
            List<Core.DBModels.InvType> ships = null;
            if (typesSearch.NotNullOrEmpty())
            {
                var types = Core.Services.DB.InvTypeService.QueryTypes(typesSearch.Select(p => p.ID).ToList());
                var typeGroups = Core.Services.DB.InvGroupService.QueryGroupIdOfCategory(new List<int>()
                        {
                            3,6,22,23,65,87,
                        });
                ships = types.Where(p => typeGroups.Contains(p.GroupID)).ToList();
            }
            var groups = Core.Services.DB.InvGroupService.Search(searchName);
            var systems = Core.Services.DB.MapSolarSystemService.Search(searchName);
            var regions = Core.Services.DB.MapRegionService.Search(searchName);
            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }
            if (ships.NotNullOrEmpty())
            {
                foreach (var ship in ships)
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
                        Id = group.ID,
                        Name = group.Name,
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
                var hashSet = result.Select(p=>p.Id).ToHashSet2();
                foreach (var name in names)
                {
                    if (!hashSet.Contains(name.Id))
                    {
                        hashSet.Add(name.Id);
                        result.Add(name);
                    }
                }
            }
            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }
            return result;
        }
        private IdName _searchItem;
        private void SearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            _searchItem = args.SelectedItem as IdName;
            ShowDetail(_searchItem);
        }
        private async void ShowDetail(IdName idName)
        {
            try
            {
                this.ShowWaiting();
                await ClientServiceHelper.GetRequiredService<KBNavigationService>().NavigationTo(idName);
                this.HideWaiting();
            }
            catch (Exception e)
            {
                this.ShowMsg(e.Message, Controls.InfoBarControl.InfoType.Error, false);
                Core.Log.Error(e);
            }
        }

        private void KBTabView_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            var item = args.Item as TabViewItem;
            if (item != null)
            {
                ClientServiceHelper.GetRequiredService<KBNavigationService>().RemoveInstance((long)item.Tag);
                if (item.Content.GetType() == typeof(KillStreamPage))
                {
                    ClientServiceHelper.GetRequiredService<PageNavigationService>().ShowMsg(this, Helpers.ResourcesHelper.GetString("ZKBHomePage_Disconnect"), Controls.InfoBarControl.InfoType.Success, true);
                }
            }
        }

        public void NavigatedTo(object parameter)
        {

        }
    }
}
