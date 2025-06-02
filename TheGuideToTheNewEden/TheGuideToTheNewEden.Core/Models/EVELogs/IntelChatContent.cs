using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.Enums;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.Core.Models.EVELogs
{
    public class IntelChatContent: ChatContent
    {
        public static new IntelChatContent Create(string content)
        {
            return ChatContent.Create(content).DepthClone<IntelChatContent>();
        }
        public IntelChatType IntelType { get; set; }
        public string Listener {  get; set; }
        public List<IntelShipContent> IntelShips { get; set; }
    }
}
