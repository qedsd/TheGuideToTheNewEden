using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TheGuideToTheNewEden.SDEBuilder.WinUI
{
    public static class SettingService
    {
        public static readonly string SDEFolderKey = "SDEFolder";

        private static readonly string Path = System.IO.Path.Combine(AppContext.BaseDirectory, "Configs", "settings.json");
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
        public static void Init()
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
        public static void SetValue(string key, string value)
        {
            Values.Remove(key);
            Values.Add(key, value);
            Save();
        }
    }
}
