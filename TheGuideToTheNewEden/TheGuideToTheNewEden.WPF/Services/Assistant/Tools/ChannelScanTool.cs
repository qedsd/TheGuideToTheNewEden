using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Helpers;
using TheGuideToTheNewEden.Core.Models.CharacterScan;
using TheGuideToTheNewEden.Core.Services;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Tools;

/// <summary>
/// 名单统计：把一串角色名（游戏内频道成员列表 Ctrl+C 的粘贴格式）解析成角色 → 军团/联盟归属，
/// 给出人数排行；可选附带 ZKB 威胁值。
/// <para>
/// 复用「频道 → 频道统计」页同一条链路：<see cref="IDNameService.GetByNames"/>（本地 IDName 库优先、ESI 兜底）
/// → ESI <c>Character.AffiliationAsync</c>（1000/批）→ <see cref="CharacterScanInfo.Create"/>。
/// **规模有意收紧**：上限 300 个名字（默认），ZKB 只查最多 30 人（默认 15）——名单越大，ESI 与 ZKB 的
/// 请求数与耗时线性增长，工具是"一问一答"而不是批处理入口（大批量请用页面，页面的 ZKB 上限可自己调）。
/// </para>
/// </summary>
public sealed class ChannelScanTool : ITool
{
    /// <summary>名字数量硬上限（防模型把整份名单丢进来打几千次 ESI）。</summary>
    private const int MaxNames = 300;

    /// <summary>ZKB 查询硬上限。</summary>
    private const int MaxZkb = 30;

    public string Name => "channel_scan";

    public string DisplayNameKey => "AssistantTool_channel_scan";

    public string Description =>
        "统计一份角色名单的军团/联盟归属与人数排行（频道里「Ctrl+C 复制成员列表」的粘贴格式即以换行分隔的角色名）。" +
        "参数 names 是角色全名数组（也可以用换行分隔的整段文本，两种都支持）；withZkb=true 时附带威胁情报" +
        "（危险值 0–100、抱团比例、击杀/损失），适合判断「这帮人是哪个团的、危险吗」，代价是要逐个角色查 zkillboard，明显更慢；" +
        "zkbTop 控制查多少人的 ZKB（默认 15，上限 30）。名单上限 300 人；名字越少数越快。" +
        "注意：只能拿到军团/联盟归属，拿不到在线状态；同名角色极少见但存在时按 ESI 返回的第一个处理。";

