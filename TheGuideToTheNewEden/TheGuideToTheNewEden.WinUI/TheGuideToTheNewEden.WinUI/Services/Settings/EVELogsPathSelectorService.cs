using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Services.Settings
{
    internal static class EVELogsPathSelectorService
    {
        private const string Key = "EVELogsPath";
        public static string Value { get; set; }


        public static void Initialize()
        {
            Value = LoadFromSettings();
            if(string.IsNullOrEmpty(Value))
            {
                Value = GetDefaultValue();
            }
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

        public static string GetDefaultValue()
        {
            return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "EVE", "logs");
        }
    }
}
