using System.Threading.Channels;
using TheGuideToTheNewEden.Core.Models.KB;
using TheGuideToTheNewEden.WPF.Helpers;
using ZKB.NET.Models.KillStream;

namespace TheGuideToTheNewEden.WPF.Services.KB;

/// <summary>
/// 击杀流中枢：进程内唯一一个 ZKB 实时 KB 流的订阅者，负责
/// 「接收 → 富化 → 过滤 → 计数 → 系统通知」并把结果分发给订阅者（界面）。
///
/// WinUI 版把这段逻辑写在各页面的 ViewModel 里，每个页面各自 <c>Sub()</c> 并起一条
/// <c>Thread.Sleep(100)</c> 忙等线程轮询 <c>ConcurrentQueue</c>，且过滤器只在连接时构建一次。
/// 这里改为：
/// <list type="bullet">
///   <item>引用计数订阅（多个订阅者共用同一条流，最后一个退出才真正断开）；</item>
///   <item>用有界 <see cref="Channel{T}"/> + <c>WaitToReadAsync</c> 取代忙等轮询（队列满时丢弃最旧，永不积压）；</item>
///   <item>配置（过滤条件）变更时<b>自动重建过滤器</b>，无需断开重连；</item>
///   <item>事件全部在后台线程触发，界面订阅者自行切回 UI 线程。</item>
/// </list>
/// </summary>
public sealed class ZkbKillStreamHub
{
    /// <summary>进程内单例。</summary>
    public static ZkbKillStreamHub Current { get; } = new();

    /// <summary>待处理队列容量（超出时丢弃最旧的，保证不落后于实时流）。</summary>
    private const int QueueCapacity = 512;

    private readonly object _gate = new();
    private readonly ZKBStreamConfig _config;

    private Channel<SKBDetail>? _channel;
    private CancellationTokenSource? _cts;
    private Task? _consumer;
    private int _refCount;

    private ZkbStreamFilter _filter = ZkbStreamFilter.Empty;
    private int _filterDirty = 1;

    private int _totalReceivedCount;
    private int _passedCount;
    private int _filteredCount;

    private ZkbKillStreamHub()
    {
        _config = Settings.ZKBSettingService.Setting;
        _config.EnsureRoleFiltersInitialized();
        HookConfig();
    }

    /// <summary>是否已连接。</summary>
    public bool IsRunning { get; private set; }

    public int TotalReceivedCount => Volatile.Read(ref _totalReceivedCount);

    public int PassedCount => Volatile.Read(ref _passedCount);

    public int FilteredCount => Volatile.Read(ref _filteredCount);

    /// <summary>通过过滤的 KB（后台线程触发）。</summary>
    public event Action<KBItemInfo>? Matched;

    /// <summary>被过滤掉的 KB（后台线程触发）。</summary>
    public event Action<KBItemInfo>? FilteredOut;

    /// <summary>计数发生变化（后台线程触发）。</summary>
    public event Action? CountersChanged;

    /// <summary>连接或消费过程中出错（后台线程触发）。</summary>
    public event Action<string>? Error;

    /// <summary>连接状态变化（后台线程触发）。</summary>
    public event Action? StateChanged;

    /// <summary>开始接收（引用计数 +1）。返回是否连接成功。</summary>
    public async Task<bool> StartAsync()
    {
        lock (_gate)
        {
            if (IsRunning)
            {
                _refCount++;
                return true;
            }
        }

        try
        {
            await Core.Services.ZKBStreamService.Current.Sub().ConfigureAwait(false);

            lock (_gate)
            {
                var channel = Channel.CreateBounded<SKBDetail>(new BoundedChannelOptions(QueueCapacity)
                {
                    FullMode = BoundedChannelFullMode.DropOldest,
                    SingleReader = true,
                    SingleWriter = false,
                });

                _channel = channel;
                _cts = new CancellationTokenSource();
                _consumer = Task.Run(() => ConsumeAsync(channel.Reader, _cts.Token));
                _refCount = 1;
                IsRunning = true;
                Interlocked.Exchange(ref _filterDirty, 1);
            }

            Core.Services.ZKBStreamService.Current.OnMessage += OnStreamMessage;
            Core.Services.ZKBStreamService.Current.OnError += OnStreamError;
            StateChanged?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            Error?.Invoke(ex.Message);
            return false;
        }
    }

