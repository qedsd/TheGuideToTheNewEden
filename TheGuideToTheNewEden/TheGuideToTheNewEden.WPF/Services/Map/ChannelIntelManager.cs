using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.WPF.Services.ChannelIntel;

namespace TheGuideToTheNewEden.WPF.Services.Map;

/// <summary>
/// 聚合当前运行中的 <see cref="ChannelIntelSession"/>（对齐 WinUI 版 <c>Services/ChannelIntelManager</c>）：
/// 供星图情报模式"无视跳数"旁路使用——按需把会话观察者切到 IgnoreJumps、聚合
/// <see cref="Core.Models.ChannelIntel.ChannelIntelObserver.OnIgnoreJumpsIntelUpdate"/> 事件。
/// 会话在 <see cref="ChannelIntelSession.Start"/> / <see cref="ChannelIntelSession.Stop"/> 时注册/注销。
/// 事件来自后台线程，消费方自行调度 UI。
/// </summary>
public sealed class ChannelIntelManager
{
    public static ChannelIntelManager Current { get; } = new();

    private readonly List<ChannelIntelSession> _sessions = [];
    /// <summary>已切换 IgnoreJumps=true 的观察者（停止情报模式时还原）。</summary>
    private readonly List<Core.Models.ChannelIntel.ChannelIntelObserver> _listening = [];
    private readonly object _lock = new();

    private ChannelIntelManager()
    {
    }

    /// <summary>星图情报模式（或任何消费方）订阅"无视跳数"情报聚合。</summary>
    public event WarningUpdate? OnIgnoreJumpsIntelUpdate;

    public delegate void WarningUpdate(object sender, IEnumerable<EarlyWarningContent> contents);

    /// <summary>当前运行中的预警会话。</summary>
    public IReadOnlyList<ChannelIntelSession> Sessions
    {
        get
        {
            lock (_lock)
            {
                return _sessions.ToList();
            }
        }
    }

    /// <summary>当前运行中的角色（Listener）名列表。</summary>
    public IReadOnlyList<string> GetActiveListeners()
    {
        lock (_lock)
        {
            return _sessions.Select(p => p.Listener).ToList();
        }
    }

    public void Register(ChannelIntelSession session)
    {
        if (session is null)
        {
            return;
        }

        lock (_lock)
        {
            if (_sessions.All(p => p.Listener != session.Listener))
            {
                _sessions.Add(session);
            }
        }
    }

    public void Unregister(ChannelIntelSession session)
    {
        if (session is null)
        {
            return;
        }

        lock (_lock)
        {
            _sessions.RemoveAll(p => p.Listener == session.Listener);
            // 若该会话的观察者正处于情报模式，先取消其监听
            var observers = session.GetObservers();
            foreach (var observer in observers)
            {
                if (_listening.Remove(observer))
                {
                    observer.IgnoreJumps = false;
                    observer.OnIgnoreJumpsIntelUpdate -= Observer_OnIgnoreJumpsIntelUpdate;
                }
            }
        }
    }

    /// <summary>
    /// 把指定角色会话的全部观察者切到 IgnoreJumps=true，并开始聚合其情报事件。
    /// </summary>
    public void ListenChannelIntel(IEnumerable<string> listeners)
    {
        var names = listeners?.ToHashSet() ?? [];
        lock (_lock)
        {
            foreach (var session in _sessions)
            {
                if (names.Count > 0 && !names.Contains(session.Listener))
                {
                    continue;
                }

                foreach (var observer in session.GetObservers())
                {
                    if (_listening.Contains(observer))
                    {
                        continue;
                    }

                    observer.IgnoreJumps = true;
                    observer.OnIgnoreJumpsIntelUpdate += Observer_OnIgnoreJumpsIntelUpdate;
                    _listening.Add(observer);
                }
            }
        }
    }

    /// <summary>还原全部 IgnoreJumps=false 并停止聚合。</summary>
    public void UnListenChannelIntel()
    {
        lock (_lock)
        {
            foreach (var observer in _listening)
            {
                observer.IgnoreJumps = false;
                observer.OnIgnoreJumpsIntelUpdate -= Observer_OnIgnoreJumpsIntelUpdate;
            }

            _listening.Clear();
        }
    }

    private void Observer_OnIgnoreJumpsIntelUpdate(Core.Models.ChannelIntel.ChannelIntelObserver observer, IEnumerable<EarlyWarningContent> contents)
        => OnIgnoreJumpsIntelUpdate?.Invoke(this, contents);
}
