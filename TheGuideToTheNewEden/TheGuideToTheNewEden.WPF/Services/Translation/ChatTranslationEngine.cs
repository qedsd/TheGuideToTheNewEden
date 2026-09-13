using System.Collections.Concurrent;
using TheGuideToTheNewEden.Core.Models.EVELogs;
using TheGuideToTheNewEden.WPF.Services.Settings;

namespace TheGuideToTheNewEden.WPF.Services.Translation;

/// <summary>一条聊天翻译结果（界面/弹窗展示用）。</summary>
public sealed class ChatTranslationItem
{
    public DateTime LocalTime { get; init; }

    /// <summary>收到该消息的本地角色（监听者）。</summary>
    public string Listener { get; init; } = string.Empty;

    public string Speaker { get; init; } = string.Empty;

    public string ChannelName { get; init; } = string.Empty;

    public string Original { get; init; } = string.Empty;

    public string Translation { get; init; } = string.Empty;

    public bool Success { get; init; }

    /// <summary>失败原因（含"未配置"这类提示）。</summary>
    public string? Error { get; init; }

    /// <summary>送翻译前清洗掉的游戏内标记个数（0 表示原文本来就没有标记）。</summary>
    public int RemovedMarkup { get; init; }

    /// <summary>模型/耗时/token（与 AI 翻译页同款 meta，界面显示在 token 行）。</summary>
    public string Meta { get; init; } = string.Empty;

    /// <summary>本次命中的术语与术语后校验提醒（界面"术语命中"同款展示）。</summary>
    public string GlossaryText { get; init; } = string.Empty;

    /// <summary>悬停提示用的完整清洗文本（保留 <c>&lt;br&gt;</c> 换行）。</summary>
    /// <remarks>
    /// 引擎写入的 <see cref="Original"/> 本来就是清洗后的文本；这里再洗一次是给"别的来源直接塞了带标记原文"
    /// 兜底，保证列表里永远不出现 <c>&lt;font&gt;</c> 这类游戏内标记。
    /// </remarks>
    public string OriginalFull => ChatMarkupProtector.ToPlainText(Original);

    /// <summary>列表里显示的原文单行预览（去标记、压缩空白、超长截断）。</summary>
    public string OriginalPreview => ChatMarkupProtector.ToSingleLine(OriginalFull, 160);
}

/// <summary>
/// 频道翻译引擎：把多个角色/多个频道的消息排进**同一条队列**串行翻译
/// （AI 源按次计费且容易触发限流，串行是最省心也最省钱的做法；单条内部已有缓存与术语短路）。
/// <para>
/// 单条流程：<see cref="ChatMarkupProtector"/> **预清洗**（&lt;br&gt;→换行、其余标记删掉）→ 跳过规则
/// → 交给翻译源（AI 优先）→ 发布 <see cref="Translated"/>。
/// <see cref="Translated"/> 从**后台线程**触发，界面订阅方需自行切回 UI 线程（与 Core 的观察者一致）。
/// </para>
/// </summary>
public sealed class ChatTranslationEngine
{
    /// <summary>默认引擎实例（页面/弹窗共用）。</summary>
    public static ChatTranslationEngine Current { get; } = new();

    private readonly ConcurrentQueue<(string Listener, ChatContent Content, ChatTranslationOptions Options)> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly Func<string, IReadOnlyList<string>?, CancellationToken, Task<TranslationOutcome>> _translator;
    private readonly object _sync = new();

    /// <summary>每个"角色 + 频道"最近几条已翻译的记录（形如 <c>原文：…\n译文：…</c>），用作下一条的上下文。</summary>
    private readonly Dictionary<string, List<string>> _contextHistory = [];

    private CancellationTokenSource? _cts;
    private Task? _worker;
    private bool _availabilityReported;

    /// <summary>上下文历史里每个频道最多留几条（内存上限；真正发给模型的条数看 <see cref="ContextLimit"/>）。</summary>
    private const int MaxContextHistory = 20;

    /// <param name="translator">
    /// 翻译委托（默认走 AI 源）；探针/测试可注入替身，从而在离线环境下验证队列、SkipMarks 规则、预清洗与上下文。
    /// 参数依次是：清洗后的原文、上下文（可为 null）、取消令牌。
    /// </param>
    public ChatTranslationEngine(Func<string, IReadOnlyList<string>?, CancellationToken, Task<TranslationOutcome>>? translator = null)
    {
        _translator = translator ?? DefaultTranslator;
    }

    /// <summary>新译文/失败结果。</summary>
    public event EventHandler<ChatTranslationItem>? Translated;

    /// <summary>正在翻译的条数（队列长度）。</summary>
    public int PendingCount => _queue.Count;

    /// <summary>已翻译条数（成功 + 失败）。</summary>
    public int ProcessedCount { get; private set; }

    /// <summary>跳过自己的发言。</summary>
    public bool SkipMyself { get; set; } = true;

    /// <summary>只翻译"非中文为主"的消息（默认开：中文玩家的诉求是看懂外文频道）。</summary>
    public bool OnlyNonChinese { get; set; } = true;

