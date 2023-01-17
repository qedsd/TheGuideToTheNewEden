using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models
{
    /// <summary>
    /// 聊天频道日志基本信息
    /// </summary>
    public class ChatChanelInfo
    {
        public string FilePath { get; set; }
        public string ChannelID { get; set; }
        public string ChannelName { get; set; }
        public string Listener { get; set; }
        public DateTime SessionStarted { get; set; }
        public string Folder()
        {
            return System.IO.Path.GetDirectoryName(FilePath);
        }
        public bool IsValid()
        {
            return string.IsNullOrEmpty(ChannelID) || string.IsNullOrEmpty(ChannelName) || string.IsNullOrEmpty(Listener) || SessionStarted != DateTime.MinValue;
        }
    }
}
