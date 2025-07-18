using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using TheGuideToTheNewEden.WPF.Helpers;
using TheGuideToTheNewEden.WPF.Services;

namespace TheGuideToTheNewEden.WPF.ViewModels
{
    internal class GeneralSettingViewModel:BaseViewModel
    {
        private int _appTheme;
        public int AppTheme
        {
            get => _appTheme;
            set
            {
                if(SetProperty(ref _appTheme, value))
                {
                    var theme = (Core.Services.Settings.ThemeSelectorService.ElementTheme)value;
                    AppThemeHelper.SetTheme(theme);
                    _ =Core.Services.Settings.ThemeSelectorService.SetThemeAsync(theme);
                }
            }
        }
        private int _appLanguage;
        public int AppLanguage
        {
            get => _appLanguage;
            set
            {
                if (SetProperty(ref _appLanguage, value))
                {
                    var lang = (Core.Services.LanguageSelectorService.AppLanguage)value;
                    AppLanguageHelper.SetLanguage(lang);
                    _=Core.Services.LanguageSelectorService.SetLangAsync(lang);
                }
            }
        }

        public GeneralSettingViewModel()
        {
            _appTheme = (int)Core.Services.Settings.ThemeSelectorService.Theme;
            _appLanguage = (int)Core.Services.LanguageSelectorService.Value;
        }
    }
}
