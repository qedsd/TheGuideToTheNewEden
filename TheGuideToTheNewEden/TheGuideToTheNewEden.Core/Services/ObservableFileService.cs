using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Interfaces;
using TheGuideToTheNewEden.Core.Models;

namespace TheGuideToTheNewEden.Core.Services
{
    public static class ObservableFileService
    {
        public delegate void Added(string file);
        public static event Added OnAdded;
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
            if(FileWatcherDic.TryGetValue(folder,out var watcher))
            {
                if (!ItemsDic.ContainsKey(item.FilePath))
                {
                    ItemsDic.Add(item.FilePath, item);
                    watcher.AddFile(item.FilePath);
                    return true;
                }
            }
            else//未存在文件夹监控器，新建
            {
                EVEFileSystemWatcher newWatcher = new EVEFileSystemWatcher(folder);
                newWatcher.OnChanged += Watcher_Changed;
                newWatcher.OnAdded += Watcher_OnAdded;
                newWatcher.Start();
                FileWatcherDic.Add(folder, newWatcher);
                ItemsDic.Add(item.FilePath, item);
                newWatcher.AddFile(item.FilePath);
                return true;
            }
            
            return false;
        }

        public static void Add(IEnumerable<IObservableFile> items)
        {
            if(items.NotNullOrEmpty())
            {
                foreach(var item in items)
                {
                    Add(item);
                }
            }
        }
        public static void Remove(IEnumerable<IObservableFile> items)
        {
            if (items.NotNullOrEmpty())
            {
                foreach (var item in items)
                {
                    Remove(item);
                }
            }
        }
        public static void Remove(IObservableFile item)
        {
            if(item != null && ItemsDic.Remove(item.FilePath))
            {
                string folder = Path.GetDirectoryName(item.FilePath);
                if (FileWatcherDic.TryGetValue(folder, out var watcher))
                {
                    watcher.RemoveFile(item.FilePath);
                }
            }
        }
        public static void Remove(string filePath)
        {
            if (ItemsDic.Remove(filePath))
            {
                string folder = Path.GetDirectoryName(filePath);
                if (FileWatcherDic.TryGetValue(folder, out var watcher))
                {
                    watcher.RemoveFile(filePath);
                }
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
            foreach(var item in ItemsDic.ToList())
            {
                if(item.Value.IsReplaced(file))
                {
                    if (FileWatcherDic.TryGetValue(Path.GetDirectoryName(file), out var watcher))
                    {
                        watcher.RemoveFile(item.Key);
                        watcher.AddFile(file);
                        ItemsDic.Remove(item.Key);
                        ItemsDic.Add(file, item.Value);
                        item.Value.Update();
                    }
                    break;
                }
                item.Value.CreatedFile(file);
            }
            OnAdded?.Invoke(file);
        }
    }

    /// <summary>
    /// 针对EVE日志文件自实现的类FileSystemWatcher
    /// 实现为Timer循环判断监控文件大小和FileSystemWatcher检测新增文件
    /// </summary>
    class EVEFileSystemWatcher
    {
        private System.Timers.Timer Timer;
        private string Folder;
        /// <summary>
        /// 监控中的文件及对应的大小
        /// </summary>
        private Dictionary<string, ulong> WatchingFiles = new Dictionary<string, ulong>();
        /// <summary>
        /// 该文件夹上一次检查时所有的文件
        /// </summary>
        private HashSet<string> AllFiles = new HashSet<string>();
        private FileSystemWatcher FileSystemWatcher;
        public EVEFileSystemWatcher(string folder, int interval = 300)
        {
            Folder = folder;
            Init();

            //检测文件更新
            Timer = new System.Timers.Timer(interval);
            Timer.Elapsed += Timer_Elapsed;
            Timer.AutoReset = false;

            //检查文件新增
            FileSystemWatcher = new FileSystemWatcher()
            {
                Path = folder,
                NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                Filter = "*.txt",
                EnableRaisingEvents = true
            };
            FileSystemWatcher.Created += FileSystemWatcher_Created;
        }

        private void FileSystemWatcher_Created(object sender, FileSystemEventArgs e)
        {
            OnAdded?.Invoke(e.FullPath);
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
        public void AddFile(string file)
        {
            WatchingFiles.Add(file, Helpers.FileHelper.GetFileLength(file));
        }
        public void RemoveFile(string file)
        {
            WatchingFiles.Remove(file);
        }
        private void Init()
        {
            var files = System.IO.Directory.GetFiles(Folder);
            if (files.NotNullOrEmpty())
            {
                foreach (var file in files)
                {
                    AllFiles.Add(file);
                }
            }
        }
        private void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            //判断监控中的文件是否有更新
            //监控外的文件无视，避免过多文件时消耗性能
            foreach (var file in WatchingFiles.ToList())
            {
                var newLength = Helpers.FileHelper.GetFileLength(file.Key);
                if (newLength != file.Value)
                {
                    WatchingFiles.Remove(file.Key);
                    WatchingFiles.Add(file.Key, newLength);
                    OnChanged?.Invoke(file.Key);
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
