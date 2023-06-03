using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TheGuideToTheNewEden.Core.DBModels;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Controls
{
    public sealed partial class SelecteRegionControl : UserControl
    {
        public List<MapRegion> MapRegions { get; set; }
        public SelecteRegionControl()
        {
            this.InitializeComponent();
        }
        private async void Init()
        {
            MapRegions = (await Core.Services.DB.MapRegionService.QueryAllAsync()).OrderBy(p=>p.RegionName).ToList();
            ListView_Regions.ItemsSource = MapRegions;
        }

        private void ListView_Regions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedItem = ListView_Regions.SelectedItem as MapRegion;
        }

        private void AutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            SelectedItem = args.SelectedItem as MapRegion;
            Search_AutoSuggestBox.Text = SelectedItem.RegionName;
        }

        private void AutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (SelectedItem?.RegionName == sender.Text)
            {
                return;
            }
            if (string.IsNullOrEmpty(sender.Text))
            {
                sender.ItemsSource = null;
            }
            else
            {
                sender.ItemsSource = MapRegions.Where(p => p.RegionName.Contains(sender.Text)).ToList();
            }
        }

        public static readonly DependencyProperty SelectedItemProperty
            = DependencyProperty.Register(
                nameof(SelectedItem),
                typeof(MapRegion),
                typeof(SelecteRegionControl),
                new PropertyMetadata(null, new PropertyChangedCallback(SelectedItemPropertyChanged)));

        public MapRegion SelectedItem
        {
            get => (MapRegion)GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }
        private static void SelectedItemPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as SelecteRegionControl).OnSelectedItemChanged?.Invoke(e.NewValue as MapRegion);
        }

        public delegate void SelectedItemChangedEventHandel(MapRegion selectedItem);
        private SelectedItemChangedEventHandel OnSelectedItemChanged;
    }
}
