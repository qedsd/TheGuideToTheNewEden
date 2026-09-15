using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using TheGuideToTheNewEden.Core.Models.KB;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.KB;
using TheGuideToTheNewEden.WPF.Services.Settings;

namespace TheGuideToTheNewEden.WPF.ViewModels.KB;

/// <summary>
/// 击杀流页的 ViewModel：消费 <see cref="ZkbKillStreamHub"/> 的事件、维护"筛选/已过滤"两个列表与计数，
/// 并暴露设置面板所需的配置。过滤与通知本身由服务层负责（见 <see cref="ZkbStreamFilter"/> / <see cref="ZkbKillStreamHub"/>）。
/// </summary>
public sealed class KillStreamViewModel : INotifyPropertyChanged
{
    private readonly ZkbKillStreamHub _hub = ZkbKillStreamHub.Current;
    private readonly Dispatcher _dispatcher = Application.Current.Dispatcher;

    /// <summary>最近收到的 killmail，用于去重（流可能重复推送同一条）。</summary>
    private readonly HashSet<int> _recentKillIds = [];
    private readonly Queue<int> _recentKillIdOrder = [];

    private bool _subscribed;
    private bool _isConnected;
    private bool _isConnecting;
    private int _totalReceivedCount;
    private int _passedCount;
    private int _filteredCount;

    public KillStreamViewModel()
    {
        Config.EnsureRoleFiltersInitialized();
    }

    /// <summary>实时流设置（与设置页共用同一份 JSON）。</summary>
    public ZKBStreamConfig Config { get; } = ZKBSettingService.Setting;

    /// <summary>通过过滤的 KB（新→旧）。</summary>
    public ObservableCollection<KBItemInfo> KBItemInfos { get; } = [];

    /// <summary>被过滤掉的 KB（新→旧）。</summary>
    public ObservableCollection<KBItemInfo> FilteredOutKBItemInfos { get; } = [];

    public bool IsConnected
    {
        get => _isConnected;
        private set
        {
            if (Set(ref _isConnected, value))
            {
                OnPropertyChanged(nameof(IsDisconnected));
                OnPropertyChanged(nameof(IsContentVisible));
                OnPropertyChanged(nameof(ShowDisconnectedHint));
            }
        }
    }

    public bool IsDisconnected => !_isConnected;

    public bool IsConnecting
    {
        get => _isConnecting;
        private set
        {
            if (Set(ref _isConnecting, value))
            {
                OnPropertyChanged(nameof(ShowDisconnectedHint));
            }
        }
    }

    /// <summary>是否显示 KB 列表区（已连接）。</summary>
    public bool IsContentVisible => _isConnected;

    /// <summary>是否显示"未连接"提示（连接中改为显示页内局部连接指示，不显示本提示）。</summary>
    public bool ShowDisconnectedHint => !_isConnected && !_isConnecting;

    public int TotalReceivedCount
    {
        get => _totalReceivedCount;
        private set => Set(ref _totalReceivedCount, value);
    }

    public int PassedCount
    {
        get => _passedCount;
        private set => Set(ref _passedCount, value);
    }

    public int FilteredCount
    {
        get => _filteredCount;
        private set => Set(ref _filteredCount, value);
    }

    /// <summary>页面首次显示时调用：按设置决定是否自动连接。</summary>
    public async Task InitializeAsync()
    {
        Subscribe();

        if (Config.AutoConnect && !_hub.IsRunning)
        {
            await ConnectAsync();
        }
        else
        {
            IsConnected = _hub.IsRunning;
        }
    }

    public async Task ConnectAsync()
    {
        if (IsConnecting || _hub.IsRunning)
        {
            IsConnected = _hub.IsRunning;
            return;
        }

        // 连接等待用页内局部指示（IsConnecting 驱动 KillStreamPage 的 SpinnerIcon），不走全局遮罩
        IsConnecting = true;
        Subscribe();

        try
        {
            var ok = await _hub.StartAsync();
            IsConnected = ok;

            if (ok)
            {
                PageNotifyService.Success(FindString("ZKBHomePage_Connected"));
                ZKBSettingService.Save();
            }
            else
            {
                PageNotifyService.Error(FindString("ZKBPage_QueryFailed"));
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error(ex.Message);
        }
        finally
        {
            IsConnecting = false;
        }
    }

    public void Disconnect()
    {
        _hub.Stop();
        IsConnected = false;
        PageNotifyService.Info(FindString("ZKBHomePage_Disconnect_Tip"));
    }

    public void ClearList()
    {
        KBItemInfos.Clear();
        FilteredOutKBItemInfos.Clear();
        _recentKillIds.Clear();
        _recentKillIdOrder.Clear();
        _hub.ClearCounters();
        TotalReceivedCount = 0;
        PassedCount = 0;
        FilteredCount = 0;
    }

    /// <summary>彻底退订（页面销毁时调用）。</summary>
    public void Dispose()
    {
        if (!_subscribed)
        {
            return;
        }

        _hub.Matched -= OnMatched;
        _hub.FilteredOut -= OnFilteredOut;
        _hub.CountersChanged -= OnCountersChanged;
        _hub.Error -= OnError;
        _subscribed = false;
    }

    private void Subscribe()
    {
        if (_subscribed)
        {
            return;
        }

        _hub.Matched += OnMatched;
        _hub.FilteredOut += OnFilteredOut;
        _hub.CountersChanged += OnCountersChanged;
        _hub.Error += OnError;
        _subscribed = true;
    }

    private void OnMatched(KBItemInfo info) => _dispatcher.Invoke(() => Append(KBItemInfos, info));

    private void OnFilteredOut(KBItemInfo info) => _dispatcher.Invoke(() => Append(FilteredOutKBItemInfos, info));

    private void OnError(string message)
    {
        Core.Log.Error(message);
        _dispatcher.Invoke(() => PageNotifyService.Error(message));
    }

    private void OnCountersChanged() => _dispatcher.Invoke(() =>
    {
        TotalReceivedCount = _hub.TotalReceivedCount;
        PassedCount = _hub.PassedCount;
        FilteredCount = _hub.FilteredCount;
    });

    private void Append(ObservableCollection<KBItemInfo> target, KBItemInfo info)
    {
        var killmailId = (int)info.SKBDetail.KillmailId;
        if (killmailId > 0)
        {
            if (!_recentKillIds.Add(killmailId))
            {
                return;
            }

            _recentKillIdOrder.Enqueue(killmailId);
            while (_recentKillIdOrder.Count > 2000)
            {
                _recentKillIds.Remove(_recentKillIdOrder.Dequeue());
            }
        }

        if (Config.SortWay == 0)
        {
            target.Insert(0, info);
        }
        else
        {
            target.Insert(FindInsertIndex(target, info), info);
        }

        var max = Math.Max(1, Config.MaxKBItems);
        while (target.Count > max)
        {
            target.RemoveAt(target.Count - 1);
        }
    }

    /// <summary>列表按发生时间降序，返回新项的插入位置。</summary>
    private static int FindInsertIndex(ObservableCollection<KBItemInfo> list, KBItemInfo info)
    {
        var time = info.SKBDetail.KillmailTime;
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].SKBDetail.KillmailTime < time)
            {
                return i;
            }
        }

        return list.Count;
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
