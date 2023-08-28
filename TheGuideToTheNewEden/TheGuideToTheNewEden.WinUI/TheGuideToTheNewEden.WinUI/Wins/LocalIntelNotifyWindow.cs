using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.WinUI.Views;

namespace TheGuideToTheNewEden.WinUI.Wins
{
    public class LocalIntelNotifyWindow : BaseWindow
    {
        private readonly LocalIntelNotifyWindowPage _localIntelNotifyWindowPage = new LocalIntelNotifyWindowPage();
        public LocalIntelNotifyWindow()
        {
            this.InitializeComponent();
            HideAppDisplayName();
            Head = Helpers.ResourcesHelper.GetString("LocalIntel_WindowNotifyTitle");
            MainContent = _localIntelNotifyWindowPage;
            Helpers.WindowHelper.TopMost(this);
            Helpers.WindowHelper.GetAppWindow(this).Resize(new Windows.Graphics.SizeInt32(500, 600));
            Helpers.WindowHelper.CenterToScreen(this);
            Helpers.WindowHelper.GetAppWindow(this).Closing += AppWindow_Closing;
        }
        public void Add(LocalIntelNotify localIntelNotify)
        {
            if (localIntelNotify != null)
            {
                this.DispatcherQueue.TryEnqueue(() =>
                {
                    _localIntelNotifyWindowPage.Add(localIntelNotify);
                    this.Activate();
                });
            }
        }
        private void AppWindow_Closing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
        {
            args.Cancel = true;
            sender.Hide();
        }

        public void Dispose()
        {
            Helpers.WindowHelper.GetAppWindow(this).Closing -= AppWindow_Closing;
            this.Close();
        }
    }
}
