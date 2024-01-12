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
using System.Windows.Input;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.KB;
using TheGuideToTheNewEden.WinUI.Converters;
using TheGuideToTheNewEden.WinUI.Views.KB;
using Windows.Foundation;
using Windows.Foundation.Collections;
using ZKB.NET.Models.KillStream;

namespace TheGuideToTheNewEden.WinUI.Controls
{
    public sealed partial class KBListControl : UserControl
    {
        public KBListControl()
        {
            this.InitializeComponent();
        }
        public static readonly DependencyProperty KBsProperty
           = DependencyProperty.Register(
               nameof(KBs),
               typeof(IEnumerable<KBItemInfo>),
               typeof(KBSystemInfoControl),
               new PropertyMetadata(null, new PropertyChangedCallback(KBsPropertyChanged)));

        public IEnumerable<KBItemInfo> KBs
        {
            get => (IEnumerable<KBItemInfo>)GetValue(KBsProperty);
            set
            {
                SetValue(KBsProperty, value);
                DataGrid.ItemsSource = value;
            }
        }
        private static void KBsPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {

        }

        private void MenuFlyoutItem_Browser_Click(object sender, RoutedEventArgs e)
        {
            
        }

        private void KBListCharacterControl_Loaded(object sender, RoutedEventArgs e)
        {
            (sender as KBListCharacterControl).KBItemInfo = (sender as KBListCharacterControl).DataContext as KBItemInfo;
        }

        private void DataGrid_SelectionChanged(object sender, Syncfusion.UI.Xaml.Grids.GridSelectionChangedEventArgs e)
        {
            if(DataGrid.SelectedItem != null)
            {
                var item = DataGrid.SelectedItem as KBItemInfo;
                if(item.SKBDetail.Victim == null)
                {
                    ShowWaiting();
                    //TODO:加载详情
                    HideWaiting();
                }
                ItemClicked?.Invoke(item);
                ItemClickedCommand?.Execute(item);
            }
            DataGrid.SelectedItem = null;
        }
        private void ShowWaiting()
        {
            WaitingProgressRing.IsActive = true;
            DataGrid.IsEnabled = false;
        }
        private void HideWaiting()
        {
            WaitingProgressRing.IsActive = false;
            DataGrid.IsEnabled = true;
        }

        #region kb点击事件
        public delegate void ItemClickedEventHandle(KBItemInfo itemInfo);
        private ItemClickedEventHandle ItemClicked;

        public static readonly DependencyProperty ItemClickedCommandProperty
           = DependencyProperty.Register(
               nameof(ItemClickedCommand),
               typeof(ICommand),
               typeof(KBListControl),
               new PropertyMetadata(default(ICommand)));

        public ICommand ItemClickedCommand
        {
            get => (ICommand)GetValue(ItemClickedCommandProperty);
            set => SetValue(ItemClickedCommandProperty, value);
        }

        public event ItemClickedEventHandle OnItemClicked
        {
            add
            {
                ItemClicked += value;
            }
            remove
            {
                ItemClicked -= value;
            }
        }
        #endregion
    }
}
