using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Models.Market
{
    public class ScalperShoppingItem:ObservableObject
    {
        public ScalperShoppingItem() { }
        public ScalperShoppingItem(ScalperItem scalperItem)
        {
            InvType = scalperItem.InvType;
            SellPrice = scalperItem.SellPrice;
            BuyPrice= scalperItem.BuyPrice;
            Volume = scalperItem.TargetSales;
        }
        public InvType InvType { get; set; }
        private double sellPrice;
        public double SellPrice
        {
            get => sellPrice;
            set
            {
                SetProperty(ref sellPrice, value);
                Cal();
            }
        }
        private double buyPrice;
        public double BuyPrice
        {
            get => buyPrice;
            set
            {
                SetProperty(ref buyPrice, value);
                Cal();
            }
        }
        private double volume;
        public double Volume
        {
            get => volume;
            set
            {
                SetProperty(ref volume, value);
                Cal();
            }
        }
        private double roi;
        public double ROI
        {
            get => roi;
            set => SetProperty(ref roi, value);
        }
        private double netProfit;
        public double NetProfit
        {
            get => netProfit;
            set => SetProperty(ref netProfit, value);
        }
        private void Cal()
        {
            if(SellPrice != 0 && BuyPrice != 0)
            {
                ROI = (SellPrice - BuyPrice) / BuyPrice;
                NetProfit = (SellPrice - BuyPrice) * Volume;
            }
        }
    }
}
