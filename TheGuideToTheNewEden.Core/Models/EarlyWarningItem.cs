using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models
{
    public class EarlyWarningItem
    {
        public EarlyWarningItem(ChatChanelInfo info)
        {
            ChatChanelInfo = info;
        }
        public ChatChanelInfo ChatChanelInfo { get; set; }
        /// <summary>
        /// 文件内容有更新
        /// </summary>
        public void Update()
        {

        }
    }
}
