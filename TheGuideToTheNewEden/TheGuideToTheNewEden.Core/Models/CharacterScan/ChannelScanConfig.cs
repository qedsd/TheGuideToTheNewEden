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
        private bool _activeIgnoreList = true;
        /// <summary>
        /// 是否启用过滤列表
        /// </summary>
        public bool ActiveIgnoreList { get => _activeIgnoreList; set => SetProperty(ref _activeIgnoreList, value); }

        private bool _showIgnoredInResultDetail;
        /// <summary>
        /// 是否把过滤列表中的显示在结果详细内
        /// </summary>
        public bool ShowIgnoredInResultDetail { get => _showIgnoredInResultDetail; set => SetProperty(ref _showIgnoredInResultDetail, value); }

        private bool _showIgnoredInResultStatistics;
        /// <summary>
        /// 是否把过滤列表中的显示在结果统计内
        /// </summary>
        public bool ShowIgnoredInResultStatistics { get => _showIgnoredInResultStatistics; set => SetProperty(ref _showIgnoredInResultStatistics, value); }

        private bool _getZKB = true;
        /// <summary>
        /// 获取zkb信息
        /// </summary>
        public bool GetZKB { get => _getZKB; set => SetProperty(ref _getZKB, value); }

        private ObservableCollection<IdName> _ignoreds = new ObservableCollection<IdName>();
        /// <summary>
        /// 过滤列表，在列表内的不进行分析
        /// 角色、军团、联盟
        /// </summary>
        public ObservableCollection<IdName> Ignoreds { get => _ignoreds; set => SetProperty(ref _ignoreds, value); }
    }
}
