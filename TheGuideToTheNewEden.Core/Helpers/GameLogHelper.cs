using System;
using System.Collections.Generic;
using System.IO;
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
                            if (headContents.Count > 0)
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
                            var array = content.Split(':');
                            if(array != null && array.Length == 2)
                            {
                                switch(array[0])
                                {
                                    case "Channel ID": chatChanelInfo.ChannelID = array[1].Trim(); break;
                                    case "Channel Name": chatChanelInfo.ChannelName = array[1].Trim(); break;
                                    case "Listener": chatChanelInfo.Listener = array[1].Trim(); break;
                                    case "Session started": chatChanelInfo.SessionStarted = DateTime.Parse(array[1].Trim()); break;
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
