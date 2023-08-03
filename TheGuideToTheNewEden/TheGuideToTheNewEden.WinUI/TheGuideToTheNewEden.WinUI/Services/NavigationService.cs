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
        internal static HomePage HomePage { get; set; }
        internal static void NavigateTo(object content, string title)
        {
            HomePage.AddTabViewItem(content, title);
        }
        internal static void SetNavigateTo(string title)
        {
            HomePage.SetNavigateTo(title);
        }
    }
}
