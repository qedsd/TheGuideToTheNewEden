using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TheGuideToTheNewEden.Core.Interfaces
{
    public interface IObservableFile
    {
        string FilePath { get;}
        WatcherChangeTypes WatcherChangeTypes { get; set; }
        /// <summary>
        /// 文件状态变化后需要执行更新操作
        /// </summary>
        void Update();
    }
}
