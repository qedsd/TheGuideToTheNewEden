using Microsoft.UI.Xaml.Media;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.WinUI.Helpers;
using static TheGuideToTheNewEden.WinUI.Helpers.FindWindowHelper;
using TheGuideToTheNewEden.Core.Extensions;
using Microsoft.UI.Xaml.Media.Imaging;
using SqlSugar.DistributedSystem.Snowflake;
using System.Drawing;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;
using System.Diagnostics;
using TheGuideToTheNewEden.Core.Models.EVELogs;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.WinUI.Services;
using TheGuideToTheNewEden.Core.Models.GamePreviews;
using TheGuideToTheNewEden.WinUI.Wins;
using Microsoft.UI.Xaml;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json.Linq;

namespace TheGuideToTheNewEden.WinUI.ViewModels
{
    internal class GamePreviewMgrViewModel : BaseViewModel
    {
        private Core.Models.GamePreviews.PreviewSetting previewSetting;
        public Core.Models.GamePreviews.PreviewSetting PreviewSetting
        {
            get => previewSetting;
            set => SetProperty(ref previewSetting, value);
        }
        private Core.Models.GamePreviews.PreviewItem setting;
        public Core.Models.GamePreviews.PreviewItem Setting
        {
            get => setting;
            set
            {
                SetProperty(ref setting, value);
                value.ProcessGUID = SelectedProcess.GetGuid();
            }
        }
        private ObservableCollection<PreviewItem> settings;
        public ObservableCollection<PreviewItem> Settings
        {
            get => settings;
            set => SetProperty(ref settings, value);
        }
        private Core.Models.GamePreviews.PreviewItem selectedSetting;
        public Core.Models.GamePreviews.PreviewItem SelectedSetting
        {
            get => selectedSetting;
            set
            {
                SetProperty(ref selectedSetting, value);
                if(value != null)
                {
                    value.ProcessGUID = SelectedProcess.GetGuid();
                    Setting = value;
                }
                else
                {
                    Setting = new PreviewItem()
                    {
                        ProcessGUID = SelectedProcess.GetGuid(),
                        Name = SelectedProcess.GetCharacterName()
                    };
                }
            }
        }
        private List<ProcessInfo> processes;
        public List<ProcessInfo> Processes
        {
            get => processes;
            set => SetProperty(ref processes, value);
        }
        private ProcessInfo selectedProcess;
        public ProcessInfo SelectedProcess
        {
            get => selectedProcess;
            set
            {
                SetProperty(ref selectedProcess, value);
                SetProcess();
            }
        }
        private ImageSource previewImage;
        public ImageSource PreviewImage
        {
            get => previewImage;
            set => SetProperty(ref previewImage, value);
        }
        private Visibility settingVisible = Visibility.Collapsed;
        public Visibility SettingVisible
        {
            get => settingVisible;
            set => SetProperty(ref settingVisible,value);
        }
        private bool running;
        public bool Running
        {
            get => running;
            set => SetProperty(ref running, value);
        }
        private static readonly string Path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "GamePreviewSetting.json");
        /// <summary>
        /// key为ProcessInfo.GetGuid(),可能为角色名，可能为GUID
        /// </summary>
        private Dictionary<string, GamePreviewWindow> _runningDic = new Dictionary<string, GamePreviewWindow>();
        public GamePreviewMgrViewModel()
        {
            if(System.IO.File.Exists(Path))
            {
                string json = System.IO.File.ReadAllText(Path);
                if (string.IsNullOrEmpty(json))
                {
                    PreviewSetting = new Core.Models.GamePreviews.PreviewSetting();
                }
                else
                {
                    PreviewSetting = JsonConvert.DeserializeObject<Core.Models.GamePreviews.PreviewSetting>(json);
                }
            }
            else
            {
                PreviewSetting = new Core.Models.GamePreviews.PreviewSetting();
            }
            if(PreviewSetting.PreviewItems.NotNullOrEmpty())
            {
                Settings = PreviewSetting.PreviewItems.ToObservableCollection();
                foreach(var item in Settings)
                {
                    item.ProcessGUID = item.Name;
                }
            }
            else
            {
                Settings = new ObservableCollection<PreviewItem>();
            }
            Init();
            HotkeyService.OnKeyboardClicked += HotkeyService_OnKeyboardClicked;
        }
        #region 切换快捷键
        private string _lastProcessGUID;
        private void HotkeyService_OnKeyboardClicked(List<Common.KeyboardHook.KeyboardInfo> keys)
        {
            if (_runningDic.Count != 0 && !string.IsNullOrEmpty(PreviewSetting.SwitchHotkey))
            {
                var keynames = PreviewSetting.SwitchHotkey.Split('+');
                if (keynames.NotNullOrEmpty())
                {
                    var targetkeys = keynames.ToList();
                    foreach (var key in targetkeys)
                    {
                        if (!keys.Where(p => p.Name.Equals(key, StringComparison.OrdinalIgnoreCase)).Any())
                        {
                            return;
                        }
                    }
                    if(_lastProcessGUID == null)
                    {
                        _runningDic.First().Value.ActiveSourceWindow();
                        _lastProcessGUID = _runningDic.First().Key;
                    }
                    else
                    {
                        for(int i = 0;i< _runningDic.Count;i++)
                        {
                            var item = _runningDic.ElementAt(i);
                            if (item.Key == _lastProcessGUID)
                            {
                                KeyValuePair<string, GamePreviewWindow> show;
                                if(i != _runningDic.Count - 1)
                                {
                                    show = _runningDic.ElementAt(i + 1);
                                }
                                else
                                {
                                    show = _runningDic.First();
                                }
                                if(show.Key != null)
                                {
                                    _lastProcessGUID = show.Key;
                                    show.Value.ActiveSourceWindow();
                                }
                                break;
                            }
                        }
                    }
                }
            }
        }
        #endregion
        private async void Init()
        {
            Window?.ShowWaiting();
            var allProcesses = Process.GetProcesses();
            if(allProcesses.NotNullOrEmpty())
            {
                List<ProcessInfo> targetProcesses = new List<ProcessInfo>();
                var keywords = PreviewSetting.ProcessKeywords.Split(',');
                await Task.Run(() =>
                {
                    foreach (var process in allProcesses)
                    {
                        foreach (var keyword in keywords)
                        {
                            if (process.ProcessName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                            {
                                if(process.MainWindowHandle != IntPtr.Zero)
                                {
                                    string title = Helpers.FindWindowHelper.GetWindowTitle(process.MainWindowHandle);
                                    ProcessInfo processInfo = new ProcessInfo()
                                    {
                                        MainWindowHandle = process.MainWindowHandle,
                                        ProcessName = process.ProcessName,
                                        WindowTitle = title,
                                    };
                                    targetProcesses.Add(processInfo);
                                }
                                break;
                            }
                        }
                    }
                });
                Processes = targetProcesses;
            }
            else
            {
                Processes = null;
            }
            Window?.HideWaiting();
        }
        private void SetProcess()
        {
            if(SelectedProcess != null)
            {
                SettingVisible = Visibility.Visible;
                var guid = SelectedProcess.GetGuid();
                if(!string.IsNullOrEmpty(guid))
                {
                    var targetSetting = Settings.FirstOrDefault(p => p.ProcessGUID == guid);
                    if (targetSetting != null)
                    {
                        Setting = targetSetting;
                        SelectedSetting = targetSetting;
                    }
                    else
                    {
                        Setting = new PreviewItem()
                        {
                            ProcessGUID = guid,
                            Name = SelectedProcess.GetCharacterName()
                        };
                    }
                }
            }
            else
            {
                SettingVisible = Visibility.Collapsed;
            }
        }

        public ICommand RefreshProcessListCommand => new RelayCommand(() =>
        {
            Init();
        });

        public ICommand StartCommand => new RelayCommand(() =>
        {
            if(Setting != null)
            {
                GamePreviewWindow gamePreviewWindow = new GamePreviewWindow(Setting);
                gamePreviewWindow.OnSettingChanged += GamePreviewWindow_OnSettingChanged;
                gamePreviewWindow.OnStop += GamePreviewWindow_OnStop;
                gamePreviewWindow.Start(SelectedProcess.MainWindowHandle);
                _runningDic.Add(SelectedProcess.GetGuid(), gamePreviewWindow);
                SelectedProcess.Running = true;
                SelectedProcess.SettingName = Setting.Name;
                Running = true;
                //保存
                if (!string.IsNullOrEmpty(Setting.Name))
                {
                    if(!PreviewSetting.PreviewItems.Contains(Setting))
                    {
                        PreviewSetting.PreviewItems.Add(Setting);
                    }
                    if(!Settings.Contains(Setting))
                    {
                        Settings.Add(Setting);
                        SelectedSetting = Setting;
                    }
                    SaveSetting();
                }
            }
        });

        private void GamePreviewWindow_OnStop(PreviewItem previewItem)
        {
            if (_runningDic.TryGetValue(previewItem.ProcessGUID, out var window))
            {
                SelectedProcess.Running = false;
                window.Stop();
                _runningDic.Remove(SelectedProcess.GetGuid());
            }
        }

        private void GamePreviewWindow_OnSettingChanged(PreviewItem previewItem)
        {
            SaveSetting();
        }
        private void SaveSetting()
        {
            if (!System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(Path)))
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            }
            string json = JsonConvert.SerializeObject(PreviewSetting);
            System.IO.File.WriteAllText(Path,json);
        }

        public ICommand StopCommand => new RelayCommand(() =>
        {
            if (Setting != null)
            {
                if(_runningDic.TryGetValue(SelectedProcess.GetGuid(),out var window))
                {
                    SelectedProcess.Running = false;
                    window.Stop();
                    _runningDic.Remove(SelectedProcess.GetGuid());
                }
                if(_runningDic.Count == 0)
                {
                    Running = false;
                }
            }
        });

        public ICommand CancelSettingCommand => new RelayCommand(() =>
        {
            Setting = new PreviewItem()
            {
                ProcessGUID = SelectedProcess.GetGuid()
            };
        });
        public ICommand NewSettingCommand => new RelayCommand(() =>
        {
            SelectedSetting = null;
        });
        public ICommand RemoveSettingCommand => new RelayCommand<PreviewItem>((item) =>
        {
            if(item != null)
            {
                if(Settings.Remove(item))
                {
                    if(PreviewSetting.PreviewItems.Remove(item))
                    {
                        SaveSetting();
                        if(item == SelectedSetting)
                        {
                            SelectedSetting = null;
                        }
                    }
                }
            }
        });
        public void StopAll()
        {
            foreach(var window in _runningDic.Values)
            {
                window.Stop();
            }
        }
    }
}
