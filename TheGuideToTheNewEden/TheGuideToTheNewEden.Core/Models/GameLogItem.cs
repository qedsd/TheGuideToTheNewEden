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
    public class GameLogItem : IObservableFile
    {
        public GameLogInfo Info { get;private set; }
        public GameLogItemConfig Setting { get; private set; }
        public string FilePath { get; set; }
        public WatcherChangeTypes WatcherChangeTypes { get; set; } = WatcherChangeTypes.Created;
        public delegate void ContentUpdate(GameLogItem item, IEnumerable<GameLogContent> news);
        /// <summary>
        /// 消息更新
        /// </summary>
        public event ContentUpdate OnContentUpdate;
        private long _fileStreamOffset = 0;

        private readonly Dictionary<GameLogMonityKey, DateTime> _keyTimes = new Dictionary<GameLogMonityKey, DateTime>();
        private GameLogItem _threadErrorGameLogItem;

        public GameLogItem(GameLogInfo gameLogInfo, GameLogItemConfig config, string filePath)
        {
            Info = gameLogInfo;
            Setting = config;
            FilePath = filePath;
            _fileStreamOffset = FileHelper.GetStreamLength(FilePath);
            foreach (var key in Setting.Keys)
            {
                _keyTimes.Add(key, DateTime.MinValue);
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
                    var newLines = Helpers.FileHelper.ReadLines(FilePath, _fileStreamOffset,Encoding.Default, out int readOffset);
                    _fileStreamOffset += readOffset;
                    if (newLines.NotNullOrEmpty())
                    {
                        List<GameLogContent> newContents = new List<GameLogContent>();
                        foreach (var line in newLines)
                        {
                            var chatContent = GameLogContent.Create(line);
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
            foreach(var key in _keyTimes)
            {
                return Regex.Match(content, key.Key.Pattern).Success;
            }
            return false;
        }

        public void CreatedFile(string newfile)
        {

        }

        public void ResetFileStreamOffset()
        {
            _fileStreamOffset = 0;
        }
    }
}
