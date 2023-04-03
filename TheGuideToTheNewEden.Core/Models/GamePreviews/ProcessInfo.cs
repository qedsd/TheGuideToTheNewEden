using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace TheGuideToTheNewEden.Core.Models.GamePreviews
{
    public class ProcessInfo : ObservableObject
    {
        public IntPtr MainWindowHandle { get; set; }
        public string WindowTitle { get; set; }
        public string ProcessName { get; set; }
        private bool running;
        public bool Running
        {
            get=>running; set => SetProperty(ref running,value);
        }
        public string GetCharacterName()
        {
            if(!string.IsNullOrEmpty(WindowTitle))
            {
                var array = WindowTitle.Split('-');
                if(array.Length == 2)
                {
                    return array[1].Trim();
                }
            }
            return null;
        }
        private string _guid = Guid.NewGuid().ToString();
        public string GetGuid()
        {
            string name = GetCharacterName();
            if(string.IsNullOrEmpty(name))
            {
                return _guid;
            }
            else
            {
                return name;
            }
        }

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
    }
}
