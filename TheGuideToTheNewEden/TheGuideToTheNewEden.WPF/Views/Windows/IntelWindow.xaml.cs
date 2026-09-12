using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using TheGuideToTheNewEden.Core.Enums;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.Core.Models.ChannelIntel;
using TheGuideToTheNewEden.Core.Models.Map;
using TheGuideToTheNewEden.WPF.Helpers;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.ChannelIntel;
using TheGuideToTheNewEden.WPF.Services.Settings;

namespace TheGuideToTheNewEden.WPF.Views.Windows;

/// <summary>
/// 频道预警的置顶小窗（对齐 WinUI 版 <c>Wins/IntelWindow</c>）：
/// 星图画布 + 底部三按钮（清除所有预警 / 最新预警信息（点击前置游戏）/ 停止声音）。
/// <para>
/// 透明实现与 WinUI 的有意差异：WinUI 用整窗 alpha（`SetLayeredWindowAttributes`），
/// 那会让<b>文字与星图一起变淡</b>；这里改为<b>逐像素透明</b>（`AllowsTransparency`），
/// "不透明度"设置只作用于<b>背板</b>（<c>BackdropPlate</c>），文字与星图始终保持实色。
/// 因为 <c>ui:FluentWindow</c> 会重置 WindowStyle 使 AllowsTransparency 失效，
/// 本窗口是普通 <see cref="Window"/> + <c>WindowChrome</c>（标题区拖动/缩放，标题区按钮
/// 用 <c>IsHitTestVisibleInChrome</c> 保证可点）。
/// </para>
/// 位置/尺寸/透明度取自 <see cref="ChannelIntelSetting"/>，位置尺寸变化即写回；
/// 自动解除与自动降低级别由 10 秒定时器驱动；
/// "必要时显示"（OverlapType=1）模式在全部预警解除后延迟 30 秒自动隐藏。
/// </summary>
public partial class IntelWindow : Window
{
    private readonly ChannelIntelSetting _setting;
    private readonly Dictionary<int, DateTime> _startTimes = [];
    private DispatcherTimer? _autoIntelTimer;
    private DispatcherTimer? _delayHideTimer;
    private bool _closing;
    private bool _disposed;

    /// <summary>用户点击标题栏关闭（会话据此停止预警）。</summary>
    public event EventHandler? StopRequested;

