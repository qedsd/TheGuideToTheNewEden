using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.WinUI.Services.Settings
{
    internal class IntelSettingService
    {
        private static IntelSettingService current;
        internal static IntelSettingService Current
        {
            get
            {
                if (current == null)
                {
                    current = new IntelSettingService();
                }
                return current;
            }
        }
        private static readonly string Path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "IntelSettings.json");
        private Dictionary<string, Core.Models.ChannelIntel.ChannelIntelSetting> Values { get; set; }
        private IntelSettingService()
        {
            if (System.IO.File.Exists(Path))
            {
                string json = System.IO.File.ReadAllText(Path);
                var values = JsonConvert.DeserializeObject<List<Core.Models.ChannelIntel.ChannelIntelSetting>>(json);
                if (values != null)
                {
                    Values = values.ToDictionary(p => p.Listener);
                }
                else
                {
                    Values = new Dictionary<string, Core.Models.ChannelIntel.ChannelIntelSetting>();
                }
            }
            else
            {
                Values = new Dictionary<string, Core.Models.ChannelIntel.ChannelIntelSetting>();
            }
        }

        public static Core.Models.ChannelIntel.ChannelIntelSetting GetValue(string characterName)
        {
            if (Current.Values.TryGetValue(characterName, out var value))
            {
                return value.DepthClone<Core.Models.ChannelIntel.ChannelIntelSetting>();
            }
            else
            {
                return null;
            }
        }
        public static void Save()
        {
            string json = JsonConvert.SerializeObject(Current.Values.Select(p=>p.Value));
            string path = System.IO.Path.GetDirectoryName(Path);
            if (!System.IO.Directory.Exists(path))
            {
                System.IO.Directory.CreateDirectory(path);
            }
            System.IO.File.WriteAllText(Path, json);
        }
        public static void SetValue(Core.Models.ChannelIntel.ChannelIntelSetting value)
        {
            Current.Values.Remove(value.Listener);
            Current.Values.Add(value.Listener, value);
            Save();
        }
    }
}
