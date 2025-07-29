using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.WinUI.Controls;

namespace TheGuideToTheNewEden.WinUI.Extensions
{
    public static class PageExtension
    {
        public static Microsoft.UI.Xaml.Window GetWindow(this Page page)
        {
            return Helpers.WindowHelper.GetWindowForElement(page);
        }
        public static void ShowMsg(this Page page, string msg, InfoBarControl.InfoType infoType, bool autoClose, string title = null)
        {
            ClientServiceHelper.GetRequiredService<Services.PageNavigationService>().ShowMsg($"Navigation.{page.GetType().Name}", msg, infoType, autoClose, title);
        }
        public static void ShowWaiting(this Page page, string tip = null)
        {
            ClientServiceHelper.GetRequiredService<Services.PageNavigationService>().ShowWaiting(page, tip);
        }
        public static void ShowError(this Page page, string msg)
        {
            page.ShowMsg(msg, InfoBarControl.InfoType.Error, false);
        }
        public static void ShowSuccess(this Page page, string msg)
        {
            page.ShowMsg(msg, InfoBarControl.InfoType.Success, true);
        }
        

        public static void HideWaiting(this Page page)
        {
            ClientServiceHelper.GetRequiredService<Services.PageNavigationService>().HideWaiting(page);
        }
    }
}
