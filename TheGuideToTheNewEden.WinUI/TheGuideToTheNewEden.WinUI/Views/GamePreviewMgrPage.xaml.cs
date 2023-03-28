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
using TheGuideToTheNewEden.WinUI.Helpers;
using TheGuideToTheNewEden.WinUI.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Views
{
    public sealed partial class GamePreviewMgrPage : Page
    {
        public GamePreviewMgrPage()
        {
            this.InitializeComponent();
            (DataContext as GamePreviewMgrViewModel).UpdatePreviewImage += GamePreviewMgrPage_UpdatePreviewImage;
            Loaded += GamePreviewMgrPage_Loaded;
        }

        private void GamePreviewMgrPage_Loaded(object sender, RoutedEventArgs e)
        {
            (DataContext as GamePreviewMgrViewModel).Window = Helpers.WindowHelper.GetWindowForElement(this) as BaseWindow;
            var processes = Process.GetProcesses();
            var tragetPro = processes.FirstOrDefault(p => p.ProcessName.Contains("exefile"));
            if(tragetPro != null)
            {
                //tragetPro.MainWindowHandle
                WindowCaptureHelper.Show(WinRT.Interop.WindowNative.GetWindowHandle(Helpers.WindowHelper.GetWindowForElement(this)),tragetPro.MainWindowHandle);
            }
        }

        private void GamePreviewMgrPage_UpdatePreviewImage(string path)
        {
            //PreviewImage.LoadImageFromFile(path);
        }
    }
}
