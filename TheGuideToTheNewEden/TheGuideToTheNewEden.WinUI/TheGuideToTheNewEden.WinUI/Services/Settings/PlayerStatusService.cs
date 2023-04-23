using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace TheGuideToTheNewEden.WinUI.Services
{
    internal static class PlayerStatusService
    {
        private const string Key = "PlayerStatusAPI";

        public static string Value { get; set; }

        public static void Initialize()
        {
            Value = LoadFromSettings();
            CoreConfig.PlayerStatusApi = Value;
        }

        public static async Task SetAsync(string api)
        {
            Value = api;
            CoreConfig.PlayerStatusApi = Value;
            await SaveToSettingsAsync(Value);
        }

        private static string LoadFromSettings()
        {
            return Settings.SettingService.GetValue(Key); ;
        }

        private static async Task SaveToSettingsAsync(string api)
        {
            await Settings.SettingService.SetValueAsync(Key, api);
        }
    }
}
