using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.ViewModels
{
    public class BaseViewModel : ObservableObject
    {
        private Services.PageNavigationService _navigationService;
        private Window window;
        public Window Window
        {
            get
            {
                return window ?? Helpers.WindowHelper.MainWindow;
            }
            set => window = value;
        }
        public BaseViewModel()
        {
            _navigationService = ClientServiceHelper.GetRequiredService<Services.PageNavigationService>();
        }
        public void ShowMsg(string msg, bool autoClose = true)
        {
            _navigationService.ShowMsg(this, msg, Controls.InfoBarControl.InfoType.Info, autoClose);
        }
        public void ShowError(string msg, bool autoClose = true)
        {
            _navigationService.ShowMsg(this, msg, Controls.InfoBarControl.InfoType.Error, autoClose);
        }
        public void ShowSuccess(string msg, bool autoClose = true)
        {
            _navigationService.ShowMsg(this, msg, Controls.InfoBarControl.InfoType.Success, autoClose);
        }
        public void ShowWaiting(string tip = null)
        {
            _navigationService.ShowWaiting(this, tip);
        }
        public void HideWaiting()
        {
            _navigationService.HideWaiting(this);
        }

        public void SetWindow(Window wWindow)
        {
            Window = window;
        }
    }
}
