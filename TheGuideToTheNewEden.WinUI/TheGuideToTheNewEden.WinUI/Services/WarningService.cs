using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.WinUI.Wins;

namespace TheGuideToTheNewEden.WinUI.Services
{
    /// <summary>
    ///管理预警通知
    /// </summary>
    internal class WarningService
    {
        private static WarningService current;
        private static WarningService Current
        {
            get
            {
                if(current == null)
                {
                    current = new WarningService();
                }
                return current; 
            }
        }
        /// <summary>
        /// 一个角色绑定一个通知窗口
        /// </summary>
        private Dictionary<string, IntelWindow> WarningWindows = new Dictionary<string, IntelWindow>();

        public static void NotifyWindow(string listener, EarlyWarningContent content)
        {
            if(Current.WarningWindows.TryGetValue(listener,out var value))
            {
                value.Intel(content);
            }
        }
        public static bool AddNotifyWindow(Core.Models.EarlyWarningSetting setting, Core.Models.Map.IntelSolarSystemMap intelMap)
        {
            if (Current.WarningWindows.ContainsKey(setting.Listener))
            {
                return false;
            }
            else
            {
                IntelWindow intelWindow = new IntelWindow(setting, intelMap);
                Current.WarningWindows.Add(setting.Listener, intelWindow);
                return true;
            }
        }
        public static void ShowWindow(string listener)
        {
            if (Current.WarningWindows.TryGetValue(listener, out var value))
            {
                value.Show();
            }
        }
        public static void RemoveWindow(string listener)
        {
            if (Current.WarningWindows.TryGetValue(listener, out var value))
            {
                value.Dispose();
                Current.WarningWindows.Remove(listener);
            }
        }
        public static void ClearWindow()
        {
            foreach(var item in Current.WarningWindows.Values)
            {
                item.Dispose();
            }
        }
        public static void NotifyToast(EarlyWarningContent content)
        {
            Notifications.IntelToast.SendToast(content);
        }

        public static void NotifySound(EarlyWarningContent content)
        {

        }
    }
}
