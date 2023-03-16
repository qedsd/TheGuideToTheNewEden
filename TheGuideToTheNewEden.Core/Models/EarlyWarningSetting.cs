using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Text;

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

        private int intelJumps = 8;
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

        private bool makeSound;
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
    }
}
