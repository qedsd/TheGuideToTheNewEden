using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.UWP.Helpers;
using Windows.Storage;

namespace TheGuideToTheNewEden.UWP.Services
{
    internal static class PlayerStatusService
    {
        private const string SettingsKey = "AppPlayerStatusAPI";

        public static string Api { get; set; }

        public static async Task InitializeAsync()
        {
            Api = await LoadFromSettingsAsync();
            CoreConfig.PlayerStatusApi = Api;
        }

        public static async Task SetAsync(string api)
        {
            Api = api;
            CoreConfig.PlayerStatusApi = Api;
            await SaveInSettingsAsync(Api);
        }

        private static async Task<string> LoadFromSettingsAsync()
        {
            return await ApplicationData.Current.LocalSettings.ReadAsync<string>(SettingsKey); ;
        }

        private static async Task SaveInSettingsAsync(string api)
        {
            await ApplicationData.Current.LocalSettings.SaveAsync(SettingsKey, api);
        }
    }
}
