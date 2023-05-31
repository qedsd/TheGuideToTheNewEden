using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Models.Market
{
    public class MarketItem
    {
        public string Name
        {
            get
            {
                if(InvMarketGroup != null)
                {
                    return InvMarketGroup.MarketGroupName;
                }
                else
                {
                    return InvType.TypeName;
                }
            }
        }
        public string Desc
        {
            get
            {
                if (InvMarketGroup != null)
                {
                    return InvMarketGroup.Description;
                }
                else
                {
                    return InvType.Description;
                }
            }
        }
        public bool IsType
        {
            get => InvType != null;
        }
        public bool IsGroup
        {
            get => InvMarketGroup != null;
        }
        public InvMarketGroup InvMarketGroup { get; set; }
        public InvMarketGroup ParentGroup { get; set; }
        public InvType InvType { get; set; }
        public List<MarketItem> Children { get; set; }
    }
}
