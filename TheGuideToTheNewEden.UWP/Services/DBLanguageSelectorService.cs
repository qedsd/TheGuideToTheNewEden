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
        private const string SettingsKey = "AppDBLanguage";

        public static Core.Enums.Language Language { get; set; } = Core.Enums.Language.Chinese;

        public static async Task InitializeAsync()
        {
            Language = await LoadLanguageFromSettingsAsync();
            Core.Services.DB.DBService.Language = Language;
        }

        public static async Task SetLangAsync(Core.Enums.Language language)
        {
            Language = language;
            Core.Services.DB.DBService.Language = Language;
            await SaveThemeInSettingsAsync(Language);
        }

        private static async Task<Core.Enums.Language> LoadLanguageFromSettingsAsync()
        {
            return await ApplicationData.Current.LocalSettings.ReadAsync<Core.Enums.Language>(SettingsKey); ;
        }

        private static async Task SaveThemeInSettingsAsync(Core.Enums.Language language)
        {
            await ApplicationData.Current.LocalSettings.SaveAsync(SettingsKey, language);
        }
    }
}
