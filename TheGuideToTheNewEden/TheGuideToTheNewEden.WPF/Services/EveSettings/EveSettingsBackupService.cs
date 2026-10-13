using System.IO;
using Newtonsoft.Json;
using TheGuideToTheNewEden.WPF.Models.EveSettings;

namespace TheGuideToTheNewEden.WPF.Services.EveSettings;

/// <summary>
/// 备份仓库：把配置文件（整目录）或单个设置文件复制到本应用的备份目录里，并支持还原/删除。
/// <para>
/// 形态与 <c>mintnick/eve-settings-manager</c> 的 <c>electron/main/ipc/backup.ts</c> 一致：
/// 整配置文件备份 = <c>Backups\&lt;名字&gt;\</c>（目录），单文件备份 = <c>Backups\&lt;名字&gt;.dat</c>，
/// 显示名统一记在 <c>Backups\meta.json</c>（<c>名字 → 显示名</c>）。
/// </para>
/// <para>
/// 与参考实现的两点差异：① 备份目录放在本应用数据目录的 <c>EveSettings\Backups</c>
/// （不放 <c>Configs\</c>，避免被当成"设置文件"误清）；② 备份名默认带时间戳，重名自动加序号，
/// 不用弹窗让用户取名（同步前的自动备份尤其需要"零打扰"）。
/// </para>
/// </summary>
public static class EveSettingsBackupService
{
    private const string MetaFileName = "meta.json";

    /// <summary>备份仓库根目录（设置可覆盖）。</summary>
    public static string GetBackupRoot()
    {
        var configured = EveSettingsSettingService.Value.BackupRoot;
        return string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(SettingsService.DataPath, "EveSettings", "Backups")
            : configured.Trim();
    }

    private static string GetMetaFile() => Path.Combine(GetBackupRoot(), MetaFileName);

