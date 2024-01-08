using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Extensions;
using TheGuideToTheNewEden.Core.Models;

namespace TheGuideToTheNewEden.WinUI.Services.Settings
{
    class ChannelMonitorSettingService
    {
        private static ChannelMonitorSettingService current;
        internal static ChannelMonitorSettingService Current
        {
            get
            {
                if (current == null)
                {
                    current = new ChannelMonitorSettingService();
                }
                return current;
            }
        }
        private static readonly string Path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "ChannelMonitorSetting.json");
        private Dictionary<string, ChannelMonitorSetting> Values { get; set; }
        private ChannelMonitorSettingService()
        {
            if (System.IO.File.Exists(Path))
            {
                string json = System.IO.File.ReadAllText(Path);
                var values = JsonConvert.DeserializeObject<List<ChannelMonitorSetting>>(json);
                if (values != null)
                {
                    Values = values.ToDictionary(p => p.Name);
                }
                else
                {
                    Values = new Dictionary<string, ChannelMonitorSetting>();
                }
            }
            else
            {
                Values = new Dictionary<string, ChannelMonitorSetting>();
            }
        }

        public static ChannelMonitorSetting GetValue(string name)
        {
            if (Current.Values.TryGetValue(name, out var value))
            {
                return value.DepthClone<ChannelMonitorSetting>();
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
        public static void SetValue(ChannelMonitorSetting value)
        {
            Current.Values.Remove(value.Name);
            Current.Values.Add(value.Name, value);
            Save();
        }
    }
}
