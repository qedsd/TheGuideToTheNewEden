using H.NotifyIcon;
using Microsoft.UI.Xaml;
using System;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
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
        private static NotificationManager notificationManager;
        public App()
        {
            this.InitializeComponent();
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);
            UnhandledException += App_UnhandledException;//UI线程
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;//后台线程
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            Application.Current.UnhandledException += Current_UnhandledException;
            //AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;
            Log.Init();
        }

        private void CurrentDomain_FirstChanceException(object sender, FirstChanceExceptionEventArgs e)
        {
            //Log.Error(e.Exception);//会记录下太多内部记录
        }

        private void Current_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            Log.Error(e.Exception);
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            e.SetObserved();
            Log.Error(e.Exception);
        }

        private void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
        {
            if(e.IsTerminating)
            {
                Log.Error("发生致命错误");
            }
            Log.Error(e.ExceptionObject);
            var processPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CrashReporter", "CrashReporter.exe");
            if(File.Exists(processPath))
            {
                System.Diagnostics.Process.Start(processPath, e.ExceptionObject.ToString());
            }
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
            HandleClose();
        }
        private static bool _closed = false;
        private static void HandleClose()
        {
            if (!_closed)
            {
                _closed = true;
                notificationManager?.Unregister();
                ForegroundWindowService.Current.Stop();
                Helpers.WindowHelper.CloseAll();
                Environment.Exit(0);
            }
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            SingleInstanceHelper = new Core.Helpers.SingleInstanceHelper();
            if (SingleInstanceHelper.RegisterSingleInstance(DataPath))
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
            m_window.AppWindow.Closing += AppWindow_Closing;
            WindowHelper.SetMainWindow(m_window);
            m_window.Activated += M_window_Activated;
            m_window.Activate();
        }

        private void AppWindow_Closing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
        {
            if (HandleClosedEvents)
            {
                args.Cancel = true;
                m_window.Hide();
                Helpers.WindowHelper.TrackWindow(m_window);
            }
        }

        private void SingleInstanceHelper_Activated(object sender, string[] e)
        {
            if (m_window != null)
            {
                Helpers.WindowHelper.SetForegroundWindow_Click(m_window.GetWindowHandle());
            }
        }


        public static void Close()
        {
            ClientServiceHelper.GetRequiredService<PageNavigationService>().Dispose();
            Services.MemoryIPCService.Dispose();
            App.HandleClosedEvents = false;
            Core.Log.Info("开始Close");
            App.HandleClose();
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

        public static string DataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TheGuideToTheNewEden");
    }
}
