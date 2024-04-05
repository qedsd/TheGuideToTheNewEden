using Microsoft.UI;
using Microsoft.UI.Xaml;
using TheGuideToTheNewEden.WinUI.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;

namespace TheGuideToTheNewEden.WinUI.Services.Settings
{
    public static class ThemeSelectorService
    {
        private const string SettingsKey = "AppBackgroundRequestedTheme";

        public static ElementTheme Theme { get; set; } = ElementTheme.Default;

        public static bool IsDark
        {
            get
            {
                if (Theme == ElementTheme.Default)
                {
                    return Application.Current.RequestedTheme == ApplicationTheme.Dark;
                }
                else
                {
                    return Theme == ElementTheme.Dark;
                }
            }
        }

        public static void Initialize()
        {
            Theme = LoadThemeFromSettings();
            SetRequestedTheme();
            OnChangedThemeHandler?.Invoke(Theme);
        }

        public static async Task SetThemeAsync(ElementTheme theme)
        {
            Theme = theme;
            SetRequestedTheme();
            OnChangedThemeHandler?.Invoke(Theme);
            await SaveThemeInSettingsAsync(Theme);
        }

        public static void SetRequestedTheme()
        {
            var res = Application.Current.Resources;
            Action<Color> SetTitleBarButtonForegroundColor = (color) => { res["WindowCaptionForeground"] = color; };
            switch (Theme)
            {
                case ElementTheme.Dark: SetTitleBarButtonForegroundColor(Colors.White); break;
                case ElementTheme.Light: SetTitleBarButtonForegroundColor(Colors.Black); break;
                case ElementTheme.Default:
                    {
                        if (Application.Current.RequestedTheme == ApplicationTheme.Dark)
                        {
                            SetTitleBarButtonForegroundColor(Colors.White);
                        }
                        else
                        {
                            SetTitleBarButtonForegroundColor(Colors.Black);
                        }
                    }
                    break;
            }
            foreach (Window window in WindowHelper.ActiveWindows)
            {
                if (window.Content is FrameworkElement rootElement)
                {
                    rootElement.RequestedTheme = Theme;
                    TitleBarHelper.TriggerTitleBarRepaint(WindowHelper.GetWindowForElement(WindowHelper.MainWindow.Content));
                }
                //window.DispatcherQueue.TryEnqueue(() =>
                //{
                //    (window as BaseWindow).ThemeService.SetElementTheme(Theme);
                //});
            }
        }
        private static ElementTheme LoadThemeFromSettings()
        {
            ElementTheme cacheTheme = ElementTheme.Default;
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
