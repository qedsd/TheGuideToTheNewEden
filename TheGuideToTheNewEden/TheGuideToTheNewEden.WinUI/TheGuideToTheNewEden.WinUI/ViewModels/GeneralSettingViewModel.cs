using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI.UI.Controls.TextToolbarSymbols;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using TheGuideToTheNewEden.WinUI.Services;
using TheGuideToTheNewEden.WinUI.Services.Settings;
using TheGuideToTheNewEden.Core.Extensions;
using System.Linq;
using TheGuideToTheNewEden.Core;

namespace TheGuideToTheNewEden.WinUI.ViewModels
{
    public class GeneralSettingViewModel : ObservableObject
    {
        private ElementTheme _elementTheme = ThemeSelectorService.Theme;

        public ElementTheme ElementTheme
        {
            get { return _elementTheme; }

            set { SetProperty(ref _elementTheme, value); }
        }

        
        private int selectedThemeIndex = (int)ThemeSelectorService.Theme;
        public int SelectedThemeIndex
        {
            get => selectedThemeIndex;
            set
            {
                selectedThemeIndex = value;
                ElementTheme = (ElementTheme)value;
                _ = ThemeSelectorService.SetThemeAsync(ElementTheme);
            }
        }
        private int selectedUILanguageIndex = LanguageSelectorService.Value == "zh-CN" ? 0 : 1;
        public int SelectedUILanguageIndex
        {
            get => selectedUILanguageIndex;
            set
            {
                selectedUILanguageIndex = value;
                switch(value)
                {
                    case 0: _= LanguageSelectorService.SetLangAsync("zh-CN"); break;
                    case 1: _ = LanguageSelectorService.SetLangAsync("en-US"); break;
                }
            }
        }

        private List<string> localDbs;
        public List<string> LocalDbs
        {
            get => localDbs;
            set => SetProperty(ref localDbs, value);
        }

        private string selectedLocalDb = LocalDbSelectorService.Value;
        public string SelectedLocalDb
        {
            get => selectedLocalDb;
            set
            {
                selectedLocalDb = value;
                _=LocalDbSelectorService.SetAsync(selectedLocalDb);
            }
        }

        private bool needLocalization = DBLocalizationSettingService.Value;
        public bool NeedLocalization
        {
            get => needLocalization;
            set
            {
                needLocalization = value;
                _ = DBLocalizationSettingService.SetAsync(needLocalization);
            }
        }

        private int selectedGameServerIndex = (int)GameServerSelectorService.Value;
        public int SelectedGameServerIndex
        {
            get => selectedGameServerIndex;
            set
            {
                selectedGameServerIndex = value;
                _ = GameServerSelectorService.SetAsync((Core.Enums.GameServerType)value);
            }
        }

        private string playerStatusApi = PlayerStatusService.Value;
        public string PlayerStatusApi
        {
            get => playerStatusApi;
            set
            {
                playerStatusApi = value;
                _ = PlayerStatusService.SetAsync(value);
            }
        }

        private bool autoUpdate = AutoUpdateService.Value;
        public bool AutoUpdate
        {
            get => autoUpdate;
            set
            {
                autoUpdate = value;
                _ = AutoUpdateService.SetAsync(value);
            }
        }

        private string evelogsPath = EVELogsPathSelectorService.Value;
        public string EvelogsPath
        {
            get => evelogsPath;
            set
            {
                evelogsPath = value;
                _ = EVELogsPathSelectorService.SetAsync(value);
            }
        }

        private ICommand _switchThemeCommand;

        public ICommand SwitchThemeCommand
        {
            get
            {
                if (_switchThemeCommand == null)
                {
                    _switchThemeCommand = new RelayCommand<ElementTheme>(
                        async (param) =>
                        {
                            ElementTheme = param;
                            await ThemeSelectorService.SetThemeAsync(param);
                        });
                }

                return _switchThemeCommand;
            }
        }

        public GeneralSettingViewModel()
        {
            InitLocalDb();
        }

        private void InitLocalDb()
        {
            LocalDbs = LocalDbSelectorService.GetAll();
        }

        public async Task InitializeAsync()
        {
            await Task.CompletedTask;
        }

        public ICommand CheckLogCommand => new RelayCommand(() =>
        {
            System.Diagnostics.Process.Start("explorer.exe", Log.GetLogPath());
        });
        public ICommand CheckConfigCommand => new RelayCommand(() =>
        {
            System.Diagnostics.Process.Start("explorer.exe", System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs"));
        });
    }
}
