using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace EVESimulation
{
    /// <summary>
    /// 从 TheGuideToTheNewEden.WPF 的数据目录导入**真实角色名 + 角色 ID**。
    /// <para>
    /// 名称与 ID 分散在几份配置里：
    /// <list type="bullet">
    ///   <item><c>Configs/Auth.json</c>：<c>CharacterID</c> + <c>CharacterName</c>（最可靠）；</item>
    ///   <item><c>Configs/GameLogInfoSettings.json</c>：只有 <c>ListenerID</c>；</item>
    ///   <item><c>Configs/ChannelMonitorSetting.json</c>（Name）、<c>IntelSettings.json</c>（Listener）、
    ///         <c>ChannelMarketSettings.json</c> / <c>ChannelTranslationSettings.json</c>（CharacterName）：只有名字；</item>
    ///   <item><c>Configs/GamePreviewSetting.json</c>：<c>ProcessOrder</c> 里的 <c>EVE - 角色名</c>。</item>
    /// </list>
    /// 只有名字的条目用 <c>Auth.json</c> 或**游戏日志头**（文件名里的角色 ID + <c>收听者:</c>）补齐 ID；
    /// 只有 ID 的条目用同样的映射补名字。两边都凑不齐的跳过（宁缺毋滥，保证"真实名称+ID"）。
    /// </para>
    /// </summary>
    public static class WpfCharacterImporter
    {
        public sealed class ImportedCharacter
        {
            public string Name { get; set; }

            public int Id { get; set; }

            public string Source { get; set; }
        }

        /// <summary>WPF 默认数据目录：优先读 <c>%LocalAppData%\TheGuideToTheNewEden\datapath.json</c> 的 DataPath。</summary>
        public static string DefaultDataPath()
        {
            var baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TheGuideToTheNewEden");
            try
            {
                var pathFile = Path.Combine(baseDir, "datapath.json");
                if (File.Exists(pathFile))
                {
                    var dataPath = JObject.Parse(File.ReadAllText(pathFile)).Value<string>("DataPath");
                    if (!string.IsNullOrWhiteSpace(dataPath) && Directory.Exists(dataPath))
                    {
                        return dataPath;
                    }
                }
            }
            catch
            {
                // 读不到就用默认目录
            }

            return baseDir;
        }

        /// <summary>官方游戏日志目录（用于从日志头解析 名字 ↔ ID）。</summary>
        public static string DefaultLogsPath()
            => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "EVE", "logs");

        /// <summary>
        /// 游戏真实日志目录：优先 WPF「设置 → 游戏日志」里配置的 <c>EVELogsPath</c>，
        /// 没有配置时用「我的文档\EVE\logs」（EVE 客户端默认写这里）。
        /// </summary>
        public static string DetectGameLogsPath(string dataPath = null)
        {
            dataPath ??= DefaultDataPath();
            var candidates = new[]
            {
                Path.Combine(dataPath, "Configs", "settings.json"),
                Path.Combine(dataPath, "settings.json"),
                Path.Combine(dataPath, "settings.wpf.json"),
            };

            foreach (var file in candidates)
            {
                if (!File.Exists(file))
                {
                    continue;
                }

                try
                {
                    var settings = JObject.Parse(File.ReadAllText(file));
                    var logsPath = settings.Value<string>("EVELogsPath");
                    if (!string.IsNullOrWhiteSpace(logsPath) && Directory.Exists(logsPath))
                    {
                        return logsPath;
                    }
                }
                catch
                {
                    // 读不到就用默认目录
                }
            }

            return DefaultLogsPath();
        }

        public static IReadOnlyList<ImportedCharacter> Import(
            string dataPath,
            string logsRoot,
            out string summary)
        {
            var configsFolder = Path.Combine(dataPath ?? string.Empty, "Configs");
            if (!Directory.Exists(configsFolder))
            {
                configsFolder = dataPath;
            }

            var byId = new Dictionary<int, ImportedCharacter>();
            var byName = new Dictionary<string, ImportedCharacter>(StringComparer.OrdinalIgnoreCase);
            var unresolved = 0;

            void Add(string name, int id, string source)
            {
                name = CleanName(name);
                if (string.IsNullOrWhiteSpace(name) || id <= 0)
                {
                    return;
                }

                if (byId.TryGetValue(id, out var existing))
                {
                    // 同一 ID 以真实名字为准（Auth/日志头来源优先）
                    if (!string.Equals(existing.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        existing.Name = name;
                        byName[name] = existing;
                    }
                    return;
                }

                if (byName.TryGetValue(name, out var sameName))
                {
                    // 同一名字不同 ID：保留第一个（真实 ID 一般唯一）
                    if (sameName.Id != id)
                    {
                        return;
                    }
                    return;
                }

                var item = new ImportedCharacter { Name = name, Id = id, Source = source };
                byId[id] = item;
                byName[name] = item;
            }

            // ① Auth.json：名字 + ID
            foreach (var (name, id) in ReadAuth(configsFolder))
            {
                Add(name, id, "Auth.json");
            }

            // ② 游戏日志头：ID ↔ 名字
            var logMap = ReadGameLogHeaders(logsRoot);
            foreach (var (id, name) in logMap)
            {
                Add(name, id, "Gamelogs");
            }

            // ③ GameLogInfoSettings.json：只有 ID，用日志头/Auth 补名字
            foreach (var id in ReadGameLogIds(configsFolder))
            {
                if (byId.ContainsKey(id))
                {
                    continue;
                }

                var name = logMap.TryGetValue(id, out var fromLog) ? fromLog : null;
                if (string.IsNullOrWhiteSpace(name))
                {
                    unresolved++;
                    continue;
                }

                Add(name, id, "GameLogInfoSettings.json");
            }

            // ④ 频道/多开配置：只有名字，用 Auth/日志头补 ID
            var logNameToId = logMap
                .GroupBy(p => p.Value, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Key, StringComparer.OrdinalIgnoreCase);

            foreach (var name in ReadCharacterNames(configsFolder))
            {
                if (byName.ContainsKey(CleanName(name) ?? string.Empty))
                {
                    continue;
                }

                if (logNameToId.TryGetValue(CleanName(name) ?? string.Empty, out var id))
                {
                    Add(name, id, "频道/多开配置");
                }
                else
                {
                    unresolved++;
                }
            }

            summary = $"导入 {byId.Count} 个角色（Auth {byId.Values.Count(p => p.Source == "Auth.json")} / " +
                      $"日志头 {byId.Values.Count(p => p.Source == "Gamelogs")} / " +
                      $"其它配置 {byId.Values.Count(p => p.Source is "GameLogInfoSettings.json" or "频道/多开配置")}），" +
                      $"未能补齐名称或 ID 的条目 {unresolved} 个。";

            return byId.Values
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string CleanName(string name)
        {
            var value = (name ?? string.Empty).Trim();
            if (value.Length == 0 || value.Length > 64)
            {
                return null;
            }

            // 占位名（*Untitled[QEDSD]）不是真实角色
            if (value.Contains('*') || value.Contains('[') || value.Contains("Untitled", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return value;
        }

        // ---------- 读取 ----------

        private static IEnumerable<(string Name, int Id)> ReadAuth(string configsFolder)
        {
            var file = Path.Combine(configsFolder, "Auth.json");
            if (!File.Exists(file))
            {
                yield break;
            }

            JArray array;
            try
            {
                array = JArray.Parse(File.ReadAllText(file));
            }
            catch
            {
                yield break;
            }

            foreach (var item in array.OfType<JObject>())
            {
                var id = item.Value<long?>("CharacterID") ?? 0;
                var name = item.Value<string>("CharacterName");
                if (id > 0 && id <= int.MaxValue)
                {
                    yield return (name, (int)id);
                }
            }
        }

        private static IEnumerable<int> ReadGameLogIds(string configsFolder)
        {
            var file = Path.Combine(configsFolder, "GameLogInfoSettings.json");
            if (!File.Exists(file))
            {
                yield break;
            }

            JArray array;
            try
            {
                array = JArray.Parse(File.ReadAllText(file));
            }
            catch
            {
                yield break;
            }

            foreach (var item in array.OfType<JObject>())
            {
                var id = item.Value<long?>("ListenerID") ?? 0;
                if (id > 0 && id <= int.MaxValue)
                {
                    yield return (int)id;
                }
            }
        }

        private static IEnumerable<string> ReadCharacterNames(string configsFolder)
        {
            // ChannelMonitorSetting.json: Name
            foreach (var name in ReadStrings(Path.Combine(configsFolder, "ChannelMonitorSetting.json"), "Name"))
            {
                yield return name;
            }

            // IntelSettings.json: Listener
            foreach (var name in ReadStrings(Path.Combine(configsFolder, "IntelSettings.json"), "Listener"))
            {
                yield return name;
            }

            // ChannelMarketSettings.json / ChannelTranslationSettings.json: CharacterName
            foreach (var name in ReadStrings(Path.Combine(configsFolder, "ChannelMarketSettings.json"), "CharacterName"))
            {
                yield return name;
            }
            foreach (var name in ReadStrings(Path.Combine(configsFolder, "ChannelTranslationSettings.json"), "CharacterName"))
            {
                yield return name;
            }

            // GamePreviewSetting.json: ProcessOrder 里的 "EVE - 角色名"
            var preview = Path.Combine(configsFolder, "GamePreviewSetting.json");
            if (File.Exists(preview))
            {
                JObject root = null;
                try
                {
                    root = JObject.Parse(File.ReadAllText(preview));
                }
                catch
                {
                    // ignore
                }

                if (root?["ProcessOrder"] is JArray order)
                {
                    foreach (var token in order)
                    {
                        var title = token.Value<string>();
                        if (string.IsNullOrWhiteSpace(title))
                        {
                            continue;
                        }

                        var index = title.IndexOf('-');
                        if (index > 0)
                        {
                            yield return title.Substring(index + 1).Trim();
                        }
                    }
                }
            }
        }

        private static IEnumerable<string> ReadStrings(string file, string property)
        {
            if (!File.Exists(file))
            {
                yield break;
            }

            JArray array;
            try
            {
                array = JArray.Parse(File.ReadAllText(file));
            }
            catch
            {
                yield break;
            }

            foreach (var item in array.OfType<JObject>())
            {
                var value = item.Value<string>(property);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    yield return value;
                }
            }
        }

        /// <summary>
        /// 扫游戏日志目录：文件名 <c>..._角色ID.txt</c> + 头部 <c>收听者:</c> / <c>Listener:</c> → ID ↔ 名字。
        /// </summary>
        private static Dictionary<int, string> ReadGameLogHeaders(string logsRoot)
        {
            var map = new Dictionary<int, string>();
            try
            {
                var folder = Path.Combine(logsRoot ?? DefaultLogsPath(), "Gamelogs");
                if (!Directory.Exists(folder))
                {
                    folder = Path.Combine(logsRoot ?? DefaultLogsPath(), "gamelogs");
                }

                if (!Directory.Exists(folder))
                {
                    return map;
                }

                var files = Directory.GetFiles(folder, "*.txt")
                    .OrderByDescending(p => File.GetLastWriteTimeUtc(p))
                    .Take(500);
                foreach (var file in files)
                {
                    var name = Path.GetFileNameWithoutExtension(file).Split('_');
                    if (name.Length != 3 || !int.TryParse(name[2], out var id) || map.ContainsKey(id))
                    {
                        continue;
                    }

                    try
                    {
                        using var reader = new StreamReader(file, Encoding.UTF8, true);
                        for (var i = 0; i < 8; i++)
                        {
                            var line = reader.ReadLine();
                            if (line == null)
                            {
                                break;
                            }

                            var match = Regex.Match(line, @"(?:收听者|Listener)\s*[:：]\s*(?<name>.+)$",
                                RegexOptions.IgnoreCase);
                            if (match.Success)
                            {
                                var character = CleanName(match.Groups["name"].Value);
                                if (character != null)
                                {
                                    map[id] = character;
                                }
                                break;
                            }
                        }
                    }
                    catch
                    {
                        // 单个文件读失败不影响整体
                    }
                }
            }
            catch
            {
                // 日志目录不存在/无权限：只用 Auth + 配置里的名字
            }

            return map;
        }
    }
}
