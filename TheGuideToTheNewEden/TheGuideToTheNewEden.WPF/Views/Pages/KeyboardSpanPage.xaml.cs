using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.ViewModels;
using TheGuideToTheNewEden.WPF.Views.Windows;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>
/// 键盘跨度页：全局监听某个按键，按"时间间隔"或"按键次数"统计通过次数。
/// 对应 WinUI 版 <c>Views/Tools/KeyboardSpanPage</c>（那里也是独立工具窗，原尺寸 400×280），
/// 逻辑见 <see cref="KeyboardSpanPageViewModel"/>。
/// <para>
/// 页面在**独立工具窗口**里打开（见 <see cref="IToolPage"/>）。关窗必须卸载键盘钩子，
/// 否则钩子会一直挂在系统里（<see cref="OnToolClosed"/>）。
/// </para>
/// </summary>
public partial class KeyboardSpanPage : Page, IToolPage
{
    private readonly KeyboardSpanPageViewModel _viewModel = new();
    private bool _loaded;

    /// <summary>开始后自动缩到的迷你尺寸（挂机条：够放大号通过数 + 底部一行，悬停还能放透明度滑条）。</summary>
    private const double MiniModeWidth = 200;

    /// <summary>见 <see cref="MiniModeWidth"/>；高度留足余量，悬停出现透明度滑条时也不会被窗口下沿切掉。</summary>
    private const double MiniModeHeight = 140;

    /// <summary>开始前的窗口尺寸，停止时用来恢复（设置表单需要空间）。</summary>
    private Size? _sizeBeforeMini;

    public KeyboardSpanPage()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    /// <summary>默认尺寸与 WinUI 版 <c>GetWindowSize</c> 一致（400×280；设置项多，页面内可滚动）。</summary>
    /// <summary>要真透明的窗口外壳：这个工具是长期置顶挂机显示用的（与测速的迷你条同定位）。</summary>
    public bool NeedsTransparentWindow => true;

    public int PreferredWidth => 400;

    public int PreferredHeight => 340;

    /// <summary>关窗即停止监听并卸载钩子。</summary>
    public void OnToolClosed() => _viewModel.Stop();

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 常驻页面只加载一次；此时刷新一次游戏窗口列表。
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        BindOpacitySliderToWindow();
        await _viewModel.RefreshCharactersAsync();
    }

    private async void OnStartClick(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is ToolTransparentWindow host)
        {
            // 运行态才允许透明：开始按滑条值设背板（默认 1 = 不透明）
            host.SetPlateOpacity(MiniOpacitySlider.Value);

            // 开始后**自动进迷你**：记住原尺寸再缩到挂机条大小（用户要求）
            _sizeBeforeMini = new Size(host.ActualWidth, host.ActualHeight);
            host.Width = MiniModeWidth;
            host.Height = MiniModeHeight;
        }

        await _viewModel.StartAsync();
    }

    private void OnStopClick(object sender, RoutedEventArgs e)
    {
        _viewModel.Stop();

        if (Window.GetWindow(this) is ToolTransparentWindow host)
        {
            // 停止后回到不透明的普通窗口（设置态不该是半透明的）
            host.SetPlateOpacity(1.0);

            // 恢复开始前的尺寸，让设置表单重新有地方；
            // 但用户若在运行期间自己拉大过窗口，就尊重他的调整、不去覆盖。
            if (_sizeBeforeMini is { } size && IsStillMiniSize(host))
            {
                host.Width = size.Width;
                host.Height = size.Height;
            }

            _sizeBeforeMini = null;
        }
    }

    /// <summary>窗口是否仍是"自动迷你"后的那个尺寸（用来判断用户有没有手动改过）。</summary>
    private static bool IsStillMiniSize(Window window)
        => Math.Abs(window.Width - MiniModeWidth) < 1 && Math.Abs(window.Height - MiniModeHeight) < 1;

    /// <summary>
    /// 透明度滑条 → 宿主窗口背板（同测速工具）。只在**运行态**生效：
    /// 设置态与停止后都保持不透明（与测速那边"透明度只属于挂机形态"的口径一致）。
    /// </summary>
    private void BindOpacitySliderToWindow()
    {
        if (Window.GetWindow(this) is not ToolTransparentWindow host)
        {
            return;
        }

        MiniOpacitySlider.ValueChanged += (_, e) =>
        {
            if (_viewModel.IsRunning)
            {
                host.SetPlateOpacity(e.NewValue);
            }
        };
    }

    private async void OnRefreshCharactersClick(object sender, RoutedEventArgs e)
        => await _viewModel.RefreshCharactersAsync();

    private void OnLocateClick(object sender, RoutedEventArgs e)
        => _viewModel.BringCharacterToFront();
}
