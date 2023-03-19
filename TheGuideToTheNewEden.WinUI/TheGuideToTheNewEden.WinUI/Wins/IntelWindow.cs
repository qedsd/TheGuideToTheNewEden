// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation.Collections;

namespace TheGuideToTheNewEden.WinUI.Wins
{
    public sealed partial class IntelWindow
    {
        private Core.Models.Map.IntelSolarSystemMap IntelMap;
        private int Jumps;
        private Canvas ContentCanvas;
        private BaseWindow Window;
        public IntelWindow(Core.Models.Map.IntelSolarSystemMap intelMap,int jumps)
        {
            Window = new BaseWindow();
            ContentCanvas = new Canvas()
            {
                Margin = new Thickness(8),
            };
            Window.MainContent = ContentCanvas;
            IntelMap = intelMap;
            Jumps = jumps;
            Window.Activated += IntelWindow_Activated;
            //IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(Window);
            //WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            //Microsoft.UI.Windowing.AppWindow appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            //appWindow.Resize(new Windows.Graphics.SizeInt32(Math.Max(200 * jumps,300), Math.Max(200 * jumps,300)));
            //appWindow.MoveInZOrderAtTop();
            //appWindow.Changed += AppWindow_Changed;
        }

        //private void AppWindow_Changed(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowChangedEventArgs args)
        //{
        //    sender.MoveInZOrderAtTop();
        //}

        public void Show()
        {
            Window.Activate();
        }
        private void IntelWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            Init(IntelMap,Jumps);
            Window.Activated -= IntelWindow_Activated;
        }

        public void Init(Core.Models.Map.IntelSolarSystemMap intelMap, int jumps)
        {
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(Window);
            WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            Microsoft.UI.Windowing.AppWindow appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            appWindow.Resize(new Windows.Graphics.SizeInt32(Math.Max(200 * jumps, 300), Math.Max(200 * jumps, 300)));

            ContentCanvas.Children.Clear();
            double width = Window.Bounds.Width;
            double height = Window.Bounds.Height;
            foreach (var item in intelMap.GetAllSolarSystem())
            {
                Ellipse ellipse = new Ellipse()
                {
                    Fill = new SolidColorBrush(Colors.DarkGray),
                    Width = 8,
                    Height = 8,
                    StrokeThickness = 0,
                };
                ContentCanvas.Children.Add(ellipse);
                Canvas.SetLeft(ellipse, width * item.X);
                Canvas.SetTop(ellipse, height * item.Y);
            }
        }
    }
}
