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
            Init();
        }

        private async void Init()
        {
            MapRegions = (await Core.Services.DB.MapRegionService.QueryAllAsync()).OrderBy(p=>p.RegionID).ToList();
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
            SelectedItem = null;
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
            (d as SelecteRegionControl).SelectedItemChanged?.Invoke(e.NewValue as MapRegion);
        }

        public static readonly DependencyProperty SelectedIdProperty
            = DependencyProperty.Register(
                nameof(SelectedId),
                typeof(MapRegion),
                typeof(SelecteRegionControl),
                new PropertyMetadata(0, new PropertyChangedCallback(SelectedIdPropertyChanged)));

        public int SelectedId
        {
            get => (int)GetValue(SelectedIdProperty);
            set => SetValue(SelectedIdProperty, value);
        }
        private static void SelectedIdPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as SelecteRegionControl).SelectedItem = (d as SelecteRegionControl).MapRegions.FirstOrDefault(p=>p.RegionID == (int)e.NewValue);
        }

        public static readonly DependencyProperty ItemSourceProperty
            = DependencyProperty.Register(
                nameof(ItemSource),
                typeof(List<MapRegion>),
                typeof(SelecteRegionControl),
                new PropertyMetadata(null, new PropertyChangedCallback(ItemSourcePropertyChanged)));

        public List<MapRegion> ItemSource
        {
            get => (List<MapRegion>)GetValue(ItemSourceProperty);
            set => SetValue(ItemSourceProperty, value);
        }
        private static void ItemSourcePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            
        }


        public static readonly DependencyProperty ShowListProperty
            = DependencyProperty.Register(
                nameof(ShowList),
                typeof(MapRegion),
                typeof(SelecteRegionControl),
                new PropertyMetadata(true, new PropertyChangedCallback(ShowListPropertyChanged)));

        public bool ShowList
        {
            get => (bool)GetValue(ShowListProperty);
            set => SetValue(ShowListProperty, value);
        }
        private static void ShowListPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as SelecteRegionControl).ListView_Regions.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public delegate void SelectedItemChangedEventHandel(MapRegion selectedItem);
        private SelectedItemChangedEventHandel SelectedItemChanged;
        public event SelectedItemChangedEventHandel OnSelectedItemChanged
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
