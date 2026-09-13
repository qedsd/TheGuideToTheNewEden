using TheGuideToTheNewEden.Core.Models;

namespace TheGuideToTheNewEden.WPF.Services.Translation;

/// <summary>翻译方向（本地数据库只支持中英互译）。</summary>
public enum TranslationDirection
{
    /// <summary>按原文自动判断：含中文 → 中译英，否则英译中（查不到时再试另一方向）。</summary>
    Auto = 0,

    /// <summary>英文 → 中文。</summary>
    EnglishToChinese = 1,

    /// <summary>中文 → 英文。</summary>
    ChineseToEnglish = 2,
}

/// <summary>一次翻译请求。</summary>
/// <param name="Text">要翻译的文本（本地数据库源只识别 EVE 专有名词）。</param>
/// <param name="Direction">翻译方向。</param>
public sealed record TranslationRequest(string Text, TranslationDirection Direction);

/// <summary>一次翻译的结果。</summary>
public sealed class TranslationOutcome
{
    /// <summary>是否成功。成功也可能一条都没匹配到（<see cref="Items"/> 为空）。</summary>
    public bool Success { get; init; } = true;

    /// <summary>失败原因（原始信息，如数据库异常），成功时为 null。</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>实际使用的翻译方向（<see cref="TranslationDirection.Auto"/> 已解析为具体方向）。</summary>
    public TranslationDirection Direction { get; init; }

    /// <summary>匹配到的名词（每条自带另一语言的译名与描述）。</summary>
    public IReadOnlyList<TranslationItem> Items { get; init; } = [];

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

    Task<TranslationOutcome> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default);
}
