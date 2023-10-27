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
    internal class GameLogInfoSettingService
    {
        private static GameLogInfoSettingService current;
        internal static GameLogInfoSettingService Current
        {
            get
            {
                if (current == null)
                {
                    current = new GameLogInfoSettingService();
                }
                return current;
            }
        }
        private static readonly string Path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "GameLogInfoSettings.json");
        private Dictionary<int, GameLogSetting> Values { get; set; }
        private GameLogInfoSettingService()
        {
            if (System.IO.File.Exists(Path))
            {
                string json = System.IO.File.ReadAllText(Path);
                var values = JsonConvert.DeserializeObject<List<GameLogSetting>>(json);
                if (values != null)
                {
                    Values = values.ToDictionary(p => p.ListenerID);
                }
                else
                {
                    Values = new Dictionary<int, GameLogSetting>();
                }
            }
            else
            {
                Values = new Dictionary<int, GameLogSetting>();
            }
        }

        public static GameLogSetting GetValue(int id)
        {
            if (Current.Values.TryGetValue(id, out var value))
            {
                return value.DepthClone<GameLogSetting>();
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
        public static void SetValue(GameLogSetting value)
        {
            Current.Values.Remove(value.ListenerID);
            Current.Values.Add(value.ListenerID, value);
            Save();
        }
    }
}
