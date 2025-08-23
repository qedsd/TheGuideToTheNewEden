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
        public static Core.Helpers.SingleInstanceHelper SingleInstanceHelper;
        public static bool HandleClosedEvents { get; set; } = true;
        private NotificationManager notificationManager;
        public App()
        {
            this.InitializeComponent();
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);
            UnhandledException += App_UnhandledException;//UI线程
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;//后台线程
            Log.Init();
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
            try
            {
                ClientServiceHelper.GetRequiredService<Services.PageNavigationService>().ShowMsg(string.Empty, e.Exception.Message, Controls.InfoBarControl.InfoType.Error, false);
            }
            catch
            {

            }
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
            SingleInstanceHelper = new Core.Helpers.SingleInstanceHelper();
            if (SingleInstanceHelper.RegisterSingleInstance())
            {
                SingleInstanceHelper.Activated += SingleInstanceHelper_Activated;
            }
            else
            {
                Application.Current.Exit();
                return;
            }
            Services.ActivationService.Init();
            ClientServiceHelper.Init(Log.GetInstance());
            m_window = new MainWindow();
            m_window.Closed += M_window_Closed;
            WindowHelper.SetMainWindow(m_window);
            m_window.Activated += M_window_Activated;
            m_window.Activate();
        }

        private void SingleInstanceHelper_Activated(object sender, string[] e)
        {
            if (m_window != null)
            {
                Helpers.WindowHelper.SetForegroundWindow_Click(m_window.GetWindowHandle());
            }
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
                ClientServiceHelper.GetRequiredService<PageNavigationService>().Dispose();
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
    }
}
