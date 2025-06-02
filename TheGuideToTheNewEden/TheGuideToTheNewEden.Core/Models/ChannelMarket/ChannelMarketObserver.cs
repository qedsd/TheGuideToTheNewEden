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
    public class ChannelMarketObserver : IObservableFile
    {
        public string FilePath { get; private set; }
        public string CharacterName { get; private set; }
        public string KeyWord { get; private set; }
        public string ItemsSeparator { get; private set; }
        private long FileStreamOffset = 0;
        public WatcherChangeTypes WatcherChangeTypes { get; set; } = WatcherChangeTypes.Changed;
        public ChannelMarketObserver(string path, string characterName, string keyWord, string itemsSeparator)
        {
            FilePath = path;
            CharacterName = characterName;
            KeyWord = keyWord;
            ItemsSeparator = itemsSeparator;
            FileStreamOffset += FileHelper.GetStreamLength(FilePath);
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
                        List<MarketChatContent> newContents = new List<MarketChatContent>();
                        foreach (var line in newLines)
                        {
                            var chatContent = MarketChatContent.Create(line);
                            if (chatContent != null)
                            {
                                chatContent.Listener = CharacterName;
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
        public delegate void ContentUpdate(ChannelMarketObserver sender, IEnumerable<MarketChatContent> news);
        /// <summary>
        /// 消息更新
        /// </summary>
        public event ContentUpdate OnContentUpdate;


        private void AnalyzeContent(MarketChatContent chatContent)
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
                    withoutKeyWord = chatContent.Content.Replace(KeyWord, string.Empty);
                }
            }
            var array = withoutKeyWord.Split(ItemsSeparator);
            chatContent.Items = new List<DBModels.InvTypeBase>();
            foreach (var item in array)
            {
                if(string.IsNullOrEmpty(item)) continue;
                var type = InvTypeService.QueryInvType(item.Trim());
                if(type != null)
                {
                    chatContent.Items.Add(type);
                }
            }
            chatContent.Important = chatContent.Items.Count > 0;
        }

        public bool IsReplaced(string file)
        {
            return false;
        }
        public void CreatedFile(string newfile)
        {

        }
    }
}
