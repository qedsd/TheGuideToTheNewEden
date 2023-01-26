using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Models;

namespace TheGuideToTheNewEden.Core.Helpers
{
    public static class GameLogHelper
    {
        /// <summary>
        /// 获取聊天频道基本信息
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static ChatChanelInfo GetChatChanelInfo(string file)
        {
            if (file.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            {
                List<string> headContents = null;//包含频道信息的内容
                using (StreamReader sr = new StreamReader(file))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (line.EndsWith("---------------------------------------------------------------"))
                        {
                            if (headContents.NotNullOrEmpty())
                            {
                                break;
                            }
                            else
                            {
                                headContents = new List<string>();
                            }
                        }
                        else
                        {
                            if (headContents != null && !string.IsNullOrEmpty(line))
                            {
                                headContents.Add(line);
                            }
                        }
                    }
                }
                if (headContents.NotNullOrEmpty())
                {
                    ChatChanelInfo chatChanelInfo = new ChatChanelInfo();
                    chatChanelInfo.FilePath = file;
                    foreach (var c in headContents)
                    {
                        string content = c.TrimStart();
                        if(content != null)
                        {
                            int index = content.IndexOf(':');
                            if (index != -1 && index != content.Length - 1)
                            {
                                string key = content.Substring(0, index);
                                string value = content.Substring(index + 1).Trim();
                                switch (key)
                                {
                                    case "Channel ID": chatChanelInfo.ChannelID = value; break;
                                    case "Channel Name": chatChanelInfo.ChannelName = value; break;
                                    case "Listener": chatChanelInfo.Listener = value; break;
                                    case "Session started": chatChanelInfo.SessionStarted = DateTime.Parse(value); break;
                                }
                            }
                        }
                    }
                    return chatChanelInfo.IsValid() ? chatChanelInfo : null;
                }
            }
            return null;
        }
    }
}
