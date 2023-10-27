using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using TheGuideToTheNewEden.Core.Helpers;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.Models.EVELogs;
using TheGuideToTheNewEden.WinUI.Services.Settings;

namespace TheGuideToTheNewEden.WinUI.ViewModels
{
    internal class GameLogMonitorViewModel : BaseViewModel
    {
        private readonly string _logPath = System.IO.Path.Combine(EVELogsPathSelectorService.Value, "Gamelogs");
        public ObservableCollection<GameLogInfo> GameLogInfos { get; } = new ObservableCollection<GameLogInfo>();
        private GameLogInfo _selectedGameLogInfo;
        public GameLogInfo SelectedGameLogInfo
        {
            get => _selectedGameLogInfo;
            set
            {
                if(SetProperty(ref _selectedGameLogInfo, value))
                {
                    if(value != null)
                    {
                        var setting = Services.Settings.GameLogInfoSettingService.GetValue(value.ListenerID);
                        GameLogSetting = setting ?? new GameLogSetting() { ListenerID = value.ListenerID };
                    }
                    else
                    {
                        GameLogSetting = null;
                    }
                }
            }
        }
        private GameLogSetting gameLogSetting;
        public GameLogSetting GameLogSetting
        {
            get => gameLogSetting;
            set => SetProperty(ref  gameLogSetting, value);
        }
        internal GameLogMonitorViewModel()
        {
            InitAsync();
        }

        private async void InitAsync()
        {
            var runningDic = GameLogInfos.Where(p => p.Running).ToDictionary(p=>p.ListenerID);
            GameLogInfos.Clear();
            if (System.IO.Directory.Exists(_logPath))
            {
                ShowWaiting();
                var infos = await Task.Run(() => GameLogHelper.GetLatestGameLogInfos(_logPath));
                HideWaiting();
                if (infos != null)
                {
                    foreach (var info in infos)
                    {
                        if(runningDic.TryGetValue(info.ListenerID, out var running))
                        {
                            GameLogInfos.Add(running);
                        }
                        else
                        {
                            GameLogInfos.Add(info);
                        }
                    }
                }
                SelectedGameLogInfo = null;
            }
        }

        public ICommand StartCommand => new RelayCommand(() =>
        {
            Core.Models.GameLogItem gameLogItem = new GameLogItem(SelectedGameLogInfo, GameLogSetting);
            Core.Services.ObservableFileService.Add(gameLogItem);
            gameLogItem.OnContentUpdate += GameLogItem_OnContentUpdate;
        });
        public delegate void ContentUpdate(GameLogItem item, IEnumerable<ChatContent> news);
        /// <summary>
        /// 消息更新
        /// </summary>
        public event ContentUpdate OnContentUpdate;
        private void GameLogItem_OnContentUpdate(GameLogItem item, IEnumerable<Core.Models.EVELogs.ChatContent> news)
        {
            OnContentUpdate?.Invoke(item, news);
            foreach(var msg in news)
            {

            }
        }

        public ICommand AddKeysCommand => new RelayCommand(() =>
        {
            GameLogSetting.keys.Add("系统消息");
        });
    }
}
