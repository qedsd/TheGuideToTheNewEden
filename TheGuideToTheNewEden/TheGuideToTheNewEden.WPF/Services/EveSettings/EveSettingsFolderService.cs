using System.IO;
using System.Text.RegularExpressions;
using TheGuideToTheNewEden.WPF.Models.EveSettings;

namespace TheGuideToTheNewEden.WPF.Services.EveSettings;

/// <summary>
/// EVE 设置目录的发现与文件系统操作（只做 IO，不含任何界面决策）。
/// <para>
/// 目录层级：<c>&lt;根&gt;/&lt;服务器目录&gt;/settings_&lt;配置文件名&gt;/core_char_&lt;角色ID&gt;.dat</c>，
/// 与 <c>mintnick/eve-settings-manager</c> 的 <c>folder.ts</c> / <c>server.ts</c> / <c>profile.ts</c> /
/// <c>settings.ts</c> 保持同一套识别规则（另按实机命名做了容错，见 <see cref="GetFiles"/>）。
/// </para>
/// <para>
/// 约定与其它 WPF 服务一致：对外不抛异常，失败时返回 null / 空集合 + <c>Log.Error</c>，
/// 需要给用户看的失败原因通过 <c>out string? error</c> 带出。
/// </para>
/// </summary>
public static class EveSettingsFolderService
{
    /// <summary>配置文件目录前缀（<c>settings_Default</c> → 名称 <c>Default</c>）。</summary>
    public const string ProfilePrefix = "settings_";

    private static readonly Regex CharacterFile = new(@"^core_char_(\d+)\.dat$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AccountFile = new(@"^core_user_(\d+)\.dat$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Windows 文件名非法字符（配置文件名会成为目录名）。</summary>
    private static readonly char[] InvalidNameChars = ['\\', '/', ':', '*', '?', '"', '<', '>', '|'];

    // ---------- 根目录 ----------

    /// <summary>默认根目录：<c>%LOCALAPPDATA%\CCP\EVE</c>。</summary>
    public static string GetDefaultRoot()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CCP",
            "EVE");

    /// <summary>把设置里记的值规整成实际要用的根目录（空 = 默认位置）。</summary>
    public static string NormalizeRoot(string? configured)
        => string.IsNullOrWhiteSpace(configured) ? GetDefaultRoot() : configured!.Trim();

    /// <summary>
    /// 允许用户"选到服务器目录或配置文件目录"，这里上溯回真正的根目录
    /// （与参考实现 <c>folder.ts resolveGameFolder</c> 同思路，另支持直接落在 <c>settings_*</c> 上）。
    /// </summary>
    public static string ResolveRootFromSelection(string selectedPath)
    {
        var path = selectedPath?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(path))
        {
            return GetDefaultRoot();
        }

        var leaf = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (EveSettingsServerCatalog.IsServerDirectoryName(leaf))
        {
            return Path.GetDirectoryName(path) ?? path;
        }

        if (leaf.StartsWith(ProfilePrefix, StringComparison.OrdinalIgnoreCase))
        {
            // <根>/<服务器>/settings_x → 上两级
            var server = Path.GetDirectoryName(path);
            return string.IsNullOrEmpty(server) ? path : Path.GetDirectoryName(server) ?? server;
        }

