using System.Collections.Concurrent;
using System.IO;
using Newtonsoft.Json;

namespace TheGuideToTheNewEden.WPF.Services.Characters;

/// <summary>
/// 角色数据缓存：内存 + 磁盘（Configs/CharacterCache/&lt;角色ID&gt;/&lt;键&gt;.json），带过期时间。
/// 目的是减少 ESI 请求与限流风险，并让切页/重启后能立即显示上次数据。
/// </summary>
public static class CharacterCache
{
    private sealed record Entry(object Value, DateTime ExpiresUtc);

    private static readonly ConcurrentDictionary<string, Entry> Memory = new();

    private static string RootFolder => Path.Combine(Services.SettingsService.DataPath, "Configs", "CharacterCache");

    /// <summary>读取缓存；未命中或已过期返回 false。</summary>
    public static bool TryGet<T>(long characterId, string key, out T? value)
    {
        value = default;

        var cacheKey = BuildKey(characterId, key);
        if (Memory.TryGetValue(cacheKey, out var entry))
        {
            if (entry.ExpiresUtc > DateTime.UtcNow)
            {
                value = (T)entry.Value;
                return true;
            }

            Memory.TryRemove(cacheKey, out _);
        }

        var file = BuildFile(characterId, key);
        if (!File.Exists(file))
        {
            return false;
        }

        try
        {
            var wrapper = JsonConvert.DeserializeObject<DiskEntry<T>>(File.ReadAllText(file));
            if (wrapper is null || wrapper.ExpiresUtc <= DateTime.UtcNow)
            {
                return false;
            }

            Memory[cacheKey] = new Entry(wrapper.Value!, wrapper.ExpiresUtc);
            value = wrapper.Value;
            return true;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return false;
        }
    }

    /// <summary>写入缓存（同时写内存与磁盘）。</summary>
    public static void Set<T>(long characterId, string key, T value, TimeSpan ttl)
    {
        var expires = DateTime.UtcNow.Add(ttl);
        Memory[BuildKey(characterId, key)] = new Entry(value!, expires);

        try
        {
            var file = BuildFile(characterId, key);
            var folder = Path.GetDirectoryName(file);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            File.WriteAllText(file, JsonConvert.SerializeObject(new DiskEntry<T> { Value = value, ExpiresUtc = expires }));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    /// <summary>清除某角色的缓存（key 为空则清除该角色全部）。</summary>
    public static void Invalidate(long characterId, string? key = null)
    {
        var prefix = $"{characterId}:";
        foreach (var pair in Memory.Where(p => key is null ? p.Key.StartsWith(prefix, StringComparison.Ordinal) : p.Key == BuildKey(characterId, key)).ToList())
        {
            Memory.TryRemove(pair.Key, out _);
        }

        try
        {
            var folder = Path.Combine(RootFolder, characterId.ToString());
            if (!Directory.Exists(folder))
            {
                return;
            }

            if (key is null)
            {
                Directory.Delete(folder, true);
            }
            else
            {
                var file = BuildFile(characterId, key);
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    private static string BuildKey(long characterId, string key) => $"{characterId}:{key}";

    private static string BuildFile(long characterId, string key) =>
        Path.Combine(RootFolder, characterId.ToString(), $"{key}.json");

    private sealed class DiskEntry<T>
    {
        public T? Value { get; set; }

        public DateTime ExpiresUtc { get; set; }
    }
}