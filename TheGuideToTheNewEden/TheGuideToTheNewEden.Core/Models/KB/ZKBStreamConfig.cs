using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Models.KB
{
    public class ZKBStreamConfig: ObservableObject
    {
        private bool _autoConnect;
        public bool AutoConnect
        {
            get => _autoConnect;
            set => SetProperty(ref _autoConnect, value);
        }
        private bool _notify = true;
        public bool Notify
        {
            get => _notify;
            set => SetProperty(ref _notify, value);
        }

        private double _minNotifyValue = 1000000000;
        public double MinNotifyValue
        {
            get => _minNotifyValue;
            set => SetProperty(ref _minNotifyValue, value);
        }
        public HashSet<int> Types { get; set; }
        public HashSet<int> Systems { get; set; }
        public HashSet<int> Regions { get; set; }
        public HashSet<int> Characters { get; set; }
        public HashSet<int> Corps { get; set; }
        public HashSet<int> Alliances { get; set; }

        private int _maxKBItems = 100;
        public int MaxKBItems
        {
            get => _maxKBItems;
            set => SetProperty(ref _maxKBItems, value);
        }


        private int _sortWay = 0;
        /// <summary>
        /// 0：上传时间
        /// 1：发生时间
        /// </summary>
        public int SortWay
        {
            get => _sortWay;
            set => SetProperty(ref _sortWay, value);
        }

        private ObservableCollection<IdName> _commonExclusions = new ObservableCollection<IdName>();
        /// <summary>
        /// 通用排除项（星系/星域/舰船）
        /// </summary>
        public ObservableCollection<IdName> CommonExclusions
        {
            get => _commonExclusions;
            set => SetProperty(ref _commonExclusions, value);
        }

        private ObservableCollection<IdName> _commonInclusions = new ObservableCollection<IdName>();
        /// <summary>
        /// 通用包含项（星系/星域/舰船）
        /// </summary>
        public ObservableCollection<IdName> CommonInclusions
        {
            get => _commonInclusions;
            set => SetProperty(ref _commonInclusions, value);
        }

        private ObservableCollection<IdName> _victimExclusions = new ObservableCollection<IdName>();
        /// <summary>
        /// 受害者排除项（黑名单）
        /// </summary>
        public ObservableCollection<IdName> VictimExclusions
        {
            get => _victimExclusions;
            set => SetProperty(ref _victimExclusions, value);
        }

        private ObservableCollection<IdName> _victimInclusions = new ObservableCollection<IdName>();
        /// <summary>
        /// 受害者包含项（白名单）
        /// </summary>
        public ObservableCollection<IdName> VictimInclusions
        {
            get => _victimInclusions;
            set => SetProperty(ref _victimInclusions, value);
        }

        private ObservableCollection<IdName> _attackerExclusions = new ObservableCollection<IdName>();
        /// <summary>
        /// 攻击者排除项（黑名单）
        /// </summary>
        public ObservableCollection<IdName> AttackerExclusions
        {
            get => _attackerExclusions;
            set => SetProperty(ref _attackerExclusions, value);
        }

        private ObservableCollection<IdName> _attackerInclusions = new ObservableCollection<IdName>();
        /// <summary>
        /// 攻击者包含项（白名单）
        /// </summary>
        public ObservableCollection<IdName> AttackerInclusions
        {
            get => _attackerInclusions;
            set => SetProperty(ref _attackerInclusions, value);
        }

        public void EnsureRoleFiltersInitialized()
        {
            if (VictimExclusions == null) VictimExclusions = new ObservableCollection<IdName>();
            if (VictimInclusions == null) VictimInclusions = new ObservableCollection<IdName>();
            if (AttackerExclusions == null) AttackerExclusions = new ObservableCollection<IdName>();
            if (AttackerInclusions == null) AttackerInclusions = new ObservableCollection<IdName>();
            if (CommonExclusions == null) CommonExclusions = new ObservableCollection<IdName>();
            if (CommonInclusions == null) CommonInclusions = new ObservableCollection<IdName>();
        }
    }
}
