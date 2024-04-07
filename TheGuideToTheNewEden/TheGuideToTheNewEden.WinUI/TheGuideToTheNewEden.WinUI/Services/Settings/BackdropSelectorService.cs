using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinUICommunity;

namespace TheGuideToTheNewEden.WinUI.Services.Settings
{
    public static class BackdropSelectorService
    {
        private const string Key = "AppBackdrop";
        public static BackdropType Value { get; set; }
        public static void Initialize()
        {
            Value = LoadFromSettings();
            Set();
        }

        public static void Set(int value)
        {
            Value = (BackdropType)value;
            Save(Value.ToString());
            Set();
            OnBackdropTypeChanged?.Invoke(null, Value);
        }

        private static BackdropType LoadFromSettings()
        {
            string value = SettingService.GetValue(Key);
            if (!string.IsNullOrEmpty(value))
            {
                return (BackdropType)Enum.Parse(typeof(BackdropType), value);
            }
            else
            {
                return BackdropType.AcrylicBase;
            }
        }

        private static void  Save(string value)
        {
            SettingService.SetValue(Key, value);
        }

        private static void Set()
        {
            foreach (Window window in Helpers.WindowHelper.ActiveWindows)
            {
                (window as BaseWindow).ThemeService?.SetBackdropType(Value);
            }
        }
        public static event EventHandler<BackdropType> OnBackdropTypeChanged;
    }
}
