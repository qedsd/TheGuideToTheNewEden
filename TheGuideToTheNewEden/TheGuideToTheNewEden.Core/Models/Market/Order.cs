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
        }
        public InvType InvType { get; set; }
        public MapSolarSystem SolarSystem { get; set; }
        public bool IsStation
        {
            get => LocationId < 70000000;
        }
        public TimeSpan DurationTime
        {
            get => TimeSpan.FromTicks(Duration);
        }
        public string LocationName { get; set; }
    }
}