        return path;
    }

    public static bool Exists(string root)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(root) && Directory.Exists(root);
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return false;
        }
    }

    // ---------- 枚举 ----------

    /// <summary>枚举根目录下的服务器目录（按目录名排序；不是服务器目录的一律忽略）。</summary>
    public static List<EveSettingsServer> GetServers(string root)
    {
        var result = new List<EveSettingsServer>();
        if (!Exists(root))
        {
            return result;
        }

        try
        {
            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                var dirName = Path.GetFileName(dir);
                var rule = EveSettingsServerCatalog.Match(dirName);
                if (rule is null)
                {
                    continue;
                }

                result.Add(new EveSettingsServer
                {
                    DirName = dirName,
                    Path = dir,
                    DisplayName = EveSettingsServerCatalog.BuildDisplayName(dirName, rule),
                    Kind = rule.Kind,
                });
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }

        return result
            .OrderBy(p => p.DirName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>枚举一个服务器目录下的配置文件（<c>settings_*</c> 目录）。</summary>
    public static List<EveSettingsProfile> GetProfiles(string serverPath)
    {
        var result = new List<EveSettingsProfile>();
        try
        {
            if (!Directory.Exists(serverPath))
            {
                return result;
            }

            foreach (var dir in Directory.EnumerateDirectories(serverPath))
            {
                var name = Path.GetFileName(dir);
                if (!name.StartsWith(ProfilePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue; // cache / reports 之类一律忽略
                }

                result.Add(new EveSettingsProfile
                {
                    Name = name[ProfilePrefix.Length..],
                    Path = dir,
                });
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }

        return result
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// 枚举配置文件里的设置文件。
    /// <para>
    /// 命名不符合 <c>core_char_&lt;ID&gt;.dat</c> / <c>core_user_&lt;ID&gt;.dat</c> 的
    /// <c>.dat</c> 归入 <see cref="EveSettingsFileKind.Unrecognized"/>（参考实现会静默丢掉它们，
    /// 这里改为"显示出来但不参与同步"，见 REFACTORING 阶段 103）。
    /// </para>
    /// <para>
    /// 注意<b>不能</b>用 <c>EnumerateFiles("*.dat")</c>：.NET 对三字符扩展名的通配符会连
    /// <c>.datx</c> 这类一起匹配回来，所以这里枚举全部文件再用后缀过滤。
    /// </para>
    /// </summary>
    public static List<EveSettingsFileItem> GetFiles(string profilePath)
    {
        var result = new List<EveSettingsFileItem>();
        try
        {
            if (!Directory.Exists(profilePath))
            {
                return result;
            }

            foreach (var file in Directory.EnumerateFiles(profilePath))
            {
                var fileName = Path.GetFileName(file);
                if (!fileName.EndsWith(".dat", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                FileInfo info;
                try
                {
                    info = new FileInfo(file);
                    if (!info.Exists)
                    {
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    Core.Log.Warn($"[配置同步] 读取文件信息失败：{file} —— {ex.Message}");
                    continue;
                }

                var kind = EveSettingsFileKind.Unrecognized;
                long id = 0;

                var charMatch = CharacterFile.Match(fileName);
                if (charMatch.Success && long.TryParse(charMatch.Groups[1].Value, out var charId) && charId > 0)
                {
                    kind = EveSettingsFileKind.Character;
                    id = charId;
                }
                else
                {
                    var userMatch = AccountFile.Match(fileName);
                    if (userMatch.Success && long.TryParse(userMatch.Groups[1].Value, out var userId) && userId > 0)
                    {
                        kind = EveSettingsFileKind.Account;
                        id = userId;
                    }
                }

                result.Add(new EveSettingsFileItem
                {
                    FileName = fileName,
                    Path = file,
                    Kind = kind,
                    Id = id,
                    Size = info.Length,
                    ModifiedAt = info.LastWriteTime,
                });
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }

        return result
            .OrderBy(p => p.Kind)
            .ThenBy(p => p.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // ---------- 配置文件的增删改 ----------

    public static string ProfileFolder(string serverPath, string name) => Path.Combine(serverPath, ProfilePrefix + name);

    public static bool ProfileExists(string serverPath, string name)
    {
        try
        {
            return Directory.Exists(ProfileFolder(serverPath, name));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return false;
        }
    }

    /// <summary>配置文件名校验（<paramref name="allowExisting"/> = 重命名等允许沿用自己名字的场景）。</summary>
    public static EveSettingsProfileNameError ValidateProfileName(string? name, string serverPath, bool allowExisting = false)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return EveSettingsProfileNameError.Empty;
        }

        var trimmed = name.Trim();
        if (trimmed.IndexOfAny(InvalidNameChars) >= 0 || trimmed.EndsWith('.'))
        {
            return EveSettingsProfileNameError.InvalidChars;
        }

        if (!allowExisting && ProfileExists(serverPath, trimmed))
        {
            return EveSettingsProfileNameError.Exists;
        }

        return EveSettingsProfileNameError.None;
    }

    /// <summary>新建配置文件目录。</summary>
    public static EveSettingsProfile? CreateProfile(string serverPath, string name, out string? error)
    {
        error = null;
        try
        {
            var path = ProfileFolder(serverPath, name.Trim());
            Directory.CreateDirectory(path);
            return new EveSettingsProfile { Name = name.Trim(), Path = path };
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            error = ex.Message;
            return null;
        }
    }

    /// <summary>重命名配置文件目录。</summary>
    public static EveSettingsProfile? RenameProfile(string serverPath, string oldName, string newName, out string? error)
    {
        error = null;
        try
        {
            var source = ProfileFolder(serverPath, oldName);
            var dest = ProfileFolder(serverPath, newName.Trim());
            Directory.Move(source, dest);
            return new EveSettingsProfile { Name = newName.Trim(), Path = dest };
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            error = ex.Message;
            return null;
        }
    }

    /// <summary>复制配置文件（连同其中所有文件）。</summary>
    public static EveSettingsProfile? DuplicateProfile(string serverPath, string sourceName, string newName, out string? error)
    {
        error = null;
        try
        {
            var source = ProfileFolder(serverPath, sourceName);
            var dest = ProfileFolder(serverPath, newName.Trim());
            CopyDirectory(source, dest, overwrite: false, out var failures);

            if (failures.Count > 0)
            {
                // 目录已经建出来了，但有个别文件没拷过去：如实报错，避免"看着成功、其实少了几个文件"。
                error = string.Join("; ", failures.Take(3));
            }

            return new EveSettingsProfile { Name = newName.Trim(), Path = dest };
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            error = ex.Message;
            return null;
        }
    }

    /// <summary>删除配置文件（连同其中所有文件）。最后一个配置文件不允许删除。</summary>
    public static bool DeleteProfile(string serverPath, string name, out string? error)
    {
        error = null;
        try
        {
            if (GetProfiles(serverPath).Count <= 1)
            {
                error = "only-one-profile";
                return false;
            }

            Directory.Delete(ProfileFolder(serverPath, name), recursive: true);
            return true;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            error = ex.Message;
            return false;
        }
    }

    /// <summary>递归复制目录（逐文件容错，返回失败清单）。</summary>
    public static void CopyDirectory(string source, string dest, bool overwrite, out List<string> failures)
    {
        failures = [];
        Directory.CreateDirectory(dest);

        foreach (var file in Directory.EnumerateFiles(source))
        {
            var target = Path.Combine(dest, Path.GetFileName(file));
            try
            {
                File.Copy(file, target, overwrite);
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
                failures.Add($"{Path.GetFileName(file)}: {ex.Message}");
            }
        }

        foreach (var dir in Directory.EnumerateDirectories(source))
        {
            CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)), overwrite, out var childFailures);
            failures.AddRange(childFailures);
        }
    }
}
