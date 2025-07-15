using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using TheGuideToTheNewEden.Core.Helpers;
using TheGuideToTheNewEden.WPF.Services;

namespace TheGuideToTheNewEden.WPF.ViewModels
{
    internal class BaseViewModel : ObservableObject
    {
        private string _pageName;
        private string _pageUIName;
        private Window window;
        public Window Window
        {
            get
            {
                return window ?? Application.Current.MainWindow;
            }
            set => window = value;
        }
        public BaseViewModel()
        {
            _pageName = this.GetType().Name.Replace("ViewModel","Page");
            _pageUIName = Helpers.ResourcesHelper.GetString($"Navigation.{_pageName}");
        }
        public void ShowMsg(string msg, bool autoClose = true)
        {
            ClientServiceHelper.GetRequiredService<Services.NavigationService>().ShowMsg(_pageUIName, msg, Controls.InfoBarControl.InfoType.Info, autoClose);
        }
        public void ShowError(string msg, bool autoClose = true)
        {
            ClientServiceHelper.GetRequiredService<Services.NavigationService>().ShowMsg(_pageUIName, msg, Controls.InfoBarControl.InfoType.Error, autoClose);
        }
        public void ShowSuccess(string msg, bool autoClose = true)
        {
            ClientServiceHelper.GetRequiredService<Services.NavigationService>().ShowMsg(_pageUIName, msg, Controls.InfoBarControl.InfoType.Success, autoClose);
        }
        public void ShowWaiting()
        {
            ClientServiceHelper.GetRequiredService<NavigationService>().ShowWaiting(_pageName);
        }
        public void HideWaiting()
        {
            ClientServiceHelper.GetRequiredService<NavigationService>().HideWaiting(_pageName);
        }

        public void SetWindow(Window window)
        {
            Window = window;
        }
    }
}
