using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Models.EVELogs;

namespace TheGuideToTheNewEden.Core.Models.ChannelMarket
{
    public class MarketChatContent : ChatContent
    {
        public static new MarketChatContent Create(string content)
        {
            return ChatContent.Create(content).DepthClone<MarketChatContent>();
        }
        public string Listener { get; set; }
        public List<InvTypeBase> Items { get; set; }
    }
}
