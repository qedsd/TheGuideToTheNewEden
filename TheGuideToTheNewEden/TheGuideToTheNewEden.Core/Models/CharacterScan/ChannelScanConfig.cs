using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Models.CharacterScan
{
    public class ChannelScanConfig : ObservableObject
    {
        private bool _activeIgnoreList;
        /// <summary>
        /// 是否启用过滤列表
        /// </summary>
        public bool ActiveIgnoreList { get => _activeIgnoreList; set => SetProperty(ref _activeIgnoreList, value); }

        private bool _showIgnoredInResult;
        /// <summary>
        /// 是否把过滤列表中的显示在结果内
        /// </summary>
        public bool ShowIgnoredInResult { get => _showIgnoredInResult; set => SetProperty(ref _showIgnoredInResult, value); }

        private ObservableCollection<IdName> _ignoreds;
        /// <summary>
        /// 过滤列表，在列表内的不进行分析
        /// 角色、军团、联盟
        /// </summary>
        public ObservableCollection<IdName> Ignoreds { get => _ignoreds; set => SetProperty(ref _ignoreds, value); }
    }
    public class ChannelScanIgonre : ObservableObject
    {
        private int _id;
        public int Id { get => _id; set => SetProperty(ref _id, value); }

        private string _name;
        public string Name { get => _name; set => SetProperty(ref _name, value); }
    }
}
