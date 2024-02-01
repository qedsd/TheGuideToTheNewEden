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
using Syncfusion.UI.Xaml.DataGrid;
using TheGuideToTheNewEden.Core.Models.Market;
using TheGuideToTheNewEden.WinUI.Dialogs;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.WinUI.Views.Business
{
    public sealed partial class ScalperPage : Page
    {
        public ScalperPage()
        {
            this.InitializeComponent();
            Loaded += ScalperPage_Loaded;
        }

        private void ScalperPage_Loaded(object sender, RoutedEventArgs e)
        {
            VM.Window = Helpers.WindowHelper.GetWindowForElement(this) as BaseWindow;
        }

        private async void MenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            if(DataGrid.SelectedItems.Count == 1)
            {
                var item = DataGrid.SelectedItems[0] as ScalperItem;
                if (item != null)
                {
                    ScalperShoppingItem scalperShoppingItem = new ScalperShoppingItem(item);
                    if (await AddToShoppingCartDialog.ShowAsync(scalperShoppingItem, this.XamlRoot))
                    {
                        AddShoppingItem?.Invoke(scalperShoppingItem);
                    }
                }
            }
            else if(DataGrid.SelectedItems.Count > 1)
            {
                List<ScalperShoppingItem> items = new List<ScalperShoppingItem>();
                foreach( var item in DataGrid.SelectedItems)
                {
                    items.Add(new ScalperShoppingItem(item as ScalperItem));
                }

            }
            
        }

        private void SfDataGrid_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            VM.SelectedScalperItem = (sender as SfDataGrid).SelectedItem as ScalperItem;
        }

        public delegate void AddShoppingItemEventHandel(ScalperShoppingItem item);
        private AddShoppingItemEventHandel AddShoppingItem;
        public event AddShoppingItemEventHandel OnAddShoppingItem
        {
            add
            {
                AddShoppingItem += value;
            }
            remove
            {
                AddShoppingItem -= value;
            }
        }


        public void AddToFilter(List<Core.DBModels.InvType> types)
        {
            VM.AddFilterTypes(types);
        }

        private void Button_RemoveSelectedFilterTypes_Click(object sender, RoutedEventArgs e)
        {
            if(ListView_FilterTypes.SelectedItems.Any())
            {
                var types = ListView_FilterTypes.SelectedItems.Select(p=> p as Core.DBModels.InvType).ToList();
                VM.RemoveFilterTypes(types);
            }
        }

        private void SfComboBox_SelectionChanged(object sender, Syncfusion.UI.Xaml.Editors.ComboBoxSelectionChangedEventArgs e)
        {
            if(e.AddedItems.NotNullOrEmpty())
            {
                foreach(var item in e.AddedItems)
                {
                    VM.SelectedInvMarketGroups.Add(item as Core.DBModels.InvMarketGroup);
                }
            }
            if (e.RemovedItems.NotNullOrEmpty())
            {
                foreach (var item in e.RemovedItems)
                {
                    VM.SelectedInvMarketGroups.Remove(item as Core.DBModels.InvMarketGroup);
                }
            }
        }
    }
}
