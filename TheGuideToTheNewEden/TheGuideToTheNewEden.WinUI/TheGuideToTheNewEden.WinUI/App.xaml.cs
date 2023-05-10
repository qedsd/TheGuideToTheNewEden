using Microsoft.UI.Xaml;
using System;
using TheGuideToTheNewEden.Core;
using TheGuideToTheNewEden.WinUI.Helpers;
using TheGuideToTheNewEden.WinUI.Notifications;
using TheGuideToTheNewEden.WinUI.Services;


// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace TheGuideToTheNewEden.WinUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private NotificationManager notificationManager;
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            notificationManager = new NotificationManager();
            notificationManager.Init();
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);
            UnhandledException += App_UnhandledException;//UI线程
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;//后台线程
            Log.Init();
            //ResourceDictionary d = new ResourceDictionary();
            //Application.Current.Resources.MergedDictionaries[0]
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
                MainContent = new Views.ShellPage()
            };
            WindowHelper.SetMainWindow(m_window);
            m_window.Activate();
        }

        private Window m_window;

        public static void ToForeground()
        {
            //if (m_window != null)
            //{
            //    HWND hwnd = (HWND)WinRT.Interop.WindowNative.GetWindowHandle(mainWindow);
            //    SwitchToThisWindow(hwnd, true);
            //}
        }

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
