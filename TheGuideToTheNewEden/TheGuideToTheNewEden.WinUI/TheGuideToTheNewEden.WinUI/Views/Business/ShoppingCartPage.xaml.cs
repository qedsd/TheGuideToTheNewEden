using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TheGuideToTheNewEden.Core.Models.Market;
using TheGuideToTheNewEden.WinUI.Dialogs;
using Windows.Foundation;
using Windows.Foundation.Collections;
using TheGuideToTheNewEden.Core.Extensions;
using ESI.NET.Models.Universe;
using Windows.ApplicationModel.DataTransfer;
using System.Text;
using TheGuideToTheNewEden.WinUI.Services;

namespace TheGuideToTheNewEden.WinUI.Views.Business
{

    public sealed partial class ShoppingCartPage : Page
    {
        private BaseWindow Window;
        private ObservableCollection<ScalperShoppingItem> ShoppingItems = new ObservableCollection<ScalperShoppingItem>();
        public ShoppingCartPage()
        {
            this.InitializeComponent();
            ItemsDataGrid.ItemsSource = ShoppingItems;
            Loaded += ShoppingCartPage_Loaded;
        }

        private void ShoppingCartPage_Loaded(object sender, RoutedEventArgs e)
        {
            Window = Helpers.WindowHelper.GetWindowForElement(this) as BaseWindow;
        }

        private void AppBarButton_CopyToGameOrder_Click(object sender, RoutedEventArgs e)
        {
            if(ShoppingItems.Any())
            {
                StringBuilder stringBuilder = new StringBuilder();
                foreach(var item in ShoppingItems)
                {
                    stringBuilder.Append(item.InvType.TypeName);
                    stringBuilder.Append(" ");
                    stringBuilder.Append(item.Quantity);
                    stringBuilder.AppendLine();
                }
                DataPackage dataPackage = new DataPackage();
                dataPackage.SetText(stringBuilder.ToString());
                Clipboard.SetContent(dataPackage);
                Window?.ShowSuccess("已复制列表，在游戏批量购买界面粘贴即可");
            }
        }

        private void AppBarButton_SaveOrder_Click(object sender, RoutedEventArgs e)
        {
            if(ShoppingItems.Any())
            {
                ShoppingRecordService.Current.Add(ShoppingItems);
                Window?.ShowSuccess("已保存");
            }
        }

        private void MenuFlyoutItem_Edit_Click(object sender, RoutedEventArgs e)
        {
            var item = ((sender as MenuFlyoutItem).CommandParameter as Syncfusion.UI.Xaml.DataGrid.GridRecordContextFlyoutInfo)?.Record as ScalperShoppingItem;
            if (item != null)
                Edit(item);
        }

        private void MenuFlyoutItem_Remove_Click(object sender, RoutedEventArgs e)
        {
            var item = ((sender as MenuFlyoutItem).CommandParameter as Syncfusion.UI.Xaml.DataGrid.GridRecordContextFlyoutInfo)?.Record as ScalperShoppingItem;
            if(item != null)
            {
                ShoppingItems.Remove(item);
                Cal();
            }
        }

        private void SfDataGrid_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            Edit(ItemsDataGrid.SelectedItem as ScalperShoppingItem);
        }
        private async void Edit(ScalperShoppingItem item)
        {
            ScalperShoppingItem tempItem = item.DepthClone<ScalperShoppingItem>();
            if (await AddToShoppingCartDialog.ShowAsync(tempItem, this.XamlRoot))
            {
                item.CopyFrom(tempItem);
                Cal();
            }
        }
        private void Cal()
        {
            if(ShoppingItems.Any())
            {
                TextBlock_ROI.Text = ShoppingItems.Average(p => p.ROI).ToString("N2");
                TextBlock_NetProfi.Text = ShoppingItems.Sum(p => p.NetProfit).ToString("N2");
                TextBlock_Principal.Text = ShoppingItems.Sum(p => p.BuyPrice * p.Quantity).ToString("N2");
                TextBlock_Volume.Text = ShoppingItems.Sum(p => p.Volume).ToString("N2");
            }
            else
            {
                TextBlock_ROI.Text = string.Empty;
                TextBlock_NetProfi.Text = string.Empty;
                TextBlock_Principal.Text = string.Empty;
                TextBlock_Volume.Text = string.Empty;
            }
        }

        public void AddItem(ScalperShoppingItem item)
        {
            ShoppingItems.Add(item);
            Cal();
        }
    }
}
