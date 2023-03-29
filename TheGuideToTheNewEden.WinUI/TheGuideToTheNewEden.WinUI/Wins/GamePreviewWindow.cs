using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Wins
{
    internal class GamePreviewWindow: Window
    {
        public GamePreviewWindow()
        {
            var presenter = Helpers.WindowHelper.GetOverlappedPresenter(this);
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            //presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
            presenter.SetBorderAndTitleBar(false, false);
            var appWindow = Helpers.WindowHelper.GetAppWindow(this);
            //appWindow.IsShownInSwitchers = false;
        }
    }
}
