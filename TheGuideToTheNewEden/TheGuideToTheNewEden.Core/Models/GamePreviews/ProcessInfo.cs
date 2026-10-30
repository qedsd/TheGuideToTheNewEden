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

        /// <summary>
        /// 进程 Id —— 多开用它做"客户端身份"。
        /// <para>
        /// 不能用 <see cref="MainWindowHandle"/> 当身份：句柄是"某一次枚举顶层窗口的结果"，
        /// 窗口重建（切全屏/改分辨率/换显示器/驱动重置）会换新值，客户端忙时也可能一瞬间读不到。
        /// 用句柄做匹配键，这种抖动就会被当成"旧客户端退出 + 新客户端接入"，
        /// 进而把预览窗关掉、把该号踢出轮次（用户反馈"切下一个号后预览窗没了、再也切不到"）。
        /// PID 只在进程退出时才变，且同一时刻唯一。
        /// </para>
        /// </summary>
        public int ProcessId { get; set; }

        /// <summary>
        /// 连续多少轮扫描没看到这个客户端（1 秒一轮）。用于"别一秒就判死"的宽限，不持久化。
        /// </summary>
        [JsonIgnore]
        public int MissingTicks { get; set; }

        /// <summary>
        /// 用户期望这个客户端在预览：<see cref="PreviewWindowManager.Start"/> 置 true，
        /// <b>只有用户主动停止</b>才置 false；异常/意外丢失（半途异常、窗口被顶掉）时保持 true，
        /// 由每轮对账把预览重新拉起来，避免该号永久掉出快捷键轮次。
        /// </summary>
        [JsonIgnore]
        public bool PreviewExpected { get; set; }
        private string _windowTitle;
        public string WindowTitle{ get => _windowTitle; set => SetProperty(ref _windowTitle, value); }
        
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
                    return WindowTitle.Substring(index + 1).Trim();
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
