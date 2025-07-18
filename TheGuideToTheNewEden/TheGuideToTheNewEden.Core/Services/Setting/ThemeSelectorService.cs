using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.Core.Services.Settings
{
    public static class ThemeSelectorService
    {
        public enum ElementTheme
        {
            //
            // 摘要:
            //     Use the Light default theme.
            Light,
            //
            // 摘要:
            //     Use the Dark default theme.
            Dark
        }
        private const string SettingsKey = "AppBackgroundRequestedTheme";

        public static ElementTheme Theme { get; set; } = ElementTheme.Dark;

        public static void Initialize()
        {
            Theme = LoadThemeFromSettings();
        }

        public static async Task SetThemeAsync(ElementTheme theme)
        {
            Theme = theme;
            OnChangedThemeHandler?.Invoke(Theme);
            await SaveThemeInSettingsAsync(Theme);
        }
        private static ElementTheme LoadThemeFromSettings()
        {
            ElementTheme cacheTheme = ElementTheme.Dark;
            string themeName = SettingService.GetValue(SettingsKey);

            if (!string.IsNullOrEmpty(themeName))
            {
                Enum.TryParse(themeName, out cacheTheme);
            }

            return cacheTheme;
        }

        private static async Task SaveThemeInSettingsAsync(ElementTheme theme)
        {
            await SettingService.SetValueAsync(SettingsKey, theme.ToString());
        }
        private static ChangedThemeDelegate OnChangedThemeHandler;
        public delegate void ChangedThemeDelegate(ElementTheme theme);

        public static event ChangedThemeDelegate OnChangedTheme
        {
            add => OnChangedThemeHandler += value;
            remove => OnChangedThemeHandler -= value;
        }
    }
}
