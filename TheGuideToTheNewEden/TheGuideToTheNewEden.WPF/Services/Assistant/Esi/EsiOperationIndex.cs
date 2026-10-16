using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using TheGuideToTheNewEden.Core.Models.Character;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Esi;

/// <summary>
/// ESI 操作索引（<c>Resources/Esi/EsiOperations.json</c>，由 <c>tools/esi/gen_esi_index.py</c>
/// 从 ESI 官方 OpenAPI 规格生成）：197 个操作的 path / 动词 / 参数 / 权限 / 请求体形状。
/// <para>
/// **为什么需要这份索引**：<c>PointyHatGames.EVEStandard</c> 4.1.0 的包内没有 XML 文档、
/// DLL 里也没有路由模板或权限特性（实测 <c>characters/{characterId}/wallet</c> 这类字符串不存在），
/// 反射只能拿到方法名与参数名——而"让模型挑端点、拼参数"必须知道 path / 动词 / scope / 说明，
/// 所以元数据一律取自官方规格，调用则直接走 HTTP（见 <see cref="EsiGateway"/>）。
/// </para>
/// 本类只做"读索引 + 检索 + 权限判定"，不发任何请求（便于离线断言）。
/// </summary>
public static class EsiOperationIndex
{
    /// <summary>索引文件相对输出目录的路径。</summary>
    public const string RelativePath = @"Resources\Esi\EsiOperations.json";

    private static readonly Lazy<EsiIndexFile?> LazyFile = new(Load, LazyThreadSafetyMode.ExecutionAndPublication);
    private static readonly object ScopeCacheLock = new();
    private static readonly Dictionary<long, (string Raw, HashSet<string> Set)> ScopeCache = [];

    /// <summary>索引文件（缺失或解析失败时为 null，调用方给明确提示）。</summary>
    public static EsiIndexFile? File => LazyFile.Value;

    /// <summary>全部操作（索引缺失时为空表）。</summary>
    public static IReadOnlyList<EsiOperation> All => File?.Operations ?? [];

    /// <summary>索引文件的实际路径（诊断用）。</summary>
    public static string IndexPath => Path.Combine(AppContext.BaseDirectory, RelativePath);

    /// <summary>索引是否可用（工具据此给出"索引缺失，请重跑生成脚本"的提示）。</summary>
    public static bool IsReady => File is { Operations.Count: > 0 };

