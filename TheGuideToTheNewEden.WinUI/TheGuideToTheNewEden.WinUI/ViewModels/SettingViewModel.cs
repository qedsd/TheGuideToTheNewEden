using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.WinUI.Enums;
using TheGuideToTheNewEden.WinUI.Models;

namespace TheGuideToTheNewEden.WinUI.ViewModels
{
    [PropertyChanged.AddINotifyPropertyChangedInterface]
    internal class SettingViewModel
    {
        public List<string> Themes { get; set; } = new List<string>()
        {  
            "亮",
            "暗",
            "自动"
        };
        /// <summary>
        /// 0 light 1 dark 2 auto
        /// </summary>
        public int SelectedThemeMode
        {
            get => (int)Setting.Instance.Theme;
            set
            {
                Setting.Instance.Theme = (ThemeModeEnum)value;
            }
        }
        /// <summary>
        /// 0 zh 1 en
        /// </summary>
        public int SelectedUILanguage
        {
            get => (int)Setting.Instance.UILanguage;
            set
            {
                Setting.Instance.UILanguage = (LanguageEnum)value;
            }
        }

        /// <summary>
        /// 0 zh 1 en
        /// </summary>
        public int SelectedDBLanguage
        {
            get => (int)Setting.Instance.DBLanguage;
            set
            {
                Setting.Instance.DBLanguage = (LanguageEnum)value;
            }
        }

        
        public SettingViewModel()
        {
        }

        public RelayCommand SaveCommand => new(() =>
        {
            Setting.Save();
        });
    }
}
