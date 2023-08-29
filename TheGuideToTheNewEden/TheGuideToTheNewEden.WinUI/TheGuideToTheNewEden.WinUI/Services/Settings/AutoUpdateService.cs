using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Services.Settings
{
    internal static class AutoUpdateService
    {
        private const string Key = "AutoUpdate";

        public static bool Value { get; set; }

        public static void Initialize()
        {
            Value = LoadFromSettings();
        }

        public static async Task SetAsync(bool value)
        {
            Value = value;
            await SaveToSettingsAsync(value);
        }

        private static bool LoadFromSettings()
        {
            var v = Settings.SettingService.GetValue(Key);
            if(string.IsNullOrEmpty(v))
            {
                return true;
            }
            else
            {
                return bool.Parse(v);
            }
        }

        private static async Task SaveToSettingsAsync(bool value)
        {
            await Settings.SettingService.SetValueAsync(Key, value.ToString());
        }
    }
}
