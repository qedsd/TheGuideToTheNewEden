using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using TheGuideToTheNewEden.WPF.Helpers;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.ChannelIntel;

namespace TheGuideToTheNewEden.WPF;

public partial class App : Application
{
    public static new Views.MainWindow? MainWindow { get; private set; }

    /// <summary>本次退出后是否重新拉起自身（改数据目录等"需要重启"的设置会用到，阶段 186）。</summary>
    private static bool _restartPending;

    /// <summary>请求退出并重新启动应用。</summary>
    public static void RequestRestart()
    {
        _restartPending = true;
        Current?.Shutdown();
    }

    private void OnStartup(object sender, StartupEventArgs e)
    {
        // Primary / Danger 按钮的图标颜色统一（默认样式不会改 Icon.Foreground）
        ButtonIconThemeBehavior.Initialize();

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        // 后台线程/未观察的 Task 异常也要落到日志：否则"界面没报错、日志也什么都没有"，
        // 只能靠猜（阶段 53 排障时正是如此——ZKB 的流消费、分页取数都在后台线程上）。
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Core.Log.Error(args.ExceptionObject as Exception ?? new Exception(args.ExceptionObject?.ToString()));
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Core.Log.Error(args.Exception);
            args.SetObserved();
        };

        // 单实例注册、命令行转发、以及"本进程是不是唯一实例"的判定，
        // 都已在 Program.Main 完成（先于 Application 创建），走到这里的进程必定是唯一实例。
        // 单实例状态放在 Program 而非本类：本类继承 Application，访问它的静态成员会连带
        // 加载 PresentationFramework，转发进程就白重了（见 Program 的说明）。
        if (Program.SingleInstance is not null)
        {
            Program.SingleInstance.Activated += OnSingleInstanceActivated;
        }

        // 数据目录必须最先确定：设置、日志路径、缓存目录都按它解析（阶段 186）。
        // 首次运行且检测到旧版本配置时，先让用户选择沿用旧目录还是用按大版本隔离的新目录。
        DataPathService.Initialize();
        if (DataPathService.NeedsDecision)
        {
            var useLegacy = false;
            var copyLegacy = true;
            try
            {
                // 选择框本身要按旧配置的语言显示（此时还没有 SettingsService）
                LanguageService.UseStartupLanguage(
                    LanguageService.TryReadUiLanguage(DataPathService.LegacyDataPath));

                var dialog = new Views.Windows.DataPathMigrationWindow();
                dialog.ShowDialog();
                useLegacy = dialog.UseLegacyFolder;
                copyLegacy = dialog.CopyLegacyConfig;
            }
            catch (Exception ex)
            {
                // 选择框本身出问题不能把程序卡在启动前：按推荐方案继续
                Core.Log.Error(ex);
            }

            DataPathService.ApplyFirstRunChoice(useLegacy, copyLegacy);
        }

        // 初始化 Core（含设置存储、数据库、ESI 凭据），之后主题/语言才能读取设置。
        CoreInitializer.Init();
        ThemeService.Initialize();
        LanguageService.Initialize();

        try
        {
            MainWindow = new Views.MainWindow();
            MainWindow.Show();
        }
        catch (Exception ex) when (TryShowFatalStartupError(ex))
        {
            Shutdown(-1);
        }
    }

    /// <summary>
    /// 建主窗口失败时的兜底（**资源不完整**是最常见的一种）。
    /// <para>
    /// WPF 把界面/图片资源打包进程序集的一个 <c>*.g.resources</c>；当**多个构建同时写同一工程**时
    /// （例如 IDE 里点生成、命令行同时 <c>dotnet build</c>），可能产出资源残缺的 dll，症状就是启动即报
    /// <c>IOException: 找不到资源"…"</c>（阶段 113 两度遇到：语言字典 / banner / app.ico）。
    /// 这里把那种"看不出所以然"的崩溃框换成一句能照做的提示。
    /// </para>
    /// </summary>
    private static bool TryShowFatalStartupError(Exception ex)
    {
        if (ex is not IOException and not System.Windows.Markup.XamlParseException)
        {
            return false;
        }

        Core.Log.Error(ex);

        // 把**内层**异常也带出来：WPF 的外层常常只说"在 X 上提供值时引发了异常"，
        // 真正的原因在内层（"找不到资源 xxx"）——阶段 113/114 排障时就因为只显示外层而多绕了一圈。
        var detail = new StringBuilder();
        var current = ex;
        for (var depth = 0; current is not null && depth < 4; depth++)
        {
            detail.Append(depth == 0 ? string.Empty : "\n→ ").Append(current.Message);
            current = current.InnerException;
        }

        MessageBox.Show(
            "界面资源加载失败。\n\n" +
            detail + "\n\n" +
            "常见原因：本次生成的 dll 资源不完整（生成过程中被其它进程同时写，或生成到一半就被启动）。\n" +
            "请「重新生成解决方案」后重试；若仍失败，请把 Log 目录里的日志反馈回来。",
            "TheGuideToTheNewEden",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        return true;
    }

    private void OnSingleInstanceActivated(object? sender, string[] args)
    {
        // 第二个进程的处境已在 Program.Main 处理完（转交命令行后直接 return 0，不构造 Application），
        // 走到这里的只可能是"用户又启动了一次"，把主窗口带到前台即可。
        Dispatcher.Invoke(() =>
        {
            MainWindow?.Show();
            MainWindow?.Activate();
        });
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;

        // 必须记日志：此前只弹框、不记录，导致"界面上报了一个异常，但日志里什么都没有"，
        // 无法定位调用链（阶段 53 排障教训）。弹框里给出类型 + 完整堆栈，便于直接贴给我们。
        Core.Log.Error(e.Exception);
        MessageBox.Show(e.Exception.ToString(), "未处理的错误", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        // 频道预警：释放预警小窗/声音播放器/舰船名缓存，并停掉全部文件监控
        IntelWarningService.Current.Dispose();
        Core.Services.ObservableFileService.StopAll();
        Core.Services.DB.ShipNameCacheService.Current.Dispose();
        SettingsService.Save();

        if (_restartPending)
        {
            StartDetachedRestart();
        }
    }

    /// <summary>
    /// 延迟 2 秒后重新启动自身：必须等本进程**真正退出**（释放单实例命名事件、
    /// 放开输出目录里的文件锁），否则新进程会被当成"第二个实例"直接转交命令行后退出。
    /// </summary>
    private static void StartDetachedRestart()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exe))
            {
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c timeout /t 2 /nobreak >nul & start \"\" \"{exe}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
            });
        }
        catch (Exception ex)
        {
            Core.Log.Warn($"[重启] 启动新进程失败：{ex.Message}");
        }
    }
}
