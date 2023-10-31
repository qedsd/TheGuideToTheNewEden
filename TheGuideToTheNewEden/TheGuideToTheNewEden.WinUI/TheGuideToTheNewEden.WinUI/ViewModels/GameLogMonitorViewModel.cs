using CommunityToolkit.Mvvm.Input;
using ESI.NET.Models.Character;
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
using TheGuideToTheNewEden.WinUI.Services;
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
                        if(setting == null)
                        {
                            GameLogSetting = new GameLogSetting()
                            {
                                ListenerID = value.ListenerID
                            };
                            GameLogSetting.Keys.Add(new GameLogMonityKey("combat"));
                            Services.Settings.GameLogInfoSettingService.SetValue(GameLogSetting);
                        }
                        else
                        {
                            GameLogSetting = setting;
                        }
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
        public ICommand RefreshCommand => new RelayCommand(() =>
        {
            InitAsync();
        });
        public ICommand StartCommand => new RelayCommand(() =>
        {
            if(!GameLogSetting.Keys.Any())
            {
                ShowError(Helpers.ResourcesHelper.GetString("GameLogMonitorPage_NoneKeyError"));
                return;
            }
            if(GameLogMonitorNotifyService.Current.Add(GameLogSetting, SelectedGameLogInfo.ListenerName))
            {
                Core.Models.GameLogItem gameLogItem = new GameLogItem(SelectedGameLogInfo, GameLogSetting);
                Core.Services.ObservableFileService.Add(gameLogItem);
                gameLogItem.OnContentUpdate += GameLogItem_OnContentUpdate;
                SelectedGameLogInfo.Running = true;
                Services.Settings.GameLogInfoSettingService.Save();
            }
            else
            {
                ShowError("添加通知服务失败",false);
            }
        });
        public delegate void ContentUpdate(GameLogItem item, IEnumerable<GameLogContent> news);
        /// <summary>
        /// 消息更新
        /// </summary>
        public event ContentUpdate OnContentUpdate;
        private void GameLogItem_OnContentUpdate(GameLogItem item, IEnumerable<Core.Models.EVELogs.GameLogContent> news)
        {
            OnContentUpdate?.Invoke(item, news);
            foreach(var msg in news)
            {
                if(msg.Important)
                {
                    GameLogMonitorNotifyService.Current.Notify(item, msg.SourceContent);
                }
            }
        }

        public ICommand AddKeysCommand => new RelayCommand(() =>
        {
            GameLogSetting.Keys.Add(new GameLogMonityKey("系统消息"));
        });
        public ICommand StopNotifyCommand => new RelayCommand(() =>
        {
            GameLogMonitorNotifyService.Current.Stop(GameLogSetting.ListenerID);
        });
        public ICommand StopCommand => new RelayCommand(() =>
        {
            GameLogMonitorNotifyService.Current.Stop(GameLogSetting.ListenerID);
            GameLogMonitorNotifyService.Current.Remove(GameLogSetting.ListenerID);
            Core.Services.ObservableFileService.Remove(SelectedGameLogInfo.FilePath);
            SelectedGameLogInfo.Running = false;
        });
        public ICommand PickSoundFileCommand => new RelayCommand(async () =>
        {
            var file = await Helpers.PickHelper.PickFileAsync(Window);
            if (file != null)
            {
                GameLogSetting.SoundFile = file.Path;
            }
        });
        public void Dispose()
        {
            GameLogInfos.Where(p => p.Running).ToList().ForEach(p =>
            {
                GameLogMonitorNotifyService.Current.Stop(p.ListenerID);
                GameLogMonitorNotifyService.Current.Remove(p.ListenerID);
                Core.Services.ObservableFileService.Remove(p.FilePath);
                p.Running = false;
            });
        }
    }
}
