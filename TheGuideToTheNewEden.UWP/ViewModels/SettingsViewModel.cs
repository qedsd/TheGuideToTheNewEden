using System;
using System.Threading.Tasks;
using System.Windows.Input;

using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;

using TheGuideToTheNewEden.UWP.Helpers;
using TheGuideToTheNewEden.UWP.Services;

using Windows.ApplicationModel;
using Windows.UI.Xaml;

namespace TheGuideToTheNewEden.UWP.ViewModels
{
    public class SettingsViewModel : ObservableObject
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
        private int selectedUILanguageIndex = LanguageSelectorService.Language == "zh-CN" ? 0 : 1;
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

        private int selectedDBLanguageIndex = (int)DBLanguageSelectorService.Language;
        public int SelectedDBLanguageIndex
        {
            get => selectedDBLanguageIndex;
            set
            {
                selectedDBLanguageIndex = value;
                _=DBLanguageSelectorService.SetLangAsync((Core.Enums.Language)value);
            }
        }

        private int selectedGameServerIndex = (int)GameServerSelectorService.GameServerType;
        public int SelectedGameServerIndex
        {
            get => selectedGameServerIndex;
            set
            {
                selectedGameServerIndex = value;
                _ = GameServerSelectorService.SetAsync((Core.Enums.GameServerType)value);
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

        public SettingsViewModel()
        {
        }

        public async Task InitializeAsync()
        {
            
            await Task.CompletedTask;
        }

       
    }
}
