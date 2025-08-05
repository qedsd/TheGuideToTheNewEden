using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class HomePage2 : Page,IPage
    {
        public HomePage2()
        {
            this.InitializeComponent();
        }

        public void Close()
        {
            
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            ClientServiceHelper.GetRequiredService<Services.PageNavigationService>().ShowWaiting(this.GetType().Name);
            await Task.Delay(3000);
            ClientServiceHelper.GetRequiredService<Services.PageNavigationService>().HideWaiting(this.GetType().Name);
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            ClientServiceHelper.GetRequiredService<Services.PageNavigationService>().ShowMsg(this.GetType().Name, "Test", Controls.InfoBarControl.InfoType.Error, false);
        }

        private void ToolWindow_Click(object sender, RoutedEventArgs e)
        {
            ToolWindow toolWindow = new ToolWindow(null, WindowTitleStyle.OnlyClose, true, false);
            toolWindow.Activate();
        }
    }
}
