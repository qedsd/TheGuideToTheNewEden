using ESI.NET;
using ESI.NET.Models.Bookmarks;
using ESI.NET.Models.Character;
using ESI.NET.Models.Market;
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
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Models.Market;
using TheGuideToTheNewEden.Core.Services;
using TheGuideToTheNewEden.WinUI.Extensions;
using TheGuideToTheNewEden.WinUI.Services;
using TheGuideToTheNewEden.WinUI.Services.Settings;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Views.Business
{
    public sealed partial class OrderPage : Page
    {
        private List<StatusOrder> _characterOrder;
        private List<StatusOrder> _corpOrder;
        private List<StatusOrder> _order;
        private EsiClient _esiClient;
        public OrderPage()
        {
            this.InitializeComponent();
            _esiClient = ESIService.GetDefaultEsi();
            Loaded += OrderPage_Loaded;
        }

        private void OrderPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OrderPage_Loaded;
            OrderTypeComboBox.SelectionChanged += OrderTypeComboBox_SelectionChanged;
            OrderFromComboBox.SelectionChanged += OrderFromComboBox_SelectionChanged;
        }

        private void SelecteCharacterControl_OnSelectedItemChanged(ESI.NET.Models.SSO.AuthorizedCharacterData selectedItem)
        {
            _esiClient.SetCharacterData(selectedItem);
            _characterOrder = null;
            _corpOrder = null;
            _order = null;
            GetOrders();
        }

        private void Button_Refresh_Click(object sender, RoutedEventArgs e)
        {
            GetOrders();
        }
        private async void GetOrders()
        {
            this.ShowWaiting();
            try
            {
                var errorCount1 = Core.Log.GetErrorCount();
                if (OrderFromComboBox.SelectedIndex == 0)
                {
                    await GetCharacterOrders();
                }
                else
                {
                    await GetCorpOrders();
                }
                if (_order.NotNullOrEmpty())
                {
                    OrderDataGrid.ItemsSource = OrderTypeComboBox.SelectedIndex == 0 ? _order.Where(p => !p.Target.IsBuyOrder) : _order.Where(p => p.Target.IsBuyOrder);
                }
                else
                {
                    OrderDataGrid.ItemsSource = null;
                }
                var errorCount2 = Core.Log.GetErrorCount();
                if (errorCount1 != errorCount2)
                {
                    this.ShowError(Core.Log.GetLastError().ToString());
                }
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
                this.ShowError(ex.Message);
            }
            this.HideWaiting();
        }
        private async Task GetCharacterOrders()
        {
            if(SelecteCharacterControl.SelectedItem != null)
            {
                var orders = await Services.MarketOrderService.Current.GetCharacterOrdersAsync(SelecteCharacterControl.SelectedItem.CharacterID);
                if (orders.NotNullOrEmpty())
                {
                    var os = await CalOrderStatus(orders);
                    _characterOrder = os;
                    _order = os;
                }
                else
                {
                    _characterOrder = null;
                    _order = null;
                }
            }
            else
            {
                this.ShowError(Helpers.ResourcesHelper.GetString("General_CharacterUnselected"));
            }
        }
        private async Task GetCorpOrders()
        {
            if (SelecteCharacterControl.SelectedItem != null)
            {
                //可能由于没军团订单权限抛错
                List<Core.Models.Market.Order> orders = null;
                try
                {
                    orders = await Services.MarketOrderService.Current.GetCorpOrdersAsync(SelecteCharacterControl.SelectedItem.CharacterID);
                }
                catch
                {
                    orders = null;
                }
                if (orders.NotNullOrEmpty())
                {
                    var os = await CalOrderStatus(orders);
                    _corpOrder = os;
                    _order = os;
                }
                else
                {
                    _corpOrder = null;
                    _order = null;
                }
            }
            else
            {
                this.ShowError(Helpers.ResourcesHelper.GetString("General_CharacterUnselected"));
            }
        }

        private async Task<List<StatusOrder>> CalOrderStatus(List<Core.Models.Market.Order> orders)
        {
            List<StatusOrder> statusOrders = new List<StatusOrder>();
            foreach(var order in orders)
            {
                List<Core.Models.Market.Order> refs = null;
                try
                {
                    if (order.IsStation)
                    {
                        var results = await Services.MarketOrderService.Current.GetRegionOrdersAsync(order.TypeId, order.RegionId, MarketOrderSettingService.ScalperSikpStructureValue);
                        if (results.NotNullOrEmpty())
                        {
                            if (order.IsBuyOrder)
                            {
                                refs = results.Where(p => p.IsBuyOrder)?.OrderByDescending(p => p.Price).ToList();
                            }
                            else
                            {
                                refs = results.Where(p => !p.IsBuyOrder)?.OrderBy(p => p.Price).ToList();
                            }
                        }
                    }
                }
                catch(Exception ex)
                {
                    Core.Log.Error(ex);
                }
                statusOrders.Add(new StatusOrder(order, refs));
            }
            return statusOrders;
        }

        private void OrderDataGrid_AddToFilterList_Click(object sender, RoutedEventArgs e)
        {
            ClientServiceHelper.GetRequiredService<BusinessService>().AddToFilter(OrderDataGrid.SelectedItems.Select(p => (p as Core.Models.Market.StatusOrder).Target.InvType)?.ToList());
        }

        private void OrderDataGrid_AddToUpdatedScalperShoppingItem_Click(object sender, RoutedEventArgs e)
        {
            ClientServiceHelper.GetRequiredService<BusinessService>().NotifyTypeCountChanged(OrderDataGrid.SelectedItems.Select(p => ((p as Core.Models.Market.StatusOrder).Target.InvType, (p as Core.Models.Market.StatusOrder).Target.VolumeRemain))?.ToList());
        }
        private void OrderDataGrid_AddToGameOrder(object sender, RoutedEventArgs e)
        {
            CopyToGameOrder(OrderDataGrid.SelectedItems.Select(p => (p as Core.Models.Market.StatusOrder)).ToList());
        }
        private void CopyToGameOrder(List<StatusOrder> orders)
        {
            if (orders.NotNullOrEmpty())
            {
                var targetOrders = orders.Where(p => p.Normal != true).ToList();
                if (targetOrders.NotNullOrEmpty())
                {
                    StringBuilder stringBuilder = new StringBuilder();
                    foreach (var item in targetOrders)
                    {
                        stringBuilder.Append(item.Target.InvType.TypeName);
                        stringBuilder.Append(' ');
                        stringBuilder.Append(1);
                        stringBuilder.AppendLine();
                    }
                    DataPackage dataPackage = new DataPackage();
                    dataPackage.SetText(stringBuilder.ToString());
                    Clipboard.SetContent(dataPackage);
                    this.ShowSuccess(Helpers.ResourcesHelper.GetString("OrderPage_AddToGameOrder_Success"));
                }
                else
                {
                    this.ShowSuccess(Helpers.ResourcesHelper.GetString("OrderPage_AddToGameOrder_Failed"));
                }
            }
            else
            {
                this.ShowSuccess(Helpers.ResourcesHelper.GetString("OrderPage_AddToGameOrder_Failed"));
            }
        }
        private async void OrderDataGrid_ShowInGame_Click(object sender, RoutedEventArgs e)
        {
            var order = OrderDataGrid.SelectedItem as Core.Models.Market.StatusOrder;
            if(order != null)
            {
                this.ShowWaiting();
                try
                {
                    if(!SelecteCharacterControl.SelectedItem.IsTokenValid())
                    {
                        if (!await SelecteCharacterControl.SelectedItem.RefreshTokenAsync())
                        {
                            Core.Log.Error(Helpers.ResourcesHelper.GetString("General_TokenExpiredAndRefrshFailed"));
                            return;
                        }
                    }
                    var resp = await _esiClient.UserInterface.MarketDetails(order.Target.TypeId);
                    if(resp.StatusCode == System.Net.HttpStatusCode.OK || resp.StatusCode == System.Net.HttpStatusCode.NoContent)
                    {
                        this.ShowSuccess(Helpers.ResourcesHelper.GetString("OrderPage_ShowInGame_Succcess"));
                    }
                    else
                    {
                        this.ShowError(resp.Message);
                    }
                }
                catch(Exception ex)
                {
                    Core.Log.Error(ex);
                    this.ShowError(ex.Message);
                }
                this.HideWaiting();
            }
        }


        /// <summary>
        /// 卖单/买单
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OrderTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_order == null) return;
            OrderDataGrid.ItemsSource = OrderTypeComboBox.SelectedIndex == 0 ? _order.Where(p => !p.Target.IsBuyOrder) : _order.Where(p => p.Target.IsBuyOrder);
        }

        /// <summary>
        /// 个人/军团
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void OrderFromComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (OrderFromComboBox.SelectedIndex == 0)
            {
                if (_characterOrder.NotNullOrEmpty())
                {
                    _order = _characterOrder;   
                }
                else
                {
                    await GetCharacterOrders();
                }
            }
            else
            {
                if (_corpOrder.NotNullOrEmpty())
                {
                    _order = _corpOrder;
                }
                else
                {
                    await GetCorpOrders();
                }
            }
            if (_order.NotNullOrEmpty())
            {
                OrderDataGrid.ItemsSource = OrderTypeComboBox.SelectedIndex == 0 ? _order.Where(p => !p.Target.IsBuyOrder) : _order.Where(p => p.Target.IsBuyOrder);
            }
            else
            {
                OrderDataGrid.ItemsSource = null;
            }
        }
    }
}
