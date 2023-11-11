using OpenCvSharp;
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
        public GameLogSetting Setting { get; private set; }
        public string FilePath => Info.FilePath;
        public WatcherChangeTypes WatcherChangeTypes { get; set; } = WatcherChangeTypes.Created;
        public delegate void ContentUpdate(GameLogItem item, IEnumerable<GameLogContent> news);
        /// <summary>
        /// 消息更新
        /// </summary>
        public event ContentUpdate OnContentUpdate;
        private long _fileStreamOffset = 0;
        private readonly List<Regex> _regexes = new List<Regex>();
        private GameLogItem _threadErrorGameLogItem;

        public GameLogItem(GameLogInfo gameLogInfo, GameLogSetting gameLogSetting)
        {
            Info = gameLogInfo;
            Setting = gameLogSetting;
            _fileStreamOffset = FileHelper.GetStreamLength(FilePath);
            foreach (var key in Setting.Keys)
            {
                _regexes.Add(new Regex(key.Pattern));
            }
        }

        public void InitThreadErrorLog()
        {
            if (!GameLogHelper.GetGameLogDateAndThreadId(FilePath, out int dateInt, out int threadId))
            {
                Core.Log.Error($"获取{Path.GetFileNameWithoutExtension(FilePath)}的日期及进程ID错误");
            }
            else
            {
                CreateThreadLog(dateInt, threadId);
            }
        }
        private bool CreateThreadLog(int dateInt, int threadId)
        {
            string threadFile = Path.Combine(Path.GetDirectoryName(FilePath), $"{dateInt}_{threadId}.txt");
            if (File.Exists(threadFile))
            {
                if (Setting.ThreadErrorKeys.NotNullOrEmpty())
                {
                    GameLogSetting errorSetting = new GameLogSetting();
                    foreach (var r in Setting.ThreadErrorKeys)
                    {
                        errorSetting.Keys.Add(r.DepthClone<GameLogMonityKey>());
                    }
                    var errorInfo = Info.DepthClone<GameLogInfo>();
                    errorInfo.FilePath = threadFile;
                    GameLogItem threadGameLogItem = new GameLogItem(errorInfo, errorSetting);
                    ObservableFileService.Add(threadGameLogItem);
                    threadGameLogItem.OnContentUpdate += ThreadGameLogItem_OnContentUpdate;
                    Core.Log.Info($"已创建{Path.GetFileNameWithoutExtension(FilePath)}的进程日志监控");
                    _threadErrorGameLogItem = threadGameLogItem;
                    return true;
                }
                else
                {
                    Core.Log.Error($"创建{Path.GetFileNameWithoutExtension(FilePath)}的进程日志监控时，ErrorRegex为空");
                }
            }
            return false;
        }

        private void ThreadGameLogItem_OnContentUpdate(GameLogItem item, IEnumerable<GameLogContent> news)
        {
            OnContentUpdate?.Invoke(this, news);
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
            foreach(var regex in _regexes)
            {
                if(regex.IsMatch(content))
                {
                    return true;
                }
            }
            return false;
        }

        public void CreatedFile(string newfile)
        {
            if(Setting.MonitorThreadError)
            {
                if (!GameLogHelper.GetGameLogDateAndThreadId(FilePath, out int dateInt, out int threadId))
                {
                    Core.Log.Error($"获取{Path.GetFileNameWithoutExtension(FilePath)}的日期及进程ID错误");
                }
                else
                {
                    if (GameLogHelper.GetGameLogDateAndThreadId(newfile, out int newDateInt, out int newThreadId))
                    {
                        if(dateInt == newDateInt && threadId == newThreadId)
                        {
                            if(CreateThreadLog(dateInt, threadId))
                            {
                                _threadErrorGameLogItem.ResetFileStreamOffset();
                                _threadErrorGameLogItem.Update();
                            }
                        }
                    }
                }
            }
        }

        public void ResetFileStreamOffset()
        {
            _fileStreamOffset = 0;
        }

        public void Dispose()
        {
            if(_threadErrorGameLogItem != null)
            {
                ObservableFileService.Remove(_threadErrorGameLogItem.FilePath);
            }
        }
    }
}