    /// <summary>最短长度（去空白后的字符数），过滤"111"这类噪声。</summary>
    public int MinLength { get; set; } = 2;

    /// <summary>
    /// 是否把**同一频道里此前已翻译过的几条**一起发给模型当上下文（默认开；每条的请求会大一些，但译文更连贯）。
    /// </summary>
    public bool UseContext { get; set; } = true;

    /// <summary>上下文条数上限（<see cref="UseContext"/> 打开时生效）。</summary>
    public int ContextLimit { get; set; } = 4;

    public bool IsRunning
    {
        get
        {
            lock (_sync)
            {
                return _worker is not null;
            }
        }
    }

    public void Start()
    {
        lock (_sync)
        {
            if (_worker is not null)
            {
                return;
            }

            _cts = new CancellationTokenSource();
            _availabilityReported = false;
            var token = _cts.Token;
            _worker = Task.Run(() => WorkerAsync(token), token);
        }
    }

    public void Stop()
    {
        CancellationTokenSource? cts;
        Task? worker;
        lock (_sync)
        {
            cts = _cts;
            worker = _worker;
            _cts = null;
            _worker = null;
        }

        try
        {
            cts?.Cancel();
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }

        // 不等待 worker：它最多在完成当前一条请求后退出（AI 请求可能还在飞行中）
        _ = worker;
        cts?.Dispose();
    }

    /// <summary>
    /// 把一批新消息排进队列。
    /// <paramref name="options"/> 是**按角色的开关快照**（跳过自己 / 只译非中文 / 最短长度 / 上下文），
    /// 传 null 时用引擎上的默认值（探针用）。
    /// </summary>
    public void Enqueue(string listenerName, IEnumerable<ChatContent> contents, ChatTranslationOptions? options = null)
    {
        foreach (var content in contents)
        {
            if (content is null)
            {
                continue;
            }

            _queue.Enqueue((listenerName, content, options ?? DefaultOptions));
            _signal.Release();
        }
    }

    /// <summary>引擎上的默认开关（<see cref="Enqueue"/> 不传 options 时使用；界面不直接用）。</summary>
    private ChatTranslationOptions DefaultOptions => new()
    {
        SkipMyself = SkipMyself,
        OnlyNonChinese = OnlyNonChinese,
        MinLength = MinLength,
        UseContext = UseContext,
        ContextLimit = ContextLimit,
    };

    /// <summary>清空尚未处理的队列（已在翻译中的那条不受影响）。</summary>
    public void ClearPending()
    {
        while (_queue.TryDequeue(out _))
        {
            // 丢弃
        }

        while (_signal.Wait(0))
        {
            // 把信号量也清空，避免空转
        }
    }

    private async Task WorkerAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _signal.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            while (_queue.TryDequeue(out var item))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                await TranslateOneAsync(item.Listener, item.Content, item.Options, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task TranslateOneAsync(string listenerName, ChatContent content, ChatTranslationOptions options, CancellationToken cancellationToken)
    {
        var raw = content.Content ?? string.Empty;

        // 预清洗：<br> → 换行，其余游戏内标记（font/b/loc/a href…）直接删掉，
        // 因为颜色/字号/链接对翻译没价值，带着它们只会让模型分心或丢占位符（用户实测 MOTD 场景）
        var original = ChatMarkupProtector.ToPlainText(raw, out var removedTags);

        if (ShouldSkip(listenerName, content, original, options, out _))
        {
            return;
        }

        ChatTranslationItem item;
        var context = BuildContext(listenerName, content, options);
        try
        {
            var outcome = await _translator(original, context, cancellationToken).ConfigureAwait(false);
            ProcessedCount++;
            if (!outcome.Success)
            {
                item = Failed(listenerName, content, original, outcome.ErrorMessage ?? "翻译失败");
            }
            else
            {
                var translated = outcome.Items.FirstOrDefault()?.Translation ?? string.Empty;
                if (string.IsNullOrWhiteSpace(translated))
                {
                    item = Failed(listenerName, content, original, "没有返回译文");
                }
                else
                {
                    item = new ChatTranslationItem
                    {
                        LocalTime = content.LocalTime,
                        Listener = listenerName,
                        Speaker = content.SpeakerName ?? string.Empty,
                        ChannelName = content.ChannelName ?? string.Empty,
                        Original = original,
                        Translation = translated.Trim(),
                        Success = true,
                        RemovedMarkup = removedTags,
                        Meta = outcome.Meta ?? string.Empty,
                        GlossaryText = BuildGlossaryText(outcome),
                    };

                    // 成功的一条进上下文历史（供同一频道后续消息使用）
                    RememberContext(listenerName, content, item, options);
                }
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            item = Failed(listenerName, content, original, ex.Message);
        }

        // 未配置只提示一次并把队列清掉，避免每条消息都弹一次同样的错误
        if (!item.Success && item.Error?.Contains("未配置", StringComparison.Ordinal) == true)
        {
            if (_availabilityReported)
            {
                return;
            }

            _availabilityReported = true;
            ClearPending();
        }

        Translated?.Invoke(this, item);
    }

    /// <summary>不应翻译的消息：空、过短、自己的发言、纯中文、纯符号（按**清洗后**的纯文本判断）。</summary>
    private bool ShouldSkip(string listenerName, ChatContent content, string text, ChatTranslationOptions options, out string? reason)
    {
        reason = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            reason = "empty";
            return true;
        }

        var trimmed = text.Trim();
        if (trimmed.Length < Math.Max(1, options.MinLength))
        {
            reason = "too-short";
            return true;
        }

        if (options.SkipMyself && !string.IsNullOrEmpty(listenerName) && string.Equals(listenerName, content.SpeakerName, StringComparison.OrdinalIgnoreCase))
        {
            reason = "self";
            return true;
        }

        if (options.OnlyNonChinese && IsMostlyChinese(trimmed))
        {
            reason = "chinese";
            return true;
        }

        // 纯符号/纯数字（去掉标记与空白后没有字母或汉字）→ 没什么可翻的
        var letters = trimmed.Count(ch => char.IsLetter(ch) || ch >= '\u4e00' && ch <= '\u9fff');
        if (letters == 0)
        {
            reason = "no-letters";
            return true;
        }

        return false;
    }

    /// <summary>汉字占比是否过半（中文消息不必再翻成中文）。</summary>
    private static bool IsMostlyChinese(string text)
    {
        var meaningful = 0;
        var chinese = 0;
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch) || char.IsPunctuation(ch) || char.IsSymbol(ch))
            {
                continue;
            }

