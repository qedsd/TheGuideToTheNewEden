using System;
using System.Threading.Tasks;
using Windows.Storage;

namespace TheGuideToTheNewEden.WinUI.Services
{
    public static class GameServerSelectorService
    {
        private const string Key = "GameServerType";
        public static Core.Enums.GameServerType Value { get; set; } = Core.Enums.GameServerType.Tranquility;

        public static void Initialize()
        {
            Value = LoadFromSettings();
            CoreConfig.DefaultGameServer = Value;
        }

        public static async Task SetAsync(Core.Enums.GameServerType value)
        {
            Value = value;
            CoreConfig.DefaultGameServer = Value;
            await SaveToSettingsAsync(value);
        }

        private static Core.Enums.GameServerType LoadFromSettings()
        {
            string v = Settings.SettingService.GetValue(Key);
            if(!string.IsNullOrEmpty(v))
            {
                return (Core.Enums.GameServerType)Enum.Parse(typeof(Core.Enums.GameServerType), Settings.SettingService.GetValue(Key));
            }
            else
            {
                return Core.Enums.GameServerType.Tranquility;
            }
        }

        private static async Task SaveToSettingsAsync(Core.Enums.GameServerType value)
        {
            await Settings.SettingService.SetValueAsync(Key, value.ToString());
        }
    }
}
