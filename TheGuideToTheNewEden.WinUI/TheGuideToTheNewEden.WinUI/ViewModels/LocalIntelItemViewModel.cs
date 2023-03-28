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
using Windows.UI.ViewManagement;

namespace TheGuideToTheNewEden.WinUI.ViewModels
{
    internal class LocalIntelItemViewModel : BaseViewModel
    {
        public ICommand PickLogFolderCommand => new RelayCommand(async() =>
        {
            var folder = await Helpers.PickHelper.PickFolderAsync(Helpers.WindowHelper.CurrentWindow());
            if(folder != null)
            {
                //LogPath = folder.Path;
            }
        });
        public ICommand PickSoundFileCommand => new RelayCommand(async () =>
        {
            var file = await Helpers.PickHelper.PickFileAsync(Window);
            if (file != null)
            {
                //Setting.SoundFilePath= file.Path;
            }
        });

        public ICommand StartCommand => new RelayCommand(async() =>
        {
            
        });

        public ICommand StopCommand => new RelayCommand(() =>
        {
            
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
