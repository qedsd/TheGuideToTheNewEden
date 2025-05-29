using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.ChannelMarket;

namespace TheGuideToTheNewEden.WinUI.ViewModels
{
    internal class ChannelMarketWinViewModel : BaseViewModel
    {
        private IEnumerable<MarketChatContent> _contents;
        public IEnumerable<MarketChatContent> Contents { get => _contents; set => SetProperty(ref _contents, value); }

        public ObservableCollection<ChannelMarketResult> Results { get ; set; } = new ObservableCollection<ChannelMarketResult>();

        private int _itemCount;
        public int ItemCount { get => _itemCount; set => SetProperty(ref _itemCount, value); }

        private bool _mutilItem;
        public bool MutilItem { get => _mutilItem; set => SetProperty(ref _mutilItem, value); }

        private double sell5P;
        public double Sell5P
        {
            get => sell5P;
            set => SetProperty(ref sell5P, value);
        }

        private double buy5P;
        public double Buy5P
        {
            get => buy5P;
            set => SetProperty(ref buy5P, value);
        }

        private double sellTop;
        public double SellTop
        {
            get => sellTop;
            set => SetProperty(ref sellTop, value);
        }

        private double buyTop;
        public double BuyTop
        {
            get => buyTop;
            set => SetProperty(ref buyTop, value);
        }

        private int _lastRegionID;
        public ChannelMarketWinViewModel()
        {

        }

        public async void UpdateContent(IEnumerable<MarketChatContent> marketChatContents, int regionID)
        {
            Contents = marketChatContents;
            ItemCount = Contents.Count();
            MutilItem = ItemCount > 1;
            Results.Clear();
            Window?.ShowWindowWaiting();
            foreach (var marketChatContent in Contents)
            {
                foreach(var item in marketChatContent.Items)
                {
                    List<Core.Models.Market.Order> orders = await Services.MarketOrderService.Current.GetRegionOrdersAsync(item.TypeID, regionID);
                    var buyOrders = orders?.Where(p => p.IsBuyOrder).OrderByDescending(p => p.Price)?.ToList();
                    var sellOrders = orders?.Where(p => !p.IsBuyOrder).OrderBy(p => p.Price)?.ToList();
                    var statistics = await Services.MarketOrderService.Current.GetHistoryAsync(item.TypeID, regionID);
                    Results.Add(new ChannelMarketResult(item, buyOrders, sellOrders, statistics));
                }
            }
            Window?.HideWindowWaiting();
            if (Results.Count > 1) 
            {
                Sell5P = Results.Sum(p => p.Sell5P);
                Buy5P = Results.Sum(p => p.Buy5P);
                SellTop = Results.Sum(p => p.SellTop);
                BuyTop = Results.Sum(p => p.BuyTop);
            }
        }

        public ICommand RefreshCommand => new RelayCommand(() =>
        {
            UpdateContent(Contents, _lastRegionID);
        });

        public ICommand DetailCommand => new RelayCommand<InvTypeBase>((type) =>
        {
            
        });
    }
}
