using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Timer = System.Timers.Timer;

namespace TheGuideToTheNewEden.WinCore
{
    /// <summary>
    /// 活动窗口监控服务
    /// </summary>
    public class ForegroundWindowService
    {
        private static ForegroundWindowService current;
        public static ForegroundWindowService Current
        {
            get
            {
                if(current == null)
                {
                    current = new ForegroundWindowService();
                }
                return current;
            }
        }
        private Timer _timer;
        public void Start()
        {
            if(_timer == null)
            {
                _timer = new Timer()
                {
                    AutoReset = false,
                    Interval = 100,
                };
                _timer.Elapsed += Timer_Elapsed;
            }
            _timer.Start();
        }
        public void Stop()
        {
            _timer?.Stop();
        }
        private IntPtr _lastForegroundWindow;
        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                var p = Win32.GetForegroundWindow();
                if (_lastForegroundWindow != p)
                {
                    _lastForegroundWindow = p;
                    OnForegroundWindowChanged?.Invoke(_lastForegroundWindow);
                }
            }
            catch(Exception ex)
            {
                Core.Log.Error(ex);
            }
            finally
            {
                _timer.Start();
            }
        }
        public delegate void ForegroundWindowChangedDelegate(IntPtr hWnd);
        public event ForegroundWindowChangedDelegate OnForegroundWindowChanged;
    }
}
