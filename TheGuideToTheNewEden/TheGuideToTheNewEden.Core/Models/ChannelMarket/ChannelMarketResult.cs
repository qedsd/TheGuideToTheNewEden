using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.Core.Models.ChannelMarket
{
    public class ChannelMarketResult
    {
        public InvTypeBase Item { get; set; }
        public IEnumerable<Core.Models.Market.Order> SellOrders {  get; set; }
        public IEnumerable<Core.Models.Market.Order> BuyOrders {  get; set; }
        public IEnumerable<ESI.NET.Models.Market.Statistic> Statistics { get; set; }
        public double Sell5P {  get; set; }
        public double Buy5P { get; set; }
        public double SellTop { get; set; }
        public double BuyTop { get; set; }
        public long SellAmount { get; set; }
        public long BuyAmount { get; set; }

        public long Volume { get; set; }
        public decimal Highest { get; set; }
        public decimal Average { get; set; }
        public decimal Lowest { get; set; }
        public ChannelMarketResult(InvTypeBase item, IEnumerable<Core.Models.Market.Order> sellOrders, IEnumerable<Core.Models.Market.Order> buyOrders, IEnumerable<ESI.NET.Models.Market.Statistic> statistics)
        {
            Item = item;
            SellOrders = sellOrders;
            BuyOrders = buyOrders;
            Statistics = statistics;
            SetOrderInfo();
            SetHistoryInfo();
        }
        private void SetOrderInfo()
        {
            if (SellOrders.NotNullOrEmpty())
            {
                int top5P = (int)(SellOrders.Count() * 0.05);
                if (top5P > 1)
                {
                    Sell5P = (double)SellOrders.Take(top5P).Average(p => p.Price);
                }
                else
                {
                    Sell5P = (double)SellOrders.FirstOrDefault()?.Price;
                }
                SellTop = (double)SellOrders.Min(p => p.Price);
                SellAmount = SellOrders.Sum(p => (long)p.VolumeRemain);
            }
            else
            {
                Sell5P = 0;
                SellTop = 0;
                SellAmount = 0;
            }

            if (BuyOrders.NotNullOrEmpty())
            {
                int top5P = (int)(BuyOrders.Count() * 0.05);
                if (top5P > 1)
                {
                    Buy5P = (double)BuyOrders.Take(top5P).Average(p => p.Price);
                }
                else
                {
                    Buy5P = (double)BuyOrders.FirstOrDefault()?.Price;
                }
                BuyTop = (double)BuyOrders.Max(p => p.Price);
                BuyAmount = BuyOrders.Sum(p => (long)p.VolumeRemain);
            }
            else
            {
                Buy5P = 0;
                BuyTop = 0;
                BuyAmount = 0;
            }
        }
        private void SetHistoryInfo()
        {
            if (Statistics != null)
            {
                var history = Statistics.Where(p => (DateTime.UtcNow - p.Date).TotalDays <= 7).ToList();
                if (history.NotNullOrEmpty())
                {
                    Volume = history.Sum(p => p.Volume);
                    Highest = history.Max(p => p.Highest);
                    Average = history.Average(p => p.Average);
                    Lowest = history.Min(p => p.Lowest);
                }
                else
                {
                    Volume = 0;
                    Highest = 0;
                    Average = 0;
                    Lowest = 0;
                }
            }
        }
    }
}
