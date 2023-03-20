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

namespace TheGuideToTheNewEden.Core.Models
{
    public class EarlyWarningItem: IObservableFile
    {
        public EarlyWarningItem(ChatChanelInfo info, EarlyWarningSetting setting)
        {
            ChatChanelInfo = info;
            Setting = setting;
            InitWords();
            FileStreamOffset += FileHelper.GetStreamLength(ChatChanelInfo.FilePath);
        }
        public Map.IntelSolarSystemMap IntelMap { get; set; }
        //public async Task InitAsync()
        //{
        //    IntelMap = await EVEHelpers.SolarSystemPosHelper.GetIntelSolarSystemMapAsync(Setting.LocationID, Setting.IntelJumps);
        //    EVEHelpers.SolarSystemPosHelper.ResetXY(IntelMap.GetAllSolarSystem());
        //}
        public ChatChanelInfo ChatChanelInfo { get; set; }
        /// <summary>
        /// 原始聊天内容
        /// </summary>
        public List<IntelChatContent> Contents { get;private set; } = new List<IntelChatContent>();
        public List<EarlyWarningContent> Warnings { get;private set; } = new List<EarlyWarningContent>();
        public WatcherChangeTypes WatcherChangeTypes { get; set; } = WatcherChangeTypes.Changed;
        public string FilePath
        {
            get => ChatChanelInfo.FilePath;
        }
        public Dictionary<string, int> SolarSystemNames { get; set; }
        private long FileStreamOffset = 0;
        public EarlyWarningSetting Setting { get; set; }
        /// <summary>
        /// 文件内容有更新
        /// </summary>
        public void Update()
        {
            Task.Run(() =>
            {
                try
                {
                    var newLines = Helpers.FileHelper.ReadLines(ChatChanelInfo.FilePath, FileStreamOffset, out int readOffset);
                    FileStreamOffset += readOffset;
                    if (newLines.NotNullOrEmpty())
                    {
                        List<IntelChatContent> newContents = new List<IntelChatContent>();
                        foreach (var line in newLines)
                        {
                            var chatContent = IntelChatContent.Create(line);
                            if(chatContent != null)
                            {
                                TrySetIntelType(chatContent);
                                newContents.Add(chatContent);
                            }
                        }
                        if(newContents.Count > 0)
                        {
                            List<EarlyWarningContent> newWarning = new List<EarlyWarningContent>();
                            foreach (var newContent in newContents)
                            {
                                var result = AnalyzeContent(newContent);
                                if (result != null)
                                {
                                    newWarning.Add(result);
                                    newContent.Important = true;
                                    newContent.IntelType= result.IntelType;
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
                    //TODO:日志输出
                }
            });
        }
        public delegate void ContentUpdate(EarlyWarningItem earlyWarningItem,IEnumerable<IntelChatContent> news);
        /// <summary>
        /// 消息更新
        /// </summary>
        public event ContentUpdate OnContentUpdate;

        public delegate void WarningUpdate(EarlyWarningItem earlyWarningItem, IEnumerable<EarlyWarningContent> news);
        /// <summary>
        /// 预警更新
        /// </summary>
        public event WarningUpdate OnWarningUpdate;

        private EarlyWarningContent AnalyzeContent(IntelChatContent chatContent)
        {
            if(chatContent.IntelType != Enums.IntelChatType.Ignore)
            {
                if (SolarSystemNames != null)
                {
                    foreach (var name in SolarSystemNames)
                    {
                        if (chatContent.Content.Contains(name.Key))
                        {
                            if(IntelMap != null)
                            {
                                int jumps = IntelMap.JumpsOf(name.Value);
                                if(jumps != -1)
                                {
                                    return new EarlyWarningContent()
                                    {
                                        Content = chatContent.Content,
                                        Time = chatContent.EVETime,
                                        SolarSystemId = name.Value,
                                        SolarSystemName = name.Key,
                                        Level = 3,//TODO:预警等级划分
                                        IntelType = chatContent.IntelType == Enums.IntelChatType.Clear ? Enums.IntelChatType.Clear : Enums.IntelChatType.Intel,
                                        IntelMap = IntelMap,
                                        Jumps = jumps
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
            if(Setting != null)
            {
                if (IgnoreWords.NotNullOrEmpty())
                {
                    foreach (var w in IgnoreWords)
                    {
                        if (content.Content.Contains(w))
                        {
                            content.IntelType = Core.Enums.IntelChatType.Ignore; break;
                        }
                    }
                }
                if(ClearWords.NotNullOrEmpty())
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
    }
}