    public IntelWindow(ChannelIntelSetting setting, IntelSolarSystemMap intelMap)
    {
        _setting = setting;
        InitializeComponent();
        ApplyBackdrop();
        ThemeService.ThemeChanged += ApplyBackdrop;

        Width = setting.WinW > 0 ? setting.WinW : 500;
        Height = setting.WinH > 0 ? setting.WinH : 500;
        Left = setting.WinX;
        Top = setting.WinY;
        EnsureOnScreen();
        TitleText.Text = $"{setting.Listener} - {setting.IntelJumps}{FindString("EarlyWarningPage_Jumps")}";
        MapView.Init(intelMap, setting);
        InitTimer();
        LocationChanged += OnWindowBoundsChanged;
        SizeChanged += OnWindowBoundsChanged;
        Closed += (_, _) => Dispose();
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;

    /// <summary>按"不透明度"设置生成背板画刷：只改 alpha，颜色仍取主题背景色（主题切换后重算）。</summary>
    private void ApplyBackdrop()
    {
        var color = (Application.Current?.TryFindResource("ApplicationBackgroundBrush") as SolidColorBrush)?.Color
            ?? (Application.Current?.TryFindResource("CardBackgroundFillColorDefaultBrush") as SolidColorBrush)?.Color
            ?? Colors.White;
        var alpha = (byte)(Math.Clamp(_setting.OverlapOpacity, 1, 100) * 255 / 100);
        BackdropPlate.Background = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
    }

    // ---------- 显示与位置 ----------

    /// <summary>显示小窗（不抢焦点；已显示时保持置顶即可）。</summary>
    public void ShowWindow()
    {
        try
        {
            if (!IsVisible)
            {
                Show();
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    private void OnWindowBoundsChanged(object sender, EventArgs e)
    {
        if (_disposed || WindowState != WindowState.Normal)
        {
            return;
        }

        _setting.WinX = (int)Left;
        _setting.WinY = (int)Top;
        _setting.WinW = (int)Width;
        _setting.WinH = (int)Height;
        IntelSettingService.SetValue(_setting);
    }

    private void EnsureOnScreen()
    {
        var virtualLeft = SystemParameters.VirtualScreenLeft;
        var virtualTop = SystemParameters.VirtualScreenTop;
        var virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
        var virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;
        if (Left < virtualLeft - Width || Left > virtualRight || Top < virtualTop - Height || Top > virtualBottom)
        {
            Left = double.NaN;
            Top = double.NaN;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
    }

    public void RestoreWindowPos()
    {
        Left = double.NaN;
        Top = double.NaN;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
    }

    // ---------- 预警内容 ----------

    /// <summary>推送一条预警（更新星图与底部"最新预警信息"）。</summary>
    public void Intel(EarlyWarningContent content)
    {
        MapView.Intel(content);
        SetIntelInfo(content);
        if (!_disposed)
        {
            if (content.IntelType == IntelChatType.Intel)
            {
                _startTimes.Remove(content.SolarSystemId);
                _startTimes.Add(content.SolarSystemId, DateTime.Now);
                ShowWindow();
            }
            else if (content.IntelType == IntelChatType.Clear)
            {
                _startTimes.Remove(content.SolarSystemId);
                TryHideWindow();
            }
        }
    }

    /// <summary>底部"最新预警信息"：星系(跳数): 内容，舰船名加粗。</summary>
    private void SetIntelInfo(EarlyWarningContent content)
    {
        var primary = Application.Current?.TryFindResource("TextFillColorPrimaryBrush") as Brush ?? Brushes.Black;
        IntelInfoParagraph.Inlines.Clear();
        var jumps = FindString("EarlyWarningPage_Jumps");
        IntelInfoParagraph.Inlines.Add(new Run($"{content.SolarSystemName}({content.Jumps} {jumps}):") { Foreground = primary });
        var text = content.Content ?? string.Empty;
        if (content.IntelShips is { Count: > 0 })
        {
            var startIndex = 0;
            foreach (var ship in content.IntelShips)
            {
                if (ship.StartIndex > startIndex)
                {
                    IntelInfoParagraph.Inlines.Add(new Run(text[startIndex..ship.StartIndex]) { Foreground = primary });
                }

                if (ship.StartIndex + ship.Length <= text.Length)
                {
                    IntelInfoParagraph.Inlines.Add(new Run(text.Substring(ship.StartIndex, ship.Length))
                    {
                        Foreground = primary,
                        FontWeight = FontWeights.Black,
                    });
                }

                startIndex = ship.StartIndex + ship.Length;
            }

            if (startIndex < text.Length)
            {
                IntelInfoParagraph.Inlines.Add(new Run(text[startIndex..]) { Foreground = primary });
            }
        }
        else
        {
            IntelInfoParagraph.Inlines.Add(new Run(text) { Foreground = primary });
        }
    }

    public void UpdateHome(IntelSolarSystemMap intelMap)
    {
        MapView.UpdateHome(intelMap);
    }

    // ---------- 自动解除 / 自动降级 ----------

    private void InitTimer()
    {
        if (_autoIntelTimer is not null)
        {
            _autoIntelTimer.Stop();
        }

        if (_setting.AutoClear || _setting.AutoDowngrade)
        {
            _autoIntelTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10),
            };
            _autoIntelTimer.Tick += (_, _) =>
            {
                if (_setting.AutoClear)
                {
                    ClearElapsed();
                }

                if (_setting.AutoDowngrade)
                {
                    DowngradeElapsed();
                }
            };
            _autoIntelTimer.Start();
        }
    }

    private void ClearElapsed()
    {
        var now = DateTime.Now;
        var remove = _startTimes.Where(p => (now - p.Value).TotalMinutes >= _setting.AutoClearMinute).Select(p => p.Key).ToList();
        foreach (var id in remove)
        {
            _startTimes.Remove(id);
        }

        if (remove.Count > 0)
        {
            MapView.Clear(remove);
            TryHideWindow();
        }
    }

    private void DowngradeElapsed()
    {
        var now = DateTime.Now;
        var changed = _startTimes.Where(p => (now - p.Value).TotalMinutes >= _setting.AutoDowngradeMinute).Select(p => p.Key).ToList();
        if (changed.Count > 0)
        {
            MapView.Downgrade(changed);
        }
    }

    /// <summary>"必要时显示"模式：全部预警解除后延迟 30 秒隐藏窗口。</summary>
    private void TryHideWindow()
    {
        if (_setting.OverlapType == 1 && _startTimes.Count == 0)
        {
            _delayHideTimer?.Stop();
            _delayHideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30),
            };
            _delayHideTimer.Tick += (_, _) =>
            {
                _delayHideTimer?.Stop();
                if (_startTimes.Count == 0 && IsVisible)
                {
                    Hide();
                    // 隐藏即停止报警声音（对齐 WinUI 的 OnHide 行为）
                    IntelWarningService.Current.StopSound(_setting.Listener);
                }
            };
            _delayHideTimer.Start();
        }
    }

    // ---------- 标题栏与底部按钮 ----------

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void OnCloseClick(object sender, RoutedEventArgs e)
        => Close();

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        MapView.Clear();
        IntelWarningService.Current.StopSound(_setting.Listener);
    }

    private void OnStopSoundClick(object sender, RoutedEventArgs e)
        => IntelWarningService.Current.StopSound(_setting.Listener);

    private void OnIntelInfoClick(object sender, RoutedEventArgs e)
        => GameWindowHelper.BringGameToFront(_setting.Listener);

    // ---------- 关闭与释放 ----------

    protected override void OnClosing(CancelEventArgs e)
    {
        // 标记"正在关闭"：此后 Dispose() 不能再调 Close()（窗口关闭期间调用会抛
        // InvalidOperationException：VerifyNotClosing）。用户点 X 的路径是
        // OnClosing → StopRequested → 会话 Stop → WarningService.Remove → Dispose，
        // 若 Dispose 再 Close() 就会命中该异常。
        _closing = true;
        if (!_disposed)
        {
            // 用户手动关闭小窗 = 停止该角色的预警（对齐 WinUI 的 OnStop）
            StopRequested?.Invoke(this, EventArgs.Empty);
        }

        base.OnClosing(e);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ThemeService.ThemeChanged -= ApplyBackdrop;
        _autoIntelTimer?.Stop();
        _delayHideTimer?.Stop();
        LocationChanged -= OnWindowBoundsChanged;
        SizeChanged -= OnWindowBoundsChanged;

        // 由 OnClosing 链路进来的调用（_closing）不重复 Close：窗口本来就在关闭中
        if (!_closing)
        {
            Close();
        }
    }
}