            meaningful++;
            if (ch >= '\u4e00' && ch <= '\u9fff')
            {
                chinese++;
            }
        }

        return meaningful > 0 && chinese * 2 > meaningful;
    }

    /// <summary>
    /// 组装这一条的上下文：同一"角色 + 频道"里最近 <see cref="ContextLimit"/> 条**翻译成功**的记录。
    /// <see cref="UseContext"/> 关闭时返回空（每条独立翻译）。
    /// </summary>
    private IReadOnlyList<string>? BuildContext(string listenerName, ChatContent content, ChatTranslationOptions options)
    {
        if (!options.UseContext || options.ContextLimit <= 0)
        {
            return null;
        }

        lock (_sync)
        {
            if (!_contextHistory.TryGetValue(ContextKey(listenerName, content), out var history) || history.Count == 0)
            {
                return null;
            }

            var take = Math.Min(Math.Max(1, options.ContextLimit), history.Count);
            return history.Skip(history.Count - take).ToList();
        }
    }

    /// <summary>把"原文 + 译文"记进该频道的上下文历史（只留最近 <see cref="MaxContextHistory"/> 条）。</summary>
    private void RememberContext(string listenerName, ChatContent content, ChatTranslationItem item, ChatTranslationOptions options)
    {
        if (!options.UseContext)
        {
            return;
        }

        var entry = $"原文：{item.Original}\n译文：{item.Translation}";
        lock (_sync)
        {
            var key = ContextKey(listenerName, content);
            if (!_contextHistory.TryGetValue(key, out var history))
            {
                history = [];
                _contextHistory[key] = history;
            }

            history.Add(entry);
            if (history.Count > MaxContextHistory)
            {
                history.RemoveRange(0, history.Count - MaxContextHistory);
            }
        }
    }

    private static string ContextKey(string listenerName, ChatContent content)
        => $"{listenerName}\u0001{content.ChannelName}";

    /// <summary>术语命中 + 术语后校验提醒（与 AI 翻译页的展示一致）。</summary>
    private static string BuildGlossaryText(TranslationOutcome outcome)
    {
        var lines = new List<string>();
        if (outcome.GlossaryHits.Count > 0)
        {
            lines.AddRange(outcome.GlossaryHits.Take(20));
            if (outcome.GlossaryHits.Count > 20)
            {
                lines.Add($"…（另有 {outcome.GlossaryHits.Count - 20} 条）");
            }
        }

        if (!string.IsNullOrWhiteSpace(outcome.GlossaryWarning))
        {
            lines.Add(outcome.GlossaryWarning);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private ChatTranslationItem Failed(string listenerName, ChatContent content, string original, string error)
        => new()
        {
            LocalTime = content.LocalTime,
            Listener = listenerName,
            Speaker = content.SpeakerName ?? string.Empty,
            ChannelName = content.ChannelName ?? string.Empty,
            Original = original,
            Translation = string.Empty,
            Success = false,
            Error = error,
        };

    /// <summary>
    /// 默认翻译委托：走 AI 源，方向用「AI 翻译」页那一套（源/目标语言 + 自动互译语言对），
    /// 上下文（如果有）一并交给 provider。
    /// </summary>
    private static async Task<TranslationOutcome> DefaultTranslator(string text, IReadOnlyList<string>? context, CancellationToken cancellationToken)
    {
        var provider = TranslationService.Ai;
        if (!provider.IsAvailable)
        {
            return TranslationOutcome.Fail("AI 翻译未配置（设置 → AI 翻译）");
        }

        var request = new TranslationRequest(text, TranslationSettingService.AiFrom, TranslationSettingService.AiTo);
        if (context is { Count: > 0 })
        {
            request = request with { Context = context };
        }

        return await provider.TranslateAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
