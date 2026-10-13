using System.IO;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using TheGuideToTheNewEden.WPF.Models.EveSettings;
using TheGuideToTheNewEden.WPF.Services.Characters;

namespace TheGuideToTheNewEden.WPF.Services.EveSettings;

/// <summary>
/// 角色 ID → 角色名。三级解析，逐级降级、任何一级失败都不影响页面：
/// <list type="number">
///   <item>本应用<b>已授权角色</b>（<see cref="CharacterStore"/>，零网络）；</item>
///   <item>本模块自己的名字缓存 <c>%LocalAppData%\TheGuideToTheNewEden\EveSettings\NameCache.json</c>；</item>
///   <item>按<b>服务器目录</b>选端点的匿名 <c>POST /universe/names/</c>（无需授权、不含个人信息）。</item>
/// </list>
/// <para>
/// <b>为什么不直接用 Core 的 <c>IDNameService</c> / <c>ESIService</c></b>：
/// 那两个都受 <c>Config.DefaultGameServer</c> 影响（单例在构造时固化了服务器与凭据，见 REFACTORING 阶段 14），
/// 而本模块必须"看着国服目录就用国服端点、看着国际服目录就用国际服端点"，
/// 且<b>绝不能</b>因为整理设置文件而触发 <c>CoreInitializer.SwitchGameServer</c>（那会重灌凭据、重置角色列表）。
/// 另外 <c>IDNameService</c> 的 ID 是 <c>int</c>，对超大 ID 有静默截断风险。
/// </para>
/// </summary>
public static class EveSettingsNameResolver
{
    /// <summary>单次请求的 ID 上限（ESI <c>/universe/names/</c> 的硬上限是 1000，留一半余量）。</summary>
    private const int MaxIdsPerRequest = 500;

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    private static readonly object Gate = new();

    private static Dictionary<string, string>? _cache;

    private static string CacheFolder => Path.Combine(SettingsService.DataPath, "EveSettings");

    private static string CacheFile => Path.Combine(CacheFolder, "NameCache.json");

    /// <summary>解析一批角色 ID；返回"能解析到的"映射（解析不到的不会出现在结果里）。</summary>
    public static async Task<Dictionary<long, string>> ResolveAsync(
        IReadOnlyCollection<long> ids,
        EveSettingsServerKind kind,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<long, string>();
        if (ids.Count == 0)
        {
            return result;
        }

        var wanted = ids.Where(p => p > 0).ToHashSet();
        if (wanted.Count == 0)
        {
            return result;
        }

        // ① 本应用已授权角色
        foreach (var character in CharacterStore.Characters)
        {
            if (wanted.Contains(character.CharacterID) && !string.IsNullOrEmpty(character.CharacterName))
            {
                result[character.CharacterID] = character.CharacterName;
            }
        }

        var prefix = BuildPrefix(kind);

        // ② 本地缓存
        var cache = LoadCache();
        var pending = new List<long>();
        foreach (var id in wanted)
        {
            if (result.ContainsKey(id))
            {
                continue;
            }

            if (cache.TryGetValue(prefix + id, out var cached) && !string.IsNullOrEmpty(cached))
            {
                result[id] = cached;
            }
            else
            {
                pending.Add(id);
            }
        }

        if (pending.Count == 0)
        {
            return result;
        }

        // ③ 匿名 ESI（奇点/Duality/雷霆穹顶没有公开端点，直接放弃）
        var endpoint = EveSettingsServerCatalog.ResolveNamesEndpoint(kind);
        if (endpoint is null)
        {
            return result;
        }

        var fetched = new Dictionary<string, string>();
        foreach (var chunk in Chunk(pending, MaxIdsPerRequest))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var content = new StringContent(
                    JsonConvert.SerializeObject(chunk),
                    Encoding.UTF8,
                    "application/json");

                using var response = await Http.PostAsync(endpoint, content, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    Core.Log.Warn($"[配置同步] 角色名解析失败：HTTP {(int)response.StatusCode}（服务器 {kind}）");
                    continue;
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var entries = JsonConvert.DeserializeObject<List<NameEntry>>(json) ?? [];
                foreach (var entry in entries)
                {
                    if (entry.Id <= 0
                        || string.IsNullOrWhiteSpace(entry.Name)
                        || !string.Equals(entry.Category, "character", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    result[entry.Id] = entry.Name!;
                    fetched[prefix + entry.Id] = entry.Name!;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // 解析名字只是锦上添花：失败就退回显示 ID，不影响页面任何功能。
                Core.Log.Warn($"[配置同步] 角色名解析失败：{ex.Message}");
            }
        }

        if (fetched.Count > 0)
        {
            SaveCache(fetched);
        }

        return result;
    }

    /// <summary>缓存键前缀：同一个数字 ID 在不同服务器上是不同的人，必须带上服务器类别。</summary>
    private static string BuildPrefix(EveSettingsServerKind kind) => kind switch
    {
        EveSettingsServerKind.Tranquility => "tq:",
        EveSettingsServerKind.Serenity => "sr:",
        EveSettingsServerKind.Infinity => "inf:",
        _ => "other:",
    };

    private static IEnumerable<List<long>> Chunk(List<long> source, int size)
    {
        for (var i = 0; i < source.Count; i += size)
        {
            yield return source.GetRange(i, Math.Min(size, source.Count - i));
        }
    }

    private static Dictionary<string, string> LoadCache()
    {
        lock (Gate)
        {
            if (_cache is not null)
            {
                return _cache;
            }

            try
            {
                if (File.Exists(CacheFile))
                {
                    var json = File.ReadAllText(CacheFile);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        _cache = JsonConvert.DeserializeObject<Dictionary<string, string>>(json)
                                 ?? new Dictionary<string, string>(StringComparer.Ordinal);
                    }
                }
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
            }

            _cache ??= new Dictionary<string, string>(StringComparer.Ordinal);
            return _cache;
        }
    }

    private static void SaveCache(Dictionary<string, string> fetched)
    {
        lock (Gate)
        {
            try
            {
                var cache = _cache ?? new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var pair in fetched)
                {
                    cache[pair.Key] = pair.Value;
                }

                _cache = cache;
                Directory.CreateDirectory(CacheFolder);
                File.WriteAllText(CacheFile, JsonConvert.SerializeObject(cache, Formatting.Indented));
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
            }
        }
    }

    private sealed class NameEntry
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("category")]
        public string? Category { get; set; }
    }
}
