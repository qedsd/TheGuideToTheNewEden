using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Interfaces;

namespace TheGuideToTheNewEden.Core.Models
{
    public class EarlyWarningItem: IObservableFile
    {
        public EarlyWarningItem(ChatChanelInfo info)
        {
            ChatChanelInfo = info;
            Contents = new ObservableCollection<string>();
        }
        public ChatChanelInfo ChatChanelInfo { get; set; }
        /// <summary>
        /// 原始聊天内容
        /// </summary>
        public ObservableCollection<string> Contents { get; set; }
        public WatcherChangeTypes WatcherChangeTypes { get; set; } = WatcherChangeTypes.Changed;
        public string FilePath
        {
            get => ChatChanelInfo.FilePath;
        }
        private int lastLineIndex;
        /// <summary>
        /// 文件内容有更新
        /// </summary>
        public void Update()
        {
            Task.Run(() =>
            {
                try
                {
                    var allLines = System.IO.File.ReadLines(ChatChanelInfo.FilePath);
                    if (allLines != null)
                    {
                        var newLines = allLines.Skip(lastLineIndex + 1);
                        if (newLines.NotNullOrEmpty())
                        {
                            lastLineIndex += newLines.Count();
                            List<string> contents = new List<string>();
                            foreach (var line in newLines)
                            {
                                if (line.StartsWith("﻿[ "))
                                {
                                    contents.Add(line);
                                    //TODO:分析
                                }
                            }
                            //TODO:UI线程
                            foreach(var line in newLines)
                            {
                                Contents.Add(line);
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
    }
}
