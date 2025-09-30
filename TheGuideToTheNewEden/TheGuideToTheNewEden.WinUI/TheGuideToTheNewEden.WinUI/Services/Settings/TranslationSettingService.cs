using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Services.Settings
{
    public class TranslationSettingService : IService
    {
        private const string FromLanguageKey = "TranslationSetting.FromLanguage";
        private const string ToLanguageKey = "TranslationSetting.ToLanguage";

        public string FromLanguage { get; set; }
        public string ToLanguage { get; set; }

        private List<string> _froms = new List<string>()
        {
            "auto",
            "de",
            "en",
            "es",
            "fr",
            "it",
            "ja",
            "ko",
            "ru",
            "zh-CHS",
            "zh-CHT",
        };
        private List<string> _tos = new List<string>()
        {
             "en",
            "de",
            "es",
            "fr",
            "it",
            "ja",
            "ko",
            "ru",
            "zh-CHS",
            "zh-CHT",
        };

        public void Dispose()
        {
            
        }

        public void Init()
        {
            FromLanguage = Settings.SettingService.GetValue(FromLanguageKey);
            if (string.IsNullOrEmpty(FromLanguage))
            {
                FromLanguage = "auto";
            }
            ToLanguage = Settings.SettingService.GetValue(ToLanguageKey); 
            if (string.IsNullOrEmpty(ToLanguage))
            {
                ToLanguage = "zh-CHS";
            }
        }
        public void SetFromLanguage(string value)
        {
            FromLanguage = value;
            Settings.SettingService.SetValue(FromLanguageKey, value);
        }
        public void SetToLanguage(string value)
        {
            ToLanguage = value;
            Settings.SettingService.SetValue(ToLanguageKey, value);
        }

        public List<string> GetFromLanguages() => _froms.ToList();
        public List<string> GetToLanguages() => _tos.ToList();

    }
}
