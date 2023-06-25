using ESI.NET.Models.Opportunities;
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
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models.Market;
using Windows.Foundation;
using Windows.Foundation.Collections;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.WinUI.Dialogs
{
    public sealed partial class AddToShoppingCartDialog : Page
    {
        private ScalperShoppingItem VM;
        private AddToShoppingCartDialog(ScalperShoppingItem scalperShoppingItem)
        {
            this.InitializeComponent();
            VM = scalperShoppingItem;
            //NumberBox_BuyPrice.Value = scalperShoppingItem.BuyPrice;
            //NumberBox_SellPrice.Value = scalperShoppingItem.SellPrice;
            //NumberBox_Quantity.Value = scalperShoppingItem.Volume;
            //TextBlock_ROI.Text = scalperShoppingItem.Volume;
            //NumberBox_Quantity.Value = scalperShoppingItem.Volume;
        }
        public static async Task<bool> ShowAsync(ScalperItem item)
        {
            return await ShowAsync(new ScalperShoppingItem(item));
        }
        public static async Task<bool> ShowAsync(ScalperShoppingItem scalperShoppingItem)
        {
            var copy = scalperShoppingItem.DepthClone<ScalperShoppingItem>();
            ContentDialog contentDialog = new ContentDialog()
            {
                Title = copy.InvType.TypeName,
                Content = new AddToShoppingCartDialog(copy),
                PrimaryButtonText = Helpers.ResourcesHelper.GetString("General_OK"),
                CloseButtonText = Helpers.ResourcesHelper.GetString("General_Cancel"),
            };
            if(await contentDialog.ShowAsync() == ContentDialogResult.Primary)
            {
                scalperShoppingItem.CopyFrom(copy);
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
