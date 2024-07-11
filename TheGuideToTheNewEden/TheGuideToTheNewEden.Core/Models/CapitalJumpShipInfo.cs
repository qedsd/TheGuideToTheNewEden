using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Models
{
    public class CapitalJumpShipInfo
    {
        /// <summary>
        /// 市场分组ID
        /// </summary>
        public int GroupID {  get; set; }
        public int TypeID { get; set; }
        /// <summary>
        /// 最大跳跃光年
        /// </summary>
        public double MaxLY { get; set; }
        /// <summary>
        /// 每光年消耗燃料
        /// </summary>
        public double PerLYFuel { get; set; }

        public InvType InvType { get; set; }
        public InvMarketGroup InvMarketGroup { get; set; }
        public string Name
        {
            get => $"{InvMarketGroup.MarketGroupName} - {InvType.TypeName}";
        }
    }
}
