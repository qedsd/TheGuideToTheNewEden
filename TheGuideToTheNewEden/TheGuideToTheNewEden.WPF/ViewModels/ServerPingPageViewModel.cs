using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TheGuideToTheNewEden.Core.Helpers;
using TheGuideToTheNewEden.WPF.Models.Tools;
using TheGuideToTheNewEden.WPF.Services;

namespace TheGuideToTheNewEden.WPF.ViewModels;

/// <summary>
/// 服务器测速 VM：对指定 <c>host:port</c> 每秒做一次 TCP 连接测速，统计当前/平均延迟、丢包率与总次数，
/// 并按阈值给出"极佳/良好/正常/较差/糟糕"档位。对应 WinUI 版 <c>Views/Tools/ServerPingPage</c>。
/// <para>
/// 相对 WinUI 版修掉的几处：① <b>成功次数为 0 时不再算出 NaN</b>（WinUI 直接 <c>_passTime / _passCount</c>）；
/// ② <b>时长不再多加 10 小时</b>（WinUI 写的是 <c>Elapsed.Hours + 10</c>）；③ 循环用
/// <c>await Task.Delay</c> 而不是 <c>Thread.Sleep</c>（不占线程池线程、取消立刻生效）；
/// ④ 测速本身走 <c>TCPingHelper.PingAsync(logErrors: false)</c>——连接失败是"丢包"这一正常结果，
/// 不该每秒往日志里写一条 ERROR；⑤ 运行中禁用服务器地址/端口输入。
/// </para>
/// </summary>
public sealed class ServerPingPageViewModel : INotifyPropertyChanged
{
    /// <summary>默认端口（EVE 客户端的 TCP 端口）。</summary>
    public const int DefaultPort = 26000;

    /// <summary>每次测速的连接超时。</summary>
    private const int PingTimeoutMs = 5000;

    /// <summary>两次测速之间的间隔（WinUI 也是 1 秒）。</summary>
    private const int PingIntervalMs = 1000;

    private const string EuHost = "tranquility.servers.eveonline.com";
    private const string SerenityHost = "42.186.79.5";

    // 档位阈值（与 WinUI 一致）
    private const double LatencyExcellent = 100;
    private const double LatencyGood = 150;
    private const double LatencyNormal = 250;
    private const double LatencyPoor = 300;
    private const double LossExcellent = 0;
    private const double LossGood = 1;
    private const double LossNormal = 2;
    private const double LossPoor = 10;

    private readonly Stopwatch _duration = new();

    private string _host = EuHost;
    private double _port = DefaultPort;
    private bool _isRunning;

    private double _currentLatency = -1;
    private double _averageLatency = -1;
    private double _lossPercent = -1;
    private int _totalCount;
    private int _successCount;
    private int _failedCount;
    private double _successTimeSum;

    private CancellationTokenSource? _cts;
    private bool _disposed;

