using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.EVELogs
{
    public class ChatContent
    {
        public ChatContent() { }
        public static ChatContent Create(string content)
        {
            if(!string.IsNullOrEmpty(content))
            {
                var content1 = content.TrimStart(' ');
                if (content1.Length > 1)
                {
                    int startIndex = content1.IndexOf('[');
                    if (startIndex > -1)
                    {
                        int index = content1.IndexOf(']');
                        if (index > 0)
                        {
                            string evetimeStr = content1.Substring(startIndex + 1, index - 2).Trim();
                            if(DateTime.TryParse(evetimeStr, out var eveTime))
                            {
                                string content2 = content1.Substring(index + 1)?.TrimStart(' ');
                                if (content2 != null && content2.Length > 0)
                                {
                                    int index2 = content2.IndexOf('>');
                                    if (index2 > 0)
                                    {
                                        string speakerName = content2.Substring(0, index2);
                                        string content3 = content2.Substring(index2 + 1);
                                        return new ChatContent()
                                        {
                                            EVETime = eveTime,
                                            SpeakerName = speakerName.Trim(),
                                            Content = content3,
                                            SourceContent = content
                                        };
                                    }
                                }
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
        public string SpeakerName { get; set; }
        public string Content { get; set; }
        /// <summary>
        /// 重要消息
        /// </summary>
        public bool Important { get; set; }
    }
}
