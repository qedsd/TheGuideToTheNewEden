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
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Models.Market;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using static TheGuideToTheNewEden.WinUI.Dialogs.CopyToGameOrderDialog;

namespace TheGuideToTheNewEden.WinUI.Dialogs
{
    public sealed partial class CopyToGameOrderDialog : Page
    {
        public class CopyItem
        {
            public IEnumerable<ScalperShoppingItem> Items { get; set; }
            public string Content {  get; set; }
        }
        private CopyToGameOrderDialog(IEnumerable<ScalperShoppingItem> items)
        {
            this.InitializeComponent();
            List<CopyItem> copyItems = new List<CopyItem>();
            int groupCount = 100;
            for(int i = 0;i< items.Count();i += groupCount)
            {
                IEnumerable<ScalperShoppingItem> item = items.Skip(i).Take(groupCount).ToList();
                copyItems.Add(new CopyItem()
                {
                    Items = item,
                    Content = $"{i}-{i + item.Count()}"
                });
            }
            ListBox.ItemsSource = copyItems;
        }
        public static async Task ShowAsync(IEnumerable<ScalperShoppingItem> items, XamlRoot xamlRoot)
        {
            ContentDialog contentDialog = new ContentDialog()
            {
                XamlRoot = xamlRoot,
                Title = Helpers.ResourcesHelper.GetString("BusinessPage_CopyToGameOrder"),
                Content = new CopyToGameOrderDialog(items),
                PrimaryButtonText = Helpers.ResourcesHelper.GetString("BusinessPage_CopyToGameOrder_CopyAll"),
                CloseButtonText = Helpers.ResourcesHelper.GetString("General_Cancel"),
            };
            if(await contentDialog.ShowAsync() == ContentDialogResult.Primary)
            {
                Copy(items);
            }
        }
        private static void Copy(IEnumerable<ScalperShoppingItem> items)
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (var item in items)
            {
                stringBuilder.Append(item.InvType.TypeName);
                stringBuilder.Append(" ");
                stringBuilder.Append(item.Quantity);
                stringBuilder.AppendLine();
            }
            DataPackage dataPackage = new DataPackage();
            dataPackage.SetText(stringBuilder.ToString());
            Clipboard.SetContent(dataPackage);
        }

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var list = sender as ListBox;
            var copyItem = list.SelectedItem as CopyItem;
            if (copyItem != null)
            {
                Copy(copyItem.Items);
            }
        }
    }
}
