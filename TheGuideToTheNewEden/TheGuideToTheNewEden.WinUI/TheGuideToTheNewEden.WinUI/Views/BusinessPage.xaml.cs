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
using TheGuideToTheNewEden.WinUI.Views.Business;
using Windows.Foundation;
using Windows.Foundation.Collections;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class BusinessPage : Page
    {
        private BaseWindow Window;
        public BusinessPage()
        {
            this.InitializeComponent();
            Loaded += BusinessPage_Loaded;
        }

        private void BusinessPage_Loaded(object sender, RoutedEventArgs e)
        {
            Window = Helpers.WindowHelper.GetWindowForElement(this) as BaseWindow;
            VM.Window = Window;
        }

        private void CharacterOrderPage_OnSelectedItemsChanged(List<Core.Models.Market.Order> orders)
        {
            if(orders.NotNullOrEmpty())
            {
                var groups = orders.GroupBy(p => p.TypeId).ToList();
                var types = groups.Select(p => p.First().InvType).ToList();
                ScalperPage.AddToFilter(types);
                Window?.ShowSuccess($"已添加{orders.GroupBy(p => p.TypeId).Count()}个物品到过滤列表");
            }
        }

        private void ScalperPage_OnAddShoppingItem(Core.Models.Market.ScalperShoppingItem item)
        {
            ShoppingCartPage.AddItem(item);
            Window?.ShowSuccess($"已添加 {item.InvType.TypeName} 到购物车");
        }

        private void ShoppingRecordPage_OnAddShoppingItems(IEnumerable<Core.Models.Market.ScalperShoppingItem> items)
        {
            foreach(var item in items)
            {
                ShoppingCartPage.AddItem(item);
            }
            Window?.ShowSuccess($"已添加 {items.Count()}个物品到购物车");
        }
    }
}
