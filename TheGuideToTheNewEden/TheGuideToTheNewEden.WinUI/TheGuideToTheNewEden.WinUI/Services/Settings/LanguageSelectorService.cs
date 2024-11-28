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
            SetLanguage(Value);
        }

        public static async Task SetLangAsync(string language)
        {
            Value = language;
            await SaveAsync(Value);
            SetLanguage(Value);
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

        private static async Task SaveAsync(string language)
        {
            await SettingService.SetValueAsync(Key, language);
        }
        private static void SetLanguage(string lang)
        {
            var oldRes = Microsoft.UI.Xaml.Application.Current.Resources.MergedDictionaries.FirstOrDefault(p => p.Source.OriginalString.Contains("Resources/Languages"));
            if (oldRes != null)
                Microsoft.UI.Xaml.Application.Current.Resources.MergedDictionaries.Remove(oldRes);
            string newStr = $"ms-appx:///Resources/Languages/{lang}.xaml";
            var resource = new Microsoft.UI.Xaml.ResourceDictionary()
            {
                Source = new Uri(newStr, UriKind.Absolute)
            };
            Microsoft.UI.Xaml.Application.Current.Resources.MergedDictionaries.Add(resource);
        }
    }
}
