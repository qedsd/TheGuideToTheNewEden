using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using TheGuideToTheNewEden.Core.Services;
using TheGuideToTheNewEden.Core.Services.DB;
using TheGuideToTheNewEden.WPF.Services.Characters;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Esi;

/// <summary>
/// ESI 响应的"ID → 名称"富化：把响应里**能认出来的 ID 字段**就地换成 <c>名称 (id)</c>。
/// <para>
/// 为什么需要它：ESI 的返回值大量是裸 ID —— 钱包流水是 <c>first_party_id</c> / <c>second_party_id</c>、
/// 联系人是 <c>contact_id</c>、声望是 <c>from_id</c>、勋章与 LP 是 <c>corporation_id</c> / <c>issuer_id</c>、
/// 资产与市场订单是 <c>type_id</c> / <c>location_id</c> / <c>system_id</c>。
/// 不富化的话，"让 AI 查任意 ESI"得到的会是"你监视了 1000125、在 30000142 有 645 件"这类没法读的答案。
/// 早先这些富化写在 <c>CharacterEsiExtraService</c>（只覆盖 7 个角色端点，912 行）；阶段 119 把它下沉成
/// **通用后处理**，一次覆盖全部 197 个端点，那 7 个工具连同服务一并删除。
/// </para>
/// <para>
/// 做法：解析 JSON → 按字段名收集候选（值 + 写入回调）→ 按类别**批量**查本地库（物品/星系/星域/空间站走 SDE；
/// 角色/军团/联盟/势力/地点走 <see cref="LocationNameResolver"/>，本地库优先、必要时一次 ESI /universe/names）→
/// 就地改写成字符串。上限见 <see cref="MaxIdsPerResponse"/>（防御"一个响应上百个不同 ID"）。
/// 解析不到的名字**保持原值**（不写空、不写错），结构 ID 超 <see cref="int.MaxValue"/> 时只查本地结构列表。
/// </para>
/// </summary>
public static class EsiResponseEnricher
{
    /// <summary>单次响应最多解析多少个不同 ID（超出部分保持原值，并在结果里注明）。</summary>
    public const int MaxIdsPerResponse = 300;

    /// <summary>超过这个体积的响应不做富化（避免把大响应整体过一次 JsonNode）。</summary>
    private const long MaxJsonBytes = 4 * 1024 * 1024;

    /// <summary>ID 类别 → 用哪种批量解析。</summary>
    private enum IdKind
    {
        /// <summary>物品类型（本地 SDE，带本地化）。</summary>
        Type,

        /// <summary>星系（本地 SDE）。</summary>
        System,

        /// <summary>星域（本地 SDE）。</summary>
        Region,

        /// <summary>空间站（本地 SDE）。</summary>
        Station,

        /// <summary>多态 ID（角色/军团/联盟/势力/星系/空间站/结构）：<see cref="LocationNameResolver"/> 分流。</summary>
        Name,
    }

    private static readonly Dictionary<string, IdKind> FieldMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // 物品类型
        ["type_id"] = IdKind.Type,
        ["type_ids"] = IdKind.Type,
        ["blueprint_type_id"] = IdKind.Type,
        ["product_type_id"] = IdKind.Type,
        ["material_type_id"] = IdKind.Type,
        ["item_type_id"] = IdKind.Type,
        ["ship_type_id"] = IdKind.Type,
        ["skill_id"] = IdKind.Type,

        // 星系 / 星域 / 空间站
        ["solar_system_id"] = IdKind.System,
        ["system_id"] = IdKind.System,
        ["system_ids"] = IdKind.System,
        ["region_id"] = IdKind.Region,
        ["region_ids"] = IdKind.Region,
        ["station_id"] = IdKind.Station,
        ["station_ids"] = IdKind.Station,

