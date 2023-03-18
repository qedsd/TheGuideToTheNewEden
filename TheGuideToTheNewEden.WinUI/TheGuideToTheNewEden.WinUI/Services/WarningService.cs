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

        public static void NotifyWindow(string Listener, Core.Models.Map.IntelSolarSystemMap intelMap, EarlyWarningContent content)
        {
            if(Current.WarningWindows.TryGetValue(Listener,out var value))
            {

            }
            else
            {
                IntelWindow intelWindow = new IntelWindow(intelMap);
                intelWindow.Show();
            }
            
            
        }
        public static void NotifyWindow(EarlyWarningContent content)
        {

        }
        public static void UpdateWindow(string Listener, Core.Models.Map.IntelSolarSystemMap intelMap)
        {
            if (Current.WarningWindows.TryGetValue(Listener, out var value))
            {
                value.Init(intelMap);
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
