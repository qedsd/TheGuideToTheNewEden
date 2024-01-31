using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.WinUI.Services.Settings;

namespace TheGuideToTheNewEden.WinUI.ViewModels.Setting
{
    public class ZKBSettingViewModel : BaseViewModel
    {
        private bool _autoConnect = ZKBSettingService.Setting.AutoConnect;
        public bool AutoConnect
        {
            get => _autoConnect;
            set
            {
                if (SetProperty(ref _autoConnect, value))
                {
                    ZKBSettingService.Save();
                }
            }
        }

        private bool _notify = ZKBSettingService.Setting.Notify;
        public bool Notify
        {
            get => _notify;
            set
            {
                if (SetProperty(ref _notify, value))
                {
                    ZKBSettingService.Save();
                }
            }
        }

        private long _minNotifyValue = ZKBSettingService.Setting.MinNotifyValue;
        public long MinNotifyValue
        {
            get => _minNotifyValue;
            set
            {
                if (SetProperty(ref _minNotifyValue, value))
                {
                    ZKBSettingService.Save();
                }
            }
        }

        private string _types = ZKBSettingService.Setting.MinNotifyValue;
        public long MinNotifyValue
        {
            get => _minNotifyValue;
            set
            {
                if (SetProperty(ref _minNotifyValue, value))
                {
                    ZKBSettingService.Save();
                }
            }
        }
    }
}
