using Microsoft.UI.Xaml;
using Microsoft.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.WinUI.Helpers;

namespace TheGuideToTheNewEden.WinUI.Services.Settings
{
    internal static class LocalDbSelectorService
    {
        private const string Key = "LocalDbPath";
        public static string Value { get; set; }


        public static void Initialize()
        {
            Value = LoadFromSettings();
        }

        public static async Task SetAsync(string value)
        {
            Value = value;
            await SaveToSettingsAsync(value);
        }

        private static string LoadFromSettings()
        {
            return SettingService.GetValue(Key);
        }

        private static async Task SaveToSettingsAsync(string value)
        {
            await SettingService.SetValueAsync(Key, value);
        }
    }
}
