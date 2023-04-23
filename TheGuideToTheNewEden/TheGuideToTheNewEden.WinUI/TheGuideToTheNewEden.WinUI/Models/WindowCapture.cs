using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using TheGuideToTheNewEden.WinUI.Helpers;

namespace TheGuideToTheNewEden.WinUI.Models
{
    internal class WindowCapture
    {
        public Helpers.WindowInfo Info { get; set; }
        private Timer Timer;
        public delegate void WindowCaptured(WindowInfo winfowInfo, Bitmap bitmap);
        public event WindowCaptured OnWindowCaptured;
        public WindowCapture(WindowInfo info) 
        {
            Info = info;
        }
        public void Start(int interval = 200)
        {
            if(Timer == null)
            {
                Timer = new Timer()
                {
                    AutoReset = false
                };
            }
            Timer.Interval = interval;
            Timer.Elapsed -= Timer_Elapsed;
            Timer.Elapsed += Timer_Elapsed;
            Timer.Start();
        }
        public void Stop()
        {
            Timer?.Stop();
        }
        public void Dispose()
        {
            Timer?.Dispose();
        }
        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            var img = Helpers.WindowCaptureHelper.GetShotCutImage(Info.hwnd);
            if (img != null)
            {
                OnWindowCaptured?.Invoke(Info, img);
            }
        }
    }
}
