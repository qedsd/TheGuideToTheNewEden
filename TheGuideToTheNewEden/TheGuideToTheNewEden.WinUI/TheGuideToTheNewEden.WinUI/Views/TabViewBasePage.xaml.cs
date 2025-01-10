using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class TabViewBasePage : Page
    {
        public TabViewBasePage()
        {
            this.InitializeComponent();
        }
        public object MainContent
        {
            get => ContentFrame.Content;

            set => ContentFrame.Content = value;
        }
        public void ShowWaiting(string tip = null)
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                if (string.IsNullOrEmpty(tip))
                {
                    WaitingText.Visibility = Visibility.Collapsed;
                }
                else
                {
                    WaitingText.Visibility = Visibility.Visible;
                    WaitingText.Text = tip;
                }
                WaitingGrid.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                WaitingProgressRing.IsActive = true;
            });
        }
        public void HideWaiting()
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                WaitingGrid.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                WaitingProgressRing.IsActive = false;
            });
        }
    }
}
