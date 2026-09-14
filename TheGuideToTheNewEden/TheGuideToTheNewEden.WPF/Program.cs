using TheGuideToTheNewEden.Core.Helpers;
using TheGuideToTheNewEden.WPF.Services;

namespace TheGuideToTheNewEden.WPF;

/// <summary>
/// 自定义入口点（由 csproj 的 <c>StartupObject</c> 指定），替代 App.xaml 生成的 Main。
/// </summary>
/// <remarks>
/// 存在的唯一理由：**让"转发进程"根本不构造 WPF <see cref="System.Windows.Application"/>**。
///
/// 浏览器授权回调由 Windows 按注册表启动**第二个客户端进程**，它唯一的任务是把命令行
/// （<c>eveauth-*://?code=…</c>）转交给已运行的实例然后退出——这是 Windows 协议激活的机制边界，
/// 没有任何办法把 URL 直接投递给一个正在运行的进程。既然这次进程启动不可避免，就让它**尽早结束**。
///
/// 为什么不能放在 App.OnStartup 里做？两点实测结论（独立 WPF 探针，net8）：
/// 1. 走到 OnStartup 时 <c>App.InitializeComponent()</c> 已经解析加载了 WPF-UI 主题/控件字典
///    与项目全部资源字典，纯属白费；
/// 2. 在 OnStartup 里调用 <c>Shutdown()</c> **依然会触发 Exit 事件**
///    （实测时序：MAIN → STARTUP → EXIT → RUN returned），于是 App.OnExit 的清理逻辑会照跑，
///    其中 <c>SettingsService.Save()</c> 在**从未调用过 Initialize()** 的进程里会用空字典
///    把共享的 <c>settings.json</c> 覆盖成 <c>{}</c>。
///
/// 提前判定后，转发进程既不加载 XAML 也不触发任何退出清理，更不碰用户数据。
/// 另经实测：只要 Main 的方法体不引用 <see cref="App"/>（WPF 派生类），进程内
/// PresentationFramework / System.Xaml / WindowsBase 会**一个都不加载**
/// （引用 App 的静态成员则会连带其基类 Application 一起加载）；
/// 而读取普通静态类（如 <see cref="SettingsService.DataPath"/>，其方法签名里虽有 WPF 的 Color）
/// 不会加载任何 WPF 程序集。这也是单实例状态留在本类、而不放在 App 上的原因。
/// </remarks>
public static class Program
{
    /// <summary>
    /// WPF 版独占的单实例标识：与 WinUI 版（沿用 Core 默认标识）并存时不再互相抢占。
    /// 若两者共用同一标识，后启动的那一版会把命令行转交给对方然后自己退出（点了没反应）。
    /// </summary>
    internal const string InstanceName = "TheGuideToTheNewEden.WPF";

    /// <summary>单实例间传递命令行的临时文件名；与 WinUI 版分开，避免互相读到对方的残留。</summary>
    internal const string InstanceTempFile = "SingleInstanceTemp.WPF";

    /// <summary>
    /// 本进程的单实例助手；只有首个实例会拿到非 null 值。
    /// 由 <see cref="Main"/> 在创建 <see cref="System.Windows.Application"/> 之前赋值。
    /// </summary>
    public static SingleInstanceHelper? SingleInstance { get; private set; }

    [STAThread]
    public static int Main()
    {
        SingleInstanceHelper? singleInstance = null;
        try
        {
            singleInstance = new SingleInstanceHelper(InstanceName, InstanceTempFile);
            if (!singleInstance.RegisterSingleInstance(SettingsService.DataPath))
            {
                // 已有实例在运行：命令行已交给它（授权回调同样走这条路径），本进程到此为止。
                return 0;
            }
        }
        catch (Exception ex)
        {
            // 单实例机制意外失败（命名事件被别的会话/权限占用等）**不能**让程序起不来：
            // 退回"不做单实例"的普通启动——最坏结果只是双击时能多开一个窗口。
            // （这段代码比 CoreInitializer.Init() 更早执行，但 Core.Log 在未初始化时会安全自举。）
            Core.Log.Warn($"单实例注册失败，本次按独立实例启动：{ex}");
            singleInstance = null;
        }

        SingleInstance = singleInstance;

        var app = new App();
        app.InitializeComponent();
        return app.Run();
    }
}
