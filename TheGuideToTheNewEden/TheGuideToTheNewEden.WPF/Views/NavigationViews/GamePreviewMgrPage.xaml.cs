using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.Core.Models.GamePreviews;
using TheGuideToTheNewEden.WPF.Helpers;

namespace TheGuideToTheNewEden.WPF.Views.NavigationViews
{
    public sealed partial class GamePreviewMgrPage : Page, IPage
    {
        public GamePreviewMgrPage()
        {
            this.InitializeComponent();
            Loaded += GamePreviewMgrPage_Loaded;
            Loaded += GamePreviewMgrPage_Loaded2;
            Unloaded += GamePreviewMgrPage_Unloaded;
            VM.HideThumbRequsted += VM_HideThumbRequsted;
            VM.ShowThumbRequsted += VM_ShowThumbRequsted;
        }

        private void VM_ShowThumbRequsted(object sender, EventArgs e)
        {
            lastThumb = WindowCaptureHelper.Show(windowHandle, (ProcessList.SelectedItem as ProcessInfo).MainWindowHandle);
            UpdateThumbDestination();
        }

        private void VM_HideThumbRequsted(object sender, EventArgs e)
        {
            if (lastThumb != IntPtr.Zero)
            {
                WindowCaptureHelper.HideThumb(lastThumb);
            }
        }

        private void GamePreviewMgrPage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (lastThumb != IntPtr.Zero)
            {
                WindowCaptureHelper.HideThumb(lastThumb);
            }
        }

        private IntPtr windowHandle = IntPtr.Zero;
        private void GamePreviewMgrPage_Loaded2(object sender, RoutedEventArgs e)
        {
            if(lastThumb!= IntPtr.Zero && ProcessList.SelectedItem != null)
            {
                lastThumb = WindowCaptureHelper.Show(windowHandle, (ProcessList.SelectedItem as ProcessInfo).MainWindowHandle);
            }
            UpdateThumbDestination();
        }
        private void GamePreviewMgrPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= GamePreviewMgrPage_Loaded;
            ProcessList.SelectionChanged += ProcessList_SelectionChanged;
            PreviewGrid.SizeChanged += PreviewGrid_SizeChanged;
            VM.RegisterHotkey();
        }

        private void PreviewGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateThumbDestination();
        }

        private IntPtr lastThumb = IntPtr.Zero;
        private void ProcessList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lastThumb != IntPtr.Zero)
            {
                WindowCaptureHelper.HideThumb(lastThumb);
            }
            if (e.AddedItems.Count > 0)
            {
                var process = e.AddedItems[0] as Core.Models.GamePreviews.ProcessInfo;
                if (process != null)
                {
                    lastThumb = WindowCaptureHelper.Show(windowHandle, process.MainWindowHandle);
                    UpdateThumbDestination();
                }
            }
        }
        private void UpdateThumbDestination()
        {
            //var thumbSize = WindowCaptureHelper.GetThumbSourceSize(lastThumb);
            //if(thumbSize.x > thumbSize.y)//横向
            //{
            //    var showWPresent = PreviewGrid.ActualWidth / ContentGrid.ActualWidth;
            //    var showW = showWPresent * (AppWindow.ClientSize.Width - (ContentGrid.Margin.Left + ContentGrid.Margin.Right));
            //    var showH = showW / thumbSize.x * thumbSize.y;
            //    int left = (int)(AppWindow.ClientSize.Width * (1 - showWPresent));
            //    int top = (int)(AppWindow.ClientSize.Height / 2 - showH / 2);
            //    int right = (int)(left + showW);
            //    int bottom = (int)(top + showH);
            //    WindowCaptureHelper.UpdateThumbDestination(lastThumb, new WindowCaptureHelper.Rect(left, top, right, bottom));
            //}
            //else//竖向
            //{
            //    var showHPresent = PreviewGrid.ActualHeight / Window.Content.ActualSize.Y;//不能显示到标题栏
            //    var showH = showHPresent * (AppWindow.ClientSize.Height - (ContentGrid.Margin.Top + ContentGrid.Margin.Bottom));
            //    var showW = showH / thumbSize.y * thumbSize.x;
            //    var showWPresent = PreviewGrid.ActualWidth / ContentGrid.ActualWidth;
            //    int top = (int)(AppWindow.ClientSize.Height * (1 - showHPresent));
            //    int left = (int)(AppWindow.ClientSize.Width * (1 - showWPresent * 0.5) - showW / 2);
            //    int right = (int)(left + showW);
            //    int bottom = (int)(top + showH);

            //    //限制范围内
            //    var minLeft = (1 - (PreviewGrid.ActualWidth / ContentGrid.ActualWidth)) * AppWindow.ClientSize.Width;
            //    var maxRight = AppWindow.ClientSize.Width;
            //    var maxBottom = AppWindow.ClientSize.Height;
            //    left = left < minLeft ? (int)minLeft : left;
            //    right = right > maxRight ? maxRight : right;
            //    bottom = bottom > maxBottom ? maxBottom : bottom;
            //    WindowCaptureHelper.UpdateThumbDestination(lastThumb, new WindowCaptureHelper.Rect(left, top, right, bottom));
            //}
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            VM.StartCommand.Execute(null);
        }
        public void Close()
        {
            VM.Dispose();
        }

        private void MenuFlyoutItem_Up_Click(object sender, RoutedEventArgs e)
        {
            var info = (sender as FrameworkElement).DataContext as ProcessInfo;
            if(info != null)
            {
                int index = VM.Processes.IndexOf(info);
                if(index > 0)
                {
                    VM.Processes.Move(index, index - 1);
                }
            }
        }

        private void MenuFlyoutItem_Down_Click(object sender, RoutedEventArgs e)
        {
            var info = (sender as FrameworkElement).DataContext as ProcessInfo;
            if (info != null)
            {
                int index = VM.Processes.IndexOf(info);
                if (index < VM.Processes.Count - 2)
                {
                    VM.Processes.Move(index, index + 1);
                }
            }
        }


        private void GlobalSetting_Flyout_Opened(object sender, object e)
        {
            VM.UpdateOrderCommand.Execute(null);
        }

        private void GlobalSettingButton_Click(object sender, RoutedEventArgs e)
        {
            if (GlobalSetting.Visibility == Visibility.Visible)
            {
                GlobalSetting.Visibility = Visibility.Collapsed;
            }
            else
            {
                GlobalSetting.Visibility = Visibility.Visible;
            }
        }

        public void Init()
        {
            
        }
    }
}
