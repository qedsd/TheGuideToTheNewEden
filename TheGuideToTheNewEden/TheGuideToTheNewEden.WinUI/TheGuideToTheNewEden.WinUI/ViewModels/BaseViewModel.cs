using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.ViewModels
{
    public class BaseViewModel : ObservableObject
    {
        public BaseWindow Window { get; set; }
        public BaseViewModel()
        {
        }
        public void ShowMsg(string msg, bool autoClose = true)
        {
            Window?.ShowMsg(msg, autoClose);
        }
        public void ShowError(string msg, bool autoClose = true)
        {
            Window?.ShowError(msg, autoClose);
        }
        public void ShowSuccess(string msg, bool autoClose = true)
        {
            Window?.ShowSuccess(msg, autoClose);
        }
        public void ShowWaiting()
        {
            Window?.ShowWaiting();
        }
        public void HideWaiting()
        {
            Window?.HideWaiting();
        }

        public void SetWindow(BaseWindow baseWindow)
        {
            Window = baseWindow;
        }
    }
}
