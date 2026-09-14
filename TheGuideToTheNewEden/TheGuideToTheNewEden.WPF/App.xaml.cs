using System.Windows;
using System.Windows.Threading;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.ChannelIntel;

namespace TheGuideToTheNewEden.WPF;

public partial class App : Application
{
    public static new Views.MainWindow? MainWindow { get; private set; }

    private void OnStartup(object sender, StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        // 单实例注册、命令行转发、以及"本进程是不是唯一实例"的判定，
        // 都已在 Program.Main 完成（先于 Application 创建），走到这里的进程必定是唯一实例。
        // 单实例状态放在 Program 而非本类：本类继承 Application，访问它的静态成员会连带
        // 加载 PresentationFramework，转发进程就白重了（见 Program 的说明）。
        if (Program.SingleInstance is not null)
        {
            Program.SingleInstance.Activated += OnSingleInstanceActivated;
        }

        // 初始化 Core（含设置存储、数据库、ESI 凭据），之后主题/语言才能读取设置。
        CoreInitializer.Init();
        ThemeService.Initialize();
        LanguageService.Initialize();

        MainWindow = new Views.MainWindow();
        MainWindow.Show();
    }

    private void OnSingleInstanceActivated(object? sender, string[] args)
    {
        // 自定义协议（注册表）通道已停用，但保留这段识别：万一有人从旧的 eveauth 链接
        // 唤起了本进程，也不该把它当成"用户又开了一次程序"而去抢窗口焦点。
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
        // 频道预警：释放预警小窗/声音播放器/舰船名缓存，并停掉全部文件监控
        IntelWarningService.Current.Dispose();
        Core.Services.ObservableFileService.StopAll();
        Core.Services.DB.ShipNameCacheService.Current.Dispose();
        SettingsService.Save();
    }
}
