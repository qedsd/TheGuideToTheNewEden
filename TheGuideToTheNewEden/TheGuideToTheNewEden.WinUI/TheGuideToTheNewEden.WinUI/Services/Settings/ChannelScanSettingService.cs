using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models.CharacterScan;

namespace TheGuideToTheNewEden.WinUI.Services.Settings
{
    internal static class ChannelScanSettingService
    {
        private static readonly string Path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "ChannelScanSetting.json");

        public static ChannelScanConfig GetChannelScanConfig()
        {
            if (System.IO.File.Exists(Path))
            {
                string json = System.IO.File.ReadAllText(Path);
                return JsonConvert.DeserializeObject<ChannelScanConfig>(json);
            }
            else
            {
                return new ChannelScanConfig();
            }
        }

        public static void Save(ChannelScanConfig config)
        {
            string json = JsonConvert.SerializeObject(config);
            string path = System.IO.Path.GetDirectoryName(Path);
            if (!System.IO.Directory.Exists(path))
            {
                System.IO.Directory.CreateDirectory(path);
            }
            System.IO.File.WriteAllText(Path, json);
        }
    }
}
