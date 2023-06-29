using ESI.NET;
using ESI.NET.Models.SSO;
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
using Windows.Foundation;
using Windows.Foundation.Collections;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.WinUI.Views.Business
{
    public sealed partial class CharacterOrderPage : Page
    {
        private BaseWindow Window;
        public CharacterOrderPage()
        {
            this.InitializeComponent();
            Loaded += CharacterOrderPage_Loaded;
        }

        private void CharacterOrderPage_Loaded(object sender, RoutedEventArgs e)
        {
            Window = Helpers.WindowHelper.GetWindowForElement(this) as BaseWindow;
        }

        private void SelecteCharacterControl_OnSelectedItemChanged(ESI.NET.Models.SSO.AuthorizedCharacterData selectedItem)
        {
            GetOrders();
        }

        private void Button_Refresh_Click(object sender, RoutedEventArgs e)
        {
            GetOrders();
        }
        private async void GetOrders()
        {
            Window?.ShowWaiting();
            await GetCharacterOrders();
            await GetCorpOrders();
            Window?.HideWaiting();
        }
        private async Task GetCharacterOrders()
        {
            if(SelecteCharacterControl.SelectedItem != null)
            {
                DataGrid_Character.ItemsSource = await Services.MarketOrderService.Current.GetCharacterOrdersAsync(SelecteCharacterControl.SelectedItem.CharacterID);
            }
            else
            {
                Window?.ShowError("未选择角色");
            }
        }
        private async Task GetCorpOrders()
        {
            if (SelecteCharacterControl.SelectedItem != null)
            {
                DataGrid_Corp.ItemsSource = await Services.MarketOrderService.Current.GetCorpOrdersAsync(SelecteCharacterControl.SelectedItem.CharacterID);
            }
            else
            {
                Window?.ShowError("未选择角色");
            }
        }
        private void Button_AddToFilterList_Click(object sender, RoutedEventArgs e)
        {
            List<Core.Models.Market.Order> orders = new List<Core.Models.Market.Order>();
            if(DataGrid_Character.SelectedItems.NotNullOrEmpty())
            {
                orders.AddRange(DataGrid_Character.SelectedItems.Select(p=> p as Core.Models.Market.Order));
            }
            if (DataGrid_Corp.SelectedItems.NotNullOrEmpty())
            {
                orders.AddRange(DataGrid_Corp.SelectedItems.Select(p => p as Core.Models.Market.Order));
            }
            SelectedItemsChanged?.Invoke(orders);
        }

        public delegate void SelectedItemsChangedEventHandel(List<Core.Models.Market.Order> orders);
        private SelectedItemsChangedEventHandel SelectedItemsChanged;
        public event SelectedItemsChangedEventHandel OnSelectedItemsChanged
        {
            add
            {
                SelectedItemsChanged += value;
            }
            remove
            {
                SelectedItemsChanged -= value;
            }
        }
    }
}
