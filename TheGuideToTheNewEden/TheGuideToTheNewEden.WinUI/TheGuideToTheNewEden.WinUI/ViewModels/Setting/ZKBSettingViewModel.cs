using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.WinUI.Services.Settings;
using TheGuideToTheNewEden.Core.Extensions;

namespace TheGuideToTheNewEden.WinUI.ViewModels.Setting
{
    public class ZKBSettingViewModel : BaseViewModel
    {
        private bool _autoConnect;
        public bool AutoConnect
        {
            get => _autoConnect;
            set
            {
                if (SetProperty(ref _autoConnect, value))
                {
                    Save();
                }
            }
        }

        private bool _notify;
        public bool Notify
        {
            get => _notify;
            set
            {
                if (SetProperty(ref _notify, value))
                {
                    Save();
                }
            }
        }

        private double _minNotifyValue;
        public double MinNotifyValue
        {
            get => _minNotifyValue;
            set
            {
                if (SetProperty(ref _minNotifyValue, value))
                {
                    Save();
                }
            }
        }

        private string _types;
        public string Types
        {
            get => _types;
            set
            {
                if (SetProperty(ref _types, value))
                {
                    Save();
                }
            }
        }

        private string _systems;
        public string Systems
        {
            get => _systems;
            set
            {
                if (SetProperty(ref _systems, value))
                {
                    Save();
                }
            }
        }

        private string _regions;
        public string Regions
        {
            get => _regions;
            set
            {
                if (SetProperty(ref _regions, value))
                {
                    Save();
                }
            }
        }

        private string _characters;
        public string Characters
        {
            get => _characters;
            set
            {
                if (SetProperty(ref _characters, value))
                {
                    Save();
                }
            }
        }

        private string _corps;
        public string Corps
        {
            get => _corps;
            set
            {
                if (SetProperty(ref _corps, value))
                {
                    Save();
                }
            }
        }

        private string _alliances;
        public string Alliances
        {
            get => _alliances;
            set
            {
                if (SetProperty(ref _alliances, value))
                {
                    Save();
                }
            }
        }

        public ZKBSettingViewModel()
        {
            var setting = ZKBSettingService.Setting;
            _autoConnect = ZKBSettingService.Setting.AutoConnect;
            _notify = ZKBSettingService.Setting.Notify;
            _minNotifyValue = ZKBSettingService.Setting.MinNotifyValue;
            StringBuilder stringBuilder = new StringBuilder();
            if (setting.Types.NotNullOrEmpty())
            {
                foreach(var type in setting.Types)
                {
                    stringBuilder.Append(type);
                    stringBuilder.Append(',');
                }
                stringBuilder.Remove(stringBuilder.Length - 1, 1);
                _types = stringBuilder.ToString();
            }
            if (setting.Systems.NotNullOrEmpty())
            {
                stringBuilder.Clear();
                foreach (var item in setting.Systems)
                {
                    stringBuilder.Append(item);
                    stringBuilder.Append(',');
                }
                stringBuilder.Remove(stringBuilder.Length - 1, 1);
                _systems = stringBuilder.ToString();
            }
            if (setting.Regions.NotNullOrEmpty())
            {
                stringBuilder.Clear();
                foreach (var item in setting.Regions)
                {
                    stringBuilder.Append(item);
                    stringBuilder.Append(',');
                }
                stringBuilder.Remove(stringBuilder.Length - 1, 1);
                _regions = stringBuilder.ToString();
            }
            if (setting.Characters.NotNullOrEmpty())
            {
                stringBuilder.Clear();
                foreach (var item in setting.Characters)
                {
                    stringBuilder.Append(item);
                    stringBuilder.Append(',');
                }
                stringBuilder.Remove(stringBuilder.Length - 1, 1);
                _characters = stringBuilder.ToString();
            }
            if (setting.Corps.NotNullOrEmpty())
            {
                stringBuilder.Clear();
                foreach (var item in setting.Corps)
                {
                    stringBuilder.Append(item);
                    stringBuilder.Append(',');
                }
                stringBuilder.Remove(stringBuilder.Length - 1, 1);
                _corps = stringBuilder.ToString();
            }
            if (setting.Alliances.NotNullOrEmpty())
            {
                stringBuilder.Clear();
                foreach (var item in setting.Alliances)
                {
                    stringBuilder.Append(item);
                    stringBuilder.Append(',');
                }
                stringBuilder.Remove(stringBuilder.Length - 1, 1);
                _alliances = stringBuilder.ToString();
            }
        }

        private void Save()
        {
            try
            {
                ZKBSettingService.Setting.AutoConnect = AutoConnect;
                ZKBSettingService.Setting.Notify = Notify;
                ZKBSettingService.Setting.MinNotifyValue = (long)MinNotifyValue;
                if (!string.IsNullOrEmpty(Types))
                {
                    ZKBSettingService.Setting.Types = Types.Split(',').Select(p => int.Parse(p)).ToHashSet2();
                }
                else
                {
                    ZKBSettingService.Setting.Types = null;
                }
                if (!string.IsNullOrEmpty(Systems))
                {
                    ZKBSettingService.Setting.Systems = Systems.Split(',').Select(p => int.Parse(p)).ToHashSet2();
                }
                else
                {
                    ZKBSettingService.Setting.Systems = null;
                }
                if (!string.IsNullOrEmpty(Regions))
                {
                    ZKBSettingService.Setting.Regions = Regions.Split(',').Select(p => int.Parse(p)).ToHashSet2();
                }
                else
                {
                    ZKBSettingService.Setting.Regions = null;
                }
                if (!string.IsNullOrEmpty(Characters))
                {
                    ZKBSettingService.Setting.Characters = Characters.Split(',').Select(p => int.Parse(p)).ToHashSet2();
                }
                else
                {
                    ZKBSettingService.Setting.Characters = null;
                }
                if (!string.IsNullOrEmpty(Corps))
                {
                    ZKBSettingService.Setting.Corps = Corps.Split(',').Select(p => int.Parse(p)).ToHashSet2();
                }
                else
                {
                    ZKBSettingService.Setting.Corps = null;
                }
                if (!string.IsNullOrEmpty(Alliances))
                {
                    ZKBSettingService.Setting.Alliances = Alliances.Split(',').Select(p => int.Parse(p)).ToHashSet2();
                }
                else
                {
                    ZKBSettingService.Setting.Alliances = null;
                }
                ZKBSettingService.Save();
            }
            catch(Exception ex)
            {
                Window?.ShowError(ex.Message);
                Core.Log.Error(ex);
            }
        }
    }
}
