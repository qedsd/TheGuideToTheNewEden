using ESI.NET.Models.PlanetaryInteraction;
using System;
using System.Collections.Generic;
using System.Text;
using TheGuideToTheNewEden.Core.Helpers;

namespace TheGuideToTheNewEden.Core.Models.EVELogs
{
    public class GameLogContent
    {
        public GameLogContent() { }
        public static GameLogContent Create(string content)
        {
            if (!string.IsNullOrEmpty(content))
            {
                var content1 = content.TrimStart(' ');
                if (content1.Length > 1)
                {
                    if (content1[0] == '[' || content1[1] == '[')
                    {
                        int index = content1.IndexOf(']');
                        if (index > 0)
                        {
                            string evetimeStr = content1.Substring(2, index - 2).Trim();
                            if (DateTime.TryParse(evetimeStr, out var eveTime))
                            {
                                string content2 = content1.Substring(index + 1)?.TrimStart(' ');
                                return new GameLogContent()
                                {
                                    EVETime = eveTime,
                                    Content = content2,
                                    SourceContent = GameLogHelper.RemoveHtmlTag(content)
                                };
                            }
                        }
                    }
                }
            }
            return null;
        }
        public string SourceContent { get; set; }
        public DateTime EVETime { get; set; }
        public DateTime LocalTime
        {
            get => EVETime.ToLocalTime();
        }
        public string Content { get; set; }
        /// <summary>
        /// 重要消息
        /// </summary>
        public bool Important { get; set; }
    }
}
