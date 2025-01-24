using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.WinUI.Views;

namespace TheGuideToTheNewEden.WinUI.Services
{
    internal static class NavigationService
    {
        internal static TabViewBasePage BasePage { get; set; }
        internal static HomePage HomePage { get; set; }
        internal static void NavigateTo(object content, string title)
        {
            HomePage.AddTabViewItem(content, title);
        }
        internal static void SetNavigateTo(string title)
        {
            HomePage.SetNavigateTo(title);
        }
        public static bool ShowWaiting(string tip = null)
        {
            if(BasePage != null)
            {
                BasePage.ShowWaiting(tip);
                return true;
            }
            else
            {
                return false;
            }
        }
        public static bool ShowWaiting(TabViewBasePage page, string tip = null)
        {
            if (page != null)
            {
                page.ShowWaiting(tip);
                return true;
            }
            else
            {
                return false;
            }
        }
        public static bool HideWaiting()
        {
            if (BasePage != null)
            {
                BasePage.HideWaiting();
                return true;
            }
            else
            {
                return false;
            }
        }
        public static bool HideWaiting(TabViewBasePage page)
        {
            if (page != null)
            {
                page.HideWaiting();
                return true;
            }
            else
            {
                return false;
            }
        }

        public static TabViewBasePage GetCurrentPage()
        {
            return BasePage;
        }
    }
}
