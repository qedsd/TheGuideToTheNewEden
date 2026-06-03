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
            try
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
            catch (Exception ex)
            {
                Core.Log.Error(ex);
            }
        }
        public void ShowError(Exception ex, bool autoClose = false)
        {
            try
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
            catch (Exception ex2)
            {
                Core.Log.Error(ex2);
            }
        }
        public void ShowError(string msg, bool autoClose = false)
        {
            try
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
            catch (Exception ex)
            {
                Core.Log.Error(ex);
            }
        }
        public void ShowSuccess(string msg, bool autoClose = true)
        {
            try
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
            catch (Exception ex)
            {
                Core.Log.Error(ex);
            }
        }
        public void ShowWaiting(string tip = null, LoadingControl.CancelWaitingCallbackDelegate cancelCallback = null)
        {
            try
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
            catch (Exception ex)
            {
                Core.Log.Error(ex);
            }
        }
        public void HideWaiting()
        {
            try
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
            catch (Exception ex)
            {
                Core.Log.Error(ex);
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
