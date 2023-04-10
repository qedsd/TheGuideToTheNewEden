using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.GamePreviews
{
    public class PreviewItem : ObservableObject
    {

        private string name;
        /// <summary>
        /// 标记id
        /// 默认为游戏角色名
        /// </summary>
        public string Name
        {
            get => name;
            set => SetProperty(ref name, value);
        }
        /// <summary>
        /// 窗口透明度
        /// 0-100
        /// </summary>
        private int overlapOpacity = 100;
        public int OverlapOpacity
        {
            get => overlapOpacity;
            set => SetProperty(ref overlapOpacity, value);
        }

        private int winX = -1;
        public int WinX
        {
            get => winX;
            set => SetProperty(ref winX, value);
        }

        private int winY = -1;
        public int WinY
        {
            get => winY;
            set => SetProperty(ref winY, value);
        }

        private int winW = 0;
        public int WinW
        {
            get => winW;
            set => SetProperty(ref winW, value);
        }

        private int winH = 0;
        public int WinH
        {
            get => winH;
            set => SetProperty(ref winH, value);
        }

        private bool showTitleBar = true;
        public bool ShowTitleBar
        {
            get => showTitleBar;
            set => SetProperty(ref showTitleBar, value);
        }

        private string hotKey;
        /// <summary>
        /// 快捷键
        /// 以+分隔
        /// </summary>
        public string HotKey
        {
            get => hotKey;
            set => SetProperty(ref hotKey, value);
        }
        private ProcessInfo processInfo;
        [JsonIgnore]
        public ProcessInfo ProcessInfo
        {
            get => processInfo; set => SetProperty(ref processInfo, value);
        }
    }
}
