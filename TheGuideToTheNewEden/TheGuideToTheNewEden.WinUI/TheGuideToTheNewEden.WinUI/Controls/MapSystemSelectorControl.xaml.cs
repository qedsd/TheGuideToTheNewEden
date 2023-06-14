using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.WinUI.Controls
{
    public sealed partial class MapSystemSelectorControl : UserControl
    {
        public List<MapSolarSystem> MapSolarSystems { get; set; }
        public MapSystemSelectorControl()
        {
            this.InitializeComponent();
            Init();
        }
        private async void Init()
        {
            MapSolarSystems = (await Core.Services.DB.MapSolarSystemService.QueryAllAsync()).OrderBy(p => p.SolarSystemID).ToList();
            ListView_Systems.ItemsSource = MapSolarSystems;
        }

        private void ListView_Systems_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedItem = ListView_Systems.SelectedItem as MapSolarSystem;
        }

        private void AutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            SelectedItem = args.SelectedItem as MapSolarSystem;
            Search_AutoSuggestBox.Text = SelectedItem.SolarSystemName;
        }

        private void AutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (SelectedItem?.SolarSystemName == sender.Text)
            {
                return;
            }
            if (string.IsNullOrEmpty(sender.Text))
            {
                sender.ItemsSource = null;
            }
            else
            {
                sender.ItemsSource = MapSolarSystems.Where(p => p.SolarSystemName.Contains(sender.Text)).ToList();
            }
        }

        public static readonly DependencyProperty SelectedItemProperty
            = DependencyProperty.Register(
                nameof(SelectedItem),
                typeof(MapSolarSystem),
                typeof(MapSystemSelectorControl),
                new PropertyMetadata(null, new PropertyChangedCallback(SelectedItemPropertyChanged)));

        public MapSolarSystem SelectedItem
        {
            get => (MapSolarSystem)GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }
        private static void SelectedItemPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as MapSystemSelectorControl).SelectedItemChanged?.Invoke(e.NewValue as MapSolarSystem);
        }
        public delegate void SelectedItemChangedEventHandel(MapSolarSystem selectedItem);
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
