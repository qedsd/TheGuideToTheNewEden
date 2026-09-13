using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using TheGuideToTheNewEden.Core.Models.GamePreviews;
using TheGuideToTheNewEden.WPF.Helpers.Interop;
using TheGuideToTheNewEden.WPF.Services.GamePreview;

namespace TheGuideToTheNewEden.WPF.Views.Windows;

/// <summary>
/// 多开预览窗口：把源 EVE 客户端用 <b>DWM 缩略图</b>实时合成进本窗口的内容区，不做抓图、不走 IPC。
/// <para>
/// 两种显示样式（<see cref="PreviewItem.ShowPreviewWindowMode"/>）：
/// <list type="bullet">
///   <item><b>0 带标题栏</b>：就是"普通 WPF-UI 窗口"——标准 <c>ui:TitleBar</c>（与主窗口/频道预警小窗同款），
///         游戏画面只占内容区；标题栏高度比默认略小。</item>
///   <item><b>1 无标题栏</b>：彩色细名称条（颜色取设置里的"名称条颜色"），画面占满其余区域。</item>
/// </list>
/// 与 WinUI 版的架构差异：不使用透明窗口（画面淡化由 DWM 缩略图自身的 opacity 控制，标题栏/边框始终实色）；
/// 窗口位置尺寸全部以物理像素经 Win32 读写；窗口带
/// <c>WS_EX_NOACTIVATE</c>，显示/点击都不抢游戏焦点。
/// </para>
/// <para>
/// <b>窗口尺寸锁定游戏客户区比例</b>：画面所在区域的尺寸会被自动校正为与"游戏客户区"同比例
/// （尺寸里的标题栏/名称条/高亮边距等固定装饰由窗口自己补足），因此画面始终铺满内容区、不出现黑边。
/// 校正在"尺寸稳定后"执行一次（防抖定时器 + 重入保护），可调维度按显示样式区分：
/// 样式 0 保持窗口高度、反算客户区宽度，样式 1 保持宽度、反算高度。想要别的比例就改游戏窗口的宽高比。
/// </para>
/// </summary>
public sealed partial class GamePreviewWindow : Wpf.Ui.Controls.FluentWindow, IPreviewWindow
{
    /// <summary><c>WM_SIZING</c> 的载荷（只需用到矩形与拖动边）。</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT32
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private const int MinWindowWidth = 200;
    private const int MinWindowHeight = 120;
    private const int DragThreshold = 4;

    /// <summary>
    /// 尺寸校正的死区（物理像素）。校正的基准量全是 Win32 整数矩形，因此收敛是精确的
    /// （数值验证：100%/125%/150%/200% 缩放下都是一轮到位、残余不足 1px）；
    /// 留 2px 只是为了不因无关的偶发取整差反复下发尺寸。
    /// </summary>
    private const int AspectTolerance = 2;

    /// <summary>尺寸稳定多久之后再做比例校正（毫秒）。</summary>
    private const int AspectCorrectionDelayMs = 80;

    /// <summary>轮询"源窗口是否最小化"的间隔（毫秒）。</summary>
    private const int MinimizeWatchIntervalMs = 300;

    /// <summary>角色名"跟随缩放"的基准窗口宽度（物理像素）：窗口等于这个宽度时字号就是设置值。</summary>
    private const int NameOverlayReferenceWidth = 960;

    /// <summary>"跟随缩放"的字号上限倍率，避免窗口拉得很大时角标大到离谱。</summary>
    private const double MaxNameOverlayScale = 4d;

    private readonly DwmThumbnail _thumbnail = new();
    private readonly PreviewSetting _globalSetting;

    /// <summary>尺寸稳定后做一次比例校正的防抖定时器。</summary>
    private readonly DispatcherTimer _aspectCorrectionTimer;

    /// <summary>轮询"源窗口是否最小化"的定时器（最小化时隐藏缩略图，避免残留快照）。</summary>
    private readonly DispatcherTimer _minimizeWatchTimer;

    private IntPtr _hwnd;

    private bool _stopped;
    private bool _isShowing;
    private bool _isHighlighted;

    /// <summary>源窗口（游戏客户端）当前是否在前台；只用于角色名底色的高亮，与边框高亮开关无关。</summary>
    private bool _isSourceForeground;

    /// <summary>比例校正期间为 true，避免"校正 → 布局回调 → 再校正"的自我循环。</summary>
    private bool _applyingAspectCorrection;

    /// <summary>最近一次下发过的窗口尺寸；同一尺寸不重复下发。</summary>
    private (int Width, int Height) _lastCorrectedSize;

    /// <summary>最近一次观察到的"源窗口已最小化"状态。</summary>
    private bool _sourceMinimized;

    /// <summary>正在走"关闭窗口"流程（用户点 X）：此时不能再对任何窗口调用 Close()。</summary>
    private bool _closing;

    /// <summary>左上角角色名叠加窗（必须在预览窗口之上，见 <see cref="CreateNameOverlay"/>）。</summary>
    private PreviewNameOverlayWindow? _nameOverlay;

    private bool _dragCandidate;
    private bool _dragging;
    private NativeMethods.POINT _dragStartCursor;
    private NativeMethods.RECT _dragStartRect;

    private bool _thumbnailUpdateQueued;

    public ProcessInfo Process { get; private set; }

    public PreviewItem Setting { get; private set; }

    public bool IsShowing => _isShowing;

    public event Action<PreviewItem>? SettingChanged;

    public event Action<PreviewItem>? StopRequested;

    public GamePreviewWindow(ProcessInfo process, PreviewItem setting, PreviewSetting globalSetting)
    {
        Process = process;
        Setting = setting;
        _globalSetting = globalSetting;

        InitializeComponent();

        _hwnd = new WindowInteropHelper(this).EnsureHandle();

        _aspectCorrectionTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(AspectCorrectionDelayMs),
        };
        _aspectCorrectionTimer.Tick += OnAspectCorrectionTick;

