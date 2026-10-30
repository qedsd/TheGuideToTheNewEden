using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Models.KB
{
    /// <summary>ZKB 过滤器单个维度的模式。</summary>
    public enum ZkbFilterMode
    {
        /// <summary>不过滤。</summary>
        None = 0,
        /// <summary>只保留命中列表的击杀；列表为空时不限制。</summary>
        Include = 1,
        /// <summary>排除命中列表的击杀；列表为空时不限制。</summary>
        Exclude = 2,
    }

    public class ZKBStreamConfig : ObservableObject
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

        private ZkbFilterMode _locationMode = ZkbFilterMode.None;
        /// <summary>位置过滤模式（星系 / 星域）。</summary>
        public ZkbFilterMode LocationMode
        {
            get => _locationMode;
            set => SetProperty(ref _locationMode, value);
        }

        private ObservableCollection<IdName> _locations = new ObservableCollection<IdName>();
        /// <summary>位置过滤列表（星系 / 星域；列表内 OR）。</summary>
        public ObservableCollection<IdName> Locations
        {
            get => _locations;
            set => SetProperty(ref _locations, value);
        }

        private ZkbFilterMode _shipMode = ZkbFilterMode.None;
        /// <summary>被击杀舰船类型过滤模式。</summary>
        public ZkbFilterMode ShipMode
        {
            get => _shipMode;
            set => SetProperty(ref _shipMode, value);
        }

        private ObservableCollection<IdName> _ships = new ObservableCollection<IdName>();
        /// <summary>被击杀舰船类型（具体 TypeID；列表内 OR，父分类由 UI 展开后加入）。</summary>
        public ObservableCollection<IdName> Ships
        {
            get => _ships;
            set => SetProperty(ref _ships, value);
        }

        private ZkbFilterMode _victimMode = ZkbFilterMode.None;
        /// <summary>受害者过滤模式。</summary>
        public ZkbFilterMode VictimMode
        {
            get => _victimMode;
            set => SetProperty(ref _victimMode, value);
        }

        private ObservableCollection<IdName> _victims = new ObservableCollection<IdName>();
        /// <summary>受害者过滤列表（角色 / 军团 / 联盟；列表内 OR）。</summary>
        public ObservableCollection<IdName> Victims
        {
            get => _victims;
            set => SetProperty(ref _victims, value);
        }

        private ZkbFilterMode _attackerMode = ZkbFilterMode.None;
        /// <summary>攻击者过滤模式。</summary>
        public ZkbFilterMode AttackerMode
        {
            get => _attackerMode;
            set => SetProperty(ref _attackerMode, value);
        }

        private ObservableCollection<IdName> _attackers = new ObservableCollection<IdName>();
        /// <summary>攻击者过滤列表（角色 / 军团 / 联盟；任一攻击者命中即可）。</summary>
        public ObservableCollection<IdName> Attackers
        {
            get => _attackers;
            set => SetProperty(ref _attackers, value);
        }

        public void EnsureRoleFiltersInitialized()
        {
            Locations ??= new ObservableCollection<IdName>();
            Ships ??= new ObservableCollection<IdName>();
            Victims ??= new ObservableCollection<IdName>();
            Attackers ??= new ObservableCollection<IdName>();
        }
    }
}
