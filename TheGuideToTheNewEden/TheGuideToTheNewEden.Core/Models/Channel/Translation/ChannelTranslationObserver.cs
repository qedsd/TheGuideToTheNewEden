using Microsoft.IdentityModel.Tokens;
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

namespace TheGuideToTheNewEden.Core.Models.ChannelMarket
{
    public class ChannelTranslationObserver : IObservableFile
    {
        public string FilePath { get; private set; }
        public string CharacterName { get; private set; }
        public string KeyWord { get; private set; }
        private long FileStreamOffset = 0;
        public WatcherChangeTypes WatcherChangeTypes { get; set; } = WatcherChangeTypes.Changed;

        private ChatChanelInfo _chatChanelInfo;
        public ChannelTranslationObserver(string path, string characterName, string keyWord)
        {
            FilePath = path;
            CharacterName = characterName;
            KeyWord = keyWord;
            FileStreamOffset += FileHelper.GetStreamLength(FilePath);
            _chatChanelInfo = GameLogHelper.GetChatChanelInfo(path);
        }

        /// <summary>
        /// 文件内容有更新
        /// </summary>
        public void Update()
        {
            Task.Run(() =>
            {
                try
                {
                    var newLines = Helpers.FileHelper.ReadLines(FilePath, FileStreamOffset, Encoding.Unicode, out int readOffset);
                    FileStreamOffset += readOffset;
                    if (newLines.NotNullOrEmpty())
                    {
                        List<ChatContent> newContents = new List<ChatContent>();
                        foreach (var line in newLines)
                        {
                            var chatContent = ChatContent.Create(line);
                            if (chatContent != null)
                            {
                                chatContent.Listener = CharacterName;
                                chatContent.ChannelName = _chatChanelInfo.ChannelName;
                                chatContent.ChannelID = _chatChanelInfo.ChannelID;
                                newContents.Add(chatContent);
                            }
                        }
                        if (newContents.Any())
                        {
                            foreach (var newContent in newContents)
                            {
                                AnalyzeContent(newContent);
                            }
                            OnContentUpdate?.Invoke(this, newContents);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }
            });
        }
        public event EventHandler<IEnumerable<ChatContent>> OnContentUpdate;

        private void AnalyzeContent(ChatContent chatContent)
        {
            string withoutKeyWord = chatContent.Content;
            if (!string.IsNullOrEmpty(KeyWord))
            {
                int keyWordIndex = chatContent.Content.IndexOf(KeyWord);
                if(keyWordIndex < 0)
                {
                    return;
                }
                else
                {
                    chatContent.Important = true;
                }
            }
            chatContent.Important = true;
        }

        public bool IsReplaced(string file)
        {
            var newChanelInfo = GameLogHelper.GetChatChanelInfo(file);
            if (newChanelInfo != null
                && newChanelInfo.Listener == _chatChanelInfo.Listener
                && newChanelInfo.ChannelName == _chatChanelInfo.ChannelName
                && newChanelInfo.SessionStarted > _chatChanelInfo.SessionStarted)
            {
                _chatChanelInfo = newChanelInfo;
                FilePath = file;
                FileStreamOffset = 0;
                return true;
            }
            else
            {
                return false;
            }
        }
        public void CreatedFile(string newfile)
        {
            
        }
    }
}
