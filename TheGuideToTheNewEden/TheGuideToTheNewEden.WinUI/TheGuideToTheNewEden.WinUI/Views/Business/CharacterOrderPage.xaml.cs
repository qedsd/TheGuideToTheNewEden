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
using TheGuideToTheNewEden.Core.Models.Market;
using ESI.NET.Models.Market;
using TheGuideToTheNewEden.Core.Services;
using ESI.NET.Models.Character;

namespace TheGuideToTheNewEden.WinUI.Views.Business
{
    public sealed partial class CharacterOrderPage : Page
    {
        private EsiClient _esiClient;
        private BaseWindow Window;
        public CharacterOrderPage()
        {
            this.InitializeComponent();
            Loaded += CharacterOrderPage_Loaded;
            _esiClient = ESIService.GetDefaultEsi();
        }

        private void CharacterOrderPage_Loaded(object sender, RoutedEventArgs e)
        {
            Window = Helpers.WindowHelper.GetWindowForElement(this) as BaseWindow;
        }

        private void SelecteCharacterControl_OnSelectedItemChanged(ESI.NET.Models.SSO.AuthorizedCharacterData selectedItem)
        {
            _esiClient.SetCharacterData(selectedItem);
            GetOrders();
        }

        private void Button_Refresh_Click(object sender, RoutedEventArgs e)
        {
            GetOrders();
        }
        private async void GetOrders()
        {
            Window?.ShowWaiting();
            try
            {
                await GetCharacterOrders();
                await GetCorpOrders();
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
                Window?.ShowError(ex.Message);
            }
            Window?.HideWaiting();
        }
        private async Task GetCharacterOrders()
        {
            if(SelecteCharacterControl.SelectedItem != null)
            {
                //var orders = await SimuOrders();
                var orders = await Services.MarketOrderService.Current.GetCharacterOrdersAsync(SelecteCharacterControl.SelectedItem.CharacterID);
                if (orders.NotNullOrEmpty())
                {
                    var os = await CalOrderStatus(orders);
                    DataGrid_Character.ItemsSource = os;
                }
                else
                {
                    DataGrid_Character.ItemsSource = null;
                }
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
                var orders = await Services.MarketOrderService.Current.GetCorpOrdersAsync(SelecteCharacterControl.SelectedItem.CharacterID);
                if (orders.NotNullOrEmpty())
                {
                    var os = await CalOrderStatus(orders);
                    DataGrid_Corp.ItemsSource = os;
                }
                else
                {
                    DataGrid_Corp.ItemsSource = null;
                }
            }
            else
            {
                Window?.ShowError("未选择角色");
            }
        }

        private async Task<List<StatusOrder>> CalOrderStatus(List<Core.Models.Market.Order> orders)
        {
            List<StatusOrder> statusOrders = new List<StatusOrder>();
            foreach(var order in orders)
            {
                var results = await Services.MarketOrderService.Current.GetRegionOrdersAsync(order.TypeId, order.RegionId);
                List<Core.Models.Market.Order> refs = null;
                if(results.NotNullOrEmpty())
                {
                    if(order.IsBuyOrder)
                    {
                        refs = results.Where(p=>p.IsBuyOrder)?.OrderByDescending(p=>p.Price).ToList();
                    }
                    else
                    {
                        refs = results.Where(p => !p.IsBuyOrder)?.OrderBy(p => p.Price).ToList();
                    }
                }
                statusOrders.Add(new StatusOrder(order, refs));
            }
            return statusOrders;
        }

