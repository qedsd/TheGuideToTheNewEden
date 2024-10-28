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
        //private Dictionary<int, MapSolarSystem> _systemDict;
        public List<MapSolarSystem> MapSolarSystems { get; set; }
        public MapSystemSelectorControl()
        {
            this.InitializeComponent();
            Init();
        }
        private async void Init()
        {
            MapSolarSystems = (await Core.Services.DB.MapSolarSystemService.QueryAllAsync()).OrderBy(p => p.SolarSystemID).ToList();
            //_systemDict = MapSolarSystems.ToDictionary(p => p.SolarSystemID);
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
            SelectedItem = null;
            if (string.IsNullOrEmpty(sender.Text))
            {
                sender.ItemsSource = null;
            }
            else
            {
                sender.ItemsSource = MapSolarSystems.Where(p => p.SolarSystemName.Contains(sender.Text.ToUpper())).ToList();
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
            //(d as MapSystemSelectorControl).SelectedId = (e.NewValue as MapSolarSystem).SolarSystemID;
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

        //public static readonly DependencyProperty SelectedIdProperty
        //    = DependencyProperty.Register(
        //        nameof(SelectedItem),
        //        typeof(int),
        //        typeof(MapSystemSelectorControl),
        //        new PropertyMetadata(null, new PropertyChangedCallback(SelectedIdPropertyChanged)));

        //public int SelectedId
        //{
        //    get => (int)GetValue(SelectedIdProperty);
        //    set => SetValue(SelectedIdProperty, value);
        //}
        //private static void SelectedIdPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        //{
        //    (d as MapSystemSelectorControl).SetSelectedId((int)e.NewValue);
        //}

        //internal void SetSelectedId(int id)
        //{
        //    if(_systemDict.TryGetValue(id, out var v))
        //    {
        //        SelectedItem = v;
        //    }
        //}
    }
}
