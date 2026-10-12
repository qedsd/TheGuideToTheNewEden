using TheGuideToTheNewEden.WPF.Services.Characters;

namespace TheGuideToTheNewEden.WPF.Services.Home;

/// <summary>钱包卡的时间区间（数值 = 天数，<see cref="All"/> 表示"尽可能全"）。</summary>
public enum HomeWalletRange
{
    /// <summary>近 7 天（默认，最省流量）。</summary>
    Days7 = 7,

    /// <summary>近 30 天。</summary>
    Days30 = 30,

    /// <summary>全部（受每角色翻页上限约束，超出会标记截断）。</summary>
    All = 0,
}

/// <summary>收支构成里的一项（<see cref="NameKey"/> 是本地化键，语言切换时重新取文本）。</summary>
public sealed class HomeWalletSlice
{
    public required string NameKey { get; init; }

    public double Amount { get; init; }

    public double Percent { get; init; }
}

/// <summary>主页钱包卡的汇总结果。</summary>
public sealed class HomeWalletSummary
{
    /// <summary>参与统计的角色数（令牌失效/取数失败的不计入）。</summary>
    public int CharacterCount { get; init; }

    /// <summary>取数失败（含令牌失效）的角色数。</summary>
    public int FailedCount { get; init; }

    /// <summary>所有角色钱包余额之和。</summary>
    public double Balance { get; init; }

    public double Income { get; init; }

    public double Outcome { get; init; }

    /// <summary>收入 / 支出构成（按金额降序，已剔除 0 项）。</summary>
    public IReadOnlyList<HomeWalletSlice> IncomeSlices { get; init; } = [];

    public IReadOnlyList<HomeWalletSlice> OutcomeSlices { get; init; } = [];

    /// <summary>是否因为翻页上限而只统计了部分流水（卡上会提示）。</summary>
    public bool Truncated { get; init; }

    public DateTime UpdatedAt { get; init; } = DateTime.Now;
}

/// <summary>
/// 主页钱包卡的取数：逐角色余额求和 + 按 <c>ref_type</c> 归类的收支构成。
/// <para>
/// <b>与 WinUI 版最重要的差异</b>：WinUI 是"所有角色 × 全部分页"的**静默全量**拉取（几百次 ESI 请求），
/// 这里改为**有界**取数——按区间翻页、每角色最多 <see cref="MaxPagesPerCharacter"/> 页、
/// 结果缓存 <see cref="CacheTtl"/>，并且逐角色容错（某个角色失败不影响其余角色）。
/// </para>
/// <para>
/// 分类口径逐条对齐 WinUI 的 <c>WalletStatistic</c>：税/费/中介费归"税"、市场归"贸易"、
/// agent/mission 归"任务"、bounty 归"赏金"、contract 归"合同"、player_donation 归"转账"，其余归"其他"。
/// </para>
/// </summary>
public static class HomeWalletSummaryService
{
    /// <summary>每角色最多翻多少页（ESI 每页 50 条 → 400 条）。</summary>
    private const int MaxPagesPerCharacter = 8;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private static readonly Dictionary<HomeWalletRange, (DateTime At, HomeWalletSummary Data)> Cache = [];

    private static readonly string[] IncomeOrder =
    [
        "Home.Wallet.Type.Bounty", "Home.Wallet.Type.Market", "Home.Wallet.Type.Contract",
        "Home.Wallet.Type.Mission", "Home.Wallet.Type.Donation", "Home.Wallet.Type.Other",
    ];

    /// <summary>
    /// 取汇总。<paramref name="forceRefresh"/> 为 true 时跳过内存缓存。
    /// 返回 null 表示<b>一个角色都没有</b>（页面据此显示引导），
    /// 而"有角色但全部取数失败"返回的是 <see cref="HomeWalletSummary.FailedCount"/> = 角色数的对象。
    /// </summary>
    public static async Task<HomeWalletSummary?> GetAsync(
        HomeWalletRange range, bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        var characters = CharacterStore.Characters.ToList();
        if (characters.Count == 0)
        {
            Cache.Remove(range);
            return null;
        }

        if (!forceRefresh && Cache.TryGetValue(range, out var cached)
            && DateTime.Now - cached.At < CacheTtl)
        {
            return cached.Data;
        }

        var dayLimit = range == HomeWalletRange.All ? 0 : (int)range;
        var cutoff = dayLimit > 0 ? DateTime.UtcNow.AddDays(-dayLimit) : DateTime.MinValue;

        var balance = 0d;
        var failed = 0;
        var truncated = false;
        var income = new Dictionary<string, double>(StringComparer.Ordinal);
        var outcome = new Dictionary<string, double>(StringComparer.Ordinal);
        var ok = 0;

        foreach (var character in characters)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var context = new CharacterContext(character);
                var rows = await LoadJournalAsync(context, cutoff, dayLimit, cancellationToken);
                if (rows is null)
                {
                    failed++;
                    continue;
                }

                balance += await CharacterWalletService.GetBalanceAsync(context);
                ok++;

                if (rows.Count >= MaxPagesPerCharacter * 50)
                {
                    truncated = true;
                }

                foreach (var row in rows)
                {
                    if (row.Amount == 0)
                    {
                        continue;
                    }

                    var key = Classify(row.RefType, row.Amount > 0);
                    var target = row.Amount > 0 ? income : outcome;
                    target[key] = target.GetValueOrDefault(key) + Math.Abs(row.Amount);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Core.Log.Error($"[主页] 钱包统计取数失败（{character.CharacterName}）：{ex.Message}");
                failed++;
            }
        }

