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
    [PropertyChanged.AddINotifyPropertyChangedInterface]
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
        private int selectedDBLanguageIndex = DBLanguageSelectorService.Language == "zh-CN" ? 0 : 1;
        public int SelectedDBLanguageIndex
        {
            get => selectedDBLanguageIndex;
            set
            {
                selectedDBLanguageIndex = value;
                switch (value)
                {
                    case 0: _ = DBLanguageSelectorService.SetLangAsync("zh-CN"); break;
                    case 1: _ = DBLanguageSelectorService.SetLangAsync("en-US"); break;
                }
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