    public JsonObject? Parameters => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["names"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "string" },
                ["description"] = "角色全名数组（换行分隔的整段文本也可以，作为单个字符串传入时按行切分）",
            },
            ["withZkb"] = new JsonObject { ["type"] = "boolean", ["description"] = "true = 附带 ZKB 威胁情报（默认 false，明显更慢）" },
            ["zkbTop"] = new JsonObject { ["type"] = "integer", ["description"] = "查多少个角色的 ZKB（默认 15，上限 30）" },
            ["top"] = new JsonObject { ["type"] = "integer", ["description"] = "军团/联盟排行显示条数（默认 10，上限 30）" },
        },
        ["required"] = new JsonArray { "names" },
    };

    public bool RequiresConfirmation => false;

    public bool IsAction => false;

    public async Task<ToolResult> ExecuteAsync(ToolContext ctx, JsonElement args, CancellationToken ct)
    {
        var raw = ReadNames(args);
        var names = SplitNames(raw);
        if (names.Count == 0)
        {
            return new ToolResult("参数 names 不能为空（请给角色全名，多个用数组或换行分隔）。");
        }

        if (names.Count > MaxNames)
        {
            return new ToolResult($"名单 {names.Count} 人超过上限 {MaxNames} 人。请分批统计，或用「频道 → 频道统计」页处理整份名单。");
        }

        var withZkb = ToolArgs.ReadBool(args, "withZkb", false);
        var zkbTop = Math.Clamp(ToolArgs.ReadInt(args, "zkbTop", 15), 1, MaxZkb);
        var top = Math.Clamp(ToolArgs.ReadInt(args, "top", 10), 1, 30);

        ct.ThrowIfCancellationRequested();
        var resolved = await IDNameService.GetByNames(names);
        if (resolved is not { Count: > 0 })
        {
            return new ToolResult($"这 {names.Count} 个名字一个都没解析出来（本地 ID 库与 ESI 都没有）。请确认是完整角色名（不是简称/昵称）。");
        }

        var characters = resolved.Where(p => p.GetCategory() == IdName.CategoryEnum.Character).ToArray();
        if (characters.Length == 0)
        {
            return new ToolResult($"解析出 {resolved.Count} 条记录，但没有一个是角色（可能给的是军团/联盟/物品名）。");
        }

        ct.ThrowIfCancellationRequested();

        // ESI 归属查询（1000/批）——与频道统计页同一接口与步进
        var infos = new List<CharacterScanInfo>(characters.Length);
        var start = 0;
        var length = Math.Min(1000, characters.Length);
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var affiliation = await ESIService.Current.EsiClient.Character.AffiliationAsync(
                characters.Skip(start).Take(length).Select(p => (long)p.Id).ToList());
            if (affiliation?.Model is not { Count: > 0 })
            {
                break;
            }

            var batch = (await Task.Run(() => ThreadHelper.RunAsync(affiliation.Model, a =>
                CharacterScanInfo.Create(a.CharacterId, a.CorporationId, a.AllianceId ?? 0))))
                .Where(p => p is not null)
                .Select(p => p!)
                .ToList();
            infos.AddRange(batch);

            var found = start + length;
            var remain = characters.Length - found;
            if (remain <= 0)
            {
                break;
            }

            // 步进必须用本批长度（found 是"已处理到的位置"，用它做 += 在多批时会跳批漏人）
            start += length;
            length = Math.Min(1000, remain);
        }

        if (infos.Count == 0)
        {
            return new ToolResult($"解析出 {characters.Length} 个角色，但 ESI 归属查询没有返回结果（网络不可用或接口失败）。");
        }

        var builder = new StringBuilder();
        builder.AppendLine($"名单解析：输入 {names.Count} 个名字 → 识别 {infos.Count} 个角色（去掉重复与未识别）");

        AppendStatistics(builder, "军团", infos, p => p.Corporation.Id, p => p.Corporation.Name, top);
        AppendStatistics(builder, "联盟", infos, p => p.Alliance.Id, p => p.Alliance.Name, top);

        if (withZkb)
        {
            ct.ThrowIfCancellationRequested();
            var targets = infos.Take(zkbTop).ToList();
            await Task.Run(() => ThreadHelper.Run(targets, p => p.GetZKBInfo()), ct);

            builder.AppendLine($"ZKB 威胁情报（前 {targets.Count} 人）：");
            foreach (var info in targets
                         .Where(p => p.DangerRatio > 0 || p.ItemDestroyed > 0 || p.ItemLost > 0)
                         .OrderByDescending(p => p.DangerRatio))
            {
                builder.AppendLine($"- {info.Name}｜危险值 {info.DangerRatio}/100｜抱团 {info.GangRatio}%｜" +
                    $"击杀 {info.ItemDestroyed} / 损失 {info.ItemLost}｜{info.Corporation.Name}");
            }

            if (!targets.Any(p => p.DangerRatio > 0 || p.ItemDestroyed > 0 || p.ItemLost > 0))
            {
                builder.AppendLine("- 没有取到任何战绩（ZKB 不可用或这些角色没有公开记录）");
            }
        }

        return new ToolResult(builder.ToString().TrimEnd(),
            ParamSummary: $"{infos.Count} 角色{(withZkb ? $" · ZKB {Math.Min(zkbTop, infos.Count)} 人" : string.Empty)}");
    }

    private static void AppendStatistics(
        StringBuilder builder,
        string label,
        List<CharacterScanInfo> infos,
        Func<CharacterScanInfo, long> keySelector,
        Func<CharacterScanInfo, string> nameSelector,
        int top)
    {
        var groups = infos
            .GroupBy(keySelector)
            .Select(g => (Id: g.Key, Name: nameSelector(g.First()), Count: g.Count()))
            .OrderByDescending(p => p.Count)
            .ToList();

        var named = groups.Where(p => p.Id > 0).ToList();
        var unknown = groups.Where(p => p.Id <= 0).Sum(p => p.Count);

        builder.AppendLine($"{label}分布（{named.Count} 个{label}）：");
        if (named.Count == 0)
        {
            builder.AppendLine($"- 没有识别到{label}归属");
        }
        else
        {
            foreach (var group in named.Take(top))
            {
                builder.AppendLine($"- {group.Name}：{group.Count} 人");
            }

            if (named.Count > top)
            {
                builder.AppendLine($"  （其余 {named.Count - top} 个{label}未列出）");
            }
        }

        if (unknown > 0)
        {
            builder.AppendLine($"- 无{label}：{unknown} 人");
        }
    }

    /// <summary>读 names：标准数组；也接受"整段换行文本"（模型常把粘贴内容直接塞进字符串）。</summary>
    private static string ReadNames(JsonElement args)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty("names", out var value))
        {
            return string.Empty;
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            var builder = new StringBuilder();
            foreach (var item in value.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    builder.AppendLine(item.GetString());
                }
            }

            return builder.ToString();
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
    }

    /// <summary>按任意换行切分并去空（游戏内复制的名单是 LF，手工输入多为 CRLF）。</summary>
    private static List<string> SplitNames(string text)
    {
        var names = new List<string>();
        foreach (var line in (text ?? string.Empty).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var name = line.Trim();
            if (name.Length > 0)
            {
                names.Add(name);
            }
        }

        // 同一名字重复出现没有意义（会重复计入人数统计）
        return names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
