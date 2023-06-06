using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.Core.Models.Market
{
    public class Order: ESI.NET.Models.Market.Order
    {
        public Order() { }  
        public Order(ESI.NET.Models.Market.Order order) 
        {
            this.CopyFrom(order);
            RemainTimeSpan = Issued.AddDays(Duration) - DateTime.Now;
        }
        public InvType InvType { get; set; }
        public MapSolarSystem SolarSystem { get; set; }
        public bool IsStation
        {
            get => LocationId < 70000000;
        }
        public double Security
        {
            get => Math.Round(SolarSystem.Security, 1, MidpointRounding.ToEven);
        }
        public TimeSpan RemainTimeSpan { get; set; }
        public string RemainTime
        {
            get => RemainTimeSpan.ToString(@"dd\.hh\:mm\:ss");
        }
        public string LocationName { get; set; }
    }
}
