using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using TheGuideToTheNewEden.Core.Models.CombatLog;
using TheGuideToTheNewEden.Core.Models.EVELogs;

namespace TheGuideToTheNewEden.Core.Services.CombatLog
{
    /// <summary>
    /// 战斗日志（Gamelogs）行解析器（阶段 357）。
    /// <para>
    /// 设计原则：**结构优先、词表兜底**。EVE 日志的正文与装备名会随客户端语言本地化，
    /// 但结构稳定：数值 + 单位 + 介词 + 角色/军团/舰船 + <c> - </c> + 装备名（伤害行后面还有命中质量）。
    /// 因此这里先按结构拆段，再用"多语言词表"判断事件类型与方向；词表集中在
    /// <see cref="CombatLogVocabulary"/>，拿到真实中文日志后只改词表即可，不用动解析逻辑。
    /// </para>
    /// <para>解析失败的 <c>(combat)</c> 行会进 <see cref="CombatLogDiagnostics"/>，便于回头补词表。</para>
    /// </summary>
    public static class CombatLogParser
    {
        private static readonly Regex HtmlTagRegex = new Regex("<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex LeadingNumberRegex = new Regex(@"^\s*([+-]?\d+(?:[.,]\d+)?)", RegexOptions.Compiled);
        private static readonly Regex GjRegex = new Regex(@"([+-]?\d+(?:[.,]\d+)?)\s*GJ", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex HpRegex = new Regex(@"([+-]?\d+(?:[.,]\d+)?)\s*(?:HP|hp|Hp)", RegexOptions.Compiled);
        private static readonly Regex CorpRegex = new Regex(@"\[([^\]]+)\]", RegexOptions.Compiled);
        private static readonly Regex ShipRegex = new Regex(@"\(([^)]+)\)", RegexOptions.Compiled);
        private static readonly Regex BracketOrParenRegex = new Regex(@"\[[^\]]+\]|\([^)]+\)", RegexOptions.Compiled);
        private static readonly Regex MultiSpaceRegex = new Regex(@"\s{2,}", RegexOptions.Compiled);

        /// <summary>
        /// 解析一行游戏日志。返回 null 表示不是可识别的战斗事件。
        /// </summary>
        /// <param name="content">日志行（<c>Content</c> 保留 HTML 标签，正好可用于分词）。</param>
        /// <param name="listenerName">日志所属角色（用于判断方向，可为空）。</param>
        public static CombatLogEvent TryParse(GameLogContent content, string listenerName = null)
        {
            if (content == null || string.IsNullOrWhiteSpace(content.Content))
            {
                return null;
            }

            var text = Normalize(content.Content);
            if (text.Length < 3)
            {
                return null;
            }

            var tagged = text.StartsWith("(combat)", StringComparison.OrdinalIgnoreCase)
                         || text.StartsWith("(notify)", StringComparison.OrdinalIgnoreCase);
            text = StripTag(text);

            string[][] prepositions;
            var kind = DetectKind(text, out prepositions);
            if (kind == CombatEventKind.Unknown)
            {
                if (tagged && text.Length > 0)
                {
                    CombatLogDiagnostics.ReportUnparsed(text);
                }

                return null;
            }

            var segments = SplitSegments(text);
            string unit;
            var amount = ExtractAmount(kind, text, segments.Prefix, out unit);
            var entity = ExtractEntity(segments.Prefix, prepositions);
            var direction = ResolveDirection(kind, segments.Prefix, entity, listenerName, out var other);

            return new CombatLogEvent
            {
                Time = content.EVETime,
                Kind = kind,
                Direction = direction,
                Pilot = other.Pilot,
                Corporation = other.Corporation,
                Ship = other.Ship,
                ModuleName = segments.Module,
                Amount = amount,
                Unit = unit,
                RawLine = text,
            };
        }

        // ---------- 文本归一化 ----------

        private static string Normalize(string raw)
        {
            var text = HtmlTagRegex.Replace(raw, " ");
            text = text.Replace("&nbsp;", " ").Replace("&amp;", "&");
            text = MultiSpaceRegex.Replace(text, " ").Trim();
            return text;
        }

        private static string StripTag(string text)
        {
            if (text.StartsWith("(combat)", StringComparison.OrdinalIgnoreCase))
            {
                return text.Substring("(combat)".Length).Trim();
            }

            if (text.StartsWith("(notify)", StringComparison.OrdinalIgnoreCase))
            {
                return text.Substring("(notify)".Length).Trim();
            }

            return text;
        }

        // ---------- 事件类型 ----------

        private static CombatEventKind DetectKind(string text, out string[][] prepositions)
        {
            // 顺序很重要：反跳/ECM 先于"伤害"（它们也可能带数字），毁电先于吸电（都含 GJ）
            if (ContainsAny(text, CombatLogVocabulary.ScramWords))
            {
                prepositions = new[] { CombatLogVocabulary.FromWords, CombatLogVocabulary.ToWords };
                return CombatEventKind.WarpScramble;
            }

            if (ContainsAny(text, CombatLogVocabulary.EcmWords))
            {
                prepositions = new[] { CombatLogVocabulary.ByWords, CombatLogVocabulary.FromWords };
                return CombatEventKind.EcmJam;
            }

            if (ContainsAny(text, CombatLogVocabulary.CapacitorTransferWords))
            {
                prepositions = new[] { CombatLogVocabulary.ByWords, CombatLogVocabulary.ToWords };
                return CombatEventKind.RemoteCapacitor;
            }

            if (ContainsAny(text, CombatLogVocabulary.RepairWords))
            {
                prepositions = new[] { CombatLogVocabulary.ByWords, CombatLogVocabulary.ToWords };
                return CombatEventKind.RemoteRepair;
            }

            if (ContainsAny(text, CombatLogVocabulary.NeutralizeWords))
            {
                prepositions = new[] { CombatLogVocabulary.FromWords, CombatLogVocabulary.ToWords };
                return CombatEventKind.CapacitorNeutralize;
            }

            if (ContainsAny(text, CombatLogVocabulary.DrainWords))
            {
                prepositions = new[] { CombatLogVocabulary.FromWords, CombatLogVocabulary.ToWords };
                return CombatEventKind.CapacitorDrain;
            }

            // 伤害行：行首有数值，且带 from/to
            if (LeadingNumberRegex.IsMatch(text)
                && (ContainsAny(text, CombatLogVocabulary.FromWords)
                    || ContainsAny(text, CombatLogVocabulary.ToWords)))
            {
                prepositions = new[] { CombatLogVocabulary.FromWords, CombatLogVocabulary.ToWords };
                return CombatEventKind.Damage;
            }

            prepositions = new string[0][];
            return CombatEventKind.Unknown;
        }

        // ---------- 分段 / 数值 / 实体 ----------

        private sealed class Segments
        {
            public Segments(string prefix, string module)
            {
                Prefix = prefix;
                Module = module;
            }

            public string Prefix { get; }

            public string Module { get; }
        }

        /// <summary>
        /// 拆成「前缀 - 装备[- 命中质量]」：伤害行的最后一段是命中质量（Hits/Penetrates…），
        /// 其余事件最后一段就是装备名。
        /// </summary>
        private static Segments SplitSegments(string text)
        {
            var parts = text.Split(new[] { " - " }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .ToArray();
            if (parts.Length >= 3 && CombatLogVocabulary.IsQuality(parts[parts.Length - 1]))
            {
                return new Segments(
                    string.Join(" - ", parts.Take(parts.Length - 2)),
                    parts[parts.Length - 2]);
            }

            if (parts.Length >= 2)
            {
                return new Segments(
                    string.Join(" - ", parts.Take(parts.Length - 1)),
                    parts[parts.Length - 1]);
            }

            return new Segments(text, null);
        }

        private static double? ExtractAmount(CombatEventKind kind, string text, string prefix, out string unit)
        {
            unit = null;
            double? value;
            switch (kind)
            {
                case CombatEventKind.CapacitorNeutralize:
                case CombatEventKind.CapacitorDrain:
                case CombatEventKind.RemoteCapacitor:
                    unit = "GJ";
                    value = TryNumber(GjRegex, text) ?? TryNumber(LeadingNumberRegex, prefix);
                    break;

                case CombatEventKind.RemoteRepair:
                    unit = "HP";
                    value = TryNumber(HpRegex, text) ?? TryNumber(LeadingNumberRegex, prefix);
                    break;

                case CombatEventKind.Damage:
                    unit = "HP";
                    // 伤害量就是行首那个数字（前缀里第一个数值）
                    value = TryNumber(LeadingNumberRegex, prefix) ?? TryNumber(LeadingNumberRegex, text);
                    break;

                default:
                    return null;
            }

            // 日志里毁电/吸电会带符号（"-15 GJ" 表示你被中和），方向另有字段，展示一律取绝对值
            return value.HasValue ? Math.Abs(value.Value) : (double?)null;
        }

        private static double? TryNumber(Regex regex, string text)
        {
            var match = regex.Match(text);
            if (!match.Success)
            {
                return null;
            }

            var raw = match.Groups[1].Value.Replace(',', '.');
            double value;
            return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                ? value
                : (double?)null;
        }

        private sealed class Entity
        {
            public Entity(string pilot, string corporation, string ship)
            {
                Pilot = pilot;
                Corporation = corporation;
                Ship = ship;
            }

            public string Pilot { get; }

            public string Corporation { get; }

            public string Ship { get; }
        }

        /// <summary>从前缀里取出"介词后面的那个实体"（角色 / 军团 / 舰船）。</summary>
        private static Entity ExtractEntity(string prefix, string[][] prepositions)
        {
            var raw = FindEntityText(prefix, prepositions);
            return raw == null ? new Entity(null, null, null) : ParseEntity(raw);
        }

        private static string FindEntityText(string text, string[][] prepositions)
        {
            foreach (var group in prepositions)
            {
                foreach (var word in group)
                {
                    var index = IndexOfWord(text, word);
                    if (index < 0)
                    {
                        continue;
                    }

                    var rest = text.Substring(index + word.Length).Trim();
                    if (rest.Length == 0)
                    {
                        continue;
                    }

                    // 截止到下一个介词（例如 "from X to you" 只要 X）
                    var cut = rest.Length;
                    foreach (var other in CombatLogVocabulary.AllPrepositions)
                    {
                        var next = IndexOfWord(rest, other);
                        if (next > 0 && next < cut)
                        {
                            cut = next;
                        }
                    }

                    var candidate = rest.Substring(0, cut).Trim();
                    if (candidate.Length > 0 && !CombatLogVocabulary.IsYou(candidate))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        private static Entity ParseEntity(string raw)
        {
            var corpMatch = CorpRegex.Match(raw);
            var corp = corpMatch.Success ? corpMatch.Groups[1].Value : null;
            var shipMatch = ShipRegex.Match(raw);
            var ship = shipMatch.Success ? shipMatch.Groups[1].Value : null;
            var bracketCount = CorpRegex.Matches(raw).Count;

            var clean = BracketOrParenRegex.Replace(raw, " ").Trim();
            clean = MultiSpaceRegex.Replace(clean, " ");
            var words = clean.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            string pilot;
            if (bracketCount >= 2 && words.Length <= 1)
            {
                // [CORP] [Pilot]
                pilot = CorpRegex.Matches(raw)[1].Groups[1].Value;
            }
            else if (words.Length >= 2 && string.IsNullOrEmpty(ship) && bracketCount >= 1)
            {
                // Pilot [CORP] Ship
                pilot = string.Join(" ", words.Take(words.Length - 1));
                ship = words[words.Length - 1];
            }
            else
            {
                pilot = words.Length > 0 ? string.Join(" ", words) : null;
            }

            return new Entity(
                string.IsNullOrWhiteSpace(pilot) ? null : pilot.Trim(),
                string.IsNullOrWhiteSpace(corp) ? null : corp.Trim(),
                string.IsNullOrWhiteSpace(ship) ? null : ship.Trim());
        }

        /// <summary>
        /// 方向：伤害/毁电/吸电按介词或"对方是否就是自己"判断；遥修/传电按 by（进）/ to（出）。
        /// </summary>
        private static CombatEventDirection ResolveDirection(
            CombatEventKind kind,
            string prefix,
            Entity entity,
            string listenerName,
            out Entity other)
        {
            other = entity;
            var fromIndex = FindAnyWord(prefix, CombatLogVocabulary.FromWords);
            var toIndex = FindAnyWord(prefix, CombatLogVocabulary.ToWords);
            var byIndex = FindAnyWord(prefix, CombatLogVocabulary.ByWords);

            if (kind == CombatEventKind.RemoteRepair || kind == CombatEventKind.RemoteCapacitor)
            {
                if (byIndex >= 0)
                {
                    return CombatEventDirection.Incoming;
                }

                if (toIndex >= 0)
                {
                    return CombatEventDirection.Outgoing;
                }
            }

            if (kind == CombatEventKind.WarpScramble || kind == CombatEventKind.EcmJam)
            {
                // "from X to you" → 进；"from you to X" → 出
                if (toIndex >= 0 && ContainsAny(prefix.Substring(toIndex), CombatLogVocabulary.YouWords))
                {
                    return CombatEventDirection.Incoming;
                }

                if (fromIndex >= 0 && ContainsAny(prefix.Substring(fromIndex), CombatLogVocabulary.YouWords))
                {
                    return CombatEventDirection.Outgoing;
                }
            }

            if (fromIndex >= 0)
            {
                return CombatEventDirection.Incoming;
            }

            if (toIndex >= 0)
            {
                return CombatEventDirection.Outgoing;
            }

            // 都没有介词时：毁电/吸电用"施放者是不是自己"兜底
            if ((kind == CombatEventKind.CapacitorNeutralize || kind == CombatEventKind.CapacitorDrain)
                && !string.IsNullOrWhiteSpace(listenerName)
                && !string.IsNullOrWhiteSpace(entity.Pilot))
            {
                return string.Equals(entity.Pilot, listenerName, StringComparison.OrdinalIgnoreCase)
                    ? CombatEventDirection.Outgoing
                    : CombatEventDirection.Incoming;
            }

            return CombatEventDirection.Unknown;
        }

        // ---------- 小工具 ----------

        private static bool ContainsAny(string text, string[] words)
        {
            foreach (var word in words)
            {
                if (IndexOfWord(text, word) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static int FindAnyWord(string text, string[] words)
        {
            var best = -1;
            foreach (var word in words)
            {
                var index = IndexOfWord(text, word);
                if (index >= 0 && (best < 0 || index < best))
                {
                    best = index;
                }
            }

            return best;
        }

        /// <summary>英文按词边界匹配（避免 "to" 命中 "tomato"），中文按包含匹配。</summary>
        private static int IndexOfWord(string text, string word)
        {
            if (string.IsNullOrEmpty(word))
            {
                return -1;
            }

            if (IsAsciiWord(word))
            {
                var match = Regex.Match(
                    text,
                    @"(?<![A-Za-z])" + Regex.Escape(word) + @"(?![A-Za-z])",
                    RegexOptions.IgnoreCase);
                return match.Success ? match.Index : -1;
            }

            return text.IndexOf(word, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAsciiWord(string word)
            => word.All(c => c < 128 && (char.IsLetterOrDigit(c) || c == '-' || c == '_'));

        /// <summary>诊断用：把一行转换成可读字符串（去掉标签、压缩空格）。</summary>
        public static string NormalizeForDiagnostics(string raw) => Normalize(raw);
    }
}
