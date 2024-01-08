using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Extensions
{
    public static class PageExtension
    {
        public static BaseWindow GetBaseWindow(this Page page)
        {
            return Helpers.WindowHelper.GetWindowForElement(page) as BaseWindow;
        }
        public static Microsoft.UI.Xaml.Window GetWindow(this Page page)
        {
            return Helpers.WindowHelper.GetWindowForElement(page);
        }
    }
}
