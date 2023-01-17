using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Models;

namespace TheGuideToTheNewEden.Core.Services
{
    public static class EarlyWarningService
    {
        /// <summary>
        /// 同一个文件夹下的文件由一个watcher监控
        /// key为文件夹路径
        /// </summary>
        private static Dictionary<string, System.IO.FileSystemWatcher> FileWatcherDic;
        /// <summary>
        /// 所有的监控项
        /// key为文件路径
        /// </summary>
        private static Dictionary<string, EarlyWarningItem> ItemsDic;
        public static void Add(EarlyWarningItem earlyWarningItem)
        {
            if(FileWatcherDic == null)
            {
                FileWatcherDic = new Dictionary<string, FileSystemWatcher>();
                ItemsDic = new Dictionary<string, EarlyWarningItem>();
            }
            if(!FileWatcherDic.ContainsKey(earlyWarningItem.ChatChanelInfo.Folder()))//未存在文件夹监控器，新建
            {
                FileSystemWatcher watcher = new FileSystemWatcher(earlyWarningItem.ChatChanelInfo.Folder());
                watcher.EnableRaisingEvents = true;
                watcher.Changed += Watcher_Changed;
                FileWatcherDic.Add(earlyWarningItem.ChatChanelInfo.Folder(), watcher);
            }
            ItemsDic.Add(earlyWarningItem.ChatChanelInfo.FilePath, earlyWarningItem);
        }
        public static void Add(List<EarlyWarningItem> items)
        {
            if(items.NotNullOrEmpty())
            {
                foreach(var item in items)
                {
                    Add(item);
                }
            }
        }
        public static void Remove(EarlyWarningItem item)
        {
            if(ItemsDic.ContainsKey(item.ChatChanelInfo.FilePath))
            {
                ItemsDic.Remove(item.ChatChanelInfo.FilePath);

            }
        }
        private static void Watcher_Changed(object sender, FileSystemEventArgs e)
        {
            if(ItemsDic.TryGetValue(e.FullPath, out var item))
            {
                item.Update();
            }
        }
    }
}