        var totalIncome = income.Values.Sum();
        var totalOutcome = outcome.Values.Sum();

        var summary = new HomeWalletSummary
        {
            CharacterCount = ok,
            FailedCount = failed,
            Balance = balance,
            Income = totalIncome,
            Outcome = totalOutcome,
            IncomeSlices = BuildSlices(income, totalIncome),
            OutcomeSlices = BuildSlices(outcome, totalOutcome),
            Truncated = truncated,
        };

        Cache[range] = (DateTime.Now, summary);
        return summary;
    }

    /// <summary>清掉缓存（设置页"刷新"或角色变化时用）。</summary>
    public static void Invalidate() => Cache.Clear();

    /// <summary>
    /// 读某角色的钱包流水：按区间翻页，最多 <see cref="MaxPagesPerCharacter"/> 页。
    /// 返回 null 表示这一页取数失败（该角色整体放弃），返回空列表表示"这个区间内没有流水"。
    /// </summary>
    private static async Task<List<WalletJournalRow>?> LoadJournalAsync(
        CharacterContext context, DateTime cutoff, int dayLimit, CancellationToken cancellationToken)
    {
        var rows = new List<WalletJournalRow>();

        for (var page = 1; page <= MaxPagesPerCharacter; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await CharacterWalletService.GetCharacterJournalAsync(context, page);
            if (result?.Items is not { Count: > 0 } items)
            {
                // 第一页就取不到 → 视为该角色失败；翻到后面取不到 → 视为已到末尾。
                return page == 1 ? null : rows;
            }

            rows.AddRange(items);

            if (dayLimit <= 0)
            {
                continue;
            }

            // 流水按时间倒序，本页已经出现早于区间的行就说明后面都不用翻了。
            if (items.Min(p => p.Date) < cutoff)
            {
                break;
            }
        }

        return dayLimit > 0 ? rows.Where(p => p.Date >= cutoff).ToList() : rows;
    }

    private static List<HomeWalletSlice> BuildSlices(Dictionary<string, double> values, double total)
    {
        if (total <= 0)
        {
            return [];
        }

        return values
            .Where(p => p.Value > 0)
            .OrderByDescending(p => p.Value)
            .ThenBy(p => Array.IndexOf(IncomeOrder, p.Key))
            .Select(p => new HomeWalletSlice
            {
                NameKey = p.Key,
                Amount = p.Value,
                Percent = p.Value / total * 100,
            })
            .ToList();
    }

    /// <summary>按 <c>ref_type</c> 归类（口径与 WinUI 的 WalletStatistic 逐条一致）。</summary>
    private static string Classify(string? refType, bool positive)
    {
        var type = (refType ?? string.Empty).ToLowerInvariant();

        if (type == "player_donation")
        {
            return "Home.Wallet.Type.Donation";
        }

        if (type.Contains("tax") || type.Contains("fee") || type == "player_trading")
        {
            // 税费几乎只会出现在支出侧；若出现正向（如退税），并入"其他"。
            return positive ? "Home.Wallet.Type.Other" : "Home.Wallet.Type.Tax";
        }

        if (type.Contains("market") || type.Contains("store"))
        {
            return "Home.Wallet.Type.Market";
        }

        if (type.Contains("agent") || type.Contains("mission"))
        {
            return positive ? "Home.Wallet.Type.Mission" : "Home.Wallet.Type.Other";
        }

        if (type.Contains("bounty"))
        {
            return positive ? "Home.Wallet.Type.Bounty" : "Home.Wallet.Type.Other";
        }

        if (type.Contains("contract"))
        {
            return "Home.Wallet.Type.Contract";
        }

        return "Home.Wallet.Type.Other";
    }
}
