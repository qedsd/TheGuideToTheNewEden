namespace TheGuideToTheNewEden.WPF.Services.Assistant.Features;

/// <summary>
/// 一个"有运行态、能被 AI 重启"的功能（阶段 155）。
/// <para>
/// 页面/VM 在构造时注册、销毁时注销；<c>feature_restart</c> 优先调用已注册的运行态，
/// 这样"停掉正在跑的预览/会话 → 重读配置 → 按需重新开始"才能作用到真实对象，
/// 而不是只改磁盘文件。
/// </para>
/// </summary>
public interface IAssistantFeatureRuntime
{
    /// <summary>功能逻辑名（与 <see cref="AssistantFeatureRegistry"/> 的条目一致）。</summary>
    string FeatureKey { get; }

    /// <summary>在 UI 线程执行一次重启，返回给模型/用户的结果说明。</summary>
    Task<string> RestartAsync();
}

/// <summary>
/// 运行态注册表（阶段 155）：弱引用持有，页面被回收后不会阻止释放。
/// </summary>
public static class AssistantFeatureRuntime
{
    private static readonly Dictionary<string, WeakReference<IAssistantFeatureRuntime>> Runtimes =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly object Sync = new();

    /// <summary>注册（同名覆盖；页面通常是常驻缓存，重复构造时以最新实例为准）。</summary>
    public static void Register(IAssistantFeatureRuntime runtime)
    {
        if (string.IsNullOrWhiteSpace(runtime.FeatureKey))
        {
            return;
        }

        lock (Sync)
        {
            Runtimes[runtime.FeatureKey] = new WeakReference<IAssistantFeatureRuntime>(runtime);
        }
    }

    /// <summary>注销（只移除自己的注册，避免旧页面注销掉新页面的）。</summary>
    public static void Unregister(IAssistantFeatureRuntime runtime)
    {
        lock (Sync)
        {
            if (Runtimes.TryGetValue(runtime.FeatureKey, out var reference)
                && reference.TryGetTarget(out var current)
                && ReferenceEquals(current, runtime))
            {
                Runtimes.Remove(runtime.FeatureKey);
            }
        }
    }

    /// <summary>取某个功能当前存活的运行态；没有（或已被回收）返回 false。</summary>
    public static bool TryGet(string? key, out IAssistantFeatureRuntime runtime)
    {
        runtime = null!;
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        lock (Sync)
        {
            if (!Runtimes.TryGetValue(key.Trim(), out var reference))
            {
                return false;
            }

            if (reference.TryGetTarget(out var current))
            {
                runtime = current;
                return true;
            }

            Runtimes.Remove(key.Trim());
            return false;
        }
    }

    /// <summary>该功能当前是否有存活的运行态（列表展示用）。</summary>
    public static bool IsLive(string? key) => TryGet(key, out _);
}
