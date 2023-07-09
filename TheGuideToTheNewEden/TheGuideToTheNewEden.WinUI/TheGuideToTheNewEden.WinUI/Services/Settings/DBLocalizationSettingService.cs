using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Services.Settings
{
    internal class DBLocalizationSettingService
    {
        private const string Key = "NeedLocalization";
        public static bool Value { get; set; }


        public static void Initialize()
        {
            Value = LoadFromSettings();
        }

        public static async Task SetAsync(bool value)
        {
            Value = value;
            await SaveToSettingsAsync(value.ToString());
        }

        private static bool LoadFromSettings()
        {
            var setting = SettingService.GetValue(Key);
            if (string.IsNullOrEmpty(setting))
            {
                return true;
            }
            else
            {
                return bool.Parse(setting);
            }
        }

        private static async Task SaveToSettingsAsync(string value)
        {
            await SettingService.SetValueAsync(Key, value);
        }
    }
}
