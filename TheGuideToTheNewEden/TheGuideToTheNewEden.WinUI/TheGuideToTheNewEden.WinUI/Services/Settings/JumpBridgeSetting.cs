using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models;

namespace TheGuideToTheNewEden.WinUI.Services.Settings
{
    public class JumpBridgeSetting
    {
        public class JumpBridge
        {
            public JumpBridge() { }
            public JumpBridge(int id1, int id2)
            {
                System1 = id1;
                System2 = id2;
            }
            public int System1 { get; set; }
            public int System2 { get; set; }
        }
        public class JumpBridgeConfig
        {
            public bool ShowInMap { get; set; } = true;
            public List<JumpBridge> JumpBridges { get; set; } = new List<JumpBridge>();
        }
        private static JumpBridgeSetting _current;
        internal static JumpBridgeSetting Current
        {
            get
            {
                if (_current == null)
                {
                    _current = new JumpBridgeSetting();
                }
                return _current;
            }
        }
        private static readonly string Path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs", "JumpBridgeSetting.json");
        private List<JumpBridge> Values { get => Config.JumpBridges; }
        private Dictionary<int, int> ValuesMap { get; set; } = new Dictionary<int, int>();
        private JumpBridgeConfig Config { get; set; }
        private JumpBridgeSetting()
        {
            if (System.IO.File.Exists(Path))
            {
                string json = System.IO.File.ReadAllText(Path);
                Config = JsonConvert.DeserializeObject<JumpBridgeConfig>(json);
                foreach (var v in Values)
                {
                    ValuesMap.Add(v.System1, v.System2);
                    ValuesMap.Add(v.System2, v.System1);
                }
            }
            else
            {
                Config = new JumpBridgeConfig();
            }
        }

        public static List<JumpBridge> GetValue()
        {
            return Current.Values;
        }
        public static bool GetValue(int systemID, out int toSystemID)
        {
            toSystemID = -1;
            return Current.ValuesMap.TryGetValue(systemID, out toSystemID);
        }
        public static bool ExistBridge()
        {
            return Current.Values?.Count > 0;
        }
        public static bool IsShowBridge()
        {
            return Current.Config.ShowInMap;
        }
        public static void SetShowBridge(bool show)
        {
            Current.Config.ShowInMap = show;
            Save();
            SettingChanged?.Invoke(Current, EventArgs.Empty);
        }
        public static bool Add(int system1, int system2)
        {
            if(system1 != system2 && !Current.ValuesMap.ContainsKey(system1)&& !Current.ValuesMap.ContainsKey(system2))
            {
                Current.Values.Add(new JumpBridge(system1, system2));
                Current.ValuesMap.Add(system1, system2);
                Current.ValuesMap.Add(system2, system1);
                Save();
                SettingChanged?.Invoke(Current, EventArgs.Empty);
                return true;
            }
            else
            {
                return false;
            }
        }
        public static void Remove(int system1, int system2)
        {
            var target = Current.Values.FirstOrDefault(p => p.System1 == system1 && p.System2 == system2);
            if(target != null)
            {
                Current.Values.Remove(target);
                Current.ValuesMap.Remove(system1);
                Current.ValuesMap.Remove(system2);
                Save();
                SettingChanged?.Invoke(Current, EventArgs.Empty);
            }
        }
        public static void Save()
        {
            string json = JsonConvert.SerializeObject(Current.Config);
            string path = System.IO.Path.GetDirectoryName(Path);
            if (!System.IO.Directory.Exists(path))
            {
                System.IO.Directory.CreateDirectory(path);
            }
            System.IO.File.WriteAllText(Path, json);
        }

        public static event EventHandler SettingChanged;
    }
}
