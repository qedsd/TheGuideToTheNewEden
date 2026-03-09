using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TheGuideToTheNewEden.Core.Models.Map;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.WinUI.Services.Settings
{
    public class MapSettingService
    {
        private static MapSettingService current;
        internal static MapSettingService Current
        {
            get
            {
                if (current == null)
                {
                    current = new MapSettingService();
                }
                return current;
            }
        }
        private static readonly string Path = System.IO.Path.Combine(App.DataPath, "Configs", "MapSettings.json");
        private Core.Models.Map.MapConfig _config;

        public MapSettingService()
        {
            if (System.IO.File.Exists(Path))
            {
                string json = System.IO.File.ReadAllText(Path);
                _config = JsonConvert.DeserializeObject<Core.Models.Map.MapConfig>(json);
                _config ??= new Core.Models.Map.MapConfig();
            }
            else
            {
                _config = new Core.Models.Map.MapConfig();
            }
        }

        public MapIntelConfig GetIntel()
        {
            return _config.Intel.DepthClone<MapIntelConfig>();
        }
        public void SaveIndel(MapIntelConfig intel)
        {
            _config.Intel = intel.DepthClone<MapIntelConfig>();
            Save();
        }

        private void Save()
        {
            string json = JsonConvert.SerializeObject(_config);
            string path = System.IO.Path.GetDirectoryName(Path);
            if (!System.IO.Directory.Exists(path))
            {
                System.IO.Directory.CreateDirectory(path);
            }
            System.IO.File.WriteAllText(Path, json);
        }
    }
}
