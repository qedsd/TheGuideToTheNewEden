using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.UWP.Helpers;
using Windows.Storage;

namespace TheGuideToTheNewEden.UWP.Services
{
    public static class GameServerSelectorService
    {
        private const string SettingsKey = "GameServerType";

        public static Core.Enums.GameServerType GameServerType { get; set; } = Core.Enums.GameServerType.Tranquility;

        public static async Task InitializeAsync()
        {
            GameServerType = await LoadFromSettingsAsync();
            CoreConfig.DefaultGameServer = GameServerType;
        }

        public static async Task SetAsync(Core.Enums.GameServerType value)
        {
            GameServerType = value;
            CoreConfig.DefaultGameServer = GameServerType;
            await SaveInSettingsAsync(value);
        }

        private static async Task<Core.Enums.GameServerType> LoadFromSettingsAsync()
        {
            return await ApplicationData.Current.LocalSettings.ReadAsync<Core.Enums.GameServerType>(SettingsKey);
        }

        private static async Task SaveInSettingsAsync(Core.Enums.GameServerType value)
        {
            await ApplicationData.Current.LocalSettings.SaveAsync(SettingsKey, value);
        }
    }
}
