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

        /// <summary>
        /// 是否需要更新文件路径
        /// 如跨天在线，日志文件会重新创建
        /// </summary>
        /// <param name="newfile"></param>
        /// <returns></returns>
        bool IsReplaced(string newfile);
    }
}
