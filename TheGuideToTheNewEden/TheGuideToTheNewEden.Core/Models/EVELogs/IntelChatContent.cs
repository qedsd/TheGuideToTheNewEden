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
        public List<IntelShipContent> IntelShips { get; set; }

        /// <summary>消息命中的星系 ID（未命中时为 0）。</summary>
        public int SolarSystemId { get; set; }

        /// <summary>消息命中的星系名（未命中时为空）。</summary>
        public string SolarSystemName { get; set; }

        /// <summary>该星系距离本地位置的跳数（未命中/不在范围内时为 -1）。</summary>
        public int Jumps { get; set; } = -1;
    }
}