    /// <summary>停止接收（引用计数 -1，归零才真正断开）。</summary>
    public void Stop()
    {
        lock (_gate)
        {
            if (!IsRunning)
            {
                return;
            }

            if (--_refCount > 0)
            {
                return;
            }

            IsRunning = false;
            _cts?.Cancel();
            _channel?.Writer.TryComplete();
            _channel = null;
            _cts = null;
            _consumer = null;
        }

        Core.Services.ZKBStreamService.Current.OnMessage -= OnStreamMessage;
        Core.Services.ZKBStreamService.Current.OnError -= OnStreamError;
        Core.Services.ZKBStreamService.Current.UnSub();
        StateChanged?.Invoke();
    }

    /// <summary>清空计数。</summary>
    public void ClearCounters()
    {
        Interlocked.Exchange(ref _totalReceivedCount, 0);
        Interlocked.Exchange(ref _passedCount, 0);
        Interlocked.Exchange(ref _filteredCount, 0);
        CountersChanged?.Invoke();
    }

    private void OnStreamMessage(object? sender, SKBDetail detail, string sourceData)
        => _channel?.Writer.TryWrite(detail);

    private void OnStreamError(object? sender, Exception e)
    {
        Core.Log.Error(e);
        Error?.Invoke(e.Message);
    }

    private async Task ConsumeAsync(ChannelReader<SKBDetail> reader, CancellationToken token)
    {
        try
        {
            while (await reader.WaitToReadAsync(token).ConfigureAwait(false))
            {
                EnsureFilter();

                while (reader.TryRead(out var detail))
                {
                    if (detail is null)
                    {
                        continue;
                    }

                    // 逐条隔离：单条富化/过滤/回调抛异常只丢这一条，
                    // 不能让它冲出循环把整个消费者任务结束掉（否则界面仍显示"已连接"却再也收不到数据）。
                    try
                    {
                        ProcessOne(detail);
                    }
                    catch (Exception ex)
                    {
                        Core.Log.Error(ex);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常停止
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            Error?.Invoke(ex.Message);
        }
    }

    private void ProcessOne(SKBDetail detail)
    {
        var info = Core.Helpers.KBHelpers.CreateKBItemInfo(detail);
        if (info is null)
        {
            return;
        }

        Interlocked.Increment(ref _totalReceivedCount);

        if (_filter.Pass(detail))
        {
            Interlocked.Increment(ref _passedCount);
            Matched?.Invoke(info);
            TryNotify(detail, info);
        }
        else
        {
            Interlocked.Increment(ref _filteredCount);
            FilteredOut?.Invoke(info);
        }

        CountersChanged?.Invoke();
    }

    private void EnsureFilter()
    {
        if (Interlocked.CompareExchange(ref _filterDirty, 0, 1) == 1)
        {
            try
            {
                _filter = ZkbStreamFilter.FromConfig(_config);
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
                _filter = ZkbStreamFilter.Empty;
            }
        }
    }

    private void TryNotify(SKBDetail detail, KBItemInfo info)
    {
        try
        {
            if (!_config.Notify)
            {
                return;
            }

            var totalValue = detail.Zkb?.TotalValue ?? 0;
            if (totalValue < _config.MinNotifyValue)
            {
                return;
            }

            var victim = info.Victim?.Name ?? detail.Victim?.CharacterId.ToString();
            var ship = info.Type?.TypeName;
            var system = info.SolarSystem?.SolarSystemName;

            var message = $"{victim}"
                          + (string.IsNullOrEmpty(ship) ? string.Empty : $" ({ship})")
                          + $" · {IskFormatHelper.Format(totalValue)} ISK"
                          + (string.IsNullOrEmpty(system) ? string.Empty : $" @ {system}");

            // 点击通知打开对应 KB 详情（KbNavigation 内含导航到 ZKB 页 + 主窗口前置/托盘恢复）。
            // 携带已富化的 info 直开——击杀刚广播的几秒内 API 还查不到，按 ID 重查会"查询失败"
            NotificationService.Show(
                FindString("Nav.ZKB"),
                message,
                () => KbNavigation.OpenKillmail(info));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    private void HookConfig()
    {
        _config.PropertyChanged += (_, _) => MarkFilterDirty();
        _config.CommonExclusions.CollectionChanged += (_, _) => MarkFilterDirty();
        _config.CommonInclusions.CollectionChanged += (_, _) => MarkFilterDirty();
        _config.VictimExclusions.CollectionChanged += (_, _) => MarkFilterDirty();
        _config.VictimInclusions.CollectionChanged += (_, _) => MarkFilterDirty();
        _config.AttackerExclusions.CollectionChanged += (_, _) => MarkFilterDirty();
        _config.AttackerInclusions.CollectionChanged += (_, _) => MarkFilterDirty();
    }

    private void MarkFilterDirty() => Interlocked.Exchange(ref _filterDirty, 1);

    private static string FindString(string key) =>
        System.Windows.Application.Current?.TryFindResource(key) as string ?? key;
}
