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
        public bool AutoConnect { get; set; } = false;
        public bool Notify { get; set; } = true;
        public long MinNotifyValue { get; set; } = 1000000000;
        public HashSet<int> Types { get; set; }
        public HashSet<int> Systems { get; set; }
        public HashSet<int> Regions { get; set; }
        public HashSet<int> Characters { get; set; }
        public HashSet<int> Corps { get; set; }
        public HashSet<int> Alliances { get; set; }
        public int MaxKBItems { get; set; } = 100;

        private ObservableCollection<IdName> _exclusions = new ObservableCollection<IdName>();
        /// <summary>
        /// 排除项
        /// </summary>
        public ObservableCollection<IdName> Exclusions
        {
            get => _exclusions;
            set => SetProperty(ref _exclusions, value);
        }

        private ObservableCollection<IdName> _inclusions = new ObservableCollection<IdName>();
        /// <summary>
        /// 包含项
        /// </summary>
        public ObservableCollection<IdName> Inclusions
        {
            get => _inclusions;
            set => SetProperty(ref _inclusions, value);
        }
    }
}
