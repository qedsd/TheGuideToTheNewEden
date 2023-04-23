using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI.UI.Converters;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Helpers;
using TheGuideToTheNewEden.Core.Models.EVELogs;
using TheGuideToTheNewEden.Core.Services.DB;
using TheGuideToTheNewEden.WinUI.Models;
using TheGuideToTheNewEden.WinUI.Services;
using TheGuideToTheNewEden.WinUI.Services.Settings;
using TheGuideToTheNewEden.WinUI.Wins;
using Windows.UI.ViewManagement;

namespace TheGuideToTheNewEden.WinUI.ViewModels
{
    internal class LocalIntelItemViewModel : BaseViewModel
    {
        private bool running;
        public bool Running { get => running; set => SetProperty(ref running, value); }
        private float xStart;
        public float XStart { get => xStart; set => SetProperty(ref xStart, value); }
        private float yStart;
        public float YStart { get => yStart; set => SetProperty(ref yStart, value); }
        private float xEnd;
        public float XEnd { get => xEnd; set => SetProperty(ref xEnd, value); }
        private float yEnd;
        public float YEnd { get => yEnd; set => SetProperty(ref yEnd, value); }
        public ICommand PickLogFolderCommand => new RelayCommand(async() =>
        {
            var folder = await Helpers.PickHelper.PickFolderAsync(Helpers.WindowHelper.CurrentWindow());
            if(folder != null)
            {
                //LogPath = folder.Path;
            }
        });
        public ICommand SelectRectCommand => new RelayCommand( () =>
        {
            SelectWindowRectWindow selectWindowRectWindow = new SelectWindowRectWindow();
            selectWindowRectWindow.Activate();
        });

        public ICommand StartCommand => new RelayCommand(async() =>
        {
            Running = true;
        });

        public ICommand StopCommand => new RelayCommand(() =>
        {
            Running = false;
        });
        /// <summary>
        /// 加载应用设置
        /// </summary>
        private void LoadSetting()
        {
            
        }

        private void SaveSetting()
        {
            
        }

        public void Dispose()
        {

        }
    }
}
