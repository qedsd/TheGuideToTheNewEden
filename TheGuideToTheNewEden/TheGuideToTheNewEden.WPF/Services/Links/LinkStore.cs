using System.IO;
using System.Text.Json;
using TheGuideToTheNewEden.Core.Models;

namespace TheGuideToTheNewEden.WPF.Services.Links;

/// <summary>
/// 快速链接数据存储：对应 WinUI 版 <c>LinksPage</c> 里的读写逻辑，并补齐导入/导出/恢复默认。
/// 数据文件与 WinUI 版共用 <c>%LocalAppData%\TheGuideToTheNewEden\Configs\Links.json</c>
/// （首次运行从程序目录的 <c>Resources\Configs\Links.json</c> 播种，该文件已被 csproj 链接到输出目录）。
/// </summary>
/// <remarks>
/// 收藏（置顶）不写进 Links.json，而是单独存 <c>settings.json</c> 的 <see cref="FavoriteKey"/>：
/// Core 的 <see cref="LinkInfo"/> 因此保持原样，用户在 WinUI 版读到同一份文件时不会因为多出字段而困惑，
/// 这边也不需要"写回时剔除 WPF 专有字段"这类同步逻辑。
/// </remarks>
public static class LinkStore
{
    /// <summary>收藏链接的 URL 集合所在的设置键。</summary>
    public const string FavoriteKey = "Links.Favorites";

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>写出时缩进：这个文件是给用户手改的（占位符、链接地址），保持可读比省字节重要。</summary>
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    /// <summary>用户数据文件（与 WinUI 版共用同一路径）。</summary>
    public static string FilePath => Path.Combine(SettingsService.DataPath, "Configs", "Links.json");

    /// <summary>随程序分发的默认链接表。</summary>
    public static string DefaultFilePath => Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "Resources", "Configs", "Links.json");

    // ---------- 读写 ----------

    /// <summary>读取用户链接表；文件不存在时先播种，读取失败返回空表（不抛异常，页面照常可用）。</summary>
    public static List<LinkInfo> Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                SeedFromDefault();
            }

            if (!File.Exists(FilePath))
            {
                Core.Log.Error($"快速链接文件不存在：{FilePath}");
                return [];
            }

            var json = File.ReadAllText(FilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                Core.Log.Error($"快速链接文件内容为空：{FilePath}");
                return [];
            }

            return Normalize(JsonSerializer.Deserialize<List<LinkInfo>>(json, ReadOptions));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return [];
        }
    }

    /// <summary>写回用户链接表。</summary>
    public static bool Save(IReadOnlyList<LinkInfo> links)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(links, WriteOptions));
            return true;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return false;
        }
    }

    /// <summary>读取随程序分发的默认链接表（导入/恢复默认用）。</summary>
    public static List<LinkInfo> LoadDefaults()
    {
        try
        {
            if (!File.Exists(DefaultFilePath))
            {
                Core.Log.Error($"默认快速链接文件不存在：{DefaultFilePath}");
                return [];
            }

            return Normalize(JsonSerializer.Deserialize<List<LinkInfo>>(File.ReadAllText(DefaultFilePath), ReadOptions));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return [];
        }
    }

    private static void SeedFromDefault()
    {
        try
        {
            if (!File.Exists(DefaultFilePath))
            {
                Core.Log.Error($"默认快速链接文件不存在，无法初始化：{DefaultFilePath}");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.Copy(DefaultFilePath, FilePath, overwrite: true);
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    /// <summary>
    /// 剔除无效项（名称与链接都为空的行常来自手改文件时的残留），并补齐 null 集合，
    /// 避免后续界面里到处判空。
    /// </summary>
    private static List<LinkInfo> Normalize(List<LinkInfo>? links)
    {
        if (links is null)
        {
            return [];
        }

        var result = new List<LinkInfo>(links.Count);
        foreach (var link in links)
        {
            if (link is null || (string.IsNullOrWhiteSpace(link.Name) && string.IsNullOrWhiteSpace(link.Url)))
            {
                continue;
            }

            link.Name = link.Name?.Trim();
            link.Url = link.Url?.Trim();
            result.Add(link);
        }

        return result;
    }

    // ---------- 导入 / 导出 / 恢复默认 ----------

    /// <summary>
    /// 按 URL 合并（只补没有的，不动已有项），返回新增条数。
    /// 用合并而不是整体替换：用户自己的收藏与手工链接不会因为"导入一份别人的表"而丢失。
    /// </summary>
    public static int Merge(IList<LinkInfo> target, IEnumerable<LinkInfo> incoming)
    {
        var existing = target
            .Select(p => p.Url)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var link in incoming)
        {
            if (string.IsNullOrWhiteSpace(link.Url) || !existing.Add(link.Url))
            {
                continue;
            }

            target.Add(link);
            added++;
        }

        return added;
    }

    /// <summary>从任意 json 文件读入一份链接表（导入用）。格式错误会抛异常，由调用方提示用户。</summary>
    public static List<LinkInfo> ImportFile(string path)
        => Normalize(JsonSerializer.Deserialize<List<LinkInfo>>(File.ReadAllText(path), ReadOptions));

    /// <summary>把链接表写到任意 json 文件（导出用）。</summary>
    public static void ExportFile(string path, IReadOnlyList<LinkInfo> links)
        => File.WriteAllText(path, JsonSerializer.Serialize(links, WriteOptions));

    // ---------- 收藏 ----------

    public static HashSet<string> LoadFavorites()
    {
        var raw = SettingsService.GetValue(FavoriteKey);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var urls = JsonSerializer.Deserialize<List<string>>(raw, ReadOptions) ?? [];
            return new HashSet<string>(urls, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public static void SaveFavorites(IEnumerable<string> urls)
    {
        try
        {
            SettingsService.SetValue(FavoriteKey, JsonSerializer.Serialize(urls.ToList()));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    /// <summary>清掉指向已删除链接的收藏项，避免设置文件里无限累积。</summary>
    public static HashSet<string> PruneFavorites(HashSet<string> favorites, IEnumerable<LinkInfo> links)
    {
        var alive = links
            .Select(p => p.Url)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var removed = favorites.RemoveWhere(p => !alive.Contains(p));
        if (removed > 0)
        {
            SaveFavorites(favorites);
        }

        return favorites;
    }
}
