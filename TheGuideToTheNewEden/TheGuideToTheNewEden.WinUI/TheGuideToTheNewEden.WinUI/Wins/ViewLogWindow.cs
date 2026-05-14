using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.WinUI.Extensions;
using TheGuideToTheNewEden.WinUI.Views;

namespace TheGuideToTheNewEden.WinUI.Wins
{
    internal class ViewLogWindow : ToolWindow
    {
        private ViewLogPage _page;
        public ViewLogWindow()
        {
            _page = new ViewLogPage();
            string title = Helpers.ResourcesHelper.GetString("HomePage_Log");
            InitWindow(_page, WindowTitleStyle.Default, true, true, true, true);

            SetDisplayTitle(title);
            SetWindowTitle(title);
            SetAlwaysOnTop();

            this.LogPositionAndSize();

            Closed += ViewLogWindow_Closed;
        }

        private void ViewLogWindow_Closed(object sender, Microsoft.UI.Xaml.WindowEventArgs args)
        {
            _page.Close();
        }
    }
}
