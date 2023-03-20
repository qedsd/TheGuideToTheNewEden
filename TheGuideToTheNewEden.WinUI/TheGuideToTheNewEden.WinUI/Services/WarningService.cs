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
        public static void AddNotifyWindow(Core.Models.EarlyWarningSetting setting, Core.Models.Map.IntelSolarSystemMap intelMap)
        {
            IntelWindow intelWindow = new IntelWindow(setting, intelMap);
            Current.WarningWindows.Add(setting.Listener, intelWindow);
        }
        public static void ShowWindow(string listener)
        {
            if (Current.WarningWindows.TryGetValue(listener, out var value))
            {
                value.Show();
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
