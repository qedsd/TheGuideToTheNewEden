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
using TheGuideToTheNewEden.Core.Models.Market;

namespace TheGuideToTheNewEden.WinUI.Controls
{
    public sealed partial class MarketLocationSelectorControl : UserControl
    {
        public MarketLocationSelectorControl()
        {
            this.InitializeComponent();
        }

        private void SelecteRegionControl_OnSelectedItemChanged(Core.DBModels.MapRegion selectedItem)
        {
            SelectedItem = new MarketLocation(selectedItem);
        }

        private void MapSystemSelectorControl_OnSelectedItemChanged(Core.DBModels.MapSolarSystem selectedItem)
        {
            SelectedItem = new MarketLocation(selectedItem);
        }

        private void StructureSelectorControl_OnSelectedItemChanged(Core.Models.Universe.Structure selectedItem)
        {
            SelectedItem = new MarketLocation(selectedItem);
        }

        public static readonly DependencyProperty SelectedItemProperty
            = DependencyProperty.Register(
                nameof(SelectedItem),
                typeof(MarketLocation),
                typeof(MarketLocationSelectorControl),
                new PropertyMetadata(null, new PropertyChangedCallback(SelectedItemPropertyChanged)));

        public MarketLocation SelectedItem
        {
            get => (MarketLocation)GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }
        private static void SelectedItemPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as MarketLocationSelectorControl).SelectedItemChanged?.Invoke(e.NewValue as MarketLocation);
        }

        public delegate void SelectedItemChangedEventHandel(MarketLocation selectedItem);
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
