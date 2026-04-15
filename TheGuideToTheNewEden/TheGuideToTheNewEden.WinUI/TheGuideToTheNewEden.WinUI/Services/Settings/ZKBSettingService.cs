using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.Models.KB;

namespace TheGuideToTheNewEden.WinUI.Services.Settings
{
    internal static class ZKBSettingService
    {
        private static ZKBStreamConfig _setting;
        public static ZKBStreamConfig Setting
        {
            get
            {
                if (_setting == null)
                {
                    if (System.IO.File.Exists(Path))
                    {
                        string json = System.IO.File.ReadAllText(Path);
                        _setting = JsonConvert.DeserializeObject<ZKBStreamConfig>(json);
                    }
                    else
                    {
                        _setting = new ZKBStreamConfig();
                    }
                }
                return _setting;
            }
        }
        private static readonly string Path = System.IO.Path.Combine(App.DataPath, "Configs", "ZKBStreamConfig.json");
        public static void Save()
        {
            string json = JsonConvert.SerializeObject(Setting);
            string path = System.IO.Path.GetDirectoryName(Path);
            if (!System.IO.Directory.Exists(path))
            {
                System.IO.Directory.CreateDirectory(path);
            }
            System.IO.File.WriteAllText(Path, json);
        }
    }
}
