using System.Windows;
using TheGuideToTheNewEden.WPF.Views.Pages;
using TheGuideToTheNewEden.WPF.Views.Windows;

namespace TheGuideToTheNewEden.WPF.Services.Tools;

/// <summary>
/// 「工具」窗口的宿主：把 <see cref="IToolPage"/> 页面放进统一的 <see cref="ToolWindow"/> 外壳里打开。
/// <para>
/// 行为：同一个工具**同时只开一个**（再次点击只是激活已开的那个）；窗口关闭时调用
/// <see cref="IToolPage.OnToolClosed"/> 停掉后台工作，并把页面/窗口实例一起丢弃——
/// 下次打开是**全新的一份**（状态干净，也不会留下还在跑的钩子或测速循环）。
/// </para>
/// <para>
/// 标题用 <c>SetResourceReference</c> 绑定到语言键，因此**切语言时窗口标题会跟着变**；
/// 窗口归属主窗口（<c>Owner</c>），所以主窗口最小化时会一起最小化，但**隐藏到托盘不会**（隐藏不是最小化）。
/// </para>
/// <para>
/// **默认置顶，并在标题栏显示置顶按钮**（用户要求）：<c>Topmost = true</c> 让它浮在*所有*窗口之上，
/// 标题栏的图钉按钮可随时取消/恢复（`ToolWindow.ShowTopmostButton` + 内部的 `UpdateTopmostVisual`）。
/// 早先刻意不给这个按钮，理由是"被拥有的窗口本来就恒在宿主之上，给了会显得没用"（§9 第 26 条）——
/// 但现在默认就是真的 <c>Topmost</c>（跨应用置顶），那个"看起来没用"的前提不再成立，按钮也就有意义了。
/// </para>
/// </summary>
public static class ToolWindowHost
{
    private static readonly Dictionary<Type, Window> Opened = new();

    /// <summary>打开（或激活）某个工具窗口。</summary>
    /// <param name="pageType">继承 <see cref="IToolPage"/> 的页面类型。</param>
    /// <param name="titleKey">窗口标题的本地化键。</param>
    /// <param name="page">
    /// 已经创建好的页面实例（可选）。传它是为了避免"多造一个页面"：WPF-UI 在 <c>Navigating</c> 里给出的
    /// <c>args.Page</c> 就是它已经实例化好的页面，拦下导航之后这个实例没人用——直接拿来当窗口内容，
    /// 既省一次构造，也不会留下一个"没人释放、却订阅着静态事件"的孤儿页面。
    /// </param>
    public static void Show(Type pageType, string titleKey, IToolPage? page = null)
    {
        if (Opened.TryGetValue(pageType, out var existing) && existing.IsLoaded)
        {
            if (existing.WindowState == WindowState.Minimized)
            {
                existing.WindowState = WindowState.Normal;
            }

            existing.Activate();
            return;
        }

        var instance = page ?? Activator.CreateInstance(pageType) as IToolPage;
        if (instance is not FrameworkElement element)
        {
            Core.Log.Error($"[ToolWindowHost] 无法创建工具页面：{pageType.FullName}");
            return;
        }

        var width = instance.PreferredWidth;
        var height = instance.PreferredHeight;

        // 外壳二选一：要"真透明"的工具（目前只有测速：迷你形态当挂机条）用 ToolTransparentWindow
        // （普通 Window + AllowsTransparency + 半透明背板，配方照 IntelWindow），其余用普通 ToolWindow。
        var useTransparentShell = instance.NeedsTransparentWindow;

        Window window = useTransparentShell
            ? new ToolTransparentWindow(element, titleKey, showInTaskbar: true, width: width, height: height)
            : new ToolWindow(
                element,
                ToolWindowTitleStyle.Default,
                showTopmostButton: true,
                showInTaskbar: true,
                width: width,
                height: height);


        // 工具窗默认置顶（用户要求）：真的 HWND_TOPMOST，浮在其它应用的窗口之上；
        // 标题栏那颗图钉可随时取消。注意要在 Show 之前设好——ToolWindow 在 Loaded 里
        // 走 ApplyToTitleBar() → UpdateTopmostVisual()，那里会按当前 Topmost 定图标与提示。
        window.Topmost = true;

        // 最小尺寸：只有当"本工具的默认尺寸比 ToolWindow.xaml 的通用下限（260×180）还小"时才需要下压
        // （例如任务栏工具 200×140，不压就会被通用下限顶大）；默认尺寸本来更大时保留通用下限，
        // 于是测速（820×560）、键盘跨度（400×280）这类窗口可以**自由缩小**。
        // 缩到比内容还小时由各页自己的 ScrollViewer 滚动，不会被裁掉（见各页 "Grid.MinHeight 绑视口高度" 的写法）。
        window.MinWidth = Math.Min(width, window.MinWidth);
        window.MinHeight = Math.Min(height, window.MinHeight);

        // 工具窗默认**居中屏幕**（用户要求）。
        // 这里刻意**不设 `Owner`**（历史上设过，为了让工具窗随主窗口最小化；后来注释掉了）：
        // 没有 Owner 时 `CenterOwner` 的落点并不明确，所以显式用 `CenterScreen`
        // ——它会落在**鼠标所在那块屏幕**的工作区正中，多显示器下也不会跑到别的屏去。
        window.WindowStartupLocation = WindowStartupLocation.CenterScreen;

        // 标题跟随语言（动态资源引用，切语言即时生效）。
        if (window is ToolWindow tool)
        {
            tool.SetResourceReference(ToolWindow.DisplayTitleProperty, titleKey);
            tool.SetResourceReference(ToolWindow.SystemTitleProperty, titleKey);

            // F1：工具窗口不在主窗口的导航里，说明书映射在这里直接给它（tools/ime 等）。
            tool.ManualId = Services.Manual.ManualToolMap.TryGetManual(pageType);
        }
        else
        {
            // 透明外壳的标题由窗口 Title 驱动（其 XAML 里标题 TextBlock 绑 Window.Title）
            window.SetResourceReference(Window.TitleProperty, titleKey);
        }

        window.Closed += (_, _) =>
        {
            Opened.Remove(pageType);

            try
            {
                instance.OnToolClosed();
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
            }
        };

        Opened[pageType] = window;
        window.Show();
        window.Activate();
    }
}
