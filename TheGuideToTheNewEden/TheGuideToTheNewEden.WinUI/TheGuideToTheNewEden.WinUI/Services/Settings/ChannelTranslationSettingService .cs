using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Models.Channel.Translation;
using TheGuideToTheNewEden.Core.Models.ChannelMarket;

namespace TheGuideToTheNewEden.WinUI.Services.Settings
{
    internal class ChannelTranslationSettingService
    {
        private static ChannelTranslationSettingService current;
        internal static ChannelTranslationSettingService Current
        {
            get
            {
                if (current == null)
                {
                    current = new ChannelTranslationSettingService();
                }
                return current;
            }
        }
        private static readonly string Path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "ChannelTranslationSettings.json");
        private Dictionary<string, ChannelTranslationSetting> Values { get; set; }
        private ChannelTranslationSettingService()
        {
            if (System.IO.File.Exists(Path))
            {
                string json = System.IO.File.ReadAllText(Path);
                var values = JsonConvert.DeserializeObject<List<ChannelTranslationSetting>>(json);
                if (values != null)
                {
                    Values = values.ToDictionary(p => p.CharacterName);
                }
                else
                {
                    Values = new Dictionary<string, ChannelTranslationSetting>();
                }
            }
            else
            {
                Values = new Dictionary<string, ChannelTranslationSetting>();
            }
        }

        public static ChannelTranslationSetting GetValue(string characterName)
        {
            if (Current.Values.TryGetValue(characterName, out var value))
            {
                return value.DepthClone<ChannelTranslationSetting>();
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
        public static void SetValue(ChannelTranslationSetting value)
        {
            Current.Values.Remove(value.CharacterName);
            Current.Values.Add(value.CharacterName, value);
            Save();
        }
    }
}
