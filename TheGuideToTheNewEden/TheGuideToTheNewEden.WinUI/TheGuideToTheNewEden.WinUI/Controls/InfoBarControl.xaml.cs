using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using TheGuideToTheNewEden.WinUI.Extensions;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Controls
{
    public sealed partial class InfoBarControl : UserControl
    {
        private Storyboard _showStoryboard;
        private Storyboard _hideStoryboard;
        private Brush _infoBrush;
        private Brush _warningBrush;
        private Brush _errorBrush;
        private Brush _successBrush;
        public InfoBarControl()
        {
            Width = 0;
            InitializeComponent();
            _showStoryboard = CreateStoryboard(0, 1, 0.2);
            _hideStoryboard = CreateStoryboard(1, 0, 0.2);
            _hideStoryboard.Completed += HideStoryboard_Completed;
            _infoBrush = Helpers.ResourcesHelper.Get("GrayBrush") as SolidColorBrush;
            _warningBrush = Helpers.ResourcesHelper.Get("YellowBrush") as SolidColorBrush;
            _errorBrush = Helpers.ResourcesHelper.Get("RedBrush") as SolidColorBrush;
            _successBrush = Helpers.ResourcesHelper.Get("GreenBrush") as SolidColorBrush;
        }

        private void HideStoryboard_Completed(object sender, object e)
        {
            this.Visibility = Visibility.Collapsed;
        }

        private Storyboard CreateStoryboard(double from, double to, double seconds)
        {
            Storyboard storyboard = new Storyboard();

            // 创建DoubleAnimation用于改变宽度
            DoubleAnimation animation = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = TimeSpan.FromSeconds(seconds),
                AutoReverse = false,
            };

            // 设置动画目标
            Storyboard.SetTarget(animation, this);
            Storyboard.SetTargetProperty(animation, "Opacity");

            // 将动画添加到Storyboard
            storyboard.Children.Add(animation);
            return storyboard;
        }
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }
        public void Show(string sender, string msg, InfoType infoType, bool autoClose, string title = null)
        {
            Helpers.WindowHelper.MainWindow.DispatcherQueue.SafelyTryEnqueue(() =>
            {
                SenderTextBlock.Text = Helpers.ResourcesHelper.GetString(sender);
                SenderTextBlock.Visibility = string.IsNullOrEmpty(sender) ? Visibility.Collapsed : Visibility.Visible;
                TitleTextBlock.Text = title;
                TitleTextBlock.Visibility = string.IsNullOrEmpty(title) ? Visibility.Collapsed : Visibility.Visible;
                MsgTextBlock.Blocks.Clear();
                Paragraph paragraph = new Paragraph();
                Run run = new Run()
                {
                    Text = msg,
                };
                paragraph.Inlines.Add(run);
                MsgTextBlock.Blocks.Add(paragraph);
                switch (infoType)
                {
                    case InfoType.Info: MainBorder.BorderBrush = _infoBrush; break;
                    case InfoType.Warning: MainBorder.BorderBrush = _warningBrush; break;
                    case InfoType.Error: MainBorder.BorderBrush = _errorBrush; break;
                    case InfoType.Success: MainBorder.BorderBrush = _successBrush; break;
                }
                this.Visibility = Visibility.Visible;
                _showStoryboard.Begin();
                if (autoClose)
                {
                    StartTimer();
                }
            });
        }
        public void Hide()
        {
            Helpers.WindowHelper.MainWindow.DispatcherQueue.SafelyTryEnqueue(() =>
            {
                _hideStoryboard.Begin();
                Timer?.Stop();
            });
        }

        private System.Timers.Timer Timer;
        private void StartTimer()
        {
            if (Timer == null)
            {
                Timer = new System.Timers.Timer(5000);
                Timer.AutoReset = false;
                Timer.Elapsed += Timer_Elapsed;
            }
            Timer.Start();
        }
        private void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            Hide();
        }

        public enum InfoType
        {
            Info,
            Warning,
            Error,
            Success
        }
    }
}
