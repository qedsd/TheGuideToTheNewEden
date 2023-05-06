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
        /// 因为是打开文件流读取，资源消耗较大，不要在高频操作下使用
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static ChatChanelInfo GetChatChanelInfo(string file)
        {
            if (file.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            {
                List<string> headContents = null;//包含频道信息的内容
                using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    byte[] b = new byte[1024];
                    if (fs.Read(b, 0, b.Length) > 0)
                    {
                        var content = Encoding.Unicode.GetString(b);
                        var contents = content.Split('\n','\r');
                        if(contents.NotNullOrEmpty())
                        {
                            foreach (var line in contents)
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

        /// <summary>
        /// 获取聊天文件对应的文件名基本信息
        /// </summary>
        /// <param name="folder"></param>
        /// <returns></returns>
        public static List<ChatlogFileInfo> GetChatlogFileInfos(string folder)
        {
            var allFiles = System.IO.Directory.GetFiles(folder);
            if(allFiles.NotNullOrEmpty())
            {
                return GetChatlogFileInfos(allFiles);
            }
            else
            {
                return null;
            }
        }
        /// <summary>
        /// 获取聊天文件对应的文件名基本信息
        /// </summary>
        /// <param name="files"></param>
        /// <returns></returns>
        public static List<ChatlogFileInfo> GetChatlogFileInfos(string[] files)
        {
            if(files.Any())
            {
                List<ChatlogFileInfo> infos = new List<ChatlogFileInfo>();
                foreach(var file in files)
                {
                    var info = ChatlogFileInfo.Create(file);
                    if(info != null)
                    {
                        infos.Add(info);
                    }
                }
                return infos;
            }
            else
            {
                Log.Error("无聊天文件");
                return null;
            }
        }

        /// <summary>
        /// 获取每个角色下最新日期的聊天频道
        /// </summary>
        /// <param name="chatlogFileInfos">聊天文件</param>
        /// <returns></returns>
        public static Dictionary<string, List<ChatChanelInfo>> GetChatChanelInfos(List<ChatlogFileInfo> chatlogFileInfos)
        {
            if(chatlogFileInfos.NotNullOrEmpty())
            {
                //key为角色名,value为角色下所有的不重复频道
                Dictionary<string, List<ChatChanelInfo>> listenerChannelDic = new Dictionary<string, List<ChatChanelInfo>>();
                var groupByListener = chatlogFileInfos.GroupBy(p => p.ListenerID).ToList();//按角色划分开
                foreach (var groupOfOneListener in groupByListener)
                {
                    Dictionary<string, ChatChanelInfo> dic = new Dictionary<string, ChatChanelInfo>();//该角色最新的频道日志，key为ChannelID
                    var infosOfOneListener = groupOfOneListener.ToList();//该角色下所有的文件
                    var groupByChannelName = infosOfOneListener.GroupBy(p => p.ChannelName).ToList();//按频道名划分
                    foreach (var groupOfOneChannel in groupByChannelName)
                    {
                        var infosOfOneChannel = groupOfOneChannel.ToList();//该频道下所有的文件
                        var latestInfo = infosOfOneChannel.OrderByDescending(p => p.Date).First();//该频道下最新的文件
                        var chanelInfo = GetChatChanelInfo(latestInfo.FilePath);
                        //因为文件名的频道名会跟语言相关，所有还需要读取文件内容里唯一的频道ID来判断
                        if (chanelInfo != null)
                        {
                            if(dic.TryGetValue(chanelInfo.ChannelID, out var exist))
                            {
                                if(exist.SessionStarted < chanelInfo.SessionStarted)
                                {
                                    //保留最新的
                                    dic.Remove(chanelInfo.ChannelID);
                                    dic.Add(chanelInfo.ChannelID, chanelInfo);
                                }
                            }
                            else
                            {
                                dic.Add(chanelInfo.ChannelID, chanelInfo);
                            }
                        }
                    }
                    var chanels = dic.Values.ToList();
                    if(listenerChannelDic.ContainsKey(chanels.First().Listener))
                    {
                        Log.Error($"存在相同角色名称但角色ID不同：{chanels.First().Listener}");
                    }
                    else
                    {
                        listenerChannelDic.Add(chanels.First().Listener, chanels);
                    }
                }
                return listenerChannelDic;
            }
            else
            {
                return null;
            }
        }
        /// <summary>
        /// 获取每个角色下最新日期的聊天频道
        /// </summary>
        /// <param name="files"></param>
        /// <returns></returns>
        public static Dictionary<string, List<ChatChanelInfo>> GetChatChanelInfos(string[] files)
        {
            var infos = GetChatlogFileInfos(files);
            if(infos.NotNullOrEmpty())
            {
                return GetChatChanelInfos(infos);
            }
            else
            {
                return null;
            }
        }
        /// <summary>
        /// 获取每个角色下最新日期的聊天频道
        /// </summary>
        /// <param name="folder"></param>
        /// <returns></returns>
        public static Dictionary<string, List<ChatChanelInfo>> GetChatChanelInfos(string folder)
        {
            var infos = GetChatlogFileInfos(folder);
            if (infos.NotNullOrEmpty())
            {
                return GetChatChanelInfos(infos);
            }
            else
            {
                return null;
            }
        }
    }
}
