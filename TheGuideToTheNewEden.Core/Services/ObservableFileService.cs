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
        private static Dictionary<string, EVEFileSystemWatcher> FileWatcherDic;
        /// <summary>
        /// 所有的监控项
        /// key为文件路径
        /// </summary>
        private static Dictionary<string, IObservableFile> ItemsDic;
        public static bool Add(IObservableFile item)
        {
            if(FileWatcherDic == null)
            {
                FileWatcherDic = new Dictionary<string, EVEFileSystemWatcher>();
                ItemsDic = new Dictionary<string, IObservableFile>();
            }
            string folder = Path.GetDirectoryName(item.FilePath);
            if (!FileWatcherDic.ContainsKey(folder))//未存在文件夹监控器，新建
            {
                EVEFileSystemWatcher watcher = new EVEFileSystemWatcher(folder);
                watcher.OnChanged += Watcher_Changed;
                watcher.OnAdded += Watcher_OnAdded;
                watcher.Start();
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
        private static void Watcher_Changed(string file)
        {
            if (ItemsDic.TryGetValue(file, out var item))
            {
                item.Update();
            }
        }
        private static void Watcher_OnAdded(string file)
        {
            foreach(var item in ItemsDic)
            {
                if(item.Value.IsReplaced(file))
                {
                    ItemsDic.Remove(item.Key);
                    ItemsDic.Add(file,item.Value);
                    item.Value.Update();
                    break;
                }
            }
        }
    }

    /// <summary>
    /// 针对EVE日志文件自实现的类FileSystemWatcher
    /// </summary>
    class EVEFileSystemWatcher
    {
        private System.Timers.Timer Timer;
        private string Folder;
        private Dictionary<string, EVEFileInfo> LastFiles = new Dictionary<string, EVEFileInfo>();

        public EVEFileSystemWatcher(string folder, int interval = 100)
        {
            Folder = folder;
            Timer = new System.Timers.Timer(interval);
            Timer.Elapsed += Timer_Elapsed;
            Timer.AutoReset = false;
            Init();
        }
        public void Start()
        {
            Timer.Start();
        }
        public void Stop()
        {
            Timer.Stop();
        }
        public void Dispose()
        {
            Timer.Dispose();
        }
        private void Init()
        {
            var files = System.IO.Directory.GetFiles(Folder);
            if (files.NotNullOrEmpty())
            {
                foreach (var file in files)
                {
                    LastFiles.Add(file, new EVEFileInfo(file));
                }
            }
        }
        private void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            //挨个检查文件修改、新增
            var files = System.IO.Directory.GetFiles(Folder);
            if(files.NotNullOrEmpty())
            {
                foreach (var file in files)
                {
                    if(LastFiles.TryGetValue(file,out var value))
                    {
                        var newLength = value.FileInfo.Length;
                        if(newLength != value.Length) 
                        {
                            value.Length = newLength;
                            OnChanged?.Invoke(file);
                        }
                    }
                    else
                    {
                        LastFiles.Add(file, new EVEFileInfo(file));
                        OnAdded?.Invoke(file);
                    }
                }
            }
            Timer.Start();
        }
        public delegate void Changed(string file);
        public event Changed OnChanged;

        public delegate void Added(string file);
        public event Added OnAdded;

        class EVEFileInfo
        {
            public FileInfo FileInfo;
            public long Length;
            public EVEFileInfo(string file)
            {
                FileInfo = new FileInfo(file);
                Length = FileInfo.Length;
            }
        }
    }
}
