using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.WinUI.Controls;
using TheGuideToTheNewEden.WinUI.Extensions;
using TheGuideToTheNewEden.WinUI.Interfaces;

namespace TheGuideToTheNewEden.WinUI.ViewModels
{
    public class BaseViewModel : ObservableObject
    {
        private Services.PageNavigationService _navigationService;
        private IWindow _window;
        public Window Window
        {
            get
            {
                return _window != null ? _window.GetWindow() :  Helpers.WindowHelper.MainWindow;
            }
        }
        public BaseViewModel()
        {
            _navigationService = ClientServiceHelper.GetRequiredService<Services.PageNavigationService>();
        }
        public void ShowMsg(string msg, bool autoClose = true)
        {
            if (_window != null)
            {
                _window.ShowMsg(msg, autoClose);
            }
            else
            {
                _navigationService.ShowMsg(this, msg, Controls.InfoBarControl.InfoType.Info, autoClose);
            }
        }
        public void ShowError(Exception ex, bool autoClose = false)
        {
            if (_window != null)
            {
                _window.ShowError(ex.ToString(), autoClose);
            }
            else
            {
                _navigationService.ShowMsg(this, ex.ToString(), Controls.InfoBarControl.InfoType.Error, autoClose);
            }
        }
        public void ShowError(string msg, bool autoClose = false)
        {
            if (_window != null)
            {
                _window.ShowError(msg, autoClose);
            }
            else
            {
                _navigationService.ShowMsg(this, msg, Controls.InfoBarControl.InfoType.Error, autoClose);
            }
        }
        public void ShowSuccess(string msg, bool autoClose = true)
        {
            if (_window != null)
            {
                _window.ShowSuccess(msg, autoClose);
            }
            else
            {
                _navigationService.ShowMsg(this, msg, Controls.InfoBarControl.InfoType.Success, autoClose);
            }
        }
        public void ShowWaiting(string tip = null, LoadingControl.CancelWaitingCallbackDelegate cancelCallback = null)
        {
            if (_window != null)
            {
                _window.ShowWaiting(tip);
            }
            else
            {
                _navigationService.ShowWaiting(this, tip, cancelCallback);
            }
        }
        public void HideWaiting()
        {
            if (_window != null)
            {
                _window.HideWaiting();
            }
            else
            {
                _navigationService.HideWaiting(this);
            }
        }

        public void SetWindow(IWindow window)
        {
            _window = window;
        }

        public void ExecuteUIAction(Action action)
        {
            Window.DispatcherQueue.SafelyTryEnqueue(action);
        }
    }
}
