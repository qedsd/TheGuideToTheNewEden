using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using TheGuideToTheNewEden.Core.DBModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using TheGuideToTheNewEden.Core.Extensions;
using EVEStandard;
using ESI.NET.Models.SSO;


namespace TheGuideToTheNewEden.WinUI.Controls
{
    public sealed partial class IdNameSearchBox : UserControl
    {
        public IdNameSearchBox()
        {
            this.InitializeComponent();
        }
        private IdName _searchItem;
        private CancellationTokenSource _cancellationTokenSource;
        private List<Core.DBModels.IdName.CategoryEnum> _filterTypes;
        private void SearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            _searchItem = args.SelectedItem as IdName;
            SelectedItemChanged?.Invoke(this, _searchItem);
        }
        private List<IdName> SerachDB(string searchName, CancellationToken cancellationToken)
        {
            List<IdName> result = new List<IdName>();
            var names = Core.Services.IDNameService.SerachByName(searchName);
            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }
            var typesSearch = IsTargetType(IdName.CategoryEnum.InventoryType) ? Core.Services.DB.InvTypeService.Search(searchName) : null;
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
            var groups = IsTargetType(IdName.CategoryEnum.Group) ? Core.Services.DB.InvGroupService.Search(searchName) : null;
            var systems = IsTargetType(IdName.CategoryEnum.SolarSystem) ? Core.Services.DB.MapSolarSystemService.Search(searchName) : null;
            var regions = IsTargetType(IdName.CategoryEnum.Region) ? Core.Services.DB.MapRegionService.Search(searchName) : null;
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
                var hashSet = result.Select(p => p.Id).ToHashSet2();
                foreach (var name in names)
                {
                    if (!hashSet.Contains(name.Id) && IsTargetType(name.GetCategory()))
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
                var items = await Task.Run(() =>
                {
                    return SerachDB(searchName, cancellationTokenSource.Token);
                });
                if (!cancellationTokenSource.IsCancellationRequested)
                {
                    sender.ItemsSource = items;
                }
            }
        }
        private bool IsTargetType(IdName.CategoryEnum category)
        {
            if (_filterTypes.NotNullOrEmpty())
            {
                return _filterTypes.Contains(category);
            }
            else
            {
                return true;
            }
        }
        private EVEStandardAPI _esi;
        private async void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if(args.ChosenSuggestion != null)
            {
                return;
            }
            _esi ??= Core.Services.ESIService.GetDefaultESI2();
            ProgressBar.Visibility = Visibility.Visible;
            SearchBox.IsEnabled = false;
            try
            {
                List<IdName> idNames = new List<IdName>();
                var result = await _esi.Universe.BulkNamesToIdsV1Async(new List<string>() { sender.Text });
                if(result?.Model != null)
                {
                    if(result.Model.Alliances.NotNullOrEmpty() && IsTargetType(IdName.CategoryEnum.Alliance))
                    {
                        foreach (var item in result.Model.Alliances)
                        {   
                            idNames.Add(new IdName((int)item.Id, item.Name, IdName.CategoryEnum.Alliance));
                        }
                    }
                    if (result.Model.Characters.NotNullOrEmpty() && IsTargetType(IdName.CategoryEnum.Character))
                    {
                        foreach (var item in result.Model.Characters)
                        {
                            idNames.Add(new IdName((int)item.Id, item.Name, IdName.CategoryEnum.Character));
                        }
                    }
                    if (result.Model.Corporations.NotNullOrEmpty() && IsTargetType(IdName.CategoryEnum.Corporation))
                    {
                        foreach (var item in result.Model.Corporations)
                        {
                            idNames.Add(new IdName((int)item.Id, item.Name, IdName.CategoryEnum.Corporation));
                        }
                    }
                }
                SearchBox.ItemsSource = idNames;
                SearchBox.IsSuggestionListOpen = true;
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
            }
            finally
            {
                ProgressBar.Visibility = Visibility.Collapsed;
                SearchBox.IsEnabled = true;
            }
        }

        public void SetFilterTypes(List<Core.DBModels.IdName.CategoryEnum> filterTypes)
        {
            _filterTypes = filterTypes;
        }


        public static readonly DependencyProperty SelectedItemProperty
            = DependencyProperty.Register(
                nameof(SelectedItem),
                typeof(IdName),
                typeof(IdNameSearchBox),
                new PropertyMetadata(null, null));

        public IdName SelectedItem
        {
            get => (IdName)GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }
        private EventHandler<IdName> SelectedItemChanged;
        public event EventHandler<IdName> OnSelectedItemChanged
        {
            add
            {
                SelectedItemChanged += value;
            }
            remove
            {
                SelectedItemChanged -= value;
            }
        }
    }
}
