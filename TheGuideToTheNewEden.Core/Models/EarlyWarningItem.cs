using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
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
        }
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
        private int FileStreamOffset = 0;
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
                    List<string> newLines = null;
                    using (FileStream fs = new FileStream(ChatChanelInfo.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        byte[] b = new byte[1024];
                        int curReadCount = 0;
                        StringBuilder stringBuilder = new StringBuilder();
                        fs.Position = FileStreamOffset;
                        while ((curReadCount = fs.Read(b, 0, b.Length)) > 0)
                        {
                            FileStreamOffset += curReadCount;
                            var content = Encoding.Unicode.GetString(b,0, curReadCount);
                            stringBuilder.Append(content);
                        }
                        if (stringBuilder.Length> 0)
                        {
                            newLines = stringBuilder.ToString().Split(new char[] { '\n', '\r' }).ToList();
                        }
                    }
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
                        var array = chatContent.Content.Replace('*', ' ').Split(' ');
                        if (array.Length > 0)
                        {
                            if (array.Contains(name.Key))
                            {
                                return new EarlyWarningContent()
                                {
                                    Content = chatContent.Content,
                                    Time = chatContent.EVETime,
                                    SolarSystemId = name.Value,
                                    SolarSystemName = name.Key,
                                    Level = 3,//TODO:预警等级划分
                                    IntelType = chatContent.IntelType == Enums.IntelChatType.Clear ? Enums.IntelChatType.Clear: Enums.IntelChatType.Intel,
                                };
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
