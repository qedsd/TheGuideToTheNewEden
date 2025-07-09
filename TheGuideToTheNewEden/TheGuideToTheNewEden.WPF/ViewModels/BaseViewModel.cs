using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using TheGuideToTheNewEden.Core.Helpers;

namespace TheGuideToTheNewEden.WPF.ViewModels
{
    internal class BaseViewModel : ObservableObject
    {
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
        }
        public void ShowMsg(string msg, bool autoClose = true)
        {
            
        }
        public void ShowError(string msg, bool autoClose = true)
        {
            MessageBox.Show(msg);
        }
        public void ShowSuccess(string msg, bool autoClose = true)
        {
           
        }
        public void ShowWaiting()
        {
            
        }
        public void HideWaiting()
        {
           
        }

        public void SetWindow(Window window)
        {
            Window = window;
        }
    }
}
