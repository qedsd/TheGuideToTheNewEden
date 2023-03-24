using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using static TheGuideToTheNewEden.WinUI.Helpers.FindWindowHelper;

namespace TheGuideToTheNewEden.WinUI.Models
{
    internal class WindowCapture
    {
        public Helpers.FindWindowHelper.WinfowInfo Info { get; set; }
        private Timer Timer;
        public delegate void WindowCaptured(WinfowInfo winfowInfo, Bitmap bitmap);
        public event WindowCaptured OnWindowCaptured;
        public WindowCapture(WinfowInfo info) 
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
