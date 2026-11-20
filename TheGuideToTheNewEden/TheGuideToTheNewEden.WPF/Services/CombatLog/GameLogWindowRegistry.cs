using System.Windows;
using TheGuideToTheNewEden.WPF.Views.Windows;

namespace TheGuideToTheNewEden.WPF.Services.CombatLog;

/// <summary>
/// 阶段 359 修正：同一角色只保留**一个**日志监控窗口，战斗日志和关键词日志共用。
/// <para>
/// 之前战斗会话和关键词会话各建一个窗口，两个窗口都置顶、都居中，战斗窗口会把关键词窗口盖住，
/// 看起来就像"开了战斗日志，频道日志就不显示了"。这里按角色做引用计数：
/// 谁先启动谁创建窗口，后来者复用同一个窗口（战斗窗口会被升级成"战斗 + 监控"双区），
/// 最后一个使用者停止时才真正关闭。
/// </para>
/// </summary>
public static class GameLogWindowRegistry
{
    private sealed class Entry
    {
        public required GameLogMsgWindow Window { get; init; }

        public HashSet<string> Owners { get; } = new(StringComparer.Ordinal);
    }

    private static readonly Dictionary<string, Entry> Entries = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object Locker = new();

    /// <summary>取（或创建）该角色的窗口，并登记一个使用者。必须在 UI 线程调用。</summary>
    public static GameLogMsgWindow Acquire(string listener, string owner, Func<GameLogMsgWindow> factory)
    {
        lock (Locker)
        {
            if (!Entries.TryGetValue(listener, out var entry))
            {
                entry = new Entry { Window = factory() };
                Entries[listener] = entry;
            }

            entry.Owners.Add(owner);
            return entry.Window;
        }
    }

    /// <summary>除指定使用者外，是否还有别的关键词监控会话在用这个窗口。</summary>
    public static bool HasOtherMonitorOwner(string listener, string owner)
    {
        lock (Locker)
        {
            return Entries.TryGetValue(listener, out var entry)
                   && entry.Owners.Any(p => !string.Equals(p, owner, StringComparison.Ordinal)
                                            && p.StartsWith("monitor:", StringComparison.Ordinal));
        }
    }
    /// <summary>释放使用者；最后一个使用者释放时关闭窗口。</summary>
    public static void Release(string listener, string owner)
    {
        GameLogMsgWindow? close = null;
        lock (Locker)
        {
            if (Entries.TryGetValue(listener, out var entry))
            {
                entry.Owners.Remove(owner);
                if (entry.Owners.Count == 0)
                {
                    Entries.Remove(listener);
                    close = entry.Window;
                }
            }
        }

        if (close is null)
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(close.CloseWindow);
        }
        else
        {
            close.CloseWindow();
        }
    }
}
