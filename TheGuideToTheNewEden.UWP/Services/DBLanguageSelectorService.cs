using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.UWP.Helpers;
using Windows.Storage;

namespace TheGuideToTheNewEden.UWP.Services
{
    public static class DBLanguageSelectorService
    {
        private const string SettingsKey = "AppPrimaryDBLanguage";

        public static string Language { get; set; } = "zh-CN";

        public static async Task InitializeAsync()
        {
            Language = await LoadLanguageFromSettingsAsync();
        }

        public static async Task SetLangAsync(string language)
        {
            Language = language;
            await SaveThemeInSettingsAsync(Language);
        }

        private static async Task<string> LoadLanguageFromSettingsAsync()
        {
            string cacheLanguage = "zh-CN";
            string savedLanguage = await ApplicationData.Current.LocalSettings.ReadAsync<string>(SettingsKey);

            if (!string.IsNullOrEmpty(savedLanguage))
            {
                cacheLanguage = savedLanguage;
            }

            return cacheLanguage;
        }

        private static async Task SaveThemeInSettingsAsync(string language)
        {
            await ApplicationData.Current.LocalSettings.SaveAsync(SettingsKey, language);
        }
    }
}
