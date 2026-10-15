using System.Windows;
using System.Windows.Threading;
using TheGuideToTheNewEden.WPF;

namespace TheGuideToTheNewEden.Probe;

/// <summary>
/// 探针里需要"应用级资源字典 + 可用的 WPF 调度上下文"的套件共用的初始化入口。
/// <para>
/// **一个进程只能有一个 <see cref="Application"/>**：多次 <c>new App()</c> 会抛异常，
/// 所以这里做"已有就复用、没有才建"，供 UiSuite / QuickRouteSuite / MapProbe 共享。
/// </para>
/// <para>
/// <b>必须自己装 <see cref="DispatcherSynchronizationContext"/></b>（项目踩坑 §90）：
/// 探针不跑 <c>Application.Run</c>，于是 STA 线程上没有同步上下文，界面代码里的 <c>await</c> 续体会落到
/// **线程池**，再去碰 UI 元素就抛"调用线程无法访问此对象"——那会让探针进程直接崩掉（阶段 115 实际踩到）。
/// 装上上下文后，续体会回到本线程，再由 <see cref="Pump"/> 推着跑完。
/// </para>
/// <para>
/// 同时收集"异步未处理异常"（<c>DispatcherUnhandledException</c> / 未观察 Task 异常）：
/// 界面里的 <c>async void</c> / fire-and-forget 出问题是**不会**被 try/catch 抓到的，
/// 这里把它登记下来供套件断言，把"进程崩溃"变成"可见的失败"。
/// </para>
/// </summary>
internal static class ProbeApplication
{
    private static readonly List<string> Unhandled = [];
    private static Application? _application;

    /// <summary>已收集到的异步未处理异常（文本）。套件可断言其为空。</summary>
    public static IReadOnlyList<string> UnhandledExceptions => Unhandled;

    public static void ClearUnhandled()
    {
        lock (Unhandled)
        {
            Unhandled.Clear();
        }
    }

    /// <summary>
    /// 确保应用资源已载入，并把异常收集装好。
    /// <para>
    /// **用静态引用记住实例**：一个 AppDomain 只能有一个 <see cref="Application"/>，
    /// 且 <c>Application.Current</c> 在某些时序下可能读到 null（此时再 new 会抛
    /// "不能在同一 AppDomain 中创建多个 Application 实例"——阶段 115 在 UiSuite 上实际踩到）。
    /// </para>
    /// </summary>
    public static Application Ensure()
    {
        if (_application is not null)
        {
            return _application;
        }

        if (Application.Current is { } existing)
        {
            Console.WriteLine("  [探针] 复用已存在的 Application 实例");
            _application = existing;
            return existing;
        }

        var app = new App();
        app.InitializeComponent();
        Console.WriteLine("  [探针] 新建 Application 实例并载入应用资源字典");

        app.DispatcherUnhandledException += (_, args) =>
        {
            Record("Dispatcher", args.Exception);
            args.Handled = true;   // 不让探针进程因为一处异步异常整个崩掉
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Record("Task", args.Exception);
            args.SetObserved();
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                Record("AppDomain", ex);
            }
        };

        _application = app;
        return app;
    }

    /// <summary>
    /// 装上 <see cref="DispatcherSynchronizationContext"/>（见类注释）：
    /// **只在"要跑界面异步代码"的套件里调用**——装上之后，探针里那些
    /// <c>task.GetAwaiter().GetResult()</c> 的阻塞式调用必须放到线程池上跑（见 <see cref="RunBlocking"/>），
    /// 否则工具内部的 <c>await</c> 会把续体投给没有在泵的队列，直接死锁。
    /// </summary>
    public static void InstallDispatcherContext()
        => SynchronizationContext.SetSynchronizationContext(
            new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));

    /// <summary>
    /// 在**线程池**上跑一段异步工作并阻塞取结果：避免"同步等异步"撞上已安装的调度上下文而死锁。
    /// 探针里所有"直接调用工具/服务"的地方都应走它。
    /// </summary>
    public static T RunBlocking<T>(Func<Task<T>> work) => Task.Run(work).GetAwaiter().GetResult();

    /// <summary>无返回值的版本。</summary>
    public static void RunBlocking(Func<Task> work) => Task.Run(work).GetAwaiter().GetResult();

    /// <summary>
    /// 把 Dispatcher 队列推着跑一小段（离屏渲染、异步续体都靠它推进）。
    /// <para>
    /// **三种写法都在这里踩过（阶段 115）**：
    /// ① "等到 ContextIdle 就退出" + <c>PushFrame</c>：星图画布有持续渲染循环，低优先级空闲回调被饿死，
    ///    <c>PushFrame</c> 永不返回（卡死过两次）；
    /// ② <see cref="DispatcherTimer"/> 限时：它本身也走队列，同样被渲染循环挡住；
    /// ③ 线程池计时器 + <c>PushFrame</c>：仍然不返回。
    /// 现在改成**不用 PushFrame**：按墙钟限时循环，每轮在最高优先级（<see cref="DispatcherPriority.Send"/>）
    /// 插一个空操作——Send 抢在所有渲染/绑定之前，必定被处理，所以既推得动队列又不可能卡死。
    /// </para>
    /// </summary>
    public static void Pump(int milliseconds = 800)
    {
        var deadline = Environment.TickCount64 + milliseconds;
        while (Environment.TickCount64 < deadline)
        {
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Send);
            Thread.Sleep(5);
        }
    }

    private static void Record(string source, Exception ex)
    {
        lock (Unhandled)
        {
            Unhandled.Add($"[{source}] {ex.GetType().Name}: {ex.Message}");
        }
    }
}
