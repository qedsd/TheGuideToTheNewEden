using Microsoft.UI.Xaml;
using System;
using TheGuideToTheNewEden.Core;
using TheGuideToTheNewEden.WinUI.Helpers;
using TheGuideToTheNewEden.WinUI.Notifications;
using TheGuideToTheNewEden.WinUI.Services;
using WinUIEx;

namespace TheGuideToTheNewEden.WinUI
{
    public partial class App : Application
    {
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
            Services.ActivationService.Init();
            m_window = new BaseWindow()
            {
                MainContent = new Views.HomePage()
            };
            m_window.AppWindow.Closing += AppWindow_Closing;
            (m_window as BaseWindow).SetTavViewHomeMode();
            WindowHelper.SetMainWindow(m_window);
            m_window.Activated += M_window_Activated;
            m_window.Activate();
        }

        private void AppWindow_Closing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
        {
            ((m_window as BaseWindow).MainContent as Views.HomePage).Dispose();
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
