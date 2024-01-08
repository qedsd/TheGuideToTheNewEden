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
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.WinUI.ViewModels
{
    internal class GameLogMonitorViewModel : BaseViewModel
    {
        private readonly string _logPath = GameLogsSettingService.GetGamelogsPath();
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
                            var errorRegex = ReadErrorRegex();
                            if (errorRegex.NotNullOrEmpty())
                            {
                                foreach (var regex in errorRegex)
                                {
                                    GameLogSetting.ThreadErrorKeys.Add(new GameLogMonityKey(regex));
                                }
                            }
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

        private Core.Services.GameLogDelayMonitorService _gameLogDelayMonitorService;
        private readonly Dictionary<int, Core.Models.GameLogItem> _gameLogItems = new Dictionary<int, GameLogItem>();
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
            if(GameLogMonitorNotifyService.Current.Add(SelectedGameLogInfo, GameLogSetting, SelectedGameLogInfo.ListenerName))
            {
                Core.Models.GameLogItem gameLogItem = new GameLogItem(SelectedGameLogInfo, GameLogSetting);
                _gameLogItems.Remove(SelectedGameLogInfo.ListenerID);
                _gameLogItems.Add(SelectedGameLogInfo.ListenerID, gameLogItem);
                Core.Services.ObservableFileService.Add(gameLogItem);
                gameLogItem.OnContentUpdate += GameLogItem_OnContentUpdate;
                if(GameLogSetting.MonitorThreadError)
                {
                    gameLogItem.InitThreadErrorLog();
                }
                SelectedGameLogInfo.Running = true;
                Services.Settings.GameLogInfoSettingService.SetValue(GameLogSetting);
                if(GameLogSetting.MonitorMode == 1)
                {
                    if(_gameLogDelayMonitorService == null)
                    {
                        _gameLogDelayMonitorService = new Core.Services.GameLogDelayMonitorService();
                        _gameLogDelayMonitorService.OnGameLogDelayExpire += GameLogDelayMonitorService_OnGameLogDelayExpire;
                    }
                }
            }
            else
            {
                ShowError("添加通知服务失败",false);
            }
        });

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
            if(_gameLogItems.TryGetValue(SelectedGameLogInfo.ListenerID,out var item))
            {
                item.Dispose();
            }
            SelectedGameLogInfo.Running = false;
            _gameLogItems.Remove(SelectedGameLogInfo.ListenerID);
        });
        public ICommand PickSoundFileCommand => new RelayCommand(async () =>
        {
            var file = await Helpers.PickHelper.PickFileAsync(Window);
            if (file != null)
            {
                GameLogSetting.SoundFile = file.Path;
            }
        });

        public ICommand AddThreadErrorKeysCommand => new RelayCommand(() =>
        {
            GameLogSetting.ThreadErrorKeys.Add(new GameLogMonityKey("接口被关闭了"));
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
            foreach(var item in _gameLogItems)
            {
                item.Value.Dispose();
            }
        }

        #region 消息处理
        public delegate void ContentUpdate(GameLogItem item, IEnumerable<GameLogContent> news);
        /// <summary>
        /// 消息更新
        /// </summary>
        public event ContentUpdate OnContentUpdate;
        private void GameLogDelayMonitorService_OnGameLogDelayExpire(int id)
        {
            Window.DispatcherQueue.TryEnqueue(() =>
            {
                GameLogMonitorNotifyService.Current.Notify(_gameLogItems[id], Helpers.ResourcesHelper.GetString("GameLogMonitorPage_DelayExpireTip"));
            });
        }
        private void GameLogItem_OnContentUpdate(GameLogItem item, IEnumerable<Core.Models.EVELogs.GameLogContent> news)
        {
            if(SelectedGameLogInfo != null && item.Info.ListenerID == SelectedGameLogInfo.ListenerID)
            {
                OnContentUpdate?.Invoke(item, news);
            }
            item.Info.LogContents.AddRange(news);
            foreach (var msg in news)
            {
                if (msg.Important)
                {
                    TryNotify(item, msg);
                }
            }
        }
        
        private void TryNotify(GameLogItem item, Core.Models.EVELogs.GameLogContent msg)
        {
            if(item.Setting.MonitorMode == 0)//立即通知
            {
                GameLogMonitorNotifyService.Current.Notify(item, msg.SourceContent);
            }
            else//只更新时间
            {
                _gameLogDelayMonitorService.Update(item.Info.ListenerID, DateTime.Now.AddSeconds(item.Setting.DisappearDelay));
            }
        }
        #endregion
        private string[] ReadErrorRegex()
        {
            return System.IO.File.ReadAllLines(System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Resources", "Configs", "GameThreadErrorLogRegex.txt"));
        }
    }
}
