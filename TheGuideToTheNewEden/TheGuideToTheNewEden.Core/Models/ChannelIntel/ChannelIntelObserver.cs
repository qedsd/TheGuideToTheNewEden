﻿using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Helpers;
using TheGuideToTheNewEden.Core.Interfaces;
using TheGuideToTheNewEden.Core.Models.EVELogs;
using TheGuideToTheNewEden.Core.Services.DB;

namespace TheGuideToTheNewEden.Core.Models.ChannelIntel
{
    /// <summary>
    /// 针对指定频道的预警日志监控
    /// </summary>
    public class ChannelIntelObserver : IObservableFile
    {
        public ChannelIntelObserver(ChatChanelInfo info, ChannelIntelSetting setting)
        {
            ChatChanelInfo = info;
            Setting = setting;
            InitWords();
            FileStreamOffset += FileHelper.GetStreamLength(ChatChanelInfo.FilePath);
        }
        public Map.IntelSolarSystemMap IntelMap { get; set; }
        public ChatChanelInfo ChatChanelInfo { get; set; }
        /// <summary>
        /// 原始聊天内容
        /// </summary>
        public List<IntelChatContent> Contents { get; private set; } = new List<IntelChatContent>();
        public List<EarlyWarningContent> Warnings { get; private set; } = new List<EarlyWarningContent>();
        public WatcherChangeTypes WatcherChangeTypes { get; set; } = WatcherChangeTypes.Changed;
        public string FilePath
        {
            get => ChatChanelInfo.FilePath;
        }
        public Dictionary<string, int> SolarSystemNames { get; set; }
        private long FileStreamOffset = 0;
        public ChannelIntelSetting Setting { get; set; }
        /// <summary>
        /// 文件内容有更新
        /// </summary>
        public void Update()
        {
            Task.Run(() =>
            {
                try
                {
                    var newLines = Helpers.FileHelper.ReadLines(ChatChanelInfo.FilePath, FileStreamOffset, Encoding.Unicode, out int readOffset);
                    FileStreamOffset += readOffset;
                    if (newLines.NotNullOrEmpty())
                    {
                        List<IntelChatContent> newContents = new List<IntelChatContent>();
                        foreach (var line in newLines)
                        {
                            var chatContent = IntelChatContent.Create(line);
                            if (chatContent != null)
                            {
                                chatContent.Listener = ChatChanelInfo.Listener;
                                TrySetIntelType(chatContent);
                                newContents.Add(chatContent);
                            }
                        }
                        if (newContents.Count > 0)
                        {
                            List<EarlyWarningContent> newWarning = new List<EarlyWarningContent>();
                            foreach (var newContent in newContents)
                            {
                                var result = AnalyzeContent(newContent);
                                if (result != null)
                                {
                                    newWarning.Add(result);
                                    newContent.Important = true;
                                    newContent.IntelType = result.IntelType;
                                    newContent.IntelShips = result.IntelShips;
                                }
                            }
                            Contents.AddRange(newContents);
                            OnContentUpdate?.Invoke(this, newContents);
                            if (newWarning.Count != 0)
                            {
                                OnWarningUpdate?.Invoke(this, newWarning);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            });
        }
        public delegate void ContentUpdate(ChannelIntelObserver channelIntelObserver, IEnumerable<IntelChatContent> news);
        /// <summary>
        /// 消息更新
        /// </summary>
        public event ContentUpdate OnContentUpdate;

        public delegate void WarningUpdate(ChannelIntelObserver channelIntelObserver, IEnumerable<EarlyWarningContent> news);
        /// <summary>
        /// 预警更新
        /// </summary>
        public event WarningUpdate OnWarningUpdate;

        private EarlyWarningContent AnalyzeContent(IntelChatContent chatContent)
        {
            if (chatContent.IntelType != Enums.IntelChatType.Ignore)
            {
                if (SolarSystemNames != null)
                {
                    foreach (var name in SolarSystemNames)
                    {
                        if (chatContent.Content.Contains(name.Key))
                        {
                            if (IntelMap != null)
                            {
                                int jumps = IntelMap.JumpsOf(name.Value);
                                if (jumps != -1)
                                {
                                    List<IntelShipContent> shipContents = null;
                                    if(chatContent.IntelType != Enums.IntelChatType.Clear)
                                    {
                                        var contents = chatContent.Content.Split(' ');
                                        int startIndex = 0;
                                        for(int i = 0;i<contents.Length;i++)
                                        {
                                            string content = contents[i];
                                            if (!string.IsNullOrEmpty(content))
                                            {
                                                var targetShip = ShipNameCacheService.Current.Search(content);
                                                if(targetShip != null)
                                                {
                                                    IntelShipContent intelShipContent = new IntelShipContent()
                                                    {
                                                        Ship = targetShip,
                                                        StartIndex = startIndex,
                                                        Length = content.Length
                                                    };
                                                    if(shipContents == null)
                                                    {
                                                        shipContents = new List<IntelShipContent>();
                                                    }
                                                    shipContents.Add(intelShipContent);
                                                }
                                            }
                                            startIndex += content.Length + 1;
                                        }
                                    }
                                    return new EarlyWarningContent()
                                    {
                                        Content = chatContent.Content,
                                        Time = chatContent.EVETime,
                                        Listener = ChatChanelInfo.Listener,
                                        SolarSystemId = name.Value,
                                        SolarSystemName = name.Key,
                                        Level = 3,//TODO:预警等级划分
                                        IntelType = chatContent.IntelType == Enums.IntelChatType.Clear ? Enums.IntelChatType.Clear : Enums.IntelChatType.Intel,
                                        IntelMap = IntelMap,
                                        Jumps = jumps,
                                        IntelShips = shipContents
                                    };
                                }
                                else
                                {
                                    return null;
                                }
                            }
                            else
                            {
                                return null;
                            }
                        }
                    }
                }
            }
            return null;
        }

        public bool IsReplaced(string file)
        {
            var newChanelInfo = GameLogHelper.GetChatChanelInfo(file);
            if (newChanelInfo != null
                && newChanelInfo.ChannelName == ChatChanelInfo.ChannelName
                && newChanelInfo.SessionStarted > ChatChanelInfo.SessionStarted)
            {
                ChatChanelInfo.FilePath = file;
                FileStreamOffset = 0;
                return true;
            }
            else
            {
                return false;
            }
        }

        #region 关键词设置
        private void InitWords()
        {
            if (Setting != null)
            {
                IgnoreWords = Setting.IgnoreWords?.Split(',');
                ClearWords = Setting.ClearWords?.Split(',');
            }
        }
        private string[] IgnoreWords;
        private string[] ClearWords;
        private void TrySetIntelType(IntelChatContent content)
        {
            if (Setting != null)
            {
                if (IgnoreWords.NotNullOrEmpty())
                {
                    foreach (var w in IgnoreWords)
                    {
                        if (!string.IsNullOrWhiteSpace(w) && content.Content.Contains(w))
                        {
                            content.IntelType = Core.Enums.IntelChatType.Ignore; break;
                        }
                    }
                }
                if (ClearWords.NotNullOrEmpty())
                {
                    foreach (var w in ClearWords)
                    {
                        if (content.Content.Contains(w))
                        {
                            content.IntelType = Core.Enums.IntelChatType.Clear; break;
                        }
                    }
                }
            }
        }
        #endregion

        public void CreatedFile(string newfile)
        {

        }
    }
}
