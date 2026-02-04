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
using TheGuideToTheNewEden.WinUI.Extensions;
using TheGuideToTheNewEden.WinUI.Models;

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
                        if(value.Setting == null)
                        {
                            var setting = GetSetting(value.ListenerID);
                            value.Setting = setting;
                            GameLogSetting = setting;
                        }
                        else
                        {
                            GameLogSetting = value.Setting;
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
            set 
            {
                if(SetProperty(ref gameLogSetting, value))
                {
                    if(value != null)
                    {
                        SelectedItemConfig = value.ItemConfigs.FirstOrDefault();
                    }
                    else
                    {
                        SelectedItemConfig = null;
                    }
                }
            } 
        }

        private GameLogItemConfig _selectedItemConfig;
        public GameLogItemConfig SelectedItemConfig
        {
            get => _selectedItemConfig;
            set => SetProperty(ref _selectedItemConfig, value);
        }

        private bool running;
        public bool Running
        {
            get => running;
            set => SetProperty(ref running, value);
        }

        /// <summary>
        /// key = GameLogItemConfig.GUID
        /// </summary>
        private readonly Dictionary<string, Models.GameLogMonitor> _monitors = new Dictionary<string, Models.GameLogMonitor>();
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
            Start(SelectedGameLogInfo);
        });
        public ICommand StartAllCommand => new RelayCommand(() =>
        {
            foreach (var info in GameLogInfos)
            {
                Start(info);
            }
        });

        /// <summary>
        /// 若保存过则读取，若没保存过则生成默认
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private GameLogSetting GetSetting(int id)
        {
            var setting = Services.Settings.GameLogInfoSettingService.GetValue(id);
            if (setting == null)
            {
                setting = new GameLogSetting()
                {
                    ListenerID = id,
                    ItemConfigs = new ObservableCollection<GameLogItemConfig>(),
                };

                setting.ItemConfigs.Add(CreateErrorLogConfig());
                setting.ItemConfigs.Add(CreateGameLogConfig());
            }
            return setting;
        }
        private bool Start(GameLogInfo gameLogInfo)
        {
            GameLogSetting setting = gameLogInfo.Setting;
            if(setting != null)
            {
                try
                {
                    if (setting.ItemConfigs.Any())
                    {
                        foreach (var itemConfig in setting.ItemConfigs)
                        {
                            if (!_monitors.TryGetValue(itemConfig.GUID, out var itemMonitor))
                            {
                                if (itemConfig.LogType == 0)
                                {
                                    itemMonitor = new Models.GameLogMonitor(gameLogInfo, itemConfig, gameLogInfo.FilePath);
                                }
                                else
                                {
                                    if (!GameLogHelper.GetGameLogDateAndThreadId(gameLogInfo.FilePath, out int dateInt, out int threadId))
                                    {
                                        string msg = $"Get Game Log Date And Thread Id Failed:{System.IO.Path.GetFileNameWithoutExtension(gameLogInfo.FilePath)}";
                                        Core.Log.Error(msg);
                                        ShowError(msg);
                                        return false;
                                    }
                                    string threadFile = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(gameLogInfo.FilePath), $"{dateInt}_{threadId}.txt");
                                    if (System.IO.File.Exists(threadFile))
                                    {
                                        itemMonitor = new Models.GameLogMonitor(gameLogInfo, itemConfig, threadFile);
                                    }
                                    else
                                    {
                                        string msg = $"Thread File Not Exists:{threadFile}";
                                        Core.Log.Error(msg);
                                        ShowError(msg);
                                        return false;
                                    }
                                }
                                itemMonitor.OnContentUpdate += ContentUpdate;
                                _monitors.Add(itemConfig.GUID, itemMonitor);
                            }
                            itemMonitor.Start();
                        }
                    }
                    gameLogInfo.Running = true;
                    Running = true;
                    return true;
                }
                catch (Exception ex)
                {
                    Core.Log.Error(ex);
                    ShowError(ex);
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        public ICommand AddKeysCommand => new RelayCommand(() =>
        {
            SelectedItemConfig.Keys.Add(new GameLogMonityKey("系统消息"));
        });
        public ICommand StopNotifyCommand => new RelayCommand(() =>
        {
            
        });
        public ICommand StopCommand => new RelayCommand(() =>
        {
            Stop(SelectedGameLogInfo);
            Running = GameLogInfos.FirstOrDefault(p=>p.Running) != null;
        });
        public ICommand StopAllCommand => new RelayCommand(() =>
        {
            foreach (var info in GameLogInfos)
            {
                Stop(info);
            }
            Running = false;
        });
        private void Stop(GameLogInfo gameLogInfo)
        {
            if (gameLogInfo != null)
            {
                var monitors = GetMonitor(gameLogInfo);
                if (monitors.NotNullOrEmpty())
                {
                    foreach(var monitor in monitors)
                    {
                        monitor.Stop();
                    }
                }
                gameLogInfo.Running = false;
            }
        }
        private List<GameLogMonitor> GetMonitor(GameLogInfo gameLogInfo)
        {
            List<GameLogMonitor> monitors = new List<GameLogMonitor>();
            foreach (var item in gameLogInfo.Setting.ItemConfigs)
            {
                if (_monitors.TryGetValue(item.GUID, out var itemMonitor))
                {
                    monitors.Add(itemMonitor);
                }
            }
            return monitors;
        }

        public ICommand PickSoundFileCommand => new RelayCommand(async () =>
        {
            var file = await Helpers.PickHelper.PickFileAsync(Window);
            if (file != null)
            {
                SelectedItemConfig.SoundFile = file.Path;
            }
        });
        public void Dispose()
        {
            GameLogInfos.Where(p => p.Running).ToList().ForEach(p =>
            {
                var monitors = GetMonitor(p);
                if (monitors.NotNullOrEmpty())
                {
                    foreach (var monitor in monitors)
                    {
                        monitor.Dispose();
                        _monitors.Remove(monitor.GetGUID());
                    }
                }
                p.Running = false;
            });
            foreach(var item in _monitors)
            {
                item.Value.Dispose();
            }
            _monitors.Clear();
        }
        public void RemoveConfig(GameLogItemConfig config)
        {
            if (config != null)
            {
                if(_monitors.TryGetValue(config.GUID, out var itemMonitor))
                {
                    itemMonitor.Dispose();
                    _monitors.Remove(config.GUID);
                }
                GameLogSetting.ItemConfigs.Remove(config);
            }
        }
        public void AddConfig(int mode)
        {
            GameLogSetting.ItemConfigs.Add(mode == 0 ? CreateGameLogConfig() : CreateErrorLogConfig());
        }

        #region 消息处理
        /// <summary>
        /// 消息更新
        /// </summary>
        public event GameLogItem.ContentUpdate OnContentUpdate;

        private void ContentUpdate(GameLogItem item, IEnumerable<Core.Models.EVELogs.GameLogContent> news)
        {
            if(SelectedGameLogInfo != null && item.Info.ListenerID == SelectedGameLogInfo.ListenerID)
            {
                OnContentUpdate?.Invoke(item, news);
            }
            item.Info.LogContents.AddRange(news);
        }
        #endregion
        private string[] ReadErrorRegex()
        {
            return System.IO.File.ReadAllLines(System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Resources", "Configs", "GameThreadErrorLogRegex.txt"));
        }

        private GameLogItemConfig CreateGameLogConfig()
        {
            GameLogItemConfig itemConfig = new GameLogItemConfig(0)
            {
                ConfigName = Helpers.ResourcesHelper.GetString("GameLogMonitorPage_Type_GameLog")
            };
            itemConfig.Keys.Add(new GameLogMonityKey("combat"));
            return itemConfig;
        }
        private GameLogItemConfig CreateErrorLogConfig()
        {
            GameLogItemConfig itemConfig = new GameLogItemConfig(1)
            {
                ConfigName = Helpers.ResourcesHelper.GetString("GameLogMonitorPage_Type_ErrorLog")
            };
            var errorRegex = ReadErrorRegex();
            if (errorRegex.NotNullOrEmpty())
            {
                foreach (var regex in errorRegex)
                {
                    itemConfig.Keys.Add(new GameLogMonityKey(regex));
                }
            }
            return itemConfig;
        }
    }
}