    /// <summary>
    /// 按 operationId 找操作。容错：忽略大小写；也接受 <c>VERB /path</c> 或纯 path；
    /// 还接受"唯一后缀/包含"匹配（模型常写 <c>wallet</c> 这种片段，多命中时返回 null 让它先检索）。
    /// </summary>
    public static EsiOperation? Find(string? key)
    {
        var text = key?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var all = All;
        var exact = all.FirstOrDefault(o => string.Equals(o.Id, text, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        var bySignature = all.FirstOrDefault(o =>
            string.Equals(o.Signature, text, StringComparison.OrdinalIgnoreCase));
        if (bySignature is not null)
        {
            return bySignature;
        }

        if (text.StartsWith('/'))
        {
            var byPath = all.Where(o => string.Equals(o.Path, text, StringComparison.OrdinalIgnoreCase)).ToList();
            return byPath.Count == 1 ? byPath[0] : null;
        }

        // "get /path" 之外的写法：动词 + path 分隔符混用
        var parts = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 2 && parts[1].StartsWith('/'))
        {
            var hit = all.FirstOrDefault(o =>
                string.Equals(o.Verb, parts[0], StringComparison.OrdinalIgnoreCase)
                && string.Equals(o.Path, parts[1], StringComparison.OrdinalIgnoreCase));
            if (hit is not null)
            {
                return hit;
            }
        }

        var fuzzy = all.Where(o => o.Id.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();
        if (fuzzy.Count == 1)
        {
            return fuzzy[0];
        }

        fuzzy = all.Where(o => o.Path.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();
        return fuzzy.Count == 1 ? fuzzy[0] : null;
    }

    /// <summary>
    /// 关键词检索。<paramref name="keyword"/> 按空格分词，**每个词都要命中**（id / 摘要 / 分组 / 路径），
    /// 这样"character wallet"不会退化成上百条；<paramref name="tag"/> 做分组精确过滤。
    /// </summary>
    public static IReadOnlyList<EsiOperation> Search(
        string? keyword,
        string? tag,
        ISet<string>? grantedScopes,
        bool filterByScope,
        int limit)
    {
        var tokens = (keyword ?? string.Empty)
            .Split([' ', ',', '，', ';', '；', '\t', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToLowerInvariant())
            .ToList();

        var results = new List<(int Score, EsiOperation Operation)>();
        foreach (var operation in All)
        {
            if (!string.IsNullOrWhiteSpace(tag)
                && !string.Equals(operation.Tag, tag, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (filterByScope && MissingScopes(operation, grantedScopes).Count > 0)
            {
                continue;
            }

            var score = 0;
            var matchedAll = true;
            foreach (var token in tokens)
            {
                var tokenScore = 0;
                if (operation.Id.ToLowerInvariant().Contains(token))
                {
                    tokenScore += 50;
                }

                if (operation.Path.ToLowerInvariant().Contains(token))
                {
                    tokenScore += 20;
                }

                if (operation.Summary.ToLowerInvariant().Contains(token))
                {
                    tokenScore += 12;
                }

                if ((operation.Tag ?? string.Empty).ToLowerInvariant().Contains(token))
                {
                    tokenScore += 30;
                }

                if (tokenScore == 0)
                {
                    matchedAll = false;
                    break;
                }

                score += tokenScore;
            }

            if (!matchedAll)
            {
                continue;
            }

            if (string.Equals(operation.Id, keyword?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                score += 500;
            }

            results.Add((score, operation));
        }

        return results
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.Operation.Id, StringComparer.Ordinal)
            .Take(Math.Clamp(limit, 1, 60))
            .Select(r => r.Operation)
            .ToList();
    }

    /// <summary>索引里的全部分组（tags），按操作数降序——模型可以先看有哪些领域。</summary>
    public static IReadOnlyList<(string Tag, int Count)> Tags()
        => All.Where(o => !string.IsNullOrWhiteSpace(o.Tag))
            .GroupBy(o => o.Tag!)
            .Select(g => (Tag: g.Key, Count: g.Count()))
            .OrderByDescending(g => g.Count)
            .ThenBy(g => g.Tag, StringComparer.Ordinal)
            .ToList();

    /// <summary>操作缺少的权限（返回空表 = 该角色可以调用；<paramref name="grantedScopes"/> 为 null = 未知，不做过滤）。</summary>
    public static IReadOnlyList<string> MissingScopes(EsiOperation operation, ISet<string>? grantedScopes)
    {
        if (grantedScopes is null || operation.Scopes.Count == 0)
        {
            return [];
        }

        return operation.Scopes.Where(s => !grantedScopes.Contains(s)).ToList();
    }

    /// <summary>角色已授权的 scope 集合（<c>AuthorizedCharacterData.Scopes</c> 是逗号分隔字符串）。</summary>
    /// <returns>null = 该角色没有记录权限（旧令牌），此时**不按权限过滤**，由服务端 403 兜底。</returns>
    public static ISet<string>? GetGrantedScopes(AuthorizedCharacterData? character)
    {
        if (character is null || character.CharacterID <= 0)
        {
            return null;
        }

        lock (ScopeCacheLock)
        {
            var raw = character.Scopes ?? string.Empty;
            if (ScopeCache.TryGetValue(character.CharacterID, out var cached) && cached.Raw == raw)
            {
                return cached.Set.Count == 0 ? null : cached.Set;
            }

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var scope in raw.Split([',', ';', ' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                set.Add(scope);
            }

            ScopeCache[character.CharacterID] = (raw, set);
            return set.Count == 0 ? null : set;
        }
    }

    private static EsiIndexFile? Load()
    {
        try
        {
            var path = IndexPath;
            if (!System.IO.File.Exists(path))
            {
                Core.Log.Error($"未找到 ESI 操作索引：{path}（请运行 tools/esi/gen_esi_index.py 重新生成）");
                return null;
            }

            var json = System.IO.File.ReadAllText(path);
            var file = JsonSerializer.Deserialize<EsiIndexFile>(json, JsonOptions);
            if (file is null || file.Operations.Count == 0)
            {
                Core.Log.Error($"ESI 操作索引为空或无法解析：{path}");
                return null;
            }

            return file;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return null;
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };
}

/// <summary>索引文件根对象（字段名与生成脚本的 JSON 一致）。</summary>
public sealed class EsiIndexFile
{
    public string Source { get; set; } = string.Empty;

    public string GeneratedAt { get; set; } = string.Empty;

    public string? SpecVersion { get; set; }

    public string? SpecTitle { get; set; }

    public int OperationCount { get; set; }

    public List<string> ReadOnlyPosts { get; set; } = [];

    public List<EsiOperation> Operations { get; set; } = [];
}

/// <summary>一个 ESI 操作。</summary>
public sealed class EsiOperation
{
    public string Id { get; set; } = string.Empty;

    public string Verb { get; set; } = "GET";

    public string Path { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string? Tag { get; set; }

    public List<string> Scopes { get; set; } = [];

    /// <summary>是否需要角色授权（规格里有 security 段）。</summary>
    public bool Auth { get; set; }

    /// <summary>是否军团级端点（<c>/corporations/...</c>，通常还需要军团职位）。</summary>
    public bool Corp { get; set; }

    public List<EsiParam> PathParams { get; set; } = [];

    public List<EsiParam> Query { get; set; } = [];

    public EsiBody? Body { get; set; }

    /// <summary>是否支持 <c>Accept-Language</c>（网关按应用语言自动补 zh/en）。</summary>
    public bool Language { get; set; }

    /// <summary>是否有 <c>page</c> 查询参数（分页）。</summary>
    public bool Pageable { get; set; }

    /// <summary>写操作（非 GET，且不是"查询型 POST"白名单）——必须走确认。</summary>
    public bool Write { get; set; }

    /// <summary><c>GET /path</c>，用于展示与检索。</summary>
    [JsonIgnore]
    public string Signature => $"{Verb} {Path}";
}

/// <summary>一个参数（路径参数或查询参数；类型来自规格的参数 schema）。</summary>
public sealed class EsiParam
{
    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = "string";

    public bool Required { get; set; }

    public string? Desc { get; set; }

    public List<string>? Enum { get; set; }

    /// <summary>数组元素类型（<see cref="Type"/> 为 <c>array</c> 时，如 <c>array&lt;object&gt;</c> 的 object）。</summary>
    public string? Item { get; set; }

    /// <summary>规格里的默认值（可能是字符串或数字，原样保留）。</summary>
    public JsonElement? Default { get; set; }

    public int? Min { get; set; }

    public int? Max { get; set; }

    /// <summary>默认值的可读文本（无默认值返回 null）。</summary>
    [JsonIgnore]
    public string? DefaultText => Default is { ValueKind: not JsonValueKind.Undefined and not JsonValueKind.Null } d
        ? d.ValueKind == JsonValueKind.String ? d.GetString() : d.ToString()
        : null;
}

/// <summary>请求体摘要（只保留顶层字段与数组元素字段，见生成脚本的裁剪规则）。</summary>
public sealed class EsiBody
{
    public string Type { get; set; } = "object";

    public bool Required { get; set; }

    /// <summary>数组元素类型（<see cref="Type"/> 为 array 时）。</summary>
    public string? Item { get; set; }

    public string? Desc { get; set; }

    public int? MinItems { get; set; }

    public int? MaxItems { get; set; }

    /// <summary>对象字段（<see cref="Type"/> 为 object 时）。</summary>
    public List<EsiParam>? Props { get; set; }

    /// <summary>数组元素的字段（元素是对象时）。</summary>
    public List<EsiParam>? ItemProps { get; set; }
}
