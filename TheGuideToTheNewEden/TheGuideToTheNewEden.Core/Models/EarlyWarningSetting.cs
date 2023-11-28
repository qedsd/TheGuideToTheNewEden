using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using TheGuideToTheNewEden.Core.Enums;

namespace TheGuideToTheNewEden.Core.Models
{
    /// <summary>
    /// 预警设置
    /// </summary>
    public class EarlyWarningSetting: ObservableObject
    {
        private string listener;
        /// <summary>
        /// 角色
        /// </summary>
        public string Listener
        {
            get => listener; 
            set => SetProperty(ref listener, value);
        }

        private List<string> channelID = new List<string>();
        /// <summary>
        /// 监听的频道
        /// </summary>
        public List<string> ChannelIDs
        {
            get => channelID;
            set=> SetProperty(ref channelID, value);
        }

        private int locationID;
        /// <summary>
        /// 位置星系id
        /// </summary>
        public int LocationID
        {
            get => locationID;
            set => SetProperty(ref locationID, value);
        }

        private bool autoUpdateLocaltion = true;
        /// <summary>
        /// 自动依据本地频道更新位置星系
        /// </summary>
        public bool AutoUpdateLocaltion
        {
            get => autoUpdateLocaltion;
            set => SetProperty(ref autoUpdateLocaltion, value);
        }

        private int intelJumps = 4;
        /// <summary>
        /// 预警跳数
        /// </summary>
        public int IntelJumps
        {
            get => intelJumps; set => SetProperty(ref intelJumps, value);
        }

        private int overlapType;
        /// <summary>
        /// 预警窗口显示方式
        /// 0 一直显示 1 必要时 2 不显示
        /// </summary>
        public int OverlapType
        {
            get => overlapType; set => SetProperty(ref overlapType, value);
        }
        private int overlapStyle;
        public int OverlapStyle { get => overlapStyle; set => SetProperty(ref overlapStyle, value); }
        private bool makeSound = true;
        /// <summary>
        /// 预警声
        /// </summary>
        public bool MakeSound
        {
            get => makeSound; set => SetProperty(ref makeSound, value);
        }

        private string soundFilePath;
        /// <summary>
        /// 预警声文件
        /// </summary>
        public string SoundFilePath
        {
            get => soundFilePath; set => SetProperty(ref soundFilePath, value);
        }

        public ObservableCollection<string> SoundFiles { get; set; } = new ObservableCollection<string>();

        private bool systemNotify = true;
        /// <summary>
        /// 系统消息通知
        /// </summary>
        public bool SystemNotify
        {
            get => systemNotify;set => SetProperty(ref systemNotify, value);
        }

        private List<string> nameDbs = new List<string>();
        /// <summary>
        /// 星系名语言数据库
        /// </summary>
        public List<string> NameDbs
        {
            get => nameDbs; set => SetProperty(ref nameDbs, value);
        }

        private string ignoreWords = "status";
        /// <summary>
        /// 忽视关键词
        /// </summary>
        public string IgnoreWords
        {
            get => ignoreWords; set => SetProperty(ref ignoreWords, value);
        }

        private string clearWords = "clr,clear";
        /// <summary>
        /// 接触预警关键词
        /// </summary>
        public string ClearWords
        {
            get => clearWords; set => SetProperty(ref clearWords, value);
        }

        private bool autoClear;
        /// <summary>
        /// 自动清除预警
        /// </summary>
        public bool AutoClear
        {
            get => autoClear;
            set => SetProperty(ref autoClear, value);
        }

        private double autoClearMinute = 20;
        /// <summary>
        /// 自动清除预警
        /// </summary>
        public double AutoClearMinute
        {
            get => autoClearMinute;
            set => SetProperty(ref autoClearMinute, value);
        }

        /// <summary>
        /// 自动减低预警
        /// </summary>
        private bool autoDowngrade;
        public bool AutoDowngrade
        {
            get => autoDowngrade;
            set => SetProperty(ref autoDowngrade, value);
        }
        /// <summary>
        /// 自动减低预警
        /// </summary>
        private double autoDowngradeMinute = 10;
        public double AutoDowngradeMinute
        {
            get => autoDowngradeMinute;
            set => SetProperty(ref autoDowngradeMinute, value);
        }

        /// <summary>
        /// 预警窗口透明度
        /// 0-100
        /// </summary>
        private int overlapOpacity = 80;
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

        private int winW = 500;
        public int WinW
        {
            get => winW;
            set => SetProperty(ref winW, value);
        }

        private int winH = 500;
        public int WinH
        {
            get => winH;
            set => SetProperty(ref winH, value);
        }
    }
}
