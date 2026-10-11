using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Converters;
using TheGuideToTheNewEden.WPF.ViewModels;
using TheGuideToTheNewEden.WPF.Views.Windows;
using Wpf.Ui.Controls;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>
/// 服务器测速页：对 host:port 每秒一次 TCP 连接测速，显示当前/平均延迟、丢包率与统计。
/// 对应 WinUI 版 <c>Views/Tools/ServerPingPage</c>（那里也是独立工具窗），逻辑见 <see cref="ServerPingPageViewModel"/>。
/// <para>
/// 页面在**独立工具窗口**里打开（见 <see cref="IToolPage"/>）。关窗时必须停掉测速循环——
/// 页面实例会被丢弃，但循环在 VM 里，不停就会一直跑下去（<see cref="OnToolClosed"/>）。
/// </para>
/// <para>
/// 窗口小到一定程度会切**迷你布局**（只看 开始/停止 + 当前延迟，见 XAML 里的
/// <see cref="PingCompactModeConverter"/>），此时窗口外壳也一起收窄：标题栏**只留「关闭」**
/// （最小化/最大化/置顶图钉全部收起，用户要求），更像一条挂机监控条。
/// 注意：收起图钉**不等于取消置顶**——窗口仍然是置顶的，只是不显示那颗开关。
/// </para>
/// </summary>
public partial class ServerPingPage : Page, IToolPage
{
    /// <summary>一键进迷你时窗口缩到的宽度（页面宽 &lt; 420 即判迷你，见转换器阈值）。</summary>
    private const double MiniModeWidth = 300;

    /// <summary>一键进迷你时窗口缩到的高度（页面高约 86：够一行按钮/读数，悬停时再多一行透明度滑条）。</summary>
    private const double MiniModeHeight = 120;

    private readonly ServerPingPageViewModel _viewModel = new();

    /// <summary>上一次是否处于迷你模式（null = 还没判过）：避免 <c>SizeChanged</c> 每次都去改标题栏。</summary>
    private bool? _compact;

    public ServerPingPage()
    {
        InitializeComponent();
        DataContext = _viewModel;

        // 迷你模式除了换布局，还要把标题栏收成"只留关闭"——见 ApplyWindowChrome
        SizeChanged += (_, _) => ApplyWindowChrome();

        // 透明度滑条要绑宿主窗口，窗口在 Loaded 时才拿得到
        Loaded += (_, _) => BindOpacitySliderToWindow();
    }

    /// <summary>要真透明的窗口外壳：迷你形态当挂机监控条，背景可调透明度、文字保持实色。</summary>
    public bool NeedsTransparentWindow => true;

    /// <summary>默认尺寸（同时决定窗口的最小尺寸，见 ToolWindowHost）：600×400。</summary>
    public int PreferredWidth => 600;

    public int PreferredHeight => 400;



    /// <summary>关窗即停止测速（并保留已有结果，供下次打开前查看——不过下次打开是全新实例）。</summary>
    public void OnToolClosed() => _viewModel.Stop();

    /// <summary>
    /// 按当前页面尺寸切换**窗口外壳**：迷你模式**只留「关闭」**（最小化/最大化/置顶图钉都收起），
    /// 回到大布局时还原「最小化 / 最大化 / 关闭 / 置顶」。
    /// 收起图钉只是不显示那颗开关，窗口本身**仍然是置顶的**（<c>Topmost</c> 不动）。
    /// 阈值与页面 XAML 里用的转换器 <see cref="PingCompactModeConverter"/> **共用同一组常量**，
    /// 所以"布局切换"和"标题栏收缩"永远同步、不会两边各写一个数字。
    /// </summary>
    private void ApplyWindowChrome()
    {
        var compact = ActualHeight < PingCompactModeConverter.CompactHeightThreshold
                      || ActualWidth < PingCompactModeConverter.CompactWidthThreshold;

        if (_compact == compact)
        {
            return;
        }

        _compact = compact;

        var host = Window.GetWindow(this);

        if (host is IToolWindowShell shell)
        {
            shell.SetVisibleTitleBarButtons(compact
                ? ToolWindowButtons.Close
                : ToolWindowButtons.Minimize | ToolWindowButtons.Maximize | ToolWindowButtons.Close | ToolWindowButtons.Topmost);
        }

        // 透明度是**迷你形态专属**的：切回完整布局时把背板恢复成不透明
        // （否则连标题栏都半透明，整窗看起来像坏掉了——用户实机反馈）。
        if (host is ToolTransparentWindow transparent)
        {
            transparent.SetPlateOpacity(compact ? MiniOpacitySlider.Value : 1.0);
        }
    }

    private async void OnStartClick(object sender, RoutedEventArgs e)
        => await _viewModel.StartAsync();

    private void OnStopClick(object sender, RoutedEventArgs e)
        => _viewModel.Stop();

    /// <summary>
    /// 一键进迷你：把**宿主窗口**缩到迷你尺寸（迷你模式本来就是尺寸驱动的，见 <see cref="PingCompactModeConverter"/>），
    /// 缩下去之后窗口会自动切迷你布局、标题栏自动收成"只留关闭"、透明度滑条也在迷你里可用。
    /// 回大布局把窗口拉大即可——尺寸驱动，两个方向天然对称，不需要额外状态。
    /// </summary>
    private void OnMiniModeClick(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is not Window host)
        {
            return;
        }

        // 迷你条的最省尺寸：够放"按钮 / 读数"一行，悬停时再多一行透明度滑条（页面高约 86）
        host.Width = MiniModeWidth;
        host.Height = MiniModeHeight;
    }

    /// <summary>
    /// 把迷你形态的透明度滑条接到**宿主窗口的亚克力透明度**上（拖动即真的透出桌面）。
    /// <para>
    /// 靠 <see cref="ToolTransparentWindow.SetPlateOpacity"/>：该外壳是逐像素透明的普通 Window（配方照 IntelWindow），
    /// 透明度只作用在**背板**上，所以文字与按钮保持实色。另两条路试过且不行（详见 §9 第 81 条）：
    /// <c>AllowsTransparency=true</c> 会与 WPF-UI 的 <c>FluentWindow</c> 冲突到开不出窗口。
    /// </para>
    /// <para>
    /// 还有个弯路：XAML 里写 `Value="{Binding Opacity, RelativeSource={RelativeSource AncestorType=Window}}"`
    /// **解析不到**宿主窗口（`ToolWindow` 用 `Frame` 承载页面），绑定失败后 `Slider.Value` 会退回默认值（＝`Minimum`）、
    /// 两头都不同步；所以这里用 <see cref="Window.GetWindow"/> 直接拿窗口、在 `ValueChanged` 里赋值。
    /// </para>
    /// </summary>
    private void BindOpacitySliderToWindow()
    {
        if (Window.GetWindow(this) is not ToolTransparentWindow host)
        {
            return;
        }

        MiniOpacitySlider.ValueChanged += (_, e) =>
        {
            // 透明度是**迷你形态专属**的：完整布局下滑条本来就不可见，这里再加一道保险
            if (_compact == true)
            {
                host.SetPlateOpacity(e.NewValue);
            }
        };

        host.SetPlateOpacity(_compact == true ? MiniOpacitySlider.Value : 1.0);
    }
}
