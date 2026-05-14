using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;

namespace TheGuideToTheNewEden.WinUI.Extensions
{
    public static class WindowExtension
    {
        private static readonly Dictionary<WindowId, string> _windows = new Dictionary<WindowId, string>();
        public static void LogPositionAndSize(this Window window, string settingKey = null)
        {
            try
            {
                window.AppWindow.Closing += AppWindow_Closing;
                window.AppWindow.Changed += AppWindow_Changed;
                string key = string.IsNullOrEmpty(settingKey) ? $"{window.GetType().Name}PositionAndSize" : settingKey;
                _windows.Add(window.AppWindow.Id, key);
                var str = Services.Settings.SettingService.GetValue(key);
                if (!string.IsNullOrEmpty(str))
                {
                    var array = str.Split(',');
                    if (array.Length == 4)
                    {
                        int x = int.Parse(array[0]);
                        int y = int.Parse(array[1]);
                        int w = int.Parse(array[2]);
                        int h = int.Parse(array[3]);
                        Helpers.WindowHelper.MoveToScreen(window, x, y);
                        window.AppWindow.Resize(new Windows.Graphics.SizeInt32(w, h));
                    }
                }
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
            }
        }

        private static void AppWindow_Closing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
        {
            _windows.Remove(sender.Id);
        }

        private static void AppWindow_Changed(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowChangedEventArgs args)
        {
            if (args.DidPositionChange)
            {
                if (!Helpers.WindowHelper.IsInWindow(sender.Position.X, sender.Position.Y))
                {
                    return;//不保存位置
                }
            }
            if (args.DidPositionChange || args.DidSizeChange)
            {
                if(_windows.TryGetValue(sender.Id, out string key))
                {
                    Services.Settings.SettingService.SetValue(key, $"{sender.Position.X},{sender.Position.Y},{sender.Size.Width},{sender.Size.Height}");
                }
            }
        }
    }
}
