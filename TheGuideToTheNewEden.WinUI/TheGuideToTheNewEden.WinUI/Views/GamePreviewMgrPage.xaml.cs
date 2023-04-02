// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

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
        private KeyboardHook _keyboardHook;
        public GamePreviewMgrPage()
        {
            this.InitializeComponent();
            Loaded += GamePreviewMgrPage_Loaded;
            _keyboardHook = new KeyboardHook();
            _keyboardHook.Start();
            _keyboardHook.KeyboardEvent += _keyboardHook_KeyboardEvent;
        }

        private void _keyboardHook_KeyboardEvent(List<KeyboardInfo> keys)
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
            Window.Closed += Window_Closed;
            (DataContext as GamePreviewMgrViewModel).Window = Window;
            ProcessList.SelectionChanged += ProcessList_SelectionChanged;
            windowHandle = Helpers.WindowHelper.GetWindowHandle(Window);
            AppWindow = Helpers.WindowHelper.GetAppWindow(Window);
            PreviewGrid.SizeChanged += PreviewGrid_SizeChanged;
            VM.PropertyChanged += VM_PropertyChanged;
        }

        private void VM_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if(e.PropertyName == nameof(VM.Setting))
            {
                NameComboBox.SelectionChanged -= ComboBox_SelectionChanged;
                if (VM.Settings.Contains(VM.Setting))
                {
                    NameComboBox.SelectedItem = VM.Setting;
                    CancelSettingButton.Visibility = Visibility.Visible;
                }
                else
                {
                    NameComboBox.SelectedItem = null;
                    CancelSettingButton.Visibility = Visibility.Collapsed;
                }
                NameComboBox.SelectionChanged += ComboBox_SelectionChanged;
            }
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            _keyboardHook.Stop();
            VM.StopAll();
        }

        private void PreviewGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateThumbDestination();
        }

        private IntPtr lastThumb = IntPtr.Zero;
        private void ProcessList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                var process = e.AddedItems.FirstOrDefault() as Core.Models.GamePreviews.ProcessInfo;
                if (process != null)
                {
                    if (lastThumb != IntPtr.Zero)
                    {
                        WindowCaptureHelper.HideThumb(lastThumb);
                    }
                    lastThumb = WindowCaptureHelper.Show(windowHandle, process.MainWindowHandle);
                    UpdateThumbDestination();
                }
            }
        }
        private void UpdateThumbDestination()
        {
            var thumbSize = WindowCaptureHelper.GetThumbSourceSize(lastThumb);
            var showWPresent = PreviewGrid.ActualWidth / ContentGrid.ActualWidth;
            var showW = showWPresent * (AppWindow.ClientSize.Width - (ContentGrid.Margin.Left + ContentGrid.Margin.Right));
            var showH = showW / thumbSize.x * thumbSize.y;
            int left = (int)(AppWindow.ClientSize.Width * (1 - showWPresent));
            int top = (int)(AppWindow.ClientSize.Height / 2 - showH / 2);
            int right = (int)(left + showW);
            int bottom = (int)(top + showH);
            WindowCaptureHelper.UpdateThumbDestination(lastThumb, new WindowCaptureHelper.Rect(left, top, right, bottom));
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(NameComboBox.SelectedIndex != -1)
            {
                VM.Setting = NameComboBox.SelectedItem as PreviewItem;
            }
            CancelSettingButton.Visibility = NameComboBox.SelectedIndex != -1 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            VM.Setting.Name = NameComboBox.Text;
            VM.StartCommand.Execute(null);
        }
    }
}
