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
            // 阶段 357：游戏日志会随会话/跨天新建文件，原实现恒 false → 监控中的会话永远读旧文件
            // （日志监控页原来只在"开始"时刷新一次，运行中轮换就断流，属于既有缺陷）。
            // 这里按文件名（yyyyMMdd_HHmmss_角色ID.txt）判断：同角色、更新的时间 → 换成新文件并从 0 读。
            if (string.IsNullOrWhiteSpace(newfile)
                || string.Equals(newfile, FilePath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var fileInfo = GameLogHelper.GetGameLogFileInfo(newfile);
            if (fileInfo is null || fileInfo.ListenerID != Info.ListenerID || fileInfo.StartTime <= Info.StartTime)
            {
                return false;
            }

            Info.FilePath = newfile;
            Info.StartTime = fileInfo.StartTime;
            FilePath = newfile;
            _fileStreamOffset = 0;
            return true;
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
            // 逐个关键词匹配，命中任意一个即为命中。
            // 原实现 foreach 第一轮就 return，导致**只有第一个关键词生效**（其余全部失效）。
            foreach (var key in _keyTimes)
            {
                try
                {
                    if (Regex.IsMatch(content, key.Key.Pattern))
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    // 单个关键词的正则非法不应中断整条日志的匹配
                    Console.WriteLine(ex.Message);
                }
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
