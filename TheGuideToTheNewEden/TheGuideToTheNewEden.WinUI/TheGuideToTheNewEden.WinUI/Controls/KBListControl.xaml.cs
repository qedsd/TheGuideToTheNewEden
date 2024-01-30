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
using System.Text;
using System.Windows.Input;
using System.Xml.Linq;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.KB;
using TheGuideToTheNewEden.WinUI.Converters;
using TheGuideToTheNewEden.WinUI.Services;
using TheGuideToTheNewEden.WinUI.Views.KB;
using Windows.Foundation;
using Windows.Foundation.Collections;
using ZKB.NET.Models.KillStream;
using static TheGuideToTheNewEden.Core.Events.IdNameEvent;

namespace TheGuideToTheNewEden.WinUI.Controls
{
    public sealed partial class KBListControl : UserControl
    {
        private KBNavigationService _kbNavigationService;
        public KBListControl()
        {
            this.InitializeComponent();
        }
        public void SetKBNavigationService(KBNavigationService kbNavigationService)
        {
            _kbNavigationService = kbNavigationService;
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
            var info = (sender as FrameworkElement).DataContext as KBItemInfo;
            if(info != null)
            {
                Helpers.UrlHelper.OpenInBrower($"https://zkillboard.com/kill/{info.SKBDetail.KillmailId}/");
            }
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
                _kbNavigationService?.NavigateToKM(item);
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

        private IdNameClickedEventHandel _idNameClicked;
        public event IdNameClickedEventHandel IdNameClicked
        {
            add
            {
                _idNameClicked += value;
            }
            remove
            {
                _idNameClicked -= value;
            }
        }

        public static readonly DependencyProperty IdNameClickedCommandProperty
          = DependencyProperty.Register(
              nameof(IdNameClickedCommand),
              typeof(ICommand),
              typeof(KBListControl),
              new PropertyMetadata(default(ICommand)));

        public ICommand IdNameClickedCommand
        {
            get => (ICommand)GetValue(IdNameClickedCommandProperty);
            set => SetValue(IdNameClickedCommandProperty, value);
        }

        private async void Invoke(IdName idName)
        {
            _idNameClicked?.Invoke(idName);
            IdNameClickedCommand?.Execute(idName);
            if (_kbNavigationService != null)
            {
                BaseWindow window = Helpers.WindowHelper.GetWindowForElement(this) as BaseWindow;
                window?.ShowWaiting();
                await _kbNavigationService.NavigationTo(idName);
                window?.HideWaiting();
            }
        }

        private void KBSystemInfoControl_OnSystemClicked(IdName idName)
        {
            Invoke(idName);
        }

        private void KBSystemInfoControl_OnRegionClicked(IdName idName)
        {
            Invoke(idName);
        }
       
        private void KBListCharacterControl_CharacterClicked(IdName idName)
        {
            Invoke(idName);
        }

        private void KBListCharacterControl_FactionClicked(IdName idName)
        {
            Invoke(idName);
        }

        private void Button_ShipType_Clicked(object sender, RoutedEventArgs e)
        {
            var info = (sender as FrameworkElement).DataContext as KBItemInfo;
            if (info != null)
            {
                Invoke(new IdName()
                {
                    Id = info.Type.TypeID,
                    Name = info.Type.TypeName,
                    Category = (int)IdName.CategoryEnum.InventoryType
                });
            }
        }

        private void Button_Group_Clicked(object sender, RoutedEventArgs e)
        {
            var info = (sender as FrameworkElement).DataContext as KBItemInfo;
            if (info != null)
            {
                Invoke(new IdName()
                {
                    Id = info.Group.GroupID,
                    Name = info.Group.GroupName,
                    Category = (int)IdName.CategoryEnum.Group
                });
            }
        }
    }
}
