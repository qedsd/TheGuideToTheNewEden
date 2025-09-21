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
            ClientServiceHelper.GetRequiredService<Services.PageNavigationService>().ShowMsg(this, string.Format(Helpers.ResourcesHelper.GetString("BusinessPage_AddedShoppingItem"), items.Count()), Controls.InfoBarControl.InfoType.Success, true);
        }

        private void ShoppingRecordPage_OnAddShoppingItems(IEnumerable<Core.Models.Market.ScalperShoppingItem> items)
        {
            ShoppingCartPage.AddItems(items.ToList());
            ClientServiceHelper.GetRequiredService<Services.PageNavigationService>().ShowMsg(this, string.Format(Helpers.ResourcesHelper.GetString("BusinessPage_AddedShoppingItem"), items.Count()), Controls.InfoBarControl.InfoType.Success, true);
        }
    }
}