        private async Task<List<Core.Models.Market.Order>> SimuOrders()
        {
            var o1 = (await Services.MarketOrderService.Current.GetRegionOrdersAsync(34, 10000002)).Where(p=>!p.IsBuyOrder).OrderBy(p=>p.Price).ToList()[0];
            var o2 = (await Services.MarketOrderService.Current.GetRegionOrdersAsync(28710, 10000002)).Where(p => !p.IsBuyOrder).OrderBy(p => p.Price).ToList()[1];
            var o3 = (await Services.MarketOrderService.Current.GetRegionOrdersAsync(34828, 10000002)).Where(p => !p.IsBuyOrder).OrderBy(p => p.Price).ToList()[2];

            var o11 = (await Services.MarketOrderService.Current.GetRegionOrdersAsync(34, 10000002)).Where(p => p.IsBuyOrder).OrderByDescending(p => p.Price).ToList()[0];
            var o22 = (await Services.MarketOrderService.Current.GetRegionOrdersAsync(28710, 10000002)).Where(p => p.IsBuyOrder).OrderByDescending(p => p.Price).ToList()[1];
            var o33 = (await Services.MarketOrderService.Current.GetRegionOrdersAsync(34828, 10000002)).Where(p => p.IsBuyOrder).OrderByDescending(p => p.Price).ToList()[2];

            return new List<Core.Models.Market.Order>()
            {
                o1,o2,o3,o11,o22,o33
            };
        }

        public delegate void SelectedItemsChangedEventHandel(List<Core.Models.Market.Order> orders);
        private SelectedItemsChangedEventHandel AddToFilterListItemsChanged;
        public event SelectedItemsChangedEventHandel OnAddToFilterListItemsChanged
        {
            add
            {
                AddToFilterListItemsChanged += value;
            }
            remove
            {
                AddToFilterListItemsChanged -= value;
            }
        }

        private SelectedItemsChangedEventHandel AddToUpdatedScalperShoppingItemsChanged;
        public event SelectedItemsChangedEventHandel OnAddToUpdatedScalperShoppingItemsChanged
        {
            add
            {
                AddToUpdatedScalperShoppingItemsChanged += value;
            }
            remove
            {
                AddToUpdatedScalperShoppingItemsChanged -= value;
            }
        }

        private void MenuFlyoutItem1_AddToFilterList_Click(object sender, RoutedEventArgs e)
        {
            AddToFilterListItemsChanged?.Invoke(DataGrid_Character.SelectedItems.Select(p => (p as Core.Models.Market.StatusOrder).Target)?.ToList());
        }

        private void MenuFlyoutItem1_AddToUpdatedScalperShoppingItem_Click(object sender, RoutedEventArgs e)
        {
            AddToUpdatedScalperShoppingItemsChanged?.Invoke(DataGrid_Character.SelectedItems.Select(p => (p as Core.Models.Market.StatusOrder).Target)?.ToList());
        }
        private void MenuFlyoutItem2_AddToFilterList_Click(object sender, RoutedEventArgs e)
        {
            AddToFilterListItemsChanged?.Invoke(DataGrid_Corp.SelectedItems.Select(p => (p as Core.Models.Market.StatusOrder).Target)?.ToList());
        }

        private void MenuFlyoutItem2_AddToUpdatedScalperShoppingItem_Click(object sender, RoutedEventArgs e)
        {
            AddToUpdatedScalperShoppingItemsChanged?.Invoke(DataGrid_Corp.SelectedItems.Select(p => (p as Core.Models.Market.StatusOrder).Target)?.ToList());
        }

        private async void MenuFlyoutItem1_ShowInGame_Click(object sender, RoutedEventArgs e)
        {
            var order = DataGrid_Character.SelectedItem as Core.Models.Market.StatusOrder;
            if(order != null)
            {
                Window?.ShowWaiting();
                try
                {
                    if(!SelecteCharacterControl.SelectedItem.IsTokenValid())
                    {
                        if (!await SelecteCharacterControl.SelectedItem.RefreshTokenAsync())
                        {
                            Core.Log.Error("Token已过期，尝试刷新失败");
                            return;
                        }
                    }
                    var resp = await _esiClient.UserInterface.MarketDetails(order.Target.TypeId);
                    if(resp.StatusCode == System.Net.HttpStatusCode.OK || resp.StatusCode == System.Net.HttpStatusCode.NoContent)
                    {
                        Window?.ShowSuccess(Helpers.ResourcesHelper.GetString("CharacterOrderPage_ShowInGame_Succcess"));
                    }
                    else
                    {
                        Window?.ShowError(resp.Message);
                    }
                }
                catch(Exception ex)
                {
                    Core.Log.Error(ex);
                    Window?.ShowError(ex.Message);
                }
                Window?.HideWaiting();
            }
        }
    }
}
