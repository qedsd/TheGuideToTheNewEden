using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TheGuideToTheNewEden.WPF.Services.Translation;

/// <summary>缓存下来的一次 AI 翻译。</summary>
/// <param name="Text">译文。</param>
/// <param name="Meta">元信息（模型/耗时/token）。</param>
/// <param name="CreatedUtc">写入时间。</param>
public sealed record CachedTranslation(string Text, string Meta, DateTime CreatedUtc);

/// <summary>
/// AI 翻译结果缓存：一个键一个文件（<c>Configs/TranslationCache/&lt;key&gt;.json</c>），
/// 与项目里其它缓存（历史订单/建筑订单）同一套做法，无需维护索引文件。
/// <para>
/// 键包含协议/地址/模型/提示词/方向/术语库签名/原文：其中任一项变了缓存自动失效，
/// 因此换模型或更新术语库后不会返回旧译文。文件数超过上限时按写入时间清理最旧的。
/// </para>
/// </summary>
public static class TranslationCache
{
    private const int MaxFiles = 3000;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private static string Folder => Path.Combine(Services.SettingsService.DataPath, "Configs", "TranslationCache");

    /// <summary>是否启用（由设置驱动）。</summary>
    public static bool IsEnabled { get; set; } = true;

    /// <summary>用各要素拼出缓存键（SHA256 前 32 位十六进制）。**方向（源→目标）必须进键**，否则换目标语言会命中旧语言的缓存。</summary>
    public static string BuildKey(string protocol, string baseUrl, string model, string systemPrompt, string from, string to, string glossarySignature, string text)
    {
        var raw = $"{protocol}\n{baseUrl}\n{model}\n{from}->{to}\n{glossarySignature}\n{systemPrompt}\n{text}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash)[..32].ToLowerInvariant();
    }

    public static bool TryGet(string key, out CachedTranslation value)
    {
        value = null!;
        if (!IsEnabled)
        {
            return false;
        }

        try
        {
            var file = Path.Combine(Folder, key + ".json");
            if (!File.Exists(file))
            {
                return false;
            }

            var cached = JsonSerializer.Deserialize<CachedTranslation>(File.ReadAllText(file), JsonOptions);
            if (cached is null || string.IsNullOrWhiteSpace(cached.Text))
            {
                return false;
            }

            value = cached;
            return true;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return false;
        }
    }

    public static void Set(string key, string text, string meta)
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Folder);
            var file = Path.Combine(Folder, key + ".json");
            var temp = file + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(new CachedTranslation(text, meta, DateTime.UtcNow), JsonOptions));
            File.Move(temp, file, overwrite: true);
            TrimIfNeeded();
        }
        catch (Exception ex)
        {
            // 缓存失败绝不能影响翻译本身
            Core.Log.Error(ex);
        }
    }

    /// <summary>清空缓存（设置页按钮）。</summary>
    public static int Clear()
    {
        try
        {
            if (!Directory.Exists(Folder))
            {
                return 0;
            }

            var files = Directory.GetFiles(Folder, "*.json");
            foreach (var file in files)
            {
                File.Delete(file);
            }

            return files.Length;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return 0;
        }
    }

    /// <summary>当前缓存条数（设置页显示）。</summary>
    public static int Count()
    {
        try
        {
            return Directory.Exists(Folder) ? Directory.GetFiles(Folder, "*.json").Length : 0;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return 0;
        }
    }

    public static long SizeInBytes()
    {
        try
        {
            return Directory.Exists(Folder)
                ? Directory.GetFiles(Folder, "*.json").Sum(p => new FileInfo(p).Length)
                : 0;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return 0;
        }
    }

    private static void TrimIfNeeded()
    {
        var files = Directory.GetFiles(Folder, "*.json");
        if (files.Length <= MaxFiles)
        {
            return;
        }

        foreach (var file in files.OrderBy(File.GetLastWriteTimeUtc).Take(files.Length - MaxFiles))
        {
            File.Delete(file);
        }
    }
}
