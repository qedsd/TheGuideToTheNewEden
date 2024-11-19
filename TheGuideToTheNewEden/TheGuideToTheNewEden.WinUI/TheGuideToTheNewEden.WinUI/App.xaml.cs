using Microsoft.UI.Xaml;
using System;
using System.IO;
using System.Linq;
using TheGuideToTheNewEden.Core;
using TheGuideToTheNewEden.WinUI.Helpers;
using TheGuideToTheNewEden.WinUI.Notifications;
using TheGuideToTheNewEden.WinUI.Services;
using WinUIEx;

namespace TheGuideToTheNewEden.WinUI
{
    public partial class App : Application
    {
        public static bool HandleClosedEvents { get; set; } = true;
        private NotificationManager notificationManager;
        private bool _launch = true;
        public App()
        {
            IntPtr same = GetSameProcess();
            if (same == IntPtr.Zero)
            {
                this.InitializeComponent();
                AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);
                UnhandledException += App_UnhandledException;//UI线程
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;//后台线程
                Log.Init();
            }
            else
            {
                Helpers.WindowHelper.SetForegroundWindow1(same);
                _launch = false;
                Exit();
            }
        }

        private void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
        {
            if(e.IsTerminating)
            {
                Log.Error("发生致命错误");
            }
            Log.Error(e.ExceptionObject);
        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            Log.Error(e.Exception);
        }
        private void OnProcessExit(object sender, EventArgs e)
        {
            notificationManager.Unregister();
            ForegroundWindowService.Current.Stop();
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            if(_launch)
            {
                Services.ActivationService.Init();
                m_window = new BaseWindow()
                {
                    MainContent = new Views.HomePage()
                };
                m_window.Closed += M_window_Closed;
                (m_window as BaseWindow).SetTavViewHomeMode();
                WindowHelper.SetMainWindow(m_window);
                m_window.Activated += M_window_Activated;
                m_window.Activate();
            }
        }

        private IntPtr GetSameProcess()
        {
            var allProcesses = System.Diagnostics.Process.GetProcesses();
            if (allProcesses.Any())
            {
                var targets = allProcesses.Where(p => p.ProcessName == "TheGuideToTheNewEden").ToList();
                if(targets.Any())
                {
                    string dir = System.AppDomain.CurrentDomain.BaseDirectory;
                    var p = targets.FirstOrDefault(p => Path.GetDirectoryName(p.MainModule.FileName) == dir);
                    if (p != null)
                    {
                        return p.MainWindowHandle;
                    }
                }
            }
            return IntPtr.Zero;
        }

        private void M_window_Closed(object sender, WindowEventArgs args)
        {
            if (HandleClosedEvents)
            {
                args.Handled = true;
                (sender as Window).Hide();
                Helpers.WindowHelper.TrackWindow(sender as Window);
            }
            else
            {
                ((m_window as BaseWindow).MainContent as Views.HomePage).Dispose();
                Services.MemoryIPCService.Dispose();
            }
        }

        private void M_window_Activated(object sender, WindowActivatedEventArgs args)
        {
            m_window.Activated -= M_window_Activated;
            System.Threading.Tasks.Task.Run(() =>
            {
                notificationManager = new NotificationManager();
                notificationManager.Init();
            });
            ForegroundWindowService.Current.Start();
        }

        private Window m_window;

        public static string GetFullPathToExe()
        {
            var path = AppDomain.CurrentDomain.BaseDirectory;
            var pos = path.LastIndexOf("\\");
            return path.Substring(0, pos);
        }

        public static string GetFullPathToAsset(string assetName)
        {
            return GetFullPathToExe() + "\\Assets\\" + assetName;
        }
    }
}
