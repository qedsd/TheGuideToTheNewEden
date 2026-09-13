using TheGuideToTheNewEden.Core.Models;

namespace TheGuideToTheNewEden.WPF.Services.Translation;

/// <summary>
/// 一次翻译请求。
/// <para>
/// 方向是**源语言 → 目标语言**两个代码（见 <see cref="TranslationLanguages"/>）：
/// <paramref name="From"/> 可以是 <see cref="TranslationLanguages.Auto"/>（按正文脚本判定），
/// <paramref name="To"/> 必须是具体语言。AI 源支持全部语言；本地词库源只支持中英。
/// </para>
/// </summary>
/// <param name="Text">要翻译的文本（本地数据库源只识别 EVE 专有名词，AI 源支持整句）。</param>
/// <param name="From">源语言代码（auto = 自动判定）。</param>
/// <param name="To">目标语言代码。</param>
public sealed record TranslationRequest(string Text, string From = TranslationLanguages.Auto, string To = TranslationLanguages.Chinese)
{
    /// <summary>流式增量回调（只支持流式的源会调用；用于界面显示"正在生成"的进度）。</summary>
    public IProgress<string>? Progress { get; init; }

    /// <summary>上文若干行（上下文翻译，AI 源会一并交给模型）。</summary>
    public IReadOnlyList<string>? Context { get; init; }
}

/// <summary>一次翻译的结果。</summary>
public sealed class TranslationOutcome
{
    /// <summary>是否成功。成功也可能一条都没匹配到（<see cref="Items"/> 为空）。</summary>
    public bool Success { get; init; } = true;

    /// <summary>失败原因（原始信息，如数据库异常），成功时为 null。</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>实际使用的源语言（<see cref="TranslationLanguages.Auto"/> 已解析成具体语言）。</summary>
    public string From { get; init; } = TranslationLanguages.Auto;

    /// <summary>实际使用的目标语言。</summary>
    public string To { get; init; } = TranslationLanguages.Chinese;

    /// <summary>结果条目：术语源是若干名词匹配，AI 源是一条整句译文。</summary>
    public IReadOnlyList<TranslationItem> Items { get; init; } = [];

    /// <summary>本次命中的术语（形如 <c>Rifter = 裂谷级</c>），界面"术语命中"区展示。</summary>
    public IReadOnlyList<string> GlossaryHits { get; init; } = [];

    /// <summary>补充信息（模型、耗时、token 用量、是否命中缓存）。</summary>
    public string? Meta { get; init; }

    /// <summary>术语后校验发现的问题（模型漏用了术语等），为空表示校验通过。</summary>
    public string? GlossaryWarning { get; init; }

    public static TranslationOutcome Fail(string message)
        => new() { Success = false, ErrorMessage = message };
}

/// <summary>
/// 翻译源：把一段文本翻译成目标语言。第一个（也是目前唯一）实现是
/// <see cref="LocalDbTranslationProvider"/>（离线、走本地 SDE 数据库）。
/// 以后接入在线翻译（如大模型 / 公开 API）时新增一个实现并注册到
/// <see cref="TranslationService"/> 即可，页面无需改动。
/// </summary>
public interface ITranslationProvider
{
    /// <summary>唯一标识（会持久化到设置里，勿随意改名）。</summary>
    string Key { get; }

    /// <summary>界面显示名的本地化键。</summary>
    string DisplayNameKey { get; }

    /// <summary>当前是否可用（本地数据库源要求主库与本地化库都已载入）。</summary>
    bool IsAvailable { get; }

    /// <summary>不可用时的原因（本地化键；可用时为 null）。</summary>
    string? UnavailableReasonKey { get; }

    /// <summary>
    /// 是否为远程源（每次请求都要联网/计费）。远程源在界面上**不做防抖自动查询**，只在回车/按钮时请求。
    /// </summary>
    bool IsRemote { get; }

    /// <summary>是否支持通用文本（整句）；本地数据库源只认 SDE 里的专有名词。</summary>
    bool SupportsFreeText { get; }

    Task<TranslationOutcome> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default);
}
