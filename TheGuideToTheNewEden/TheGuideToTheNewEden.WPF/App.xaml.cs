using System.Windows;
using System.Windows.Threading;
using TheGuideToTheNewEden.WPF.Services;

namespace TheGuideToTheNewEden.WPF;

public partial class App : Application
{
    public static new Views.MainWindow? MainWindow { get; private set; }

    private void OnStartup(object sender, StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        // 初始化 Core（含设置存储、数据库），之后主题/语言才能读取设置。
        CoreInitializer.Init();
        ThemeService.Initialize();
        LanguageService.Initialize();

        MainWindow = new Views.MainWindow();
        MainWindow.Show();

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
