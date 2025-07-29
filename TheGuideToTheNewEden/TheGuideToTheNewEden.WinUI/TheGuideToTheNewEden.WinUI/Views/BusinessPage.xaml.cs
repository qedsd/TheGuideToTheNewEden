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
        public BusinessPage()
        {
            this.InitializeComponent();
        }

        private void ScalperPage_OnAddShoppingItem(List<Core.Models.Market.ScalperShoppingItem> items)
        {
            ShoppingCartPage.AddItems(items);
            ClientServiceHelper.GetRequiredService<Services.PageNavigationService>().ShowMsg("Navigation.BusinessPage",$"已添加{items.Count}个物品到购物车", Controls.InfoBarControl.InfoType.Success, true);
        }

        private void ShoppingRecordPage_OnAddShoppingItems(IEnumerable<Core.Models.Market.ScalperShoppingItem> items)
        {
            ShoppingCartPage.AddItems(items.ToList());
            ClientServiceHelper.GetRequiredService<Services.PageNavigationService>().ShowMsg("Navigation.BusinessPage",$"已添加 {items.Count()}个物品到购物车", Controls.InfoBarControl.InfoType.Success, true);
        }

        private void CharacterOrderPage_OnAddToFilterListItemsChanged(List<Core.Models.Market.Order> orders)
        {
            if (orders.NotNullOrEmpty())
            {
                var groups = orders.GroupBy(p => p.TypeId).ToList();
                var types = groups.Select(p => p.First().InvType).ToList();
                ScalperPage.AddToFilter(types);
                ClientServiceHelper.GetRequiredService<Services.PageNavigationService>().ShowMsg("Navigation.BusinessPage","已添加{orders.GroupBy(p => p.TypeId).Count()}个物品到过滤列表", Controls.InfoBarControl.InfoType.Success,true);
            }
        }

        private void CharacterOrderPage_OnAddToUpdatedScalperShoppingItemsChanged(List<Core.Models.Market.Order> orders)
        {
            if (orders.NotNullOrEmpty())
            {
                ShoppingCartPage.UpdateItems(orders);
            }
        }
    }
}
