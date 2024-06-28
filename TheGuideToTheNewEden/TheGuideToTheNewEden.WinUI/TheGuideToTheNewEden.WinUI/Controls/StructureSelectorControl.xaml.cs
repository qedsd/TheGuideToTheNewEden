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
using System.Collections.ObjectModel;
using TheGuideToTheNewEden.Core.Models.Universe;
using TheGuideToTheNewEden.Core.DBModels;
using Syncfusion.UI.Xaml.Data;

namespace TheGuideToTheNewEden.WinUI.Controls
{
    public sealed partial class StructureSelectorControl : UserControl
    {
        private ObservableCollection<Structure> Structures { get; set; }
        public StructureSelectorControl()
        {
            this.InitializeComponent();
            Init();
        }
        private void Init()
        {
            Structures = Services.StructureService.GetMarketStrutures();
            if(Structures.Any())
            {
                ListView_List.Visibility = Visibility.Visible;
                TextBlock_EmptyTip.Visibility = Visibility.Collapsed;
            }
            else
            {
                ListView_List.Visibility = Visibility.Collapsed;
                TextBlock_EmptyTip.Visibility = Visibility.Visible;
            }
            Structures.CollectionChanged += Structures_CollectionChanged;
            ListView_List.ItemsSource = Structures;
        }

        private void Structures_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if(Structures.Any())
            {
                ListView_List.Visibility = Visibility.Visible;
                TextBlock_EmptyTip.Visibility = Visibility.Collapsed;
            }
            else
            {
                ListView_List.Visibility = Visibility.Collapsed;
                TextBlock_EmptyTip.Visibility = Visibility.Visible;
            }
        }

        private void AutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            SelectedItem = args.SelectedItem as Structure;
            sender.Text = SelectedItem.RegionName;
            
        }

        private void AutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (SelectedItem?.Name == sender.Text)
            {
                return;
            }
            if (string.IsNullOrEmpty(sender.Text))
            {
                sender.ItemsSource = null;
            }
            else
            {
                sender.ItemsSource = Structures.Where(p => p.Name.Contains(sender.Text)).ToList();
            }
        }

        private void ListView_List_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedItem = ListView_List.SelectedItem as Structure;
        }

        public static readonly DependencyProperty SelectedItemProperty
            = DependencyProperty.Register(
                nameof(SelectedItem),
                typeof(Structure),
                typeof(StructureSelectorControl),
                new PropertyMetadata(null, new PropertyChangedCallback(SelectedItemPropertyChanged)));

        public Structure SelectedItem
        {
            get => (Structure)GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }
        private static void SelectedItemPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as StructureSelectorControl).SelectedItemChanged?.Invoke(e.NewValue as Structure);
        }

        public delegate void SelectedItemChangedEventHandel(Structure selectedItem);
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
