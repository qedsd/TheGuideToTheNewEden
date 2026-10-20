using TheGuideToTheNewEden.WPF.Services.Assistant;

namespace TheGuideToTheNewEden.WPF.Services.Translation;

/// <summary>翻译上下文的组装结果。</summary>
/// <param name="Context">按时间顺序保留的记录（"原文：… / 译文：…"）。</param>
/// <param name="UsedTokens">保留记录估算占用的 token。</param>
/// <param name="Omitted">因预算被省略的更早记录条数。</param>
public sealed record TranslationContextResult(
    IReadOnlyList<string> Context,
    int UsedTokens,
    int Omitted);

/// <summary>
/// AI 翻译页的上下文组装（阶段 148）：把"原文/译文"记录按 <b>token 预算</b>装进去，
/// **最近优先**——预算不足时省略更早的记录，而不是把整段历史无限塞给模型。
/// <para>
/// 之前是"不限条数、不截断"：一条频道置顶信息就可能 5KB，几十条的长对话会让提示词迅速膨胀。
/// 现在用 <see cref="AssistantTokenEstimator"/> 估算（与助手同一套保守口径），
/// 预算来自「设置 → AI → 翻译 → 上下文预算（token）」；0 = 不限制（老行为）。
/// </para>
/// <para>纯函数：不读设置、不碰界面，探针可离线断言"带哪些、省几条"。</para>
/// </summary>
public static class AiTranslationContextBuilder
{
    /// <summary>组装上下文。</summary>
    /// <param name="entries">按时间顺序排列的上下文文本（每条形如 <c>原文：…\n译文：…</c>）。</param>
    /// <param name="maxTokens">token 预算；<c>&lt;= 0</c> = 不限制。</param>
    public static TranslationContextResult Build(IReadOnlyList<string> entries, int maxTokens)
    {
        if (entries is null || entries.Count == 0)
        {
            return new TranslationContextResult([], 0, 0);
        }

        if (maxTokens <= 0)
        {
            var used = entries.Sum(AssistantTokenEstimator.Estimate);
            return new TranslationContextResult([.. entries], used, 0);
        }

        var chosen = new List<string>();
        var tokens = 0;
        for (var i = entries.Count - 1; i >= 0; i--)
        {
            var cost = AssistantTokenEstimator.Estimate(entries[i]);
            if (tokens + cost > maxTokens)
            {
                break;   // 更早的一律省略（保持"最近的整段上下文"而不是截断最新一条）
            }

            chosen.Insert(0, entries[i]);
            tokens += cost;
        }

        return new TranslationContextResult(chosen, tokens, entries.Count - chosen.Count);
    }
}
