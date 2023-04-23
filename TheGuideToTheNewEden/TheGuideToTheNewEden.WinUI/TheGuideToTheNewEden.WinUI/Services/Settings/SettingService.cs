using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Services.Settings
{
    public static class SettingService
    {
        private static readonly string Path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "settings.json");
        public static Dictionary<string, string> Values { get; set; }
        public static void Save()
        {
            string json = JsonConvert.SerializeObject(Values);
            string path = System.IO.Path.GetDirectoryName(Path);
            if (!System.IO.Directory.Exists(path))
            {
                System.IO.Directory.CreateDirectory(path);
            }
            System.IO.File.WriteAllText(Path, json);
        }
        public static void Load()
        {
            if (System.IO.File.Exists(Path))
            {
                string json = System.IO.File.ReadAllText(Path);
                Values = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                if (Values == null)
                {
                    Values = new Dictionary<string, string>();
                }
            }
            else
            {
                Values = new Dictionary<string, string>();
            }
            
            GameServerSelectorService.Initialize();
            LanguageSelectorService.Initialize();
            LocalDbSelectorService.Initialize();
            PlayerStatusService.Initialize();
            ThemeSelectorService.Initialize();
            EVELogsPathSelectorService.Initialize();
        }
        public static string GetValue(string key)
        {
            if (Values.TryGetValue(key, out string value))
            {
                return value;
            }
            else
            {
                return null;
            }
        }
        public static async Task SetValueAsync(string key, string value)
        {
            await Task.Run(() =>
            {
                Values.Remove(key);
                Values.Add(key, value);
                Save();
            });
        }
    }
}
