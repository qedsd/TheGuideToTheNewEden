using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.Core.Services
{
    public static class LanguageSelectorService
    {
        public enum AppLanguage
        {
            Chinese,
            English
        }
        private const string Key = "AppPrimaryLanguage";

        public static AppLanguage Value { get; set; } = AppLanguage.Chinese;

        public static void Initialize()
        {
            Value = LoadLanguageFromSettings();
        }

        public static async Task SetLangAsync(AppLanguage language)
        {
            Value = language;
            await SaveAsync(Value);
            AppLanguageChanged?.Invoke(null, language);
        }

        private static AppLanguage LoadLanguageFromSettings()
        {
            string cacheLanguage = AppLanguage.Chinese.ToString();
            string savedLanguage = Settings.SettingService.GetValue(Key);

            if (!string.IsNullOrEmpty(savedLanguage))
            {
                cacheLanguage = savedLanguage;
            }

            if(Enum.TryParse<AppLanguage>(cacheLanguage, out var lang))
            {
                return lang;
            }
            else
            {
                return AppLanguage.Chinese;
            }
        }

        private static async Task SaveAsync(AppLanguage language)
        {
            await Settings.SettingService.SetValueAsync(Key, language.ToString());
        }

        public static event EventHandler<AppLanguage> AppLanguageChanged;
    }
}
