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
        private static KBSetting _setting;
        public static KBSetting Setting
        {
            get
            {
                if (_setting == null)
                {
                    if (System.IO.File.Exists(Path))
                    {
                        string json = System.IO.File.ReadAllText(Path);
                        _setting = JsonConvert.DeserializeObject<KBSetting>(json);
                    }
                    else
                    {
                        _setting = new KBSetting();
                    }
                }
                return _setting;
            }
        }
        private static readonly string Path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "ZKBSetting.json");
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
