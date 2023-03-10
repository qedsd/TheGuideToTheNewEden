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
        public EarlyWarningItem(ChatChanelInfo info)
        {
            ChatChanelInfo = info;
        }
        public ChatChanelInfo ChatChanelInfo { get; set; }
        /// <summary>
        /// 原始聊天内容
        /// </summary>
        public List<ChatContent> Contents { get;private set; } = new List<ChatContent>();
        public List<EarlyWarningContent> Warnings { get;private set; } = new List<EarlyWarningContent>();
        public WatcherChangeTypes WatcherChangeTypes { get; set; } = WatcherChangeTypes.Changed;
        public string FilePath
        {
            get => ChatChanelInfo.FilePath;
        }
        private int FileStreamOffset = 0;
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
                        StringBuilder stringBuilder= new StringBuilder();
                        fs.Position = FileStreamOffset;
                        while ((curReadCount = fs.Read(b, 0, b.Length)) > 0)
                        {
                            FileStreamOffset += curReadCount;
                            var content = Encoding.Unicode.GetString(b);
                            stringBuilder.Append(content);
                        }
                        if(stringBuilder.Length> 0)
                        {
                            newLines = stringBuilder.ToString().Split(new char[] { '\n', '\r' }).ToList();
                        }
                    }
                    if (newLines.NotNullOrEmpty())
                    {
                        List<ChatContent> newContents = new List<ChatContent>();
                        foreach (var line in newLines)
                        {
                            var chatContent = ChatContent.Create(line);
                            if(chatContent != null)
                            {
                                newContents.Add(chatContent);
                            }
                        }
                        if(newContents.Count > 0)
                        {
                            Contents.AddRange(newContents);
                            OnContentUpdate?.Invoke(this, newContents);
                            List<EarlyWarningContent> newWarning = new List<EarlyWarningContent>();
                            foreach (var newContent in newContents)
                            {
                                var result = AnalyzeContent(newContent.Content);
                                if (result != null)
                                {
                                    newWarning.Add(result);
                                }
                            }
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
        public delegate void ContentUpdate(EarlyWarningItem earlyWarningItem,IEnumerable<ChatContent> news);
        /// <summary>
        /// 消息更新
        /// </summary>
        public event ContentUpdate OnContentUpdate;

        public delegate void WarningUpdate(EarlyWarningItem earlyWarningItem, IEnumerable<EarlyWarningContent> news);
        /// <summary>
        /// 预警更新
        /// </summary>
        public event WarningUpdate OnWarningUpdate;

        private EarlyWarningContent AnalyzeContent(string str)
        {
            //TODO:分析
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
    }
}