    public ServerPingPageViewModel()
    {
        LanguageService.LanguageChanged += OnLanguageChanged;
        ThemeService.ThemeChanged += OnThemeChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>可选的服务器预设（ComboBox 的候选项；输入框本身可编辑）。</summary>
    public IReadOnlyList<string> HostPresets { get; } = new[] { EuHost, SerenityHost };

    public string Host
    {
        get => _host;
        set
        {
            if (Set(ref _host, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(HostTooltip));
            }
        }
    }

    public double Port
    {
        get => _port;
        set => Set(ref _port, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (Set(ref _isRunning, value))
            {
                OnPropertyChanged(nameof(IsIdle));
            }
        }
    }

    /// <summary>未在测速中（用于禁用输入与切换开始/停止按钮）。</summary>
    public bool IsIdle => !_isRunning;

    /// <summary>悬停提示：选中的是预设服务器时说明它是欧服/国服，否则回落到 "Host/IP"。</summary>
    public string HostTooltip
        => _host switch
        {
            EuHost => FindString("ServerPingPage_TranquilityHost"),
            SerenityHost => FindString("ServerPingPage_SerenityHost"),
            _ => FindString("ServerPingPage_Host"),
        };

    // ---------- 结果 ----------

    public string CurrentLatencyText => FormatLatency(_currentLatency);

    public string AverageLatencyText => FormatLatency(_averageLatency);

    public string LossText => _lossPercent < 0 ? "--" : _lossPercent.ToString("F2");

    public PingQuality CurrentQuality => _currentLatency < 0 ? PingQuality.Unknown : LatencyQuality(_currentLatency);

    public PingQuality AverageQuality => _averageLatency < 0 ? PingQuality.Unknown : LatencyQuality(_averageLatency);

    public PingQuality LossQuality => _lossPercent < 0 ? PingQuality.Unknown : LossQualityOf(_lossPercent);

    public string CurrentVerdict => VerdictText(CurrentQuality);

    public string AverageVerdict => VerdictText(AverageQuality);

    public string LossVerdict => VerdictText(LossQuality);

    public string TotalCountText => _totalCount.ToString();

    public string SuccessCountText => _successCount.ToString();

    public string FailedCountText => _failedCount.ToString();

    public string DurationText => _duration.Elapsed.TotalSeconds < 1 ? "--" : FormatDuration(_duration.Elapsed);

    // ---------- 控制 ----------

    /// <summary>开始测速（已在运行时直接返回）。</summary>
    public async Task StartAsync()
    {
        if (_isRunning || _disposed)
        {
            return;
        }

        var host = _host?.Trim() ?? string.Empty;
        if (host.Length == 0)
        {
            PageNotifyService.Error(FindString("ServerPingPage_NoHost"));
            return;
        }

        var port = (int)Math.Round(_port);
        if (port is <= 0 or > 65535)
        {
            PageNotifyService.Error(FindString("ServerPingPage_InvalidPort"));
            return;
        }

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        ResetCounters();
        _duration.Restart();
        IsRunning = true;

        try
        {
            while (!token.IsCancellationRequested)
            {
                // 失败返回 -1，不抛异常、也不写 ERROR 日志（连接不上就是丢包）。
                var latency = await TCPingHelper.PingAsync(host, port, PingTimeoutMs, logErrors: false, token);

                if (token.IsCancellationRequested)
                {
                    break;
                }

                _totalCount++;
                if (latency < 0)
                {
                    _failedCount++;
                    _currentLatency = -1;
                }
                else
                {
                    _successCount++;
                    _successTimeSum += latency;
                    _currentLatency = latency;
                }

                // 成功次数为 0 时平均延迟与丢包率都按"无数据"处理，避免 WinUI 那个 0 除。
                _averageLatency = _successCount > 0 ? _successTimeSum / _successCount : -1;
                _lossPercent = _totalCount > 0 ? _failedCount * 100.0 / _totalCount : 0;

                PublishResults();

                try
                {
                    await Task.Delay(PingIntervalMs, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
        finally
        {
            _duration.Stop();
            IsRunning = false;
            PublishResults();
            OnPropertyChanged(nameof(DurationText));
        }
    }

    /// <summary>停止测速（保留当前结果，和 WinUI 一致）。</summary>
    public void Stop()
    {
        try
        {
            _cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // 已经收尾过了。
        }
    }

    /// <summary>离开页面时调用：停掉循环并退订事件。</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
        LanguageService.LanguageChanged -= OnLanguageChanged;
        ThemeService.ThemeChanged -= OnThemeChanged;
        _cts?.Dispose();
        _cts = null;
    }

    private void ResetCounters()
    {
        _totalCount = 0;
        _successCount = 0;
        _failedCount = 0;
        _successTimeSum = 0;
        _currentLatency = -1;
        _averageLatency = -1;
        _lossPercent = -1;
        PublishResults();
        OnPropertyChanged(nameof(DurationText));
    }

    private void PublishResults()
    {
        OnPropertyChanged(nameof(CurrentLatencyText));
        OnPropertyChanged(nameof(AverageLatencyText));
        OnPropertyChanged(nameof(LossText));
        OnPropertyChanged(nameof(CurrentQuality));
        OnPropertyChanged(nameof(AverageQuality));
        OnPropertyChanged(nameof(LossQuality));
        OnPropertyChanged(nameof(CurrentVerdict));
        OnPropertyChanged(nameof(AverageVerdict));
        OnPropertyChanged(nameof(LossVerdict));
        OnPropertyChanged(nameof(TotalCountText));
        OnPropertyChanged(nameof(SuccessCountText));
        OnPropertyChanged(nameof(FailedCountText));
    }

    private void OnLanguageChanged(object? sender, string language)
    {
        // 档位文案与预设提示都是"求值时查资源"的，换语言后重新求值（§9 第 63 条）。
        OnPropertyChanged(nameof(CurrentVerdict));
        OnPropertyChanged(nameof(AverageVerdict));
        OnPropertyChanged(nameof(LossVerdict));
        OnPropertyChanged(nameof(HostTooltip));
    }

    private void OnThemeChanged()
    {
        // 档位颜色由转换器在求值时查主题画刷：重发一次通知让它立刻换色（停在那儿不动时也正确）。
        OnPropertyChanged(nameof(CurrentQuality));
        OnPropertyChanged(nameof(AverageQuality));
        OnPropertyChanged(nameof(LossQuality));
    }

    private static string FormatLatency(double value)
        => value < 0 ? "--" : value.ToString("F0");

    /// <summary>时长的可读文本。注意 WinUI 在这里写了 <c>Elapsed.Hours + 10</c>（凭空多 10 小时），这里已修正。</summary>
    private static string FormatDuration(TimeSpan elapsed)
    {
        var parts = new List<string>(3);
        if (elapsed.Hours > 0)
        {
            parts.Add($"{elapsed.Hours}h");
        }

        if (elapsed.Minutes > 0)
        {
            parts.Add($"{elapsed.Minutes}min");
        }

        parts.Add($"{elapsed.Seconds}s");
        return string.Join(' ', parts);
    }

    private static PingQuality LatencyQuality(double latency)
        => latency <= LatencyExcellent ? PingQuality.Excellent
            : latency <= LatencyGood ? PingQuality.Good
            : latency <= LatencyNormal ? PingQuality.Normal
            : latency <= LatencyPoor ? PingQuality.Poor
            : PingQuality.Bad;

    private static PingQuality LossQualityOf(double loss)
        => loss <= LossExcellent ? PingQuality.Excellent
            : loss <= LossGood ? PingQuality.Good
            : loss <= LossNormal ? PingQuality.Normal
            : loss <= LossPoor ? PingQuality.Poor
            : PingQuality.Bad;

    private string VerdictText(PingQuality quality)
        => quality == PingQuality.Unknown ? string.Empty : FindString($"ServerPingPage_Result_{quality}");

    private static string FindString(string key)
        => Application.Current?.TryFindResource(key) as string ?? key;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
