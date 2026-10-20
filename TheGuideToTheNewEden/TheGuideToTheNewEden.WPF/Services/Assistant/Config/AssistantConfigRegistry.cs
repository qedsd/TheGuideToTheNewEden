using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using TheGuideToTheNewEden.WPF.Services;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.ConfigFiles;

/// <summary>
/// AI 可读写的**功能配置文件白名单**（阶段 152）。
/// <para>
/// 安全边界（有意为之，不做"任意文件读写"）：
/// ① 只认这里登记的逻辑名，**不接受路径**；解析后必须仍落在 <c>Configs</c> 目录内；
/// ② 密钥类键（API Key / 令牌 / 密码 / secret）**读取时省略、写入时拒绝**；
/// ③ <c>settings.json</c> 是键值字典：**只能改已存在且非密钥的键**，经 <see cref="SettingsService"/> 写（内存与磁盘一致）；
/// ④ 其它功能文件允许 patch/整体替换，写入前做 JSON 校验、大小上限、密钥扫描，并**先备份、再原子替换**；
/// ⑤ 历史、缓存、授权令牌、<c>ESILicense.txt</c> 等**不在白名单**里，天然不可达。
/// </para>
/// </summary>
public static class AssistantConfigRegistry
{
    /// <summary>单个配置文件的大小上限（防止误写巨大内容）。</summary>
    public const int MaxFileBytes = 256 * 1024;

    /// <summary>单次读取返回的字符上限；超过时只给结构摘要（避免把整个大文件塞进上下文）。</summary>
    public const int MaxReadChars = 24_000;

    /// <summary>密钥类键名（命中即不读、不写）。</summary>
    private static readonly Regex SecretKey = new(
        @"(?i)(api[_-]?key|secret|token|password|passwd|credential|refresh[_-]?token|access[_-]?token)",
        RegexOptions.Compiled);

    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    /// <summary>一个可访问的配置文件。</summary>
    public sealed record Entry(
        string Name,
        string RelativePath,
        string Description,
        bool Writable,
        bool IsSettingsFile = false);

    /// <summary>白名单（顺序即展示顺序）。</summary>
    public static IReadOnlyList<Entry> Entries { get; } =
    [
        new("settings", "settings.json", "应用共用键值设置（只能改已存在的非密钥键）", true, IsSettingsFile: true),
        new("scalper", "ScalperSetting.json", "倒货设置（源/目的市场、价格口径、目标物品、线程数）", true),
        new("appraisal", "AppraisalSetting.json", "估价设置（市场、买卖口径与百分比、历史天数）", true),
        new("gamepreview", "GamePreviewSetting.json", "多开预览设置（窗口样式、颜色、快捷键、名称叠加）", true),
        new("map", "MapSettings.json", "星图设置（图层、配色、显示参数、情报开关）", true),
        new("jumpbridge", "JumpBridgeSetting.json", "跳桥配置（星系对、显示开关）", true),
        new("intel", "IntelSettings.json", "频道预警设置（每角色的频道、关键词、声音、范围）", true),
        new("channelmonitor", "ChannelMonitorSetting.json", "频道监控设置（频道、正则关键词、通知方式）", true),
        new("channelscan", "ChannelScanSetting.json", "频道统计设置（ZKB 开关、上限、忽略名单）", true),
        new("channelmarket", "ChannelMarketSettings.json", "频道查价设置（关键词、分隔符、市场）", true),
        new("channeltranslation", "ChannelTranslationSettings.json", "频道翻译设置（每角色频道、上下文条数、跳过规则）", true),
        new("gamelog", "GameLogInfoSettings.json", "日志监控设置（配置、关键词、判定间隔、通知方式）", true),
        new("glossary", "UserGlossary.json", "用户术语表（英文名 → 中文译名）", true),
        new("marketfavorites", "StaredMarketInvType.json", "市场收藏（物品 TypeId 数组；优先用 market_favorite 工具）", true),
        new("links", "Links.json", "快速链接库（名称、地址、分类）", true),
        new("homeshortcuts", "HomeShortcuts.json", "主页快捷方式（磁贴、顺序、显示策略）", true),
        new("evesettings", "EveSettingsSyncSetting.json", "EVE 客户端配置同步设置", true),
    ];

    /// <summary>配置文件目录（与 <see cref="SettingsService"/> 同一处）。</summary>
    public static string ConfigFolder => Path.Combine(SettingsService.DataPath, "Configs");