        _minimizeWatchTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(MinimizeWatchIntervalMs),
        };
        _minimizeWatchTimer.Tick += MinimizeWatchTick;

        TitleBar.CloseClicked += (_, _) => StopRequested?.Invoke(Setting);
        PreviewMouseWheel += OnPreviewMouseWheel;
        AttachDragHandlers(ThumbnailFrame);
    }

    // ---------- 生命周期 ----------

    /// <summary>
    /// WPF-UI 的窗口外观在 SourceInitialized 阶段设置，所以扩展样式必须在 base 之后再加，
    /// 否则会被覆盖掉。
    /// </summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ApplyWindowStyles();

        _hwnd = new WindowInteropHelper(this).Handle;

        // 用鼠标拖边框改尺寸时，在 WM_SIZING 里把"另一条边"改写成同游戏客户区比例所需的长度。
        // 注意：这里只改写消息里的矩形、**不调用 SetWindowPos**，所以不存在
        // "改尺寸 → 又收到尺寸消息 → 再改尺寸"的自循环（那个坑见 REFACTORING 阶段 44）。
        HwndSource.FromHwnd(_hwnd)?.AddHook(OnWindowMessage);
    }

    private IntPtr OnWindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_SIZING = 0x0214;

        if (msg != WM_SIZING || _stopped)
        {
            return IntPtr.Zero;
        }

        try
        {
            var rect = Marshal.PtrToStructure<RECT32>(lParam);
            var locked = LockClientAspect(rect, wParam.ToInt32());
            if (locked is { } target)
            {
                rect = target;
                Marshal.StructureToPtr(rect, lParam, true);
            }
        }
        catch (Exception ex)
        {
            // 钩子里抛异常会打断窗口消息处理，这里只记录、放行
            Core.Log.Error(ex);
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// 把拖动中的窗口矩形改写成"画面区域仍与游戏客户区同比例"的矩形；无需调整时返回 null。
    /// <para>
    /// 用户拖动哪条边就以哪条边为准：左右边 → 固定宽度、按比例反算高度；上下边 → 固定高度、反算宽度；
    /// 角 → 固定宽度、反算高度（与左右边一致）。这样鼠标始终控制他正在拖的那条边。
    /// </para>
    /// </summary>
    private RECT32? LockClientAspect(RECT32 rect, int edge)
    {
        // WM_SIZING 的 wParam：1 左 / 2 右 / 3 上 / 4 左上 / 5 右上 / 6 下 / 7 左下 / 8 右下
        const int WMSZ_LEFT = 1;
        const int WMSZ_TOP = 3;
        const int WMSZ_TOPLEFT = 4;
        const int WMSZ_TOPRIGHT = 5;
        const int WMSZ_BOTTOM = 6;
        const int WMSZ_BOTTOMLEFT = 7;

        var proposedWidth = rect.Right - rect.Left;
        var proposedHeight = rect.Bottom - rect.Top;
        if (proposedWidth <= 0 || proposedHeight <= 0)
        {
            return null;
        }

        // 源窗口最小化时它的客户区是"任务栏缩略图"那种又宽又扁的小矩形，
        // 拿它当比例依据会把预览窗压扁；此时不锁定比例，等还原后再算
        if (IsSourceMinimized())
        {
            return null;
        }

        // 标题栏/名称条的高度必须直接量元素——WPF-UI 把标题栏画在客户区内部，
        // "窗口高 − 客户区高"恒为 0（详见 GetChromeHeight 的注释）。
        if (!NativeMethods.GetClientRect(_hwnd, out var client)
            || !TryGetSourceSize(out var sourceWidth, out var sourceHeight))
        {
            return null;
        }

        var padding = GetHighlightPadding();
        var paddingWidth = (int)Math.Round(padding.Horizontal);
        var paddingHeight = (int)Math.Round(padding.Vertical);
        var chromeHeight = GetChromeHeight();

        // 画面区域 = 客户区 − 标题栏/名称条 − 高亮留白（两者都在客户区内部）
        var areaHeight = Math.Max(1, client.Height - chromeHeight - paddingHeight);
        var areaWidth = Math.Max(1, client.Width - paddingWidth);
        var aspect = (double)sourceWidth / sourceHeight;

        int width;
        int height;
        var draggingVertical = edge is WMSZ_TOP or WMSZ_BOTTOM;
        if (draggingVertical)
        {
            height = proposedHeight;
            width = (int)Math.Round(areaHeight * aspect) + paddingWidth;
        }
        else
        {
            width = proposedWidth;
            height = (int)Math.Round(areaWidth / aspect) + paddingHeight + chromeHeight;
        }

        // 夹进工作区时按"窗口尺寸自身的比例"收缩——那就是最终要维持的比例
        var (clampedWidth, clampedHeight) = ClampToWorkArea(width, height);
        if (Math.Abs(clampedWidth - proposedWidth) <= AspectTolerance && Math.Abs(clampedHeight - proposedHeight) <= AspectTolerance)
        {
            return null;
        }

        // 只动"用户没拖的那两条边"：拖左边/上边时锚点在右/下，反之锚点在左/上
        var anchorRight = edge is WMSZ_LEFT or WMSZ_TOPLEFT or WMSZ_BOTTOMLEFT;
        var anchorBottom = edge is WMSZ_TOP or WMSZ_TOPLEFT or WMSZ_TOPRIGHT;

        var result = new RECT32 { Left = rect.Left, Top = rect.Top, Right = rect.Right, Bottom = rect.Bottom };
        if (anchorRight)
        {
            result.Left = result.Right - clampedWidth;
        }
        else
        {
            result.Right = result.Left + clampedWidth;
        }

        if (anchorBottom)
        {
            result.Top = result.Bottom - clampedHeight;
        }
        else
        {
            result.Bottom = result.Top + clampedHeight;
        }

        return result;
    }

    /// <summary>把尺寸夹进"当前窗口所在显示器的工作区"，保持自身比例，避免按比例放大后跑出屏幕。</summary>
    private (int Width, int Height) ClampToWorkArea(int width, int height)
    {
        if (!NativeMethods.TryGetMonitorWorkArea(_hwnd, out var workArea) || workArea.Width <= 0 || workArea.Height <= 0)
        {
            return (Math.Max(MinWindowWidth, width), Math.Max(MinWindowHeight, height));
        }

        return PreviewGeometry.ScalePreservingAspect(
            width,
            height,
            width,
            height,
            1.0,
            MinWindowWidth,
            MinWindowHeight,
            Math.Max(MinWindowWidth, workArea.Width),
            Math.Max(MinWindowHeight, workArea.Height));
    }

    /// <summary>窗口带标准标题栏（样式 0）还是彩色细名称条（样式 1）。</summary>
    private bool HasTitleBar => Setting.ShowPreviewWindowMode <= 0;

    /// <summary>
    /// 标题栏 / 名称条占用的高度（物理像素）。
    /// <para>
    /// <b>不能用「窗口高 − 客户区高」求它</b>：WPF-UI 的 <c>FluentWindow</c> 把标题栏画在<b>客户区内部</b>，
    /// <c>GetClientRect</c> 返回的就是整个窗口，该差值恒为 0（实测日志：<c>窗口=1595x842 客户区=1595x842 非客户区高=0</c>）。
    /// 少扣这 40px 会让"画面区域"虚高，窗口被算得偏宽约 77px，表现为**恒定的左右黑边**。
    /// 因此直接量标题栏元素自身的渲染高度。
    /// </para>
    /// <para>
    /// <b>样式 1 返回 0</b>：无标题栏样式的画面占满整个窗口（角色名由独立叠加窗显示），
    /// 客户区里没有任何占高度的固定装饰，画面区域 = 整个客户区。
    /// </para>
    /// </summary>
    private int GetChromeHeight()
    {
        if (!HasTitleBar)
        {
            // 名称条只叠加显示、不占画面区域
            return 0;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        return Math.Max(0, (int)Math.Round(TitleBar.ActualHeight * dpi.DpiScaleY));
    }

    /// <summary>预览窗口不进 Alt+Tab，也不抢焦点（显示/点击都不激活）。</summary>
    private void ApplyWindowStyles()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        var exStyle = NativeMethods.GetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(
            _hwnd,
            NativeMethods.GWL_EXSTYLE,
            exStyle | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE);
    }

    /// <summary>
    /// 预览窗口不允许最大化/最小化：它不在任务栏，最小化后无法找回；
    /// 双击标题栏被系统最大化的路径在这里被拉回来。
    /// </summary>
    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (_stopped || WindowState == WindowState.Normal)
        {
            return;
        }

        WindowState = WindowState.Normal;
        NativeMethods.SetWindowBounds(_hwnd, Setting.WinX, Setting.WinY, Setting.WinW, Setting.WinH);
        ScheduleThumbnailUpdate();
    }

    public void Start()
    {
        var bounds = ResolveStartBounds();

        _isShowing = true;
        _lastCorrectedSize = default;
        NativeMethods.SetWindowBounds(_hwnd, bounds.Left, bounds.Top, bounds.Width, bounds.Height);

        Show();

        // FluentWindow 在显示时可能再次调整窗口样式，这里补一次
        ApplyWindowStyles();

        Rebind(Process, Setting);
        SetHighlight(Setting.Highlight);

        // 真实前台状态：正常由前台的 ForegroundChanged 维护，启动时先量一次，
        // 避免刚开的预览窗用着"上一次的"前台状态（等于角色名底色选错）
        _isSourceForeground = Process.MainWindowHandle != IntPtr.Zero
            && Process.MainWindowHandle == NativeMethods.GetForegroundWindow();

        // 角色名叠加窗：内容与大小就绪后摆放（ActualWidth/Height 需在布局之后才有效）；
        // 带标题栏的样式下它保持隐藏（名字已在标题栏里）
        CreateNameOverlay();
        ShowNameOverlayIfNeeded();

        // 首次布局完成后（尺寸稳定）按游戏客户区比例校正一次：
        // 存档里的旧尺寸可能与游戏比例不符，校正后画面才铺满、没有黑边
        ScheduleThumbnailUpdate();
        ScheduleAspectCorrection();

        _sourceMinimized = IsSourceMinimized();
        ApplyPreviewPlaceholder();
        _minimizeWatchTimer.Start();
    }

    public void Stop()
    {
        if (_stopped)
        {
            return;
        }

        _stopped = true;
        _isShowing = false;
        _aspectCorrectionTimer.Stop();
        _minimizeWatchTimer.Stop();
        _thumbnail.Unregister();
        DestroyNameOverlay();

        // 用户点 X 的路径：Stop 是在 OnClosing 里被回调进来的，此时窗口正在关闭，
        // 再调 Close() 会抛「在窗口关闭期间，无法……调用 Close」（VerifyNotClosing）。
        if (!_closing)
        {
            Close();
        }
    }

    public void Dispose()
    {
        Stop();
        _thumbnail.Dispose();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // 用户点关闭键：不直接关，交给管理器走统一的 Stop 流程（避免重复 Stop 与悬空引用）
        if (!_stopped)
        {
            e.Cancel = true;
            _closing = true;
            StopRequested?.Invoke(Setting);
            return;
        }

        base.OnClosing(e);
    }

    // ---------- IPreviewWindow ----------

    public void ShowWindow()
    {
        if (!_isShowing || !IsVisible)
        {
            Show();
            _isShowing = true;
        }

        if (WindowState != WindowState.Normal)
        {
            WindowState = WindowState.Normal;
        }

        // 隐藏期间源窗口可能已经最小化/还原，重新开始轮询并立刻对齐一次
        _sourceMinimized = IsSourceMinimized();
        ApplyPreviewPlaceholder();
        _minimizeWatchTimer.Start();
        ShowNameOverlayIfNeeded();

        Recover();
    }

    public void HideWindow()
    {
        if (_isShowing && IsVisible)
        {
            _thumbnail.Hide();
            _minimizeWatchTimer.Stop();
            _nameOverlay?.Hide();
            Hide();
        }
    }

    /// <summary>只移动叠加窗（拖动过程中跟随），坐标已是物理像素。</summary>
    private void MoveNameOverlay(int x, int y)
    {
        if (_nameOverlay is null || !NativeMethods.GetWindowRect(_hwnd, out var window))
        {
            return;
        }

        // 窗口尺寸没变，宽度直接沿用当前值（字号缩放只与宽度有关）
        UpdateNameOverlayBounds(new NativeMethods.RECT
        {
            Left = x,
            Top = y,
            Right = x + window.Width,
            Bottom = y + window.Height,
        });
    }

    public void SetHighlight(bool highlight)
    {
        if (_isHighlighted == highlight)
        {
            return;
        }

        _isHighlighted = highlight;
        ApplyVisuals();
    }

    /// <summary>
    /// 该客户端当前是否在前台。
    /// <para>
    /// 只影响**角色名底色的高亮**：用户的"高亮"开关只管边框，这里拿的是未过滤的真实前台状态，
    /// 所以关掉边框高亮后，文字底色照样会跟着前台切换（用户明确要求）。
    /// </para>
    /// </summary>
    public void SetForegroundState(bool isForeground)
    {
        if (_isSourceForeground == isForeground)
        {
            return;
        }

        _isSourceForeground = isForeground;
        UpdateNameOverlayBounds();
    }

    public void SetPosition(int x, int y) => NativeMethods.MoveWindow(_hwnd, x, y);

    /// <summary>
    /// 设置尺寸。带的宽高是<b>画面区域</b>的尺寸（窗口会给标题栏/名称条/高亮留白补足差额），
    /// 因此调用方不必自己去算装饰尺寸；不满足游戏客户区比例的请求由尺寸校正自动修正。
    /// </summary>
    public void SetSize(int width, int height)
    {
        var rect = GetRect();
        NativeMethods.SetWindowBounds(
            _hwnd,
            rect.X,
            rect.Y,
            Math.Max(MinWindowWidth, width),
            Math.Max(MinWindowHeight, height));
        PersistBounds();
    }

    public Int32Rect GetRect()
    {
        NativeMethods.GetWindowRect(_hwnd, out var rect);
        return new Int32Rect(rect.Left, rect.Top, rect.Width, rect.Height);
    }

    public void ActivateSource() => GameClientService.Activate(Process.MainWindowHandle, _globalSetting.SetForegroundWindowMode);

    public void ApplySettings()
    {
        ApplyVisuals();
        ScheduleThumbnailUpdate();

        // 字体/字号/底色/跟随缩放都是立即生效的；字号可能与窗口宽度有关，这里同步套用一次
        UpdateNameOverlayBounds();

        // 高亮边距属于"画面之外的固定留白"，改了它画面比例会跟着变，需要重新校正一次
        ScheduleAspectCorrection();
    }

    public void Rebind(ProcessInfo process, PreviewItem setting)
    {
        Process = process;
        Setting = setting;

        // 源窗口换了，比例可能不同，允许重新下发一次尺寸
        _lastCorrectedSize = default;

        ApplyVisuals();
        _thumbnail.Rebind(_hwnd, process.MainWindowHandle);
        ScheduleThumbnailUpdate();
        ScheduleAspectCorrection();
    }

    public void Recover()
    {
        if (!_isShowing)
        {
            return;
        }

        if (!_thumbnail.IsRegistered || _thumbnail.SourceWindow != Process.MainWindowHandle)
        {
            _thumbnail.Rebind(_hwnd, Process.MainWindowHandle);
        }

        ScheduleThumbnailUpdate();
    }

    // ---------- 外观 ----------

    /// <summary>
    /// 高亮边框的实际留白（DIP，左/上/右/下）。
    /// <para>
    /// <b>带标题栏的样式 0 不画上边</b>：高亮区紧挨着标题栏，再留一条上边会显得像多余的空隙，
    /// 所以这种样式下"上边距"设置不生效（只画左/右/下三边），画面从标题栏正下方开始。
    /// 无标题栏的样式 1 四条边照旧。
    /// </para>
    /// </summary>
    private Thickness GetHighlightThickness()
    {
        if (!_isHighlighted)
        {
            return new Thickness(0);
        }

        return new Thickness(
            Math.Max(0d, Setting.HighlightMarginLeft),
            HasTitleBar ? 0d : Math.Max(0d, Setting.HighlightMarginTop),
            Math.Max(0d, Setting.HighlightMarginRight),
            Math.Max(0d, Setting.HighlightMarginBottom));
    }

    /// <summary>
    /// 高亮留白在水平/垂直方向占用的总量（物理像素）。与 <see cref="GetHighlightThickness"/> 同源，
    /// 供比例校正使用——校正的"画面区域"必须与界面上真正留给画面的区域一致。
    /// </summary>
    private (double Horizontal, double Vertical) GetHighlightPadding()
    {
        var thickness = GetHighlightThickness();
        var dpi = VisualTreeHelper.GetDpi(this);
        return (
            (thickness.Left + thickness.Right) * dpi.DpiScaleX,
            (thickness.Top + thickness.Bottom) * dpi.DpiScaleY);
    }

    /// <summary>
    /// 该预览画面应保持的宽高比；源窗口最小化或取不到时返回 0。
    /// 最小化时客户区是任务栏缩略图的形状，绝不能拿来当比例依据。
    /// </summary>
    public double SourceAspect
        => IsSourceMinimized() ? 0
            : PreviewGeometry.TryGetSourceSize(Process.MainWindowHandle, out var width, out var height) && width > 0 && height > 0
                ? (double)width / height
                : 0;

    /// <summary>源画面（游戏客户区）尺寸；失败时返回 false。</summary>
    private bool TryGetSourceSize(out int width, out int height)
        => PreviewGeometry.TryGetSourceSize(Process.MainWindowHandle, out width, out height);

    private void ApplyVisuals()
    {
        var name = string.IsNullOrWhiteSpace(Setting.Name) ? Process.WindowTitle : Setting.Name;

        // 显示样式：0 标准标题栏；1 无标题栏（画面占满整个窗口）。历史配置里的 2（已废弃的 IPC 无标题栏）并入 1。
        var showTitleBar = HasTitleBar;
        TitleBar.Visibility = showTitleBar ? Visibility.Visible : Visibility.Collapsed;
        TitleBar.Title = name;
        Title = name;

        // 左上角角色名叠加窗**只在无标题栏样式下显示**：带标题栏时名字已经写在标题栏里了，
        // 再叠一个角标是重复信息（用户要求）。
        // 叠加窗本身仍保留，切回无标题栏样式要能立刻用上。
        if (_nameOverlay is not null)
        {
            _nameOverlay.DisplayName = name;
            ShowNameOverlayIfNeeded();
        }

        // 高亮由缩略图外框（HighlightColor + 边距）体现；"上边距"在带标题栏的样式下不生效
        // （高亮区紧挨标题栏，画面从标题栏正下方开始），详见 GetHighlightThickness。
        ThumbnailFrame.Background = _isHighlighted
            ? PreviewColorHelper.ToBrush(Setting.HighlightColor)
            : Brushes.Transparent;
        ThumbnailFrame.Padding = GetHighlightThickness();

        ApplyPreviewPlaceholder();
    }

    // ---------- 角色名叠加窗 ----------

    /// <summary>
    /// 角色名叠加窗只在"无标题栏样式 + 窗口正在显示"时出现。
    /// 带标题栏时名字已写在标题栏里，再叠一个角标是重复信息（用户要求）。
    /// </summary>
    private void ShowNameOverlayIfNeeded()
    {
        if (_nameOverlay is null)
        {
            return;
        }

        if (HasTitleBar || !_isShowing)
        {
            _nameOverlay.Hide();
            return;
        }

        _nameOverlay.Show();
        UpdateNameOverlayBounds();
    }

    /// <summary>
    /// 建立角色名叠加窗：它必须**独立成窗**并压在预览窗口之上——
    /// DWM 缩略图由系统在本窗口内容之上合成，画在本窗口里的文字一律会被盖住（实测过）。
    /// </summary>
    private void CreateNameOverlay()
    {
        if (_nameOverlay is not null)
        {
            return;
        }

        _nameOverlay = new PreviewNameOverlayWindow
        {
            Owner = this,
            DisplayName = string.IsNullOrWhiteSpace(Setting.Name) ? Process.WindowTitle : Setting.Name,
        };
        _nameOverlay.Show();
    }

    private void DestroyNameOverlay()
    {
        if (_nameOverlay is null)
        {
            return;
        }

        var overlay = _nameOverlay;
        _nameOverlay = null;

        // 窗口正在关闭时不主动 Close()：同一次关闭流程里再关别的窗口会触发
        // VerifyNotClosing（"在窗口关闭期间，无法……调用 Close"）。叠加窗是它的从属窗口，
        // 会随所有者一起销毁。
        if (!_closing)
        {
            overlay.Close();
        }
    }

    /// <summary>
    /// 角色名叠加窗的**实际字号**（DIP）。
    /// <para>
    /// 不跟随时就是设置里的字号（固定视觉大小）。
    /// 跟随时按"窗口宽度 / 基准宽"等比放大：基准宽取 <see cref="NameOverlayReferenceWidth"/>，
    /// 所以预览窗越大字越大；再乘上设置里的倍率，让用户能整体压一档/提一档。
    /// </para>
    /// </summary>
    private double GetNameOverlayFontSize(int windowWidth)
    {
        var baseSize = Math.Max(1d, Setting.NameOverlayFontSize);
        if (!Setting.NameOverlayFollowScale || windowWidth <= 0)
        {
            return baseSize;
        }

        var factor = Math.Max(0.1d, Setting.NameOverlayScaleFactor);
        return Math.Clamp(
            baseSize * (windowWidth / (double)NameOverlayReferenceWidth) * factor,
            1d,
            baseSize * MaxNameOverlayScale);
    }

    /// <summary>把角色名叠加窗摆到画面左上角（物理像素，随高亮留白内缩），并同步外观。</summary>
    private void UpdateNameOverlayBounds()
    {
        if (_hwnd == IntPtr.Zero || !NativeMethods.GetWindowRect(_hwnd, out var window))
        {
            return;
        }

        UpdateNameOverlayBounds(window);
    }

    private void UpdateNameOverlayBounds(NativeMethods.RECT window)
    {
        if (_nameOverlay is null)
        {
            return;
        }

        // 先套用字号（"跟随缩放"时与窗口宽度有关，字号又决定角标尺寸），再按量好的尺寸摆放。
        // 底色两段式：平时用"背景色"，该客户端在前台时换成"高亮背景色"。
        // 注意用 _isSourceForeground（真实前台）而不是 _isHighlighted（受"高亮"开关过滤）——
        // 关掉边框高亮不该把文字底色的高亮一起关掉。
        _nameOverlay.ApplyAppearance(
            Setting.NameOverlayFontFamily,
            GetNameOverlayFontSize(window.Width),
            Setting.NameOverlayForegroundColor,
            _isSourceForeground ? Setting.NameOverlayBackgroundColorHighlight : Setting.NameOverlayBackgroundColor);

        var dpi = VisualTreeHelper.GetDpi(this);
        var thickness = GetHighlightThickness();
        _nameOverlay.SetBounds(
            window.Left + (int)Math.Round(thickness.Left * dpi.DpiScaleX),
            window.Top + (int)Math.Round(thickness.Top * dpi.DpiScaleY),
            (int)Math.Ceiling(_nameOverlay.ActualWidth * dpi.DpiScaleX),
            (int)Math.Ceiling(_nameOverlay.ActualHeight * dpi.DpiScaleY));
    }

    /// <summary>
    /// 没有实时画面时（源窗口已最小化/已退出）的呈现：**不显示黑色底**，改为
    /// 高亮色（有高亮时）或主题底色，并叠上与"设置界面右侧的选中预览"相同的提示文字。
    /// <para>
    /// 有画面时底色保持黑，因为缩略图按源比例居中摆放时留边露出的就该是黑边（letterbox）。
    /// </para>
    /// </summary>
    private void ApplyPreviewPlaceholder()
    {
        var hasPicture = !_sourceMinimized && TryGetSourceSize(out _, out _);

        if (hasPicture)
        {
            PreviewHint.Visibility = Visibility.Collapsed;
            ThumbnailArea.Background = Brushes.Black;
            return;
        }

        var background = _sourceMinimized
            ? PreviewColorHelper.ToBrush(Setting.HighlightColor)
            : null;
        if (background is not null)
        {
            ThumbnailArea.Background = background;
            // 底色由用户设置，提示文字按底色取黑/白，保证可读
            PreviewHint.Foreground = PreviewColorHelper.ContrastForeground(Setting.HighlightColor);
        }
        else
        {
            ThumbnailArea.SetResourceReference(BackgroundProperty, "CardBackgroundFillColorSecondaryBrush");
            PreviewHint.SetResourceReference(ForegroundProperty, "TextFillColorSecondaryBrush");
        }

        PreviewHint.SetResourceReference(TextBlock.TextProperty, "GamePreviewPage_PreviewUnavailable");
        PreviewHint.Visibility = Visibility.Visible;
    }

    // ---------- 缩略图 ----------

    private void ScheduleThumbnailUpdate()
    {
        if (_thumbnailUpdateQueued)
        {
            return;
        }

        _thumbnailUpdateQueued = true;

        // 在排队前先刷新一次"是否最小化"：否则最小化后的第一次更新会先画出那张残留快照，
        // 等下一次轮询（最多 300ms）才被抹掉——表现为闪一下压缩画面。
        _sourceMinimized = IsSourceMinimized();

        Dispatcher.BeginInvoke(
            () =>
            {
                _thumbnailUpdateQueued = false;
                UpdateThumbnailDestination();
            },
            DispatcherPriority.Loaded);
    }

    private void UpdateThumbnailDestination()
    {
        if (!_isShowing || _hwnd == IntPtr.Zero || !_thumbnail.IsRegistered)
        {
            return;
        }

        // 源窗口最小化时没有实时画面，DWM 会退化成一张**被压缩的残留快照**（用户反馈的现象）。
        // 状态由 Start / ShowWindow / 轮询定时器维护，这里只读，避免每次布局都去 IsIconic。
        // 注意不能在这里 return：恢复时要能重新画上，重新绘制由 MinimizeWatchTick 触发。
        if (_sourceMinimized)
        {
            _thumbnail.Hide();
            return;
        }

        // 元素在窗口客户区内的位置（DIP）→ 物理像素
        var origin = ThumbnailArea.TransformToAncestor(this).Transform(new Point(0, 0));
        var dpi = VisualTreeHelper.GetDpi(this);
        var left = (int)Math.Round(origin.X * dpi.DpiScaleX);
        var top = (int)Math.Round(origin.Y * dpi.DpiScaleY);
        var right = (int)Math.Round((origin.X + ThumbnailArea.ActualWidth) * dpi.DpiScaleX);
        var bottom = (int)Math.Round((origin.Y + ThumbnailArea.ActualHeight) * dpi.DpiScaleY);

        if (right <= left || bottom <= top)
        {
            return;
        }

        // 按源画面纵横比摆放目标矩形
        var container = new NativeMethods.RECT { Left = left, Top = top, Right = right, Bottom = bottom };
        int sourceWidth;
        int sourceHeight;
        var hasSource = TryGetSourceSize(out sourceWidth, out sourceHeight);
        var destination = hasSource
            ? PreviewGeometry.FitAspect(container, sourceWidth, sourceHeight)
            : container;

        var opacity = (byte)Math.Clamp((int)Math.Round(Setting.OverlapOpacity * 255.0 / 100.0), 0, 255);
        _thumbnail.TryUpdate(destination, opacity, true);

        // 角色名叠加窗跟随预览窗口的位置/高亮留白
        UpdateNameOverlayBounds();
    }

    /// <summary>
    /// 源窗口（游戏客户端）当前是否最小化。
    /// 只认"窗口仍存在且已最小化"：窗口句柄失效（客户端已退出）不算最小化，
    /// 那种情况由 <see cref="Recover"/> / 进程列表刷新去换绑新句柄。
    /// </summary>
    private bool IsSourceMinimized()
    {
        var source = Process.MainWindowHandle;
        return source != IntPtr.Zero && NativeMethods.IsWindow(source) && NativeMethods.IsIconic(source);
    }

    /// <summary>
    /// 轮询源窗口的最小化/还原并据此刷新缩略图。
    /// <para>
    /// 为什么是轮询而不是只靠事件：最小化**不会**改变本窗口尺寸，所以 <c>OnRenderSizeChanged</c> 不会触发；
    /// 而监听源窗口的 <c>WM_SIZE</c>/<c>WM_WINDOWPOSCHANGED</c> 需要跨进程子类化（复杂，且项目已移除 Vanara）。
    /// 本窗口是置顶浮窗、数量有限，300ms 一次 <c>IsIconic</c> 的开销可以忽略，且只在状态真的翻转时才动缩略图。
    /// </para>
    /// </summary>
    private void MinimizeWatchTick(object? sender, EventArgs e)
    {
        if (_stopped || !_isShowing)
        {
            return;
        }

        // 只在状态真的翻转时才动缩略图与占位呈现；_sourceMinimized 由 ScheduleThumbnailUpdate 统一刷新
        if (IsSourceMinimized() == _sourceMinimized)
        {
            return;
        }

        ScheduleThumbnailUpdate();
        ApplyPreviewPlaceholder();
    }

    // ---------- 比例校正 ----------

    /// <summary>
    /// 画面比例已锁定时只需一次校正；但校正本身会改变窗口尺寸、进而再次触发布局回调，
    /// 所以不能在布局回调里直接改尺寸（会自我循环）。这里用"尺寸稳定后再校正一次"的防抖：
    /// 每次尺寸变化只把定时器往后推，等它真正停下来才动手。
    /// </summary>
    private void ScheduleAspectCorrection()
    {
        if (_stopped)
        {
            return;
        }

        _aspectCorrectionTimer.Stop();
        _aspectCorrectionTimer.Start();
    }

    private void OnAspectCorrectionTick(object? sender, EventArgs e)
    {
        _aspectCorrectionTimer.Stop();
        CorrectWindowSizeToAspect();
    }

    /// <summary>
    /// 把窗口尺寸校正成"画面区域与游戏客户区同比例"，并同步持久化。
    /// 位置不变，所以拖动过程中调用也安全。
    /// </summary>
    private void CorrectWindowSizeToAspect()
    {
        if (!_isShowing || _hwnd == IntPtr.Zero || _stopped || _applyingAspectCorrection)
        {
            return;
        }

        if (!NativeMethods.GetWindowRect(_hwnd, out var window))
        {
            return;
        }

        if (CorrectSizeToAspect(window.Width, window.Height) is not { } size)
        {
            return;
        }

        if (Math.Abs(size.Width - window.Width) < AspectTolerance && Math.Abs(size.Height - window.Height) < AspectTolerance)
        {
            return;
        }

        // 同一个尺寸不再重复下发，彻底断掉"下发 → 布局回调 → 再下发"的可能
        if (size == _lastCorrectedSize)
        {
            return;
        }

        _applyingAspectCorrection = true;
        try
        {
            _lastCorrectedSize = size;
            NativeMethods.SetWindowBounds(_hwnd, window.Left, window.Top, size.Width, size.Height);
            PersistBounds();
        }
        finally
        {
            _applyingAspectCorrection = false;
        }
    }

    /// <summary>
    /// 由当前的窗口矩形算出"与游戏客户区同比例"的窗口矩形（物理像素）；取不到源画面时返回 null。
    /// <para>
    /// 两条关键口径（都踩过坑，见 REFACTORING 阶段 44）：
    /// ① 客户区尺寸取 <b>Win32 的 <c>GetClientRect</c></b>，不要用 WPF 的 <c>ActualWidth/ActualHeight</c>——
    /// 布局值带 DIP↔像素取整误差，反算尺寸会让误差每轮累积（实测窗口每轮缩窄几十像素，永不收敛）；
    /// ② 标题栏高度取 <b>标题栏元素自身的渲染高度</b>，不能用"窗口高 − 客户区高"——WPF-UI 把标题栏画在
    /// 客户区内部，该差值恒为 0，会漏扣约 40px、把窗口算宽约 77px（表现为恒定左右黑边）。
    /// </para>
    /// <para>
    /// 固定装饰 = 标题栏/名称条高度 + 高亮留白。两种样式可调的维度不同：
    /// 样式 0 的标题栏高度固定，调的是客户区高度与随之而来的宽度；样式 1 的名称条高度固定，宽度不变、只调高度。
    /// </para>
    /// </summary>
    private (int Width, int Height)? CorrectSizeToAspect(int requestedWidth, int requestedHeight)
    {
        // 源窗口最小化时**不要校正**：那时它的客户区是"任务栏缩略图"那种又宽又扁的小矩形
        // （实测最小化后 GetClientRect 给出的是很宽很矮的形状），拿它当比例依据会把预览窗
        // 压成很扁的一条——表现就是"最小化状态下启动预览，保存的尺寸没被沿用、高度明显变小"。
        // 保持调用方给的尺寸即可，等游戏还原后下一次校正自然会按真实比例修正。
        if (IsSourceMinimized())
        {
            return null;
        }

        if (!NativeMethods.GetWindowRect(_hwnd, out var window)
            || !NativeMethods.GetClientRect(_hwnd, out var client)
            || client.Width <= 0
            || client.Height <= 0
            || !TryGetSourceSize(out var sourceWidth, out var sourceHeight))
        {
            return null;
        }

        var padding = GetHighlightPadding();

        // 画面区域（物理像素）：客户区再去掉"标题栏/名称条"与"画面之外的固定留白"
        // （标题栏由 GetChromeHeight 直接量元素，不能用窗口高 − 客户区高，那里恒为 0）
        var chromeHeight = GetChromeHeight();
        var paddingHeight = (int)Math.Round(padding.Vertical);
        var paddingWidth = (int)Math.Round(padding.Horizontal);
        var sourceAreaWidth = Math.Max(1, client.Width - paddingWidth);
        var sourceAreaHeight = Math.Max(1, client.Height - chromeHeight - paddingHeight);

        int width;
        int height;
        if (HasTitleBar)
        {
            // 标题栏高度固定 → 保持画面区域的高度，按比例反算它需要的宽度
            height = window.Height;
            width = (int)Math.Round(sourceAreaHeight * (double)sourceWidth / sourceHeight) + paddingWidth;
        }
        else
        {
            // 名称条高度固定、窗口高度随内容 → 宽度不动，按比例反算高度
            width = requestedWidth;
            height = (int)Math.Round(sourceAreaWidth * (double)sourceHeight / sourceWidth)
                + paddingHeight
                + chromeHeight;
        }

        return (Math.Max(MinWindowWidth, width), Math.Max(MinWindowHeight, height));
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo info)
    {
        base.OnRenderSizeChanged(info);
        ScheduleThumbnailUpdate();

        // 尺寸变了就安排一次比例校正（防抖，稳定后才执行）
        ScheduleAspectCorrection();
    }

    // ---------- 交互 ----------

    private void AttachDragHandlers(UIElement element)
    {
        element.MouseLeftButtonDown += OnDragStart;
        element.MouseMove += OnDragMove;
        element.MouseLeftButtonUp += OnDragEnd;
    }

    private void OnDragStart(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount >= 2)
        {
            ActivateSource();
            return;
        }

        NativeMethods.GetCursorPos(out _dragStartCursor);
        NativeMethods.GetWindowRect(_hwnd, out _dragStartRect);
        _dragCandidate = true;
        _dragging = false;
        ((UIElement)sender).CaptureMouse();
        e.Handled = true;
    }

    private void OnDragMove(object sender, MouseEventArgs e)
    {
        if (!_dragCandidate || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        NativeMethods.GetCursorPos(out var cursor);
        var dx = cursor.X - _dragStartCursor.X;
        var dy = cursor.Y - _dragStartCursor.Y;

        // 位移超过阈值才算拖动，否则视为"点击激活"
        if (!_dragging && (Math.Abs(dx) + Math.Abs(dy)) < DragThreshold)
        {
            return;
        }

        _dragging = true;
        NativeMethods.MoveWindow(_hwnd, _dragStartRect.Left + dx, _dragStartRect.Top + dy);
        MoveNameOverlay(_dragStartRect.Left + dx, _dragStartRect.Top + dy);
        PersistBounds();
    }

    private void OnDragEnd(object sender, MouseButtonEventArgs e)
    {
        var element = (UIElement)sender;
        if (element.IsMouseCaptured)
        {
            element.ReleaseMouseCapture();
        }

        var wasDragging = _dragging;
        _dragCandidate = false;
        _dragging = false;

        if (!wasDragging)
        {
            ActivateSource();
        }
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var rect = GetRect();
        var factor = e.Delta > 0 ? 1.05 : 1.0 / 1.05;

        var maxWidth = rect.Width;
        var maxHeight = rect.Height;
        if (NativeMethods.TryGetMonitorWorkArea(_hwnd, out var workArea))
        {
            maxWidth = workArea.Width;
            maxHeight = workArea.Height;
        }

        // 缩放同样保持源画面比例：否则窗口比例一旦跑偏，画面就会一直留边
        PreviewGeometry.TryGetSourceSize(Process.MainWindowHandle, out var sourceWidth, out var sourceHeight);
        var (width, height) = PreviewGeometry.ScalePreservingAspect(
            rect.Width,
            rect.Height,
            sourceWidth,
            sourceHeight,
            factor,
            MinWindowWidth,
            MinWindowHeight,
            Math.Max(MinWindowWidth, maxWidth),
            Math.Max(MinWindowHeight, maxHeight));

        NativeMethods.SetWindowBounds(_hwnd, rect.X, rect.Y, width, height);
        PersistBounds();
        e.Handled = true;
    }

    private void PersistBounds()
    {
        NativeMethods.GetWindowRect(_hwnd, out var rect);
        Setting.WinW = rect.Width;
        Setting.WinH = rect.Height;

        // 完全移出所有显示器时不保存位置，避免下次预览开在看不见的地方
        if (NativeMethods.MonitorFromWindow(_hwnd, 0) != IntPtr.Zero)
        {
            Setting.WinX = rect.Left;
            Setting.WinY = rect.Top;
        }

        ScheduleThumbnailUpdate();
        SettingChanged?.Invoke(Setting);
    }

    private NativeMethods.RECT ResolveStartBounds()
    {
        var width = Setting.WinW > 0 ? Setting.WinW : 480;
        var height = Setting.WinH > 0 ? Setting.WinH : 270;

        if (Setting.WinW <= 0 || Setting.WinH <= 0)
        {
            // 无历史尺寸时按源画面比例给个默认大小（比例未知——例如源窗口最小化——就用 16:9 兜底）
            var aspect = SourceAspect;
            if (aspect <= 0)
            {
                aspect = 16.0 / 9.0;
            }

            width = 480;
            height = Math.Max(MinWindowHeight, (int)Math.Round(480.0 / aspect));
        }

        var x = Setting.WinX;
        var y = Setting.WinY;
        NativeMethods.TryGetMonitorWorkArea(Process.MainWindowHandle, out var workArea);

        var maxX = Math.Max(workArea.Left, workArea.Right - width);
        var maxY = Math.Max(workArea.Top, workArea.Bottom - height);
        x = Math.Clamp(x, workArea.Left, maxX);
        y = Math.Clamp(y, workArea.Top, maxY);

        return new NativeMethods.RECT { Left = x, Top = y, Right = x + width, Bottom = y + height };
    }
}
