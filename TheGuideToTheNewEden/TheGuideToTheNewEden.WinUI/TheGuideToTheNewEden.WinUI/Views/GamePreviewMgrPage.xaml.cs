using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using TheGuideToTheNewEden.Core.Models.GamePreviews;
using TheGuideToTheNewEden.WinUI.Common;
using TheGuideToTheNewEden.WinUI.Helpers;
using TheGuideToTheNewEden.WinUI.Services;
using TheGuideToTheNewEden.WinUI.ViewModels;
using TheGuideToTheNewEden.WinUI.Wins;
using Windows.Foundation;
using Windows.Foundation.Collections;
using static TheGuideToTheNewEden.WinUI.Common.KeyboardHook;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class GamePreviewMgrPage : Page
    {
        private BaseWindow Window;
        private Microsoft.UI.Windowing.AppWindow AppWindow;
        public GamePreviewMgrPage()
        {
            HotkeyService.Start();
            this.InitializeComponent();
            Loaded += GamePreviewMgrPage_Loaded;
            //HotkeyService.OnKeyboardClicked += HotkeyService_OnKeyboardClicked;
        }

        /// <summary>
        /// 测试检测按键
        /// </summary>
        /// <param name="keys"></param>
        private void HotkeyService_OnKeyboardClicked(List<KeyboardInfo> keys)
        {
            StringBuilder stringBuilder = new StringBuilder();
            keys.ForEach(p => stringBuilder.Append($"{p.Name} "));
            Window.DispatcherQueue.TryEnqueue(() =>
            {
                TestBox.Text = stringBuilder.ToString();
            });
        }

        private IntPtr windowHandle = IntPtr.Zero;
        private void GamePreviewMgrPage_Loaded(object sender, RoutedEventArgs e)
        {
            Window = Helpers.WindowHelper.GetWindowForElement(this) as BaseWindow;
            (DataContext as GamePreviewMgrViewModel).Window = Window;
            ProcessList.SelectionChanged += ProcessList_SelectionChanged;
            windowHandle = Helpers.WindowHelper.GetWindowHandle(Window);
            AppWindow = Helpers.WindowHelper.GetAppWindow(Window);
            AppWindow.Closing += AppWindow_Closing;
            PreviewGrid.SizeChanged += PreviewGrid_SizeChanged;
        }

        private void AppWindow_Closing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
        {
            VM.StopAll();
            HotkeyService.Stop();
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
                var process = e.AddedItems.FirstOrDefault() as Core.Models.GamePreviews.ProcessInfo;
                if (process != null)
                {
                    lastThumb = WindowCaptureHelper.Show(windowHandle, process.MainWindowHandle);
                    UpdateThumbDestination();
                }
            }
        }
        private void UpdateThumbDestination()
        {
            var thumbSize = WindowCaptureHelper.GetThumbSourceSize(lastThumb);
            if(thumbSize.x > thumbSize.y)//横向
            {
                var showWPresent = PreviewGrid.ActualWidth / ContentGrid.ActualWidth;
                var showW = showWPresent * (AppWindow.ClientSize.Width - (ContentGrid.Margin.Left + ContentGrid.Margin.Right));
                var showH = showW / thumbSize.x * thumbSize.y;
                int left = (int)(AppWindow.ClientSize.Width * (1 - showWPresent));
                int top = (int)(AppWindow.ClientSize.Height / 2 - showH / 2);
                int right = (int)(left + showW);
                int bottom = (int)(top + showH);
                WindowCaptureHelper.UpdateThumbDestination(lastThumb, new WindowCaptureHelper.Rect(left, top, right, bottom));
            }
            else//竖向
            {
                var showHPresent = PreviewGrid.ActualHeight / Window.Content.ActualSize.Y;//不能显示到标题栏
                var showH = showHPresent * (AppWindow.ClientSize.Height - (ContentGrid.Margin.Top + ContentGrid.Margin.Bottom));
                var showW = showH / thumbSize.y * thumbSize.x;
                var showWPresent = PreviewGrid.ActualWidth / ContentGrid.ActualWidth;
                int top = (int)(AppWindow.ClientSize.Height * (1 - showHPresent));
                int left = (int)(AppWindow.ClientSize.Width * (1 - showWPresent * 0.5) - showW / 2);
                int right = (int)(left + showW);
                int bottom = (int)(top + showH);

                //限制范围内
                var minLeft = (1 - (PreviewGrid.ActualWidth / ContentGrid.ActualWidth)) * AppWindow.ClientSize.Width;
                var maxRight = AppWindow.ClientSize.Width;
                var maxBottom = AppWindow.ClientSize.Height;
                left = left < minLeft ? (int)minLeft : left;
                right = right > maxRight ? maxRight : right;
                bottom = bottom > maxBottom ? maxBottom : bottom;
                WindowCaptureHelper.UpdateThumbDestination(lastThumb, new WindowCaptureHelper.Rect(left, top, right, bottom));
            }
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            VM.Setting.Name = SettingNameBox.Text;//只在开始后才保存修改的名字
            VM.StartCommand.Execute(null);
        }

        private void RemoveSetting_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null)
            {
                VM.RemoveSettingCommand.Execute(button.DataContext as PreviewItem);
            }
        }

        private void SettingList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SettingListFlyout.Hide();
        }
    }
}
