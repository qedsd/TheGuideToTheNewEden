using System.IO;
using System.Text;

namespace TheGuideToTheNewEden.WPF.Services.Assistant.Files;

/// <summary>
/// 阶段 350：AI 读写本地文件的**受限访问层**。
/// <para>
/// 安全模型（与 <c>config_*</c> 工具的“白名单”同一思路，但粒度是目录）：
/// <list type="number">
///   <item><b>根目录白名单</b>：只允许 <see cref="AiAssistantSettingService.FileRoots"/> 下的路径
///         （默认就是应用数据目录，用户可在 设置 → AI → 工具 里追加别的目录）；
///         <c>..</c> 之类靠 <see cref="Path.GetFullPath(string)"/> 归一后做前缀比对，逃不出去。</item>
///   <item><b>敏感文件</b>：令牌类（Auth*.json）与 ESI 授权文件永远拒绝，写入还拒绝可执行文件。</item>
///   <item><b>体积</b>：读上限 256KB / 写上限 200K 字符，超限给“用 offset/limit 或 file_list”的提示，
///         而不是把整份大文件塞进上下文。</item>
///   <item><b>文本</b>：二进制（前 8KB 含 NUL）拒绝；读写统一 UTF-8。</item>
///   <item><b>写入</b>：整体覆盖前把旧内容备份成 <c>.bak</c>，先写临时文件再原子替换——写一半崩不会毁原文件。</item>
/// </list>
/// 所有失败都返回中文原因（模型据此改参数），不抛异常。
/// </para>
/// </summary>
internal static class AssistantFileService
{
    /// <summary>单次读取的最大字节数（超出只提示，不截半份给模型）。</summary>
    internal const int MaxReadBytes = 256 * 1024;

    /// <summary>单次写入的最大字符数（约 200K 字符，足够写日志摘要/导出文本）。</summary>
    internal const int MaxWriteChars = 200 * 1024;

    /// <summary>列表返回的最大条目数。</summary>
    internal const int MaxListEntries = 200;