    /// <summary>按逻辑名查条目（大小写不敏感）。</summary>
    public static Entry? Find(string? name)
        => Entries.FirstOrDefault(entry => string.Equals(entry.Name, name?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>解析成绝对路径（白名单条目 + 目录内校验，双重保险）。</summary>
    public static string FullPath(Entry entry)
    {
        var root = Path.GetFullPath(ConfigFolder);
        var full = Path.GetFullPath(Path.Combine(root, entry.RelativePath));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(full, root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"配置路径越界：{entry.RelativePath}");
        }

        return full;
    }

    /// <summary>该条目当前是否可写（总开关 + 条目 + 用户拒绝名单）。</summary>
    public static bool IsWritable(Entry entry)
        => entry.Writable
           && AiAssistantSettingService.ConfigWritesAllowed
           && !AiAssistantSettingService.ConfigDeniedFiles.Contains(entry.Name);

    /// <summary>列表（给模型看的紧凑文本）。</summary>
    public static string ListText()
    {
        var builder = new StringBuilder();
        builder.AppendLine("可读写的配置文件（用 name 字段调用 config_read / config_write）：");
        foreach (var entry in Entries)
        {
            var writable = IsWritable(entry) ? "可写" : "只读/已禁用";
            builder.AppendLine($"- {entry.Name}（{entry.RelativePath}，{writable}）：{entry.Description}");
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>读取一个配置；密钥键省略，过大的文件只给结构摘要。</summary>
    public static ConfigOperationResult Read(Entry entry)
    {
        if (entry.IsSettingsFile)
        {
            var node = new JsonObject();
            foreach (var pair in SettingsService.Values.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                if (IsSecretKey(pair.Key))
                {
                    continue;
                }

                node[pair.Key] = pair.Value;
            }

            return Format(node, entry);
        }

        var path = FullPath(entry);
        if (!File.Exists(path))
        {
            return new ConfigOperationResult(true, $"{entry.Name}：文件尚不存在（{entry.RelativePath}）。可用 config_write 创建。");
        }

        try
        {
            var text = File.ReadAllText(path);
            var node = JsonNode.Parse(text);
            RemoveSecrets(node);
            return Format(node, entry);
        }
        catch (Exception ex)
        {
            return new ConfigOperationResult(false, $"{entry.Name}：读取/解析失败（{ex.GetType().Name}: {ex.Message}）。");
        }
    }

    /// <summary>
    /// 写入：<paramref name="patch"/>（顶层键合并）与 <paramref name="content"/>（整体替换）二选一。
    /// </summary>
    public static ConfigOperationResult Write(Entry entry, JsonObject? patch, string? content)
    {
        if (!AiAssistantSettingService.ConfigWritesAllowed)
        {
            return new ConfigOperationResult(false, "配置写入已在设置里关闭（Assistant.Config.Writes=false），本次未执行。");
        }

        if (!IsWritable(entry))
        {
            return new ConfigOperationResult(false, $"{entry.Name} 当前不可写（条目只读或在拒绝名单里）。");
        }

        if (patch is not null && content is not null)
        {
            return new ConfigOperationResult(false, "patch 与 content 只能二选一。");
        }

        if (patch is null && string.IsNullOrWhiteSpace(content))
        {
            return new ConfigOperationResult(false, "需要给出 patch（顶层键）或 content（完整 JSON）。");
        }

        return entry.IsSettingsFile
            ? WriteSettings(entry, patch)
            : WriteJsonFile(entry, patch, content);
    }

    private static ConfigOperationResult WriteSettings(Entry entry, JsonObject? patch)
    {
        if (patch is null)
        {
            return new ConfigOperationResult(false, "settings 只支持 patch（顶层键合并），不支持整体替换。");
        }

        // 先全部校验，再统一落盘：避免"一个键合法、另一个非法"时写出半套改动
        var pending = new List<(string Key, string Value, string? Before)>();
        var unknown = new List<string>();
        var secret = new List<string>();

        foreach (var pair in patch)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                continue;
            }

            if (IsSecretKey(pair.Key))
            {
                secret.Add(pair.Key);
                continue;
            }

            if (!SettingsService.Values.ContainsKey(pair.Key))
            {
                // 只允许"改已存在的键"：避免 AI 往共用设置里塞任意新键
                unknown.Add(pair.Key);
                continue;
            }

            if (!TryConvertSettingsValue(pair.Value, out var value))
            {
                return new ConfigOperationResult(false, $"键 {pair.Key} 的值必须是字符串/数字/布尔（不支持对象或数组）。");
            }

            pending.Add((pair.Key, value, SettingsService.GetValue(pair.Key)));
        }

        if (secret.Count > 0)
        {
            return new ConfigOperationResult(false, $"拒绝写入密钥类键：{string.Join("、", secret)}。");
        }

        if (unknown.Count > 0)
        {
            return new ConfigOperationResult(false,
                $"这些键在 settings.json 里不存在，不能新增：{string.Join("、", unknown)}（只允许修改已有键）。");
        }

        if (pending.Count == 0)
        {
            return new ConfigOperationResult(false, "没有可写入的键。");
        }

        CreateBackup(entry);

        var changes = new List<string>();
        foreach (var (key, value, before) in pending)
        {
            SettingsService.SetValue(key, value);
            changes.Add($"{key}：{Preview(before)} → {Preview(value)}");
        }

        var text = $"已修改 settings.json 的 {changes.Count} 个键：\n" + string.Join("\n", changes.Take(10));
        if (changes.Count > 10)
        {
            text += $"\n…（还有 {changes.Count - 10} 个）";
        }

        text += "\n提示：主题/语言/快捷键等需要界面刷新或重启才完全生效。";
        return new ConfigOperationResult(true, text, $"修改 settings.json：{string.Join("、", changes.Take(5).Select(c => c.Split('：')[0]))}");
    }

    private static ConfigOperationResult WriteJsonFile(Entry entry, JsonObject? patch, string? content)
    {
        var path = FullPath(entry);
        var existed = File.Exists(path);

        JsonNode? existing = null;
        if (existed)
        {
            try
            {
                existing = JsonNode.Parse(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                return new ConfigOperationResult(false, $"{entry.Name}：现有文件不是有效 JSON，拒绝覆盖（{ex.Message}）。");
            }
        }

        if (patch is not null && patch.Any(pair => IsSecretKey(pair.Key)))
        {
            return new ConfigOperationResult(false, "拒绝写入密钥类键（api key / token / secret / password）。");
        }

        JsonNode? result;
        if (content is not null)
        {
            try
            {
                result = JsonNode.Parse(content);
            }
            catch (Exception ex)
            {
                return new ConfigOperationResult(false, $"content 不是有效 JSON（{ex.Message}）。");
            }

            if (result is null)
            {
                return new ConfigOperationResult(false, "content 不能为空。");
            }

            if (ContainsSecretKey(result))
            {
                return new ConfigOperationResult(false, "content 里含密钥类键，拒绝写入。");
            }

            // 顶层类型要一致（存在时），避免把对象写成数组
            if (existing is not null && KindOf(existing) != KindOf(result))
            {
                return new ConfigOperationResult(false,
                    $"顶层类型不一致：现有文件是 {KindOf(existing)}，content 是 {KindOf(result)}。");
            }
        }
        else
        {
            if (existing is JsonArray)
            {
                return new ConfigOperationResult(false, $"{entry.Name} 是数组，patch 不适用；请用 content 整体替换。");
            }

            var target = existing as JsonObject ?? new JsonObject();
            foreach (var pair in patch!)
            {
                if (!string.IsNullOrWhiteSpace(pair.Key))
                {
                    target[pair.Key] = pair.Value?.DeepClone();
                }
            }

            result = target;
        }

        var json = result.ToJsonString(Pretty);
        var bytes = Encoding.UTF8.GetByteCount(json);
        if (bytes > MaxFileBytes)
        {
            return new ConfigOperationResult(false, $"写入内容 {bytes} 字节，超过上限 {MaxFileBytes} 字节。");
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (existed)
            {
                CreateBackup(entry);
            }

            var temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
            File.WriteAllText(temp, json, new UTF8Encoding(false));
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return new ConfigOperationResult(false, $"{entry.Name}：写入失败（{ex.GetType().Name}: {ex.Message}）。");
        }

        var changed = patch is null
            ? "整体替换"
            : string.Join("、", patch.Select(pair => pair.Key));
        return new ConfigOperationResult(
            true,
            $"已写入 {entry.RelativePath}（{bytes} 字节）：{changed}。"
            + (existed ? "旧内容已备份到同名 .bak。" : string.Empty)
            + " 若对应页面已打开，请刷新或重启以生效。",
            $"写入 {entry.Name}：{changed}");
    }

    // ==================================================================
    //  自动备份与回滚（阶段 153）
    // ==================================================================

    /// <summary>每个配置保留的自动备份份数（超出后删最旧的）。</summary>
    public const int MaxBackupsPerConfig = 20;

    /// <summary>自动备份目录（<c>Configs/.backups</c>，不在白名单里、不能通过 config_read 读内容）。</summary>
    public static string BackupFolder => Path.Combine(ConfigFolder, ".backups");

    /// <summary>一份自动备份。</summary>
    public sealed record BackupInfo(string FileName, long SizeBytes);

    /// <summary>列出某个配置的自动备份（新的在前）。</summary>
    public static IReadOnlyList<BackupInfo> GetBackups(Entry entry)
    {
        try
        {
            if (!Directory.Exists(BackupFolder))
            {
                return [];
            }

            var prefix = entry.RelativePath + ".";
            return Directory.GetFiles(BackupFolder, prefix + "*.bak")
                .Select(path => new FileInfo(path))
                .Where(info => info.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(info => info.Name, StringComparer.Ordinal)
                .Select(info => new BackupInfo(info.Name, info.Length))
                .ToList();
        }
        catch (Exception ex)
        {
            Core.Log.Warn($"配置备份：列出 {entry.Name} 的备份失败：{ex.Message}");
            return [];
        }
    }

    /// <summary>
    /// 回滚到某份自动备份（<paramref name="backupFileName"/> 省略 = 最新一份）。
    /// <c>settings.json</c> 回滚后还会重新载入 <see cref="SettingsService.Values"/>。
    /// </summary>
    public static ConfigOperationResult Restore(Entry entry, string? backupFileName)
    {
        if (!AiAssistantSettingService.ConfigWritesAllowed)
        {
            return new ConfigOperationResult(false, "配置写入已在设置里关闭（Assistant.Config.Writes=false），本次未执行。");
        }

        if (!IsWritable(entry))
        {
            return new ConfigOperationResult(false, $"{entry.Name} 当前不可写（条目只读或在拒绝名单里）。");
        }

        var backups = GetBackups(entry);
        if (backups.Count == 0)
        {
            return new ConfigOperationResult(false, $"{entry.Name} 还没有自动备份（第一次写入前不会产生备份）。");
        }

        var target = string.IsNullOrWhiteSpace(backupFileName)
            ? backups[0]
            : backups.FirstOrDefault(info => string.Equals(info.FileName, backupFileName.Trim(), StringComparison.OrdinalIgnoreCase));

        if (target is null)
        {
            return new ConfigOperationResult(false,
                $"找不到备份「{backupFileName}」。可用备份（新的在前）：{string.Join("、", backups.Take(10).Select(info => info.FileName))}");
        }

        // 只允许恢复本配置的备份文件名（不含路径分隔符，GetBackups 已按前缀过滤）
        if (target.FileName.Contains('/') || target.FileName.Contains('\\') || target.FileName.Contains(".."))
        {
            return new ConfigOperationResult(false, "备份标识非法。");
        }

        try
        {
            var source = Path.Combine(BackupFolder, target.FileName);
            File.Copy(source, FullPath(entry), overwrite: true);

            if (entry.IsSettingsFile)
            {
                // 让内存里的键值字典与磁盘一致；其它服务在启动时缓存的值仍需重启
                SettingsService.Initialize();
            }

            return new ConfigOperationResult(
                true,
                $"已把 {entry.Name} 回滚到备份 {target.FileName}（{target.SizeBytes} 字节）。"
                + (entry.IsSettingsFile
                    ? " settings.json 已重新载入；主题/语言/快捷键等启动期设置需重启后完全生效。"
                    : " 若对应页面已打开，请刷新或重启。"),
                $"回滚 {entry.Name} 到 {target.FileName}");
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return new ConfigOperationResult(false, $"{entry.Name}：回滚失败（{ex.GetType().Name}: {ex.Message}）。");
        }
    }

    /// <summary>写盘前把现有内容复制到 <c>.backups</c>（同时保留一份同名 <c>.bak</c>），并按份数剪枝。</summary>
    private static void CreateBackup(Entry entry)
    {
        var path = FullPath(entry);
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(BackupFolder);
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
            var suffix = Guid.NewGuid().ToString("N")[..6];   // 同一毫秒连续写入也不撞名
            var backup = Path.Combine(BackupFolder, $"{entry.RelativePath}.{stamp}-{suffix}.bak");
            File.Copy(path, backup, overwrite: false);

            try
            {
                File.Copy(path, path + ".bak", overwrite: true);
            }
            catch (Exception ex)
            {
                Core.Log.Warn($"配置写入：更新同名 .bak 失败（继续写入）：{ex.Message}");
            }

            PruneBackups(entry.RelativePath);
        }
        catch (Exception ex)
        {
            Core.Log.Warn($"配置写入：备份 {entry.RelativePath} 失败（继续写入）：{ex.Message}");
        }
    }

    private static void PruneBackups(string relativePath)
    {
        try
        {
            var prefix = relativePath + ".";
            var files = Directory.GetFiles(BackupFolder, prefix + "*.bak")
                .OrderByDescending(file => Path.GetFileName(file), StringComparer.Ordinal)
                .ToList();

            foreach (var file in files.Skip(MaxBackupsPerConfig))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    Core.Log.Warn($"配置备份：清理旧备份失败（{Path.GetFileName(file)}）：{ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Core.Log.Warn($"配置备份：剪枝 {relativePath} 失败：{ex.Message}");
        }
    }

    private static ConfigOperationResult Format(JsonNode? node, Entry entry)
    {
        var json = node?.ToJsonString(Pretty) ?? "{}";
        if (json.Length <= MaxReadChars)
        {
            return new ConfigOperationResult(true, $"{entry.Name}（{entry.RelativePath}）：\n{json}");
        }

        var summary = node is JsonObject obj
            ? "对象，顶层键：" + string.Join("、", obj.Select(pair => $"{pair.Key}({KindOf(pair.Value)})").Take(60))
            : node is JsonArray array
                ? $"数组，共 {array.Count} 项，元素类型 {KindOf(array.FirstOrDefault())}"
                : $"标量（{KindOf(node)}）";

        return new ConfigOperationResult(true,
            $"{entry.Name}（{entry.RelativePath}）较大（{json.Length} 字符），已省略内容。{summary}。"
            + "请用 config_write 的 patch 只改需要的顶层键。");
    }

    private static bool TryConvertSettingsValue(JsonNode? node, out string value)
    {
        value = string.Empty;
        switch (node)
        {
            case null:
                return false;
            case JsonValue jsonValue when jsonValue.TryGetValue<string>(out var text):
                value = text;
                return true;
            case JsonValue jsonValue when jsonValue.TryGetValue<bool>(out var flag):
                value = flag ? "true" : "false";
                return true;
            case JsonValue jsonValue when jsonValue.TryGetValue<double>(out var number):
                value = number.ToString(CultureInfo.InvariantCulture);
                return true;
            default:
                return false;
        }
    }

    private static bool IsSecretKey(string key) => SecretKey.IsMatch(key);

    private static bool ContainsSecretKey(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var pair in obj)
                {
                    if (IsSecretKey(pair.Key) || ContainsSecretKey(pair.Value))
                    {
                        return true;
                    }
                }

                return false;
            case JsonArray array:
                return array.Any(ContainsSecretKey);
            default:
                return false;
        }
    }

    private static void RemoveSecrets(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                var secretKeys = obj.Where(pair => IsSecretKey(pair.Key)).Select(pair => pair.Key).ToList();
                foreach (var key in secretKeys)
                {
                    obj.Remove(key);
                }

                foreach (var pair in obj.ToList())
                {
                    RemoveSecrets(pair.Value);
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    RemoveSecrets(item);
                }

                break;
        }
    }

    private static string KindOf(JsonNode? node) => node switch
    {
        JsonObject => "object",
        JsonArray => "array",
        JsonValue value when value.TryGetValue<string>(out _) => "string",
        JsonValue value when value.TryGetValue<bool>(out _) => "bool",
        JsonValue => "number",
        _ => "null",
    };

    private static string Preview(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "(空)";
        }

        return value.Length <= 40 ? value : value[..40] + "…";
    }
}

/// <summary>配置读写的返回（<c>Success</c> 供工具决定要不要附失败语义）。</summary>
public sealed record ConfigOperationResult(bool Success, string Text, string? Summary = null);
