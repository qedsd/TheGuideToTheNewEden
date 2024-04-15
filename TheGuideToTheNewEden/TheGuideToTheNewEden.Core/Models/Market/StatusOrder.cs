using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.Core.Models.Market
{
    public class StatusOrder
    {
        public Order Target { get; set; }
        public List<Order> References {  get; set; }
        public decimal Difference { get; set; }
        public bool Normal {  get; set; }
        public StatusOrder(Order target, List<Order> references)
        {
            Target = target;
            References = references;
            if(references.NotNullOrEmpty())
            {
                var diff = Target.Price - references.First().Price;
                Difference = Math.Abs(diff);
                Normal = target.IsBuyOrder ? (diff > 0) : (diff <= 0);
            }
        }
    }
}