        // 多态（角色 / 军团 / 联盟 / 势力 / 地点 / 结构）
        ["character_id"] = IdKind.Name,
        ["character_ids"] = IdKind.Name,
        ["corporation_id"] = IdKind.Name,
        ["corporation_ids"] = IdKind.Name,
        ["alliance_id"] = IdKind.Name,
        ["alliance_ids"] = IdKind.Name,
        ["faction_id"] = IdKind.Name,
        ["contact_id"] = IdKind.Name,
        ["contact_ids"] = IdKind.Name,
        ["from"] = IdKind.Name,
        ["from_id"] = IdKind.Name,
        ["to_id"] = IdKind.Name,
        ["issuer_id"] = IdKind.Name,
        ["member_id"] = IdKind.Name,
        ["members"] = IdKind.Name,
        ["sender_id"] = IdKind.Name,
        ["recipient_id"] = IdKind.Name,
        ["recipient_ids"] = IdKind.Name,
        ["creator_id"] = IdKind.Name,
        ["first_party_id"] = IdKind.Name,
        ["second_party_id"] = IdKind.Name,
        ["tax_receiver_id"] = IdKind.Name,
        ["context_id"] = IdKind.Name,
        ["location_id"] = IdKind.Name,
        ["location_ids"] = IdKind.Name,
        ["start_location_id"] = IdKind.Name,
        ["end_location_id"] = IdKind.Name,
        ["facility_id"] = IdKind.Name,
        ["destination_id"] = IdKind.Name,
        ["structure_id"] = IdKind.Name,
        ["structure_ids"] = IdKind.Name,
    };

    /// <summary>
    /// 该字段名是不是"ID 类字段"（富化映射表里的名字，或 <c>_id</c> / <c>_ids</c> 结尾）。
    /// <para>
    /// 网关用它决定"调用参数里出现了富化后的文本要不要还原成 ID"——见 <see cref="UnwrapId"/>。
    /// </para>
    /// </summary>
    public static bool IsIdField(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return FieldMap.ContainsKey(name)
               || name.EndsWith("_id", StringComparison.OrdinalIgnoreCase)
               || name.EndsWith("_ids", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 从富化后的文本里取回 ID：<c>吉他 (30000142)</c> → <c>30000142</c>；不是这个形状返回 null。
    /// <para>
    /// **为什么需要**：富化把值改成了字符串，模型很可能把看到的整个值当参数传回来
    /// （例如 <c>path: {"region_id": "伏尔戈 (10000002)"}</c>）。不还原就会拼出
    /// <c>/markets/%E4%BC%8F%E5%B0%94%E6%88%88%20(10000002)/orders</c> 这种必然失败的 URL。
    /// </para>
    /// </summary>
    public static string? UnwrapId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var match = System.Text.RegularExpressions.Regex.Match(
            value.Trim(), @"^.+?\(\s*(\d{1,18})\s*\)$",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>请求体里的 ID 字段若是富化文本，就地还原成数字（写好工具收到模型回传的富化值时用）。</summary>
    public static string UnwrapIdsInJson(string? json)
    {
        var text = json ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text) || text[0] is not ('{' or '['))
        {
            return text;
        }

        try
        {
            var root = JsonNode.Parse(text);
            if (root is null)
            {
                return text;
            }

            UnwrapNode(root);
            return root.ToJsonString(JsonOptions);
        }
        catch (JsonException)
        {
            return text;
        }
    }

    private static void UnwrapNode(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var property in obj.ToList())
                {
                    if (!IsIdField(property.Key))
                    {
                        UnwrapNode(property.Value);
                        continue;
                    }

                    if (property.Value is JsonArray array)
                    {
                        for (var i = 0; i < array.Count; i++)
                        {
                            if (ToId(array[i]) is { } id)
                            {
                                array[i] = JsonValue.Create(id);
                            }
                        }

                        continue;
                    }

                    if (ToId(property.Value) is { } scalarId)
                    {
                        obj[property.Key] = JsonValue.Create(scalarId);
                    }
                }

                break;

            case JsonArray array:
                foreach (var item in array)
                {
                    UnwrapNode(item);
                }

                break;
        }
    }

    /// <summary>富化文本 → 数字；不是该形状返回 null（保持原值）。</summary>
    private static long? ToId(JsonNode? node)
    {
        if (node is not JsonValue value || !value.TryGetValue(out string? text) || text is null)
        {
            return null;
        }

        return UnwrapId(text) is { } digits && long.TryParse(digits, out var id) ? id : null;
    }

    /// <summary>富化结果。</summary>
    /// <param name="Json">（可能被改写过的）JSON。</param>
    /// <param name="Resolved">成功换成"名称 (id)"的字段数。</param>
    /// <param name="Unresolved">认出来但查不到名字、保持原值的字段数。</param>
    /// <param name="Truncated">是否因为 ID 数超过上限而跳过了部分解析。</param>
    public sealed record EnrichResult(string Json, int Resolved, int Unresolved, bool Truncated);

    /// <summary>就地富化（失败/不适用时原样返回，绝不抛异常）。</summary>
    public static async Task<EnrichResult> EnrichAsync(string? json, CancellationToken ct)
    {
        var text = json ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text) || text.Length > MaxJsonBytes || text[0] is not ('{' or '['))
        {
            return new EnrichResult(text, 0, 0, false);
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return new EnrichResult(text, 0, 0, false);
        }

        if (root is null)
        {
            return new EnrichResult(text, 0, 0, false);
        }

        var candidates = new List<Candidate>();
        Walk(root, null, candidates);
        if (candidates.Count == 0)
        {
            return new EnrichResult(text, 0, 0, false);
        }

        // 按类别收集唯一 ID（全局上限，防止一个响应里上百个不同 ID 把本地库/ESI 打爆）
        var byKind = new Dictionary<IdKind, HashSet<long>>();
        var truncated = false;
        var accepted = 0;
        var distinct = new HashSet<(IdKind, long)>();
        foreach (var candidate in candidates)
        {
            if (!distinct.Add((candidate.Kind, candidate.Value)))
            {
                continue;
            }

            if (accepted++ >= MaxIdsPerResponse)
            {
                truncated = true;
                continue;
            }

            if (!byKind.TryGetValue(candidate.Kind, out var set))
            {
                set = [];
                byKind[candidate.Kind] = set;
            }

            set.Add(candidate.Value);
        }

        var names = new Dictionary<(IdKind, long), string>();
        foreach (var (kind, ids) in byKind)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                foreach (var pair in await ResolveAsync(kind, ids).ConfigureAwait(false))
                {
                    if (!string.IsNullOrWhiteSpace(pair.Value))
                    {
                        names[(kind, pair.Key)] = pair.Value;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // 富化是"锦上添花"：某一类解析失败不该让整次调用失败
                Core.Log.Error(ex);
            }
        }

        var resolved = 0;
        var unresolved = 0;
        foreach (var candidate in candidates)
        {
            if (names.TryGetValue((candidate.Kind, candidate.Value), out var name))
            {
                candidate.Apply($"{name} ({candidate.Value})");
                resolved++;
            }
            else if (distinct.Contains((candidate.Kind, candidate.Value)))
            {
                unresolved++;
            }
        }

        if (resolved == 0)
        {
            return new EnrichResult(text, 0, unresolved, truncated);
        }

        return new EnrichResult(root.ToJsonString(JsonOptions), resolved, unresolved, truncated);
    }

    /// <summary>序列化选项：中文原样输出（不转 \uXXXX），结果更省 token。</summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static async Task<Dictionary<long, string>> ResolveAsync(IdKind kind, HashSet<long> ids)
    {
        var result = new Dictionary<long, string>();

        if (kind == IdKind.Name)
        {
            foreach (var pair in await LocationNameResolver.ResolveAsync(ids))
            {
                result[pair.Key] = pair.Value;
            }

            return result;
        }

        var safe = ids.Where(id => id is > 0 and <= int.MaxValue).Select(id => (int)id).Distinct().ToList();
        if (safe.Count == 0)
        {
            return result;
        }

        switch (kind)
        {
            case IdKind.Type:
                foreach (var type in InvTypeService.QueryTypes(safe))
                {
                    if (!string.IsNullOrWhiteSpace(type.TypeName))
                    {
                        result[type.TypeID] = type.TypeName;
                    }
                }

                break;

            case IdKind.System:
                foreach (var system in MapSolarSystemService.Query(safe))
                {
                    if (!string.IsNullOrWhiteSpace(system.SolarSystemName))
                    {
                        result[system.SolarSystemID] = system.SolarSystemName;
                    }
                }

                break;

            case IdKind.Region:
                foreach (var region in MapRegionService.Query(safe))
                {
                    if (!string.IsNullOrWhiteSpace(region.RegionName))
                    {
                        result[region.RegionID] = region.RegionName;
                    }
                }

                break;

            case IdKind.Station:
                foreach (var station in await StaStationService.QueryAsync(safe))
                {
                    if (!string.IsNullOrWhiteSpace(station.StationName))
                    {
                        result[station.StationID] = station.StationName;
                    }
                }

                break;
        }

        return result;
    }

    /// <summary>遍历 JSON，收集"字段名认得出来 + 值是数字"的候选（含映射字段的纯数字数组）。</summary>
    private static void Walk(JsonNode? node, string? propertyName, List<Candidate> found)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var property in obj.ToList())
                {
                    if (property.Value is null)
                    {
                        continue;
                    }

                    if (property.Value is JsonArray array && FieldMap.TryGetValue(property.Key, out var arrayKind))
                    {
                        CollectArray(array, arrayKind, found);
                        continue;
                    }

                    if (TryReadNumber(property.Value, out var value)
                        && FieldMap.TryGetValue(property.Key, out var kind))
                    {
                        var target = obj;
                        var key = property.Key;
                        found.Add(new Candidate(kind, value, text => target[key] = JsonValue.Create(text)));
                        continue;
                    }

                    Walk(property.Value, property.Key, found);
                }

                break;

            case JsonArray array:
                // 嵌套数组：沿用外层的字段名（如 type_ids: [[1,2]])
                if (propertyName is not null && FieldMap.TryGetValue(propertyName, out var nestedKind))
                {
                    CollectArray(array, nestedKind, found);
                }
                else
                {
                    foreach (var item in array)
                    {
                        Walk(item, propertyName, found);
                    }
                }

                break;
        }
    }

    private static void CollectArray(JsonArray array, IdKind kind, List<Candidate> found)
    {
        for (var i = 0; i < array.Count; i++)
        {
            if (!TryReadNumber(array[i], out var value))
            {
                // 数组里混着对象（如 recipients）→ 继续往里走，靠内层字段名识别
                Walk(array[i], null, found);
                continue;
            }

            var target = array;
            var index = i;
            found.Add(new Candidate(kind, value, text => target[index] = JsonValue.Create(text)));
        }
    }

    private static bool TryReadNumber(JsonNode? node, out long value)
    {
        value = 0;
        if (node is not JsonValue jsonValue)
        {
            return false;
        }

        if (jsonValue.TryGetValue(out long asLong))
        {
            value = asLong;
            return true;
        }

        if (jsonValue.TryGetValue(out int asInt))
        {
            value = asInt;
            return true;
        }

        return false;
    }

    private sealed record Candidate(IdKind Kind, long Value, Action<string> Apply);
}
