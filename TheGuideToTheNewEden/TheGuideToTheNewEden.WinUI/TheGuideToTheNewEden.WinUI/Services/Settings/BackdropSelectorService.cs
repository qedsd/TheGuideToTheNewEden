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
        public enum BackdropType
        {
            None,
            Mica,
            MicaAlt,
            DesktopAcrylic,
            AcrylicThin,
            AcrylicBase,
            Transparent,
            CustomPicture
        }
        private const string BackdropTypeKey = "AppBackdrop";
        private const string CustomPictureFileKey = "AppBackdropCustomPictureFile";
        private const string CustomPictureOverlapColorKey = "AppBackdropCustomPictureOverlapColor";
        /// <summary>
        /// 0-6对应 BackdropType 7为自定义图片
        /// </summary>
        public static BackdropType BackdropTypeValue { get; set; }
        public static string CustomPictureFileValue {  get; set; }
        public static string CustomPictureOverlapColorValue { get; set; }
        public static byte CustomPictureOverlapColorValue_A
        {
            get => byte.Parse(CustomPictureOverlapColorValue.Substring(1, 2));
        }
        public static void Initialize()
        {
            BackdropTypeValue = LoadFromSettings();
            CustomPictureFileValue = LoadCustomPictureFileFromSettings();
            CustomPictureOverlapColorValue = LoadCustomPictureOverlapColorFromSettings();
            Set();
        }

        public static void Set(int value)
        {
            BackdropTypeValue = (BackdropType)value;
            Save(BackdropTypeValue.ToString());
            Set();
            OnBackdropTypeChanged?.Invoke(null, BackdropTypeValue);
        }
        public static void SetCustomPictureFile(string value)
        {
            CustomPictureFileValue = value;
            SaveCustomPictureFile(CustomPictureFileValue);
            OnCustomPictureFileChanged?.Invoke(null, CustomPictureFileValue);
        }
        public static void SetCustomPictureOverlapColor(string value)
        {
            CustomPictureOverlapColorValue = value;
            SaveCustomPictureOverlapColor(CustomPictureOverlapColorValue);
            OnCustomPictureOverlapColorChanged?.Invoke(null, CustomPictureOverlapColorValue);
        }
        public static WinUICommunity.BackdropType GetWinUICommunityBackdropTypeValue()
        {
            return (WinUICommunity.BackdropType)(((int)BackdropTypeValue) % 7);
        }
        public static Windows.UI.Color GetCustomPictureOverlapColor()
        {
            byte a = Convert.ToByte(CustomPictureOverlapColorValue.Substring(1, 2), 16);
            byte r = Convert.ToByte(CustomPictureOverlapColorValue.Substring(3, 2), 16);
            byte g = Convert.ToByte(CustomPictureOverlapColorValue.Substring(5, 2), 16);
            byte b = Convert.ToByte(CustomPictureOverlapColorValue.Substring(7, 2), 16);
            return Windows.UI.Color.FromArgb(a,r,g,b);
        }
        private static BackdropType LoadFromSettings()
        {
            string value = SettingService.GetValue(BackdropTypeKey);
            if (!string.IsNullOrEmpty(value))
            {
                return (BackdropType)Enum.Parse(typeof(BackdropType), value);
            }
            else
            {
                return BackdropType.AcrylicBase;
            }
        }
        private static string LoadCustomPictureFileFromSettings()
        {
            string value = SettingService.GetValue(CustomPictureFileKey);
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }
            else
            {
                return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Images", "home.jpg");
            }
        }
        private static string LoadCustomPictureOverlapColorFromSettings()
        {
            string value = SettingService.GetValue(CustomPictureOverlapColorKey);
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }
            else
            {
                return "#00000000";
            }
        }

        private static void  Save(string value)
        {
            SettingService.SetValue(BackdropTypeKey, value);
        }
        private static void SaveCustomPictureFile(string value)
        {
            SettingService.SetValue(CustomPictureFileKey, value);
        }
        private static void SaveCustomPictureOverlapColor(string value)
        {
            SettingService.SetValue(CustomPictureOverlapColorKey, value);
        }

        private static void Set()
        {
            foreach (Window window in Helpers.WindowHelper.ActiveWindows)
            {
                (window as BaseWindow).ThemeService?.SetBackdropType(GetWinUICommunityBackdropTypeValue());
            }
        }
        public static event EventHandler<BackdropType> OnBackdropTypeChanged;
        public static event EventHandler<string> OnCustomPictureFileChanged;
        public static event EventHandler<string> OnCustomPictureOverlapColorChanged;
    }
}
