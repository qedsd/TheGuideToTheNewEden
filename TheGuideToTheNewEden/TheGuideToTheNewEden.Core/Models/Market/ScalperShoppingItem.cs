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
            Jumps = scalperItem.Jumps;
            InvType = scalperItem.InvType;
            SellPrice = scalperItem.SellPrice;
            BuyPrice= scalperItem.BuyPrice;
            Quantity = scalperItem.TargetSales;
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
        private double quantity;
        /// <summary>
        /// 数量
        /// </summary>
        public double Quantity
        {
            get => quantity;
            set
            {
                SetProperty(ref quantity, value);
                Volume = value * InvType.PackagedVolume;
                Cal();
            }
        }
        private double volume;
        /// <summary>
        /// 体积
        /// </summary>
        public double Volume
        {
            get => volume;
            set
            {
                SetProperty(ref volume, value);
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

        /// <summary>
        /// 源市场与目的市场距离跳数
        /// </summary>
        public int Jumps { get; set; }

        private double iskPerJump;
        public double IskPerJump
        {
            get => iskPerJump;
            set => SetProperty(ref iskPerJump, value);
        }

        private double iskPerVolume;
        public double IskPerVolume
        {
            get => iskPerVolume;
            set => SetProperty(ref iskPerVolume, value);
        }


        private void Cal()
        {
            if(SellPrice != 0 && BuyPrice != 0)
            {
                ROI = (SellPrice - BuyPrice) / BuyPrice * 100;
                NetProfit = (SellPrice - BuyPrice) * Quantity;
                IskPerJump = NetProfit / Jumps;
                IskPerVolume = Volume / Jumps;
            }
        }
    }
}
