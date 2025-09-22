using DevWinUI;
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
using TheGuideToTheNewEden.Core.Models.Market;
using TheGuideToTheNewEden.WinUI.Services;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Views.Business
{
    public sealed partial class ShoppingRecordPage : Page
    {
        public ShoppingRecordPage()
        {
            this.InitializeComponent();
            ItemsList.ItemsSource = Services.ShoppingRecordService.Current.Files;
        }

        private void MenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            Services.ShoppingRecordService.Current.Remove((sender as MenuFlyoutItem).DataContext as string);
        }

        private void ItemsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var file = (sender as ListView).SelectedItem as string;
            if(!string.IsNullOrEmpty(file))
            {
                ItemsDataGrid.ItemsSource = Services.ShoppingRecordService.Current.Load(file);
            }
        }

        private void Button_Add_Click(object sender, RoutedEventArgs e)
        {
            if(ItemsDataGrid.SelectedItems != null && ItemsDataGrid.SelectedItems.Any())
            {
                AddShoppingItems?.Invoke(ItemsDataGrid.SelectedItems.Select(p=>p as ScalperShoppingItem));
            }
        }
        public delegate void AddShoppingItemEventHandel(IEnumerable<ScalperShoppingItem> items);
        private AddShoppingItemEventHandel AddShoppingItems;
        public event AddShoppingItemEventHandel OnAddShoppingItems
        {
            add
            {
                AddShoppingItems += value;
            }
            remove
            {
                AddShoppingItems -= value;
            }
        }

        private async void DeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            if (ItemsList.SelectedItems.Count == 0) return;
            DevWinUI.WindowedContentDialog dialog = new()
            {
                Content = string.Format(Helpers.ResourcesHelper.GetString("BusinessPage_ShoppingRecord_DeleteCount"), ItemsList.SelectedItems.Count),
                Title = Helpers.ResourcesHelper.GetString("General_RemoveSelected"),
                PrimaryButtonText = Helpers.ResourcesHelper.GetString("General_OK"),
                CloseButtonText = Helpers.ResourcesHelper.GetString("General_Cancel"),
                IsSecondaryButtonEnabled = false,
                OwnerWindow = Helpers.WindowHelper.MainWindow,
            };
            if (await dialog.ShowAsync(true) == ContentDialogResult.Primary)
            {
                foreach (var item in ItemsList.SelectedItems)
                {
                    Services.ShoppingRecordService.Current.Remove((item as string));
                }
                ClientServiceHelper.GetRequiredService<PageNavigationService>().ShowMsg(this, Helpers.ResourcesHelper.GetString("General_RemoveSuccess"), Controls.InfoBarControl.InfoType.Success, true);
            }
        }
    }
}
