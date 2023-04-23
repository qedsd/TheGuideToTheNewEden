using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.WinUI.Services.Settings;
using Windows.Storage;

namespace TheGuideToTheNewEden.WinUI.Services
{
    public static class LanguageSelectorService
    {
        private const string Key = "AppPrimaryLanguage";

        public static string Value { get; set; } = "zh-CN";

        public static void Initialize()
        {
            Value = LoadLanguageFromSettings();
            //Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = Value;//仅打包版本支持切换
        }

        public static async Task SetLangAsync(string language)
        {
            Value = language;
            await SaveThemeInSettingsAsync(Value);
        }

        private static string LoadLanguageFromSettings()
        {
            string cacheLanguage = "zh-CN";
            string savedLanguage = SettingService.GetValue(Key);

            if (!string.IsNullOrEmpty(savedLanguage))
            {
                cacheLanguage = savedLanguage;
            }

            return cacheLanguage;
        }

        private static async Task SaveThemeInSettingsAsync(string language)
        {
            await SettingService.SetValueAsync(Key, language);
        }
    }
}