    /// <summary>列出全部备份（整配置文件目录 + 单文件），按创建时间倒序。</summary>
    public static List<EveSettingsBackupItem> GetBackups()
    {
        var result = new List<EveSettingsBackupItem>();
        var root = GetBackupRoot();

        try
        {
            if (!Directory.Exists(root))
            {
                return result;
            }

            var meta = ReadMeta();

            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                var name = Path.GetFileName(dir);
                var info = new DirectoryInfo(dir);
                var files = SafeEnumerateFiles(dir);
                result.Add(new EveSettingsBackupItem
                {
                    Name = name,
                    Path = dir,
                    IsFolder = true,
                    FileCount = files.Count,
                    TotalBytes = files.Sum(p => p.Length),
                    CreatedAt = info.CreationTime,
                    DisplayName = meta.TryGetValue(name, out var folderMeta) ? folderMeta.DisplayName : null,
                });
            }

            foreach (var file in SafeEnumerateFiles(root))
            {
                if (!file.Name.EndsWith(".dat", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var name = Path.GetFileNameWithoutExtension(file.Name);
                result.Add(new EveSettingsBackupItem
                {
                    Name = name,
                    Path = file.FullName,
                    IsFolder = false,
                    FileCount = 1,
                    TotalBytes = file.Length,
                    CreatedAt = file.CreationTime,
                    DisplayName = meta.TryGetValue(name, out var fileMeta) ? fileMeta.DisplayName : null,
                });
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }

        return result
            .OrderByDescending(p => p.CreatedAt)
            .ToList();
    }

    private static List<FileInfo> SafeEnumerateFiles(string folder)
    {
        try
        {
            return new DirectoryInfo(folder).EnumerateFiles().ToList();
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return [];
        }
    }

    // ---------- 名字 ----------

    /// <summary>整配置文件备份的默认名：时间戳 + 服务器 + 配置文件。</summary>
    public static string BuildFolderBackupName(string serverDirName, string profileName)
        => $"{DateTime.Now:yy.MM.dd_HHmm}_{Sanitize(serverDirName)}_{Sanitize(profileName)}";

    /// <summary>单文件备份的默认名：时间戳 + 服务器 + 配置文件 + 文件名。</summary>
    public static string BuildFileBackupName(string serverDirName, string profileName, string fileName)
        => $"{DateTime.Now:yy.MM.dd_HHmm}_{Sanitize(serverDirName)}_{Sanitize(profileName)}_{Sanitize(Path.GetFileNameWithoutExtension(fileName))}";

    /// <summary>重名时自动加 <c>_2</c>/<c>_3</c>…（备份永不覆盖已有备份）。</summary>
    public static string EnsureUniqueName(string baseName)
    {
        var root = GetBackupRoot();
        var name = baseName;
        var index = 2;

        while (Directory.Exists(Path.Combine(root, name)) || File.Exists(Path.Combine(root, name + ".dat")))
        {
            name = $"{baseName}_{index++}";
        }

        return name;
    }

    /// <summary>把路径里不能做文件名的字符换成下划线。</summary>
    private static string Sanitize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "unknown";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var chars = text.Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        return new string(chars);
    }

    // ---------- 创建 ----------

    /// <summary>备份整个配置文件（目录）里的全部 <c>.dat</c>。</summary>
    public static EveSettingsBackupItem? CreateFolderBackup(string profilePath, string name, string? displayName, out string? error)
    {
        error = null;
        try
        {
            var root = GetBackupRoot();
            var backupPath = Path.Combine(root, name);
            Directory.CreateDirectory(backupPath);

            var files = SafeEnumerateFiles(profilePath)
                .Where(p => p.Name.EndsWith(".dat", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var copied = 0;
            var failures = new List<string>();
            foreach (var file in files)
            {
                if (TryCopy(file.FullName, Path.Combine(backupPath, file.Name), out var copyError))
                {
                    copied++;
                }
                else
                {
                    failures.Add($"{file.Name}: {copyError}");
                }
            }

            if (copied == 0)
            {
                error = failures.Count > 0 ? string.Join("; ", failures.Take(3)) : "no-dat-file";
                return null;
            }

            if (!string.IsNullOrWhiteSpace(displayName))
            {
                var meta = ReadMeta();
                meta[name] = new EveSettingsBackupMeta { DisplayName = displayName };
                WriteMeta(meta);
            }

            return new EveSettingsBackupItem
            {
                Name = name,
                Path = backupPath,
                IsFolder = true,
                FileCount = copied,
                TotalBytes = files.Where(p => p.Name.EndsWith(".dat", StringComparison.OrdinalIgnoreCase)).Sum(p => p.Length),
                CreatedAt = DateTime.Now,
                DisplayName = displayName,
            };
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            error = ex.Message;
            return null;
        }
    }

    /// <summary>备份单个设置文件。</summary>
    public static EveSettingsBackupItem? CreateFileBackup(string sourcePath, string name, string? displayName, out string? error)
    {
        error = null;
        try
        {
            var root = GetBackupRoot();
            Directory.CreateDirectory(root);

            var destPath = Path.Combine(root, name + ".dat");
            if (!TryCopy(sourcePath, destPath, out var copyError))
            {
                error = copyError;
                return null;
            }

            // 记下"原文件名"：还原时必须按它写回配置文件，否则会在档案里留下一个
            // 以备份名命名的 .dat（游戏根本不认，用户还以为还原成功了）。
            var meta = ReadMeta();
            meta[name] = new EveSettingsBackupMeta
            {
                DisplayName = displayName,
                SourceFileName = Path.GetFileName(sourcePath),
            };
            WriteMeta(meta);

            var info = new FileInfo(destPath);
            return new EveSettingsBackupItem
            {
                Name = name,
                Path = destPath,
                IsFolder = false,
                FileCount = 1,
                TotalBytes = info.Exists ? info.Length : 0,
                CreatedAt = DateTime.Now,
                DisplayName = displayName,
            };
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            error = ex.Message;
            return null;
        }
    }

    // ---------- 还原 ----------

    /// <summary>
    /// 把备份还原到当前配置文件。
    /// <para>
    /// 整配置文件备份 → 其中全部 <c>.dat</c> 覆盖进配置文件；单文件备份 → 按文件名覆盖回去。
    /// <b>不删除</b>配置文件里"备份中没有"的文件（只在结果里报数量，避免一次误操作抹掉新角色）。
    /// </para>
    /// </summary>
    public static EveSettingsRestoreResult Restore(EveSettingsBackupItem backup, string profilePath)
    {
        var result = new EveSettingsRestoreResult();
        try
        {
            if (!Directory.Exists(profilePath))
            {
                result.Error = "profile-not-found";
                return result;
            }

            if (backup.IsFolder)
            {
                var files = SafeEnumerateFiles(backup.Path)
                    .Where(p => p.Name.EndsWith(".dat", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (files.Count == 0)
                {
                    result.Error = "backup-empty";
                    return result;
                }

                var names = files.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var file in files)
                {
                    if (TryCopy(file.FullName, Path.Combine(profilePath, file.Name), out var copyError))
                    {
                        result.Copied++;
                    }
                    else
                    {
                        result.Failed++;
                        result.Failures.Add((file.Name, copyError));
                    }
                }

                result.ExtraInProfile = SafeEnumerateFiles(profilePath)
                    .Count(p => p.Name.EndsWith(".dat", StringComparison.OrdinalIgnoreCase) && !names.Contains(p.Name));
            }
            else
            {
                if (!File.Exists(backup.Path))
                {
                    result.Error = "backup-not-found";
                    return result;
                }

                // 按**原文件名**写回（meta 里记着），取不到时才退回备份文件自己的名字。
                var meta = ReadMeta();
                var fileName = meta.TryGetValue(backup.Name, out var fileMeta)
                               && !string.IsNullOrWhiteSpace(fileMeta.SourceFileName)
                    ? fileMeta.SourceFileName!
                    : Path.GetFileName(backup.Path);

                if (TryCopy(backup.Path, Path.Combine(profilePath, fileName), out var copyError))
                {
                    result.Copied++;
                }
                else
                {
                    result.Failed++;
                    result.Failures.Add((fileName, copyError));
                }
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            result.Error = ex.Message;
        }

        return result;
    }

    // ---------- 删除 ----------

    public static bool Delete(EveSettingsBackupItem backup, out string? error)
    {
        error = null;
        try
        {
            if (backup.IsFolder)
            {
                if (Directory.Exists(backup.Path))
                {
                    Directory.Delete(backup.Path, recursive: true);
                }
            }
            else
            {
                if (File.Exists(backup.Path))
                {
                    File.Delete(backup.Path);
                }

                var meta = ReadMeta();
                if (meta.Remove(backup.Name))
                {
                    WriteMeta(meta);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            error = ex.Message;
            return false;
        }
    }

    // ---------- meta.json ----------

    /// <summary>
    /// 备份的附加信息（<c>Backups\meta.json</c>：<c>备份名 → 本对象</c>）。
    /// <para>
    /// <see cref="SourceFileName"/> 只对单文件备份有意义：还原时必须按它写回配置文件，
    /// 否则会把备份文件以自己的名字拷进档案（游戏不认那个文件名，等于没还原）。
    /// </para>
    /// </summary>
    private sealed class EveSettingsBackupMeta
    {
        public string? DisplayName { get; set; }

        public string? SourceFileName { get; set; }
    }

    private static Dictionary<string, EveSettingsBackupMeta> ReadMeta()
    {
        var result = new Dictionary<string, EveSettingsBackupMeta>(StringComparer.Ordinal);

        try
        {
            var file = GetMetaFile();
            if (!File.Exists(file))
            {
                return result;
            }

            var json = File.ReadAllText(file);
            if (string.IsNullOrWhiteSpace(json))
            {
                return result;
            }

            try
            {
                var meta = JsonConvert.DeserializeObject<Dictionary<string, EveSettingsBackupMeta>>(json);
                if (meta is not null)
                {
                    foreach (var pair in meta)
                    {
                        result[pair.Key] = pair.Value ?? new EveSettingsBackupMeta();
                    }

                    return result;
                }
            }
            catch (Exception ex)
            {
                // 兼容"值是纯字符串"的更早格式：只当显示名用。
                Core.Log.Warn($"[配置同步] meta.json 按对象解析失败，尝试旧格式：{ex.Message}");
                var legacy = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                if (legacy is not null)
                {
                    foreach (var pair in legacy)
                    {
                        result[pair.Key] = new EveSettingsBackupMeta { DisplayName = pair.Value };
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }

        return result;
    }

    private static void WriteMeta(Dictionary<string, EveSettingsBackupMeta> meta)
    {
        try
        {
            Directory.CreateDirectory(GetBackupRoot());
            File.WriteAllText(GetMetaFile(), JsonConvert.SerializeObject(meta, Formatting.Indented));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    // ---------- 复制 ----------

    /// <summary>覆盖式复制（重试与修改时间保留见 <see cref="EveSettingsFileCopy"/>）。</summary>
    private static bool TryCopy(string source, string dest, out string error)
        => EveSettingsFileCopy.CopyWithRetry(source, dest, out error);
}
