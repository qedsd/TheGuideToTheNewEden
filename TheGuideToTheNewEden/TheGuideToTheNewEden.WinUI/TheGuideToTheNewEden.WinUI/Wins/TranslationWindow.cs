using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.WinUI.Extensions;
using TheGuideToTheNewEden.WinUI.Views;

namespace TheGuideToTheNewEden.WinUI.Wins
{
    internal class TranslationWindow : ToolWindow
    {
        private TranslationWindowPage _page;
        public TranslationWindow()
        {
            _page = new TranslationWindowPage();
            InitWindow(_page, WindowTitleStyle.Default, true, true, true, true);

            SetDisplayTitle(Helpers.ResourcesHelper.GetString("TranslationPage"));
            SetWindowTitle(Helpers.ResourcesHelper.GetString("TranslationPage"));
            _page.SetWindow(this);
            this.SetAlwaysOnTop();
            this.LogPositionAndSize();
        }
    }
}
