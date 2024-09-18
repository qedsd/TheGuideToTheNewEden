using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.Core.Models.GamePreviews
{
    public class ProcessInfo : ObservableObject
    {
        public System.Diagnostics.Process Process { get; set; }
        public IntPtr MainWindowHandle { get; set; }
        public string WindowTitle { get; set; }
        public string ProcessName { get; set; }
        private bool running;
        public bool Running
        {
            get=>running; set => SetProperty(ref running,value);
        }
        public string GUID
        {
            get => _guid;
        }
        public string GetCharacterName()
        {
            if(!string.IsNullOrEmpty(WindowTitle))
            {
                int index = WindowTitle.IndexOf('-');
                if(index > -1)
                {
                    return WindowTitle.Substring(index);
                }
            }
            return null;
        }
        /// <summary>
        /// 获取账号名称
        /// </summary>
        /// <returns></returns>
        public string GetUserName()
        {
            string localAppDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string processesFilePath = System.IO.Path.Combine(localAppDataFolder, "CCP", "EVE", "Launcher", "processes.json");
            if(System.IO.File.Exists(processesFilePath))
            {
                var json = System.IO.File.ReadAllText(processesFilePath);
                if(!string.IsNullOrEmpty(json))
                {
                    var pross = JsonConvert.DeserializeObject<LauncherProcesses>(json);
                    if(pross != null && pross.Processes.NotNullOrEmpty())
                    {
                        return pross.Processes.FirstOrDefault(p => p.Pid == Process.Id)?.Username;
                    }
                }
            }
            return null;
        }

        private string _guid = Guid.NewGuid().ToString();

        private string settingName;
        public string SettingName
        {
            get => settingName;
            set
            {
                SetProperty(ref settingName, value);
                ShowSettingName = !string.IsNullOrEmpty(value);
            }
        }

        private bool showSettingName;
        public bool ShowSettingName
        {
            get => showSettingName; set => SetProperty(ref showSettingName, value);
        }

        private PreviewItem setting;
        [JsonIgnore]
        public PreviewItem Setting
        {
            get => setting;set => SetProperty(ref setting, value);
        }

        private class LauncherProcesses
        {
            public List<LauncherProcess> Processes { get; set; }
        }
        private class LauncherProcess
        {
            public int Pid { get; set; }
            public string Servername { get; set; }
            public int UserId { get; set; }
            public string Username { get; set; }
        }

        [JsonIgnore]
        public int Sort { get; set; } = int.MaxValue;
    }
}
