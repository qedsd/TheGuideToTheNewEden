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
        private static readonly string Path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "GamePreviewSetting.json");
        private Dictionary<string, GamePreviewWindow> _runningDic = new Dictionary<string, GamePreviewWindow>();
        //private List<PreviewItem> _settings = new List<PreviewItem>();
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
        }
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
                    }
                    else
                    {
                        Setting = new PreviewItem()
                        {
                            ProcessGUID = guid
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
                gamePreviewWindow.Start(SelectedProcess.MainWindowHandle);
                _runningDic.Add(SelectedProcess.GetGuid(), gamePreviewWindow);
                SelectedProcess.Running = true;
                SelectedProcess.SettingName = Setting.Name;
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
                    }
                    SaveSetting();
                }
            }
        });

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
            }
        });

        public ICommand CancelSettingCommand => new RelayCommand(() =>
        {
            Setting = new PreviewItem()
            {
                ProcessGUID = SelectedProcess.GetGuid()
            };
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