    /// <summary>无论根目录怎么配都拒绝的敏感文件名。</summary>
    private static readonly HashSet<string> BlockedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "auth.json", "auth_serenity.json", "esilicense.txt", "settings.json",
    };

    /// <summary>拒绝写入的扩展名（可执行/数据库/二进制）。</summary>
    private static readonly HashSet<string> BlockedWriteExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".pdb", ".db", ".sqlite", ".sqlite3", ".msi", ".zip", ".7z", ".rar",
    };

    /// <summary>允许的根目录（空表时退化为应用数据目录）。</summary>
    internal static IReadOnlyList<string> Roots
    {
        get
        {
            var configured = AiAssistantSettingService.FileRoots
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToList();
            if (configured.Count == 0)
            {
                configured.Add(SettingsService.DataPath);
            }

            return configured;
        }
    }

    /// <summary>把用户给的路径解析成绝对路径，并做白名单校验。</summary>
    internal static bool TryResolve(string? input, out string fullPath, out string error)
    {
        fullPath = string.Empty;
        error = string.Empty;

        var raw = input?.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            error = "缺少 path 参数。";
            return false;
        }

        string candidate;
        try
        {
            // 相对路径按第一个根目录解析（方便模型写 "Configs/GamePreviewSetting.json"）
            candidate = Path.IsPathRooted(raw)
                ? Path.GetFullPath(raw)
                : Path.GetFullPath(Path.Combine(Roots[0], raw));
        }
        catch (Exception ex)
        {
            error = "路径无效：" + ex.Message;
            return false;
        }

        if (!IsUnderRoots(candidate))
        {
            error = $"路径不在允许的目录内：{candidate}。允许的根目录：{string.Join("；", Roots)}"
                    + "（可在 设置 → AI → 工具 里追加）。";
            return false;
        }

        var name = Path.GetFileName(candidate);
        if (BlockedNames.Contains(name))
        {
            error = $"拒绝访问敏感文件：{name}（令牌/授权/设置文件不开放给 AI）。";
            return false;
        }

        fullPath = candidate;
        return true;
    }

    private static bool IsUnderRoots(string fullPath)
    {
        foreach (var root in Roots)
        {
            string normalizedRoot;
            try
            {
                normalizedRoot = Path.GetFullPath(root);
            }
            catch
            {
                continue;
            }

            var prefix = normalizedRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                         + Path.DirectorySeparatorChar;
            if (fullPath.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase)
                || fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>列目录。</summary>
    internal static (string Text, string Summary) List(string? path, string? pattern)
    {
        if (!TryResolve(path, out var fullPath, out var error))
        {
            return (error, "列目录失败");
        }

        if (File.Exists(fullPath))
        {
            var info = new FileInfo(fullPath);
            return ($"{fullPath}\n（这是文件，不是目录）大小 {info.Length} 字节，修改于 {info.LastWriteTime:yyyy-MM-dd HH:mm}",
                $"查看文件 {info.Name}");
        }

        if (!Directory.Exists(fullPath))
        {
            return ($"目录不存在：{fullPath}", "列目录失败");
        }

        var searchPattern = string.IsNullOrWhiteSpace(pattern) ? "*" : pattern.Trim();
        List<FileSystemInfo> entries;
        try
        {
            var directory = new DirectoryInfo(fullPath);
            entries = directory.EnumerateFileSystemInfos(searchPattern)
                .OrderByDescending(item => item is DirectoryInfo)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .Take(MaxListEntries + 1)
                .Cast<FileSystemInfo>()
                .ToList();
        }
        catch (Exception ex)
        {
            return ("列目录失败：" + ex.Message, "列目录失败");
        }

        var truncated = entries.Count > MaxListEntries;
        if (truncated)
        {
            entries = entries.Take(MaxListEntries).ToList();
        }

        var builder = new StringBuilder();
        builder.AppendLine($"目录 {fullPath}（{entries.Count} 项{(truncated ? "，已截断" : string.Empty)}）：");
        foreach (var entry in entries)
        {
            if (entry is DirectoryInfo directoryInfo)
            {
                builder.AppendLine($"- [目录] {directoryInfo.Name}");
            }
            else if (entry is FileInfo fileInfo)
            {
                builder.AppendLine($"- {fileInfo.Name}  {fileInfo.Length} 字节  {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm}");
            }
        }

        return (builder.ToString().TrimEnd(), Truncate($"列目录 {Path.GetFileName(fullPath)}", 40, "列目录"));
    }

    /// <summary>读文本文件（可按行取一段）。</summary>
    internal static (string Text, string Summary) Read(string? path, int offsetLine, int maxLines, int maxChars)
    {
        if (!TryResolve(path, out var fullPath, out var error))
        {
            return (error, "读文件失败");
        }

        if (!File.Exists(fullPath))
        {
            return ($"文件不存在：{fullPath}", "读文件失败");
        }

        long length;
        try
        {
            length = new FileInfo(fullPath).Length;
        }
        catch (Exception ex)
        {
            return ("读文件失败：" + ex.Message, "读文件失败");
        }

        if (length > MaxReadBytes)
        {
            return ($"文件过大（{length} 字节，上限 {MaxReadBytes}）：请用 offset/limit 分段读取，"
                    + "或先 file_list 看看有哪些文件。", "读文件失败（过大）");
        }

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(fullPath);
        }
        catch (Exception ex)
        {
            return ("读文件失败：" + ex.Message, "读文件失败");
        }

        // 二进制探测：前 8KB 含 NUL 就当二进制拒绝（不把乱码塞进上下文）
        var probe = Math.Min(bytes.Length, 8 * 1024);
        for (var i = 0; i < probe; i++)
        {
            if (bytes[i] == 0)
            {
                return ($"{Path.GetFileName(fullPath)} 看起来是二进制文件，不读取。", "读文件失败（二进制）");
            }
        }

        var text = DecodeUtf8(bytes);
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var start = Math.Max(1, offsetLine);
        var take = Math.Clamp(maxLines, 1, 2000);
        var slice = lines.Skip(start - 1).Take(take).ToArray();

        var builder = new StringBuilder();
        builder.AppendLine($"文件 {fullPath}（共 {lines.Length} 行，本次第 {start} 行起 {slice.Length} 行）：");
        for (var i = 0; i < slice.Length; i++)
        {
            builder.AppendLine($"{start + i,5}| {slice[i]}");
        }

        var body = ToolArgs.TruncateForModel(builder.ToString().TrimEnd(), Math.Clamp(maxChars, 512, 20000));
        return (body, Truncate($"读 {Path.GetFileName(fullPath)}", 40, "读文件"));
    }

    /// <summary>写文本文件（覆盖前自动备份，append=true 追加）。</summary>
    internal static (string Text, string Summary) Write(string? path, string? content, bool append)
    {
        if (!AiAssistantSettingService.FileWritesAllowed)
        {
            return ("写入已在设置里关闭（Assistant.Files.Writes=false）。", "写文件失败（已禁用）");
        }

        if (!TryResolve(path, out var fullPath, out var error))
        {
            return (error, "写文件失败");
        }

        var extension = Path.GetExtension(fullPath);
        if (BlockedWriteExtensions.Contains(extension))
        {
            return ($"拒绝写入 {extension} 文件（可执行/数据库/压缩包不开放写入）。", "写文件失败（类型受限）");
        }

        var payload = content ?? string.Empty;
        if (payload.Length > MaxWriteChars)
        {
            return ($"内容过大（{payload.Length} 字符，上限 {MaxWriteChars}）。", "写文件失败（过大）");
        }

        if (Directory.Exists(fullPath))
        {
            return ($"{fullPath} 是目录，不能写入。", "写文件失败（是目录）");
        }

        try
        {
            var folder = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var existed = File.Exists(fullPath);
            if (existed)
            {
                // 覆盖前留一份 .bak（追加不动原内容，不备份）
                if (!append)
                {
                    File.Copy(fullPath, fullPath + ".bak", true);
                }
            }

            if (append)
            {
                File.AppendAllText(fullPath, payload, new UTF8Encoding(false));
            }
            else
            {
                // 临时文件 + 原子替换：写一半崩不会毁原文件
                var temp = fullPath + ".tmp";
                File.WriteAllText(temp, payload, new UTF8Encoding(false));
                File.Move(temp, fullPath, true);
            }

            var size = new FileInfo(fullPath).Length;
            var action = append ? "追加" : (existed ? "覆盖" : "新建");
            var backup = existed && !append ? "（旧内容已备份为 .bak）" : string.Empty;
            return ($"已{action} {fullPath}，{payload.Length} 字符{backup}，当前 {size} 字节。",
                Truncate($"{action} {Path.GetFileName(fullPath)}", 40, "写文件"));
        }
        catch (Exception ex)
        {
            return ("写文件失败：" + ex.Message, "写文件失败");
        }
    }

    private static string DecodeUtf8(byte[] bytes)
    {
        try
        {
            using var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8, true);
            return reader.ReadToEnd();
        }
        catch
        {
            return Encoding.UTF8.GetString(bytes);
        }
    }

    private static string Truncate(string text, int max, string fallback)
        => string.IsNullOrWhiteSpace(text) ? fallback : ToolArgs.Truncate(text, max);
}
