using System.Windows;
using System.Windows.Threading;
using TheGuideToTheNewEden.Core.Helpers;
using TheGuideToTheNewEden.WPF.Services;

namespace TheGuideToTheNewEden.WPF;

public partial class App : Application
{
    public static new Views.MainWindow? MainWindow { get; private set; }

    /// <summary>单实例助手：第二个实例（如 ESI 授权回调）通过它把命令行交给本实例。</summary>
    public static SingleInstanceHelper? SingleInstanceHelper { get; private set; }

    private void OnStartup(object sender, StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        // 初始化 Core（含设置存储、数据库、ESI 凭据），之后主题/语言才能读取设置。
        CoreInitializer.Init();
        ThemeService.Initialize();
        LanguageService.Initialize();

        SingleInstanceHelper = new SingleInstanceHelper();
        if (!SingleInstanceHelper.RegisterSingleInstance(SettingsService.DataPath))
        {
            // 已有实例在运行：参数已交给它（授权回调也走这条路径），本进程直接退出。
            Shutdown();
            return;
        }

        SingleInstanceHelper.Activated += OnSingleInstanceActivated;

        MainWindow = new Views.MainWindow();
        MainWindow.Show();


    }

    private void OnSingleInstanceActivated(object? sender, string[] args)
    {
        // 授权回调由正在等待的授权流程消费，这里不处理。
        var isAuthCallback = args?.Any(a => a.StartsWith("eveauth", StringComparison.OrdinalIgnoreCase)) == true;
        if (isAuthCallback)
        {
            return;
        }

        Dispatcher.Invoke(() =>
        {
            MainWindow?.Show();
            MainWindow?.Activate();
        });
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        MessageBox.Show(e.Exception.Message, "未处理的错误", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        SettingsService.Save();
    }
}
