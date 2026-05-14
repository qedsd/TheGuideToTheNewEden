using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;

namespace TheGuideToTheNewEden.WinUI.Interfaces
{
    public interface IWindow
    {
        DevWinUI.IThemeService ThemeService {  get; set;}
        Window GetWindow();
        void ShowMsg(string msg, bool autoClose = true);
        void ShowError(string msg, bool autoClose = true);
        void ShowSuccess(string msg, bool autoClose = true);
        void ShowWaiting(string tip = null);
        void HideWaiting();
    }
}
