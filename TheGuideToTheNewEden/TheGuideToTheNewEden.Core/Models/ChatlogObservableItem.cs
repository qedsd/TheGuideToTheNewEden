using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Helpers;
using TheGuideToTheNewEden.Core.Interfaces;
using TheGuideToTheNewEden.Core.Models.EVELogs;
using TheGuideToTheNewEden.Core.Services;

namespace TheGuideToTheNewEden.Core.Models
{
    public class ChatlogObservableItem : IObservableFile
    {
        public ChatChanelInfo ChatChanelInfo;
        public ChannelMonitorItem ChannelMonitorItem;
        public string FilePath => ChatChanelInfo.FilePath;
        public WatcherChangeTypes WatcherChangeTypes { get; set; } = WatcherChangeTypes.Created;
        public delegate void ContentUpdate(ChatlogObservableItem item, IEnumerable<ChatContent> news);
        /// <summary>
        /// 消息更新
        /// </summary>
        public event ContentUpdate OnContentUpdate;
        private long _fileStreamOffset = 0;
        private readonly List<Regex> _regexes = new List<Regex>();

        public ChatlogObservableItem(ChatChanelInfo chatChanelInfo, ChannelMonitorItem channelMonitorItem)
        {
            ChatChanelInfo = chatChanelInfo;
            ChannelMonitorItem = channelMonitorItem;
            _fileStreamOffset = FileHelper.GetStreamLength(FilePath);
            foreach (var key in channelMonitorItem.Setting.Keys)
            {
                _regexes.Add(new Regex(key.Pattern));
            }
        }

        public bool IsReplaced(string newfile)
        {
            return false;
        }

        public void Update()
        {
            Task.Run(() =>
            {
                try
                {
                    var newLines = Helpers.FileHelper.ReadLines(FilePath, _fileStreamOffset, Encoding.Unicode, out int readOffset);
                    _fileStreamOffset += readOffset;
                    if (newLines.NotNullOrEmpty())
                    {
                        List<ChatContent> newContents = new List<ChatContent>();
                        foreach (var line in newLines)
                        {
                            var chatContent = ChatContent.Create(line);
                            if (chatContent != null)
                            {
                                newContents.Add(chatContent);
                            }
                        }
                        if (newContents.Any())
                        {
                            foreach (var newContent in newContents)
                            {
                                newContent.Important = IsMatch(newContent.SourceContent);
                            }
                            OnContentUpdate?.Invoke(this, newContents);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            });
        }

        private bool IsMatch(string content)
        {
            foreach (var regex in _regexes)
            {
                if (regex.IsMatch(content))
                {
                    return true;
                }
            }
            return false;
        }

        public void ResetFileStreamOffset()
        {
            _fileStreamOffset = 0;
        }

        public void CreatedFile(string newfile)
        {
            
        }
    }
}
