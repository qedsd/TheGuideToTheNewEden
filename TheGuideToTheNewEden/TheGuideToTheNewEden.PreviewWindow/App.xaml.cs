﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace TheGuideToTheNewEden.PreviewWindow
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// hwnd w h x y a r g b o
        /// </summary>
        public static string[] Args { get; private set; }
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            Args = e.Args;
        }
        public static IntPtr GetHwnd()
        {
            return IntPtr.Parse(Args[0]);
        }
        public static int GetW()
        {
            return int.Parse(Args[1]);
        }
        public static int GetH()
        {
            return int.Parse(Args[2]);
        }
        public static int GetX()
        {
            return int.Parse(Args[3]);
        }
        public static int GetY()
        {
            return int.Parse(Args[4]);
        }
        public static byte GetA()
        {
            return byte.Parse(Args[5]);
        }
        public static byte GetR()
        {
            return byte.Parse(Args[6]);
        }
        public static byte GetG()
        {
            return byte.Parse(Args[7]);
        }
        public static byte GetB()
        {
            return byte.Parse(Args[8]);
        }
        public static int GetOpacity()
        {
            return int.Parse(Args[9]);
        }
    }
}