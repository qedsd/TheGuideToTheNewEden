using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Models;

namespace TheGuideToTheNewEden.Core.Helpers
{
    public static class GameLogHelper
    {
        #region 聊天频道
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
                    try
                    {
                        Dictionary<string, ChatChanelInfo> dic = new Dictionary<string, ChatChanelInfo>();//该角色最新的频道日志，key为ChannelID
                        var infosOfOneListener = groupOfOneListener.ToList();//该角色下所有的文件
                        var groupByChannelName = infosOfOneListener.GroupBy(p => p.ChannelName).ToList();//按频道名划分
                        foreach (var groupOfOneChannel in groupByChannelName)
                        {
                            var infosOfOneChannel = groupOfOneChannel.ToList();//该频道下所有的文件
                            //同一天可能有多个文件
                            var dateGroup = infosOfOneChannel.GroupBy(p => p.Date).ToList();
                            var latsetDateGroup = dateGroup.OrderByDescending(p => p.Key).First();//该频道下最新一天的所有文件
                            try
                            {
                                List<ChatChanelInfo> infos = new List<ChatChanelInfo>();
                                //存在日志文件内容是空的情况
                                foreach(var latsetDateGroupFile in latsetDateGroup)
                                {
                                    var info = GetChatChanelInfo(latsetDateGroupFile.FilePath);
                                    if(info == null)
                                    {
                                        Core.Log.Error($"存在不规范文件:{latsetDateGroupFile.FilePath}");
                                    }
                                    else
                                    {
                                        infos.Add(info);
                                    }
                                }
                                if(infos.Any())
                                {
                                    var chanelInfo = infos.OrderByDescending(p => p.SessionStarted).FirstOrDefault();//该频道下最新的文件                                                                                                                                 //因为文件名的频道名会跟语言相关，所有还需要读取文件内容里唯一的频道ID来判断
                                    if (chanelInfo != null)
                                    {
                                        if (dic.TryGetValue(chanelInfo.ChannelID, out var exist))
                                        {
                                            if (exist.SessionStarted < chanelInfo.SessionStarted)
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
                            }
                            catch(Exception ex)
                            {
                                Log.Error($"分析日志{groupOfOneChannel.Key}时发生未知异常");
                                Log.Error(ex);
                            }
                        }
                        var chanels = dic.Values.ToList();
                        if (listenerChannelDic.ContainsKey(chanels.First().Listener))
                        {
                            Log.Error($"存在相同角色名称但角色ID不同：{chanels.First().Listener}");
                        }
                        else
                        {
                            listenerChannelDic.Add(chanels.First().Listener, chanels);
                        }
                    }
                    catch(Exception ex)
                    {
                        Log.Error($"分析角色{groupOfOneListener.Key}日志时发生未知异常");
                        Log.Error(ex);
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
        public static Dictionary<string, List<ChatChanelInfo>> GetChatChanelInfos(string folder, int duration)
        {
            var infos = GetChatlogFileInfos(folder);
            if (infos.NotNullOrEmpty())
            {
                if(duration > 0)
                {
                    var inDuration = infos.Where(p => (DateTime.Now - p.Date).TotalDays <= duration)?.ToList();
                    return GetChatChanelInfos(inDuration);
                }
                else
                {
                    return GetChatChanelInfos(infos);
                }
            }
            else
            {
                return null;
            }
        }
        #endregion

        #region 游戏日志
        /// <summary>
        /// 获取游戏日志基本信息
        /// 因为是打开文件流读取，资源消耗较大，不要在高频操作下使用
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static GameLogInfo GetGameLogInfo(string file)
        {
            try
            {
                if (file.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                {
                    var nameArray = Path.GetFileNameWithoutExtension(file).Split('_');
                    int listenerId = 0;
                    if (nameArray.Length != 3 || !int.TryParse(nameArray[2], out listenerId))
                    {
                        return null;
                    }
                    List<string> headContents = null;//包含频道信息的内容
                    using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        byte[] b = new byte[1024];
                        if (fs.Read(b, 0, b.Length) > 0)
                        {
                            var content = Encoding.Default.GetString(b);
                            var contents = content.Split('\n', '\r');
                            if (contents.NotNullOrEmpty())
                            {
                                foreach (var line in contents)
                                {
                                    if (line.EndsWith("------------------------------------------------------------"))
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
                        GameLogInfo info = new GameLogInfo();
                        info.ListenerID = listenerId;
                        info.FilePath = file;
                        foreach (var c in headContents)
                        {
                            string content = c.TrimStart();
                            if (content != null)
                            {
                                int index = content.IndexOf(':');
                                if (index != -1 && index != content.Length - 1)
                                {
                                    string key = content.Substring(0, index);
                                    string value = content.Substring(index + 1).Trim();
                                    //头内容只有收听者和时间
                                    if (DateTime.TryParse(value, out var time))
                                    {
                                        info.StartTime = time;
                                    }
                                    else
                                    {
                                        info.ListenerName = value;
                                    }
                                }
                            }
                        }
                        return info.IsValid() ? info : null;
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return null;
            }
        }

        /// <summary>
        /// 获取游戏日志文件名信息
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static GameLogFileInfo GetGameLogFileInfo(string file)
        {
            try
            {
                if (file.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                {
                    var nameArray = Path.GetFileNameWithoutExtension(file).Split('_');
                    int listenerId = 0;
                    if (nameArray.Length != 3 || !int.TryParse(nameArray[2], out listenerId) || nameArray[1].Length != 6 || nameArray[0].Length != 8)
                    {
                        return null;
                    }
                    GameLogFileInfo info = new GameLogFileInfo() { ListenerID = listenerId, FilePath = file };
                    DateTimeFormatInfo dfInfo = new DateTimeFormatInfo();
                    dfInfo.ShortDatePattern = "yyyyMMdd hh:mm:ss";
                    info.StartTime = DateTime.Parse($"{nameArray[0].Substring(0,4)}.{nameArray[0].Substring(4, 2)}.{nameArray[0].Substring(6, 2)} {nameArray[1].Substring(0, 2)}:{nameArray[1].Substring(2, 2)}:{nameArray[1].Substring(4, 2)}");
                    return info;
                }
                else
                {
                    return null;
                }
            }
            catch(Exception ex)
            {
                Log.Error(ex);
                return null;
            }
        }
        public static List<GameLogFileInfo> GetGameLogFileInfos(string folder)
        {
            var allFiles = System.IO.Directory.GetFiles(folder);
            if (allFiles.NotNullOrEmpty())
            {
                List<GameLogFileInfo> infos = new List<GameLogFileInfo>();
                foreach (var file in allFiles)
                {
                    var info = GetGameLogFileInfo(file);
                    if(info != null)
                    {
                        infos.Add(info);
                    }
                }
                return infos;
            }
            else
            {
                Log.Error("无游戏日志文件");
                return null;
            }
        }
        /// <summary>
        /// 获取每个角色最新的日志
        /// </summary>
        /// <param name="infos"></param>
        /// <returns></returns>
        public static List<GameLogFileInfo> GetLatestGameLogFileInfos(List<GameLogFileInfo> infos)
        {
            if(infos.NotNullOrEmpty())
            {
                List<GameLogFileInfo> values = new List<GameLogFileInfo>();
                var group = infos.GroupBy(p => p.ListenerID).ToList();
                foreach (var s in group)
                {
                    values.Add(s.OrderByDescending(p => p.StartTime).First());
                }
                return values;
            }
            else
            {
                return null;
            }
        }
        /// <summary>
        /// 从指定文件夹获取每个角色最新的日志文件
        /// </summary>
        /// <param name="folder"></param>
        /// <returns></returns>
        public static List<GameLogInfo> GetLatestGameLogInfos(string folder)
        {
            var allFileInfos = GetGameLogFileInfos(folder);
            var latestFileInfos = GetLatestGameLogFileInfos(allFileInfos);
            if(latestFileInfos.NotNullOrEmpty())
            {
                List<GameLogInfo> infos = new List<GameLogInfo>();
                foreach(var latestFileInfo in latestFileInfos)
                {
                    var info = GetGameLogInfo(latestFileInfo.FilePath);
                    if(info != null)
                    {
                        infos.Add(info);
                    }
                }
                return infos;
            }
            else
            {
                return null;
            }
        }

        public static bool GetGameLogDateAndThreadId(string file, out int date, out int threadId)
        {
            date = -1;
            threadId = -1;
            try
            {
                if (file.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                {
                    var nameArray = Path.GetFileNameWithoutExtension(file).Split('_');
                    if (nameArray.Length > 3 || nameArray[0].Length != 8 || !int.TryParse(nameArray[0], out date) || nameArray[1].Length != 6 || !int.TryParse(nameArray[1], out threadId))
                    {
                        return false;
                    }
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return false;
            }
        }
        #endregion

        /// <summary>
        /// 正则表达式去除HTML标记
        /// </summary>
        /// <param name="inStr"></param>
        /// <returns></returns>
        public static string RemoveHtmlTag(string inStr)
        {
            //先把<b>77</b>这种移除掉
            Regex regHtml2 = new Regex(@"\<b.+?\b>", RegexOptions.IgnoreCase);
            StringBuilder str2 = new StringBuilder();
            str2.Append(inStr);
            Match cMatch2;
            for (cMatch2 = regHtml2.Match(str2.ToString()); cMatch2.Success; cMatch2 = cMatch2.NextMatch())
                str2.Replace(cMatch2.Groups[0].Value.ToString(), "");

            //再移除其他正常的标签
            Regex regHtml = new Regex(@"\<.+?\>", RegexOptions.IgnoreCase);
            StringBuilder str = new StringBuilder();
            str.Append(str2.ToString());
            Match cMatch;
            for (cMatch = regHtml.Match(str.ToString()); cMatch.Success; cMatch = cMatch.NextMatch())
                str.Replace(cMatch.Groups[0].Value.ToString(), "");

            return str.ToString();
        }
    }
}
