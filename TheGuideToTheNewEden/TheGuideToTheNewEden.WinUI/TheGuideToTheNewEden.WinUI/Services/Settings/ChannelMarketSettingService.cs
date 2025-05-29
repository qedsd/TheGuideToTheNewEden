using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Models.ChannelMarket;

namespace TheGuideToTheNewEden.WinUI.Services.Settings
{
    internal class ChannelMarketSettingService
    {
        private static ChannelMarketSettingService current;
        internal static ChannelMarketSettingService Current
        {
            get
            {
                if (current == null)
                {
                    current = new ChannelMarketSettingService();
                }
                return current;
            }
        }
        private static readonly string Path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "ChannelMarketSettings.json");
        private Dictionary<string, ChannelMarketSetting> Values { get; set; }
        private ChannelMarketSettingService()
        {
            if (System.IO.File.Exists(Path))
            {
                string json = System.IO.File.ReadAllText(Path);
                var values = JsonConvert.DeserializeObject<List<ChannelMarketSetting>>(json);
                if (values != null)
                {
                    Values = values.ToDictionary(p => p.CharacterName);
                }
                else
                {
                    Values = new Dictionary<string, ChannelMarketSetting>();
                }
            }
            else
            {
                Values = new Dictionary<string, ChannelMarketSetting>();
            }
        }

        public static ChannelMarketSetting GetValue(string characterName)
        {
            if (Current.Values.TryGetValue(characterName, out var value))
            {
                return value.DepthClone<ChannelMarketSetting>();
            }
            else
            {
                return null;
            }
        }
        public static void Save()
        {
            string json = JsonConvert.SerializeObject(Current.Values.Select(p => p.Value));
            string path = System.IO.Path.GetDirectoryName(Path);
            if (!System.IO.Directory.Exists(path))
            {
                System.IO.Directory.CreateDirectory(path);
            }
            System.IO.File.WriteAllText(Path, json);
        }
        public static void SetValue(ChannelMarketSetting value)
        {
            Current.Values.Remove(value.CharacterName);
            Current.Values.Add(value.CharacterName, value);
            Save();
        }
    }
}
