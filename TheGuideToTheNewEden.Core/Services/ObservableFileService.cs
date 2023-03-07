using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Interfaces;
using TheGuideToTheNewEden.Core.Models;

namespace TheGuideToTheNewEden.Core.Services
{
    public static class ObservableFileService
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
        private static Dictionary<string, IObservableFile> ItemsDic;
        public static bool Add(IObservableFile item)
        {
            if(FileWatcherDic == null)
            {
                FileWatcherDic = new Dictionary<string, FileSystemWatcher>();
                ItemsDic = new Dictionary<string, IObservableFile>();
            }
            string folder = Path.GetDirectoryName(item.FilePath);
            if (!FileWatcherDic.ContainsKey(folder))//未存在文件夹监控器，新建
            {
                FileSystemWatcher watcher = new FileSystemWatcher(folder);
                watcher.EnableRaisingEvents = true;
                watcher.Changed += Watcher_Changed;
                FileWatcherDic.Add(folder, watcher);
            }
            if(!ItemsDic.ContainsKey(item.FilePath))
            {
                ItemsDic.Add(item.FilePath, item);
                return true;
            }
            return false;
        }
        public static void Add(List<IObservableFile> items)
        {
            if(items.NotNullOrEmpty())
            {
                foreach(var item in items)
                {
                    Add(item);
                }
            }
        }
        public static void Remove(IObservableFile item)
        {
            if(ItemsDic.ContainsKey(item.FilePath))
            {
                ItemsDic.Remove(item.FilePath);

            }
        }
        private static void Watcher_Changed(object sender, FileSystemEventArgs e)
        {
            if(ItemsDic.TryGetValue(e.FullPath, out var item))
            {
                if (e.ChangeType == item.WatcherChangeTypes)
                {
                    item.Update();
                }
            }
        }
    }
}
