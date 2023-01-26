using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.ViewModels
{
    internal class BaseViewModel : ObservableObject
    {
        internal BaseWindow Window { get; set; }
        internal BaseViewModel()
        {
            Window = Microsoft.UI.Xaml.Window.Current as BaseWindow;
        }
        internal void ShowMsg(string msg, bool autoClose = true)
        {
            Window?.ShowMsg(msg, autoClose);
        }
        internal void ShowError(string msg, bool autoClose = true)
        {
            Window?.ShowError(msg, autoClose);
        }
        internal void ShowSuccess(string msg, bool autoClose = true)
        {
            Window?.ShowSuccess(msg, autoClose);
        }
        internal void ShowWaiting()
        {
            Window?.ShowWaiting();
        }
        internal void HideWaiting()
        {
            Window?.HideWaiting();
        }
    }
}
