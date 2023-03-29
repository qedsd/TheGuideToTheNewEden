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

namespace TheGuideToTheNewEden.WinUI.ViewModels
{
    internal class GamePreviewMgrViewModel : BaseViewModel
    {
        private Core.Models.WindowPreviews.PreviewSetting previewSetting;
        public Core.Models.WindowPreviews.PreviewSetting PreviewSetting
        {
            get => previewSetting;
            set => SetProperty(ref previewSetting, value);
        }
        private Core.Models.WindowPreviews.PreviewItem setting;
        public Core.Models.WindowPreviews.PreviewItem Setting
        {
            get => setting;
            set => SetProperty(ref setting, value);
        }
        private List<Process> processes;
        public List<Process> Processes
        {
            get => processes;
            set => SetProperty(ref processes, value);
        }
        private Process selectedProcesses;
        public Process SelectedProcesses
        {
            get => selectedProcesses;
            set
            {
                SetProperty(ref selectedProcesses, value);
                SelectWindowInfo();
            }
        }
        private ImageSource previewImage;
        public ImageSource PreviewImage
        {
            get => previewImage;
            set => SetProperty(ref previewImage, value);
        }
        private bool isRunning;
        public bool IsRunning
        {
            get => isRunning;
            set => SetProperty(ref isRunning, value);
        }
        private static readonly string Path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "GamePreviewSetting.json");
        public GamePreviewMgrViewModel()
        {
            if(System.IO.File.Exists(Path))
            {
                string json = System.IO.File.ReadAllText(Path);
                if (string.IsNullOrEmpty(json))
                {
                    PreviewSetting = new Core.Models.WindowPreviews.PreviewSetting();
                }
                else
                {
                    PreviewSetting = JsonConvert.DeserializeObject<Core.Models.WindowPreviews.PreviewSetting>(json);
                }
            }
            else
            {
                PreviewSetting = new Core.Models.WindowPreviews.PreviewSetting();
            }
            Init();
        }
        private async void Init()
        {
            Window?.ShowWaiting();
            var allProcesses = Process.GetProcesses();
            if(allProcesses.NotNullOrEmpty())
            {
                List<Process> targetProcesses = new List<Process>();
                var keywords = PreviewSetting.ProcessKeywords.Split(',');
                await Task.Run(() =>
                {
                    foreach (var process in allProcesses)
                    {
                        foreach (var keyword in keywords)
                        {
                            if (process.ProcessName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                            {
                                targetProcesses.Add(process);
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
        private void SelectWindowInfo()
        {

        }

        public ICommand RefreshWindowCommand => new RelayCommand(() =>
        {
            Init();
        });

        public ICommand StartCommand => new RelayCommand(async () =>
        {
            
        });

        public ICommand StopCommand => new RelayCommand(() =>
        {
            
        });
    }
}
