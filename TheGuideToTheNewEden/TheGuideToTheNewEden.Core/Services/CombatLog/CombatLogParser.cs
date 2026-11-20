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
    /// 中文客户端实测（2026-11-19 日志）形态：
    /// <list type="bullet">
    ///   <item>伤害：<c>209 来自 QEDSD[I K N](小鹰级*) - 模块 - 命中</c>；出向是 <c>209 对 目标 - 模块 - 命中</c>。</item>
    ///   <item>毁电：<c>45 GJ能量中和 黑鸟级* [I K N] [QEDSD] - 模块</c>（没有介词，施放者在"能量中和"后面）。</item>
    ///   <item>吸电：<c>-0 GJ被吸取到 黑鸟级* [I K N] [QEDSD] - 模块</c>（进）；<c>+0 GJ被从Arya...吸取 - 模块</c>（出）。</item>
    ///   <item>遥修：<c>64远程护盾回充增量由 黑鸟级* [I K N] [QEDSD] - 模块</c>（进）；<c>64远程护盾回充增量至Arya... - 模块</c>（出）。</item>
    ///   <item>反跳：<c>来自 黑鸟级* [I K N] [QEDSD] - 试图跃迁扰频 你！</c>；第三方的 <c>A - 试图跃迁扰断 B</c> 不是我们，按出向丢弃。</item>
    ///   <item>ECM：<c>Arya...的磁力ECM I*正在干扰你</c>（进）；<c>磁力ECM I*对 小鹰级* ... -干扰</c>（出）。</item>
    ///   <item>未命中：<c>QEDSD完全没有打中你 - 模块</c>，没有数值，直接忽略。</item>
    /// </list>
    /// 另外模块名/舰船名是 <c>&lt;localized hint="英文名"&gt;中文名*&lt;/localized&gt;</c>，
    /// 中文名末尾带 <c>*</c>：展示时去掉 <c>*</c>，反查分类时优先用 **hint 里的英文名**（最可靠）。
    /// </para>
    /// </summary>
    public static class CombatLogParser
    {
        private static readonly Regex HtmlTagRegex = new Regex("<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex LocalizedRegex = new Regex(
            "<localized\\s+hint=\"(?<hint>[^\"]+)\"[^>]*>(?<text>.*?)</localized>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private static readonly Regex LeadingNumberRegex = new Regex(@"^\s*([+-]?\d+(?:[.,]\d+)?)", RegexOptions.Compiled);
        private static readonly Regex GjRegex = new Regex(@"([+-]?\d+(?:[.,]\d+)?)\s*GJ", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex HpRegex = new Regex(@"([+-]?\d+(?:[.,]\d+)?)\s*(?:HP|hp|Hp)", RegexOptions.Compiled);
        private static readonly Regex CorpRegex = new Regex(@"\[([^\]]+)\]", RegexOptions.Compiled);
        private static readonly Regex ShipRegex = new Regex(@"\(([^)]+)\)", RegexOptions.Compiled);
        private static readonly Regex BracketOrParenRegex = new Regex(@"\[[^\]]+\]|\([^)]+\)", RegexOptions.Compiled);
        private static readonly Regex MultiSpaceRegex = new Regex(@"\s{2,}", RegexOptions.Compiled);
        private static readonly Regex NeutActorRegex = new Regex(@"能量中和\s*(?<actor>.+?)(?:\s+-\s+|$)", RegexOptions.Compiled);
        private static readonly Regex NeutEnActorRegex = new Regex(@"neutralized\s+from\s+(?<actor>.+?)(?:\s+-\s+|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex DrainToRegex = new Regex(@"被吸取到\s*(?<actor>.+?)(?:\s+-\s+|$)", RegexOptions.Compiled);
        private static readonly Regex DrainFromRegex = new Regex(@"被从\s*(?<actor>.+?)\s*吸取", RegexOptions.Compiled);

        /// <summary>解析一行游戏日志。返回 null 表示不是可识别的战斗事件。</summary>
        public static CombatLogEvent TryParse(GameLogContent content, string listenerName = null)
        {
            if (content == null || string.IsNullOrWhiteSpace(content.Content))
            {
                return null;
            }

            var hints = ExtractLocalizedHints(content.Content);
            var text = Normalize(content.Content);
            if (text.Length < 3)
            {
                return null;
            }

            var isCombat = text.StartsWith("(combat)", StringComparison.OrdinalIgnoreCase);
            text = StripTag(text);

            if (IsNoise(text))
            {
                return null;
            }

            string[][] prepositions;
            var kind = DetectKind(text, out prepositions);
            if (kind == CombatEventKind.Unknown)
            {
                if (isCombat && text.Length > 0)
                {
                    CombatLogDiagnostics.ReportUnparsed(text);
                }

                return null;
            }

            var segments = SplitSegments(text, kind);
            string unit;
            var amount = ExtractAmount(kind, text, segments.Prefix, out unit);
            if (kind != CombatEventKind.Damage
                && kind != CombatEventKind.WarpScramble
                && kind != CombatEventKind.EcmJam
                && amount is null)
            {
                // 遥修/毁电/吸电/传电没有数值的多半是"装料/维护"之类的行，不是战斗事件
                return null;
            }

            if (amount.HasValue && amount.Value <= 0
                && kind != CombatEventKind.Damage)
            {
                // 中文日志会为"满护盾的遥修 / 吸不到电的掠能器"写 0，显示出来只是噪音
                return null;
            }

            var entity = ResolveOther(kind, text, segments.Prefix, listenerName, out var direction);
            var module = CleanName(segments.Module);
            var hint = LookupHint(hints, module);

            return new CombatLogEvent
            {
                Time = content.EVETime,
                Kind = kind,
                Direction = direction,
                Pilot = entity.Pilot,
                Corporation = entity.Corporation,
                Ship = entity.Ship,
                ModuleName = module,
                ModuleHintName = hint,
                Amount = amount,
                Unit = unit,
                RawLine = text,
            };
        }

        // ---------- 文本归一化 / hint ----------

        private static Dictionary<string, string> ExtractLocalizedHints(string raw)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in LocalizedRegex.Matches(raw))
            {
                var display = CleanName(HtmlTagRegex.Replace(match.Groups["text"].Value, " "));
                var hint = match.Groups["hint"].Value.Trim();
                if (!string.IsNullOrWhiteSpace(display) && !string.IsNullOrWhiteSpace(hint)
                    && !map.ContainsKey(display))
                {
                    map[display] = hint;
                }
            }

            return map;
        }

        private static string LookupHint(Dictionary<string, string> hints, string module)
        {
            if (string.IsNullOrWhiteSpace(module) || hints.Count == 0)
            {
                return null;
            }

            string hint;
            return hints.TryGetValue(module, out hint) ? hint : null;
        }

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

        /// <summary>去掉名字末尾的 <c>*</c>、首尾横线与空白。</summary>
        private static string CleanName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var text = value.Trim().Trim('*').Trim();
            text = text.Trim('-').Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        private static bool IsNoise(string text)
            => ContainsAny(text, CombatLogVocabulary.MissWords);

        // ---------- 事件类型 ----------

        private static CombatEventKind DetectKind(string text, out string[][] prepositions)
        {
            if (ContainsAny(text, CombatLogVocabulary.ScramWords))
            {
                prepositions = new[] { CombatLogVocabulary.FromWords, CombatLogVocabulary.ToWords };
                return CombatEventKind.WarpScramble;
            }

            if (ContainsAny(text, CombatLogVocabulary.EcmWords))
            {
                prepositions = new[] { CombatLogVocabulary.ByWords, CombatLogVocabulary.ToWords };
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

        // ---------- 分段 ----------

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

        private static Segments SplitSegments(string text, CombatEventKind kind)
        {
            if (kind == CombatEventKind.WarpScramble)
            {
                return new Segments(text, null);
            }

            if (kind == CombatEventKind.EcmJam)
            {
                return SplitEcm(text);
            }

            var parts = text.Split(new[] { " - " }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => p.Length > 0)
                .ToArray();
            if (parts.Length < 2)
            {
                return new Segments(text, null);
            }

            var moduleIndex = parts.Length - 1;
            if (parts.Length >= 3 && CombatLogVocabulary.IsQuality(parts[parts.Length - 1]))
            {
                moduleIndex = parts.Length - 2;
            }

            return new Segments(
                string.Join(" - ", parts.Take(moduleIndex)),
                parts[moduleIndex]);
        }

        /// <summary>ECM：出向是「模块 对 目标 … -干扰」，进向是「某人 的 模块 正在干扰 你」。</summary>
        private static Segments SplitEcm(string text)
        {
            // 英文 / 旧式中文：模块在最后一段（" - 模块"）
            var dash = text.LastIndexOf(" - ", StringComparison.Ordinal);
            if (dash > 0)
            {
                return new Segments(text.Substring(0, dash), text.Substring(dash + 3));
            }

            var index = text.IndexOf("对", StringComparison.Ordinal);
            if (index > 0)
            {
                return new Segments(text, text.Substring(0, index));
            }

            var jam = text.IndexOf("正在干扰", StringComparison.Ordinal);
            if (jam < 0)
            {
                jam = text.IndexOf("干扰", StringComparison.Ordinal);
            }

            if (jam > 0)
            {
                var before = text.Substring(0, jam);
                var possessive = before.LastIndexOf('的');
                return new Segments(text, possessive >= 0 ? before.Substring(possessive + 1) : before);
            }

            return new Segments(text, null);
        }

        // ---------- 数值 ----------

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
                    value = TryNumber(LeadingNumberRegex, prefix) ?? TryNumber(LeadingNumberRegex, text);
                    break;

                default:
                    return null;
            }

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

        // ---------- 对方 / 方向 ----------

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

        private static Entity ResolveOther(
            CombatEventKind kind,
            string text,
            string prefix,
            string listenerName,
            out CombatEventDirection direction)
        {
            switch (kind)
            {
                case CombatEventKind.Damage:
                {
                    var from = AfterPhrase(prefix, "来自") ?? AfterPhrase(prefix, "from");
                    if (from != null)
                    {
                        direction = CombatEventDirection.Incoming;
                        return ParseEntity(from);
                    }

                    var to = AfterPhrase(prefix, "对") ?? AfterPhrase(prefix, "to");
                    if (to != null)
                    {
                        direction = CombatEventDirection.Outgoing;
                        return ParseEntity(to);
                    }

                    direction = CombatEventDirection.Unknown;
                    return ParseEntity(prefix);
                }

                case CombatEventKind.CapacitorNeutralize:
                {
                    var match = NeutActorRegex.Match(text);
                    if (!match.Success)
                    {
                        match = NeutEnActorRegex.Match(text);
                    }

                    var entity = match.Success ? ParseEntity(match.Groups["actor"].Value) : new Entity(null, null, null);
                    direction = IsListener(entity.Pilot, listenerName)
                        ? CombatEventDirection.Outgoing
                        : CombatEventDirection.Incoming;
                    return entity;
                }

                case CombatEventKind.CapacitorDrain:
                {
                    var toMatch = DrainToRegex.Match(text);
                    if (toMatch.Success)
                    {
                        direction = CombatEventDirection.Incoming;
                        return ParseEntity(toMatch.Groups["actor"].Value);
                    }

                    var fromMatch = DrainFromRegex.Match(text);
                    if (fromMatch.Success)
                    {
                        direction = CombatEventDirection.Outgoing;
                        return ParseEntity(fromMatch.Groups["actor"].Value);
                    }

                    var from = AfterPhrase(text, "from");
                    if (from != null)
                    {
                        direction = CombatEventDirection.Incoming;
                        return ParseEntity(from);
                    }

                    var to = AfterPhrase(text, "to");
                    direction = to != null ? CombatEventDirection.Outgoing : CombatEventDirection.Unknown;
                    return ParseEntity(to);
                }

                case CombatEventKind.RemoteRepair:
                case CombatEventKind.RemoteCapacitor:
                {
                    var by = AfterPhrase(prefix, "由") ?? AfterPhrase(prefix, "by") ?? AfterPhrase(prefix, "被");
                    if (by != null)
                    {
                        direction = CombatEventDirection.Incoming;
                        return ParseEntity(by);
                    }

                    var to = AfterPhrase(prefix, "至") ?? AfterPhrase(prefix, "to");
                    if (to != null)
                    {
                        direction = CombatEventDirection.Outgoing;
                        return ParseEntity(to);
                    }

                    direction = CombatEventDirection.Unknown;
                    return new Entity(null, null, null);
                }

                case CombatEventKind.WarpScramble:
                    return ResolveScram(text, listenerName, out direction);

                case CombatEventKind.EcmJam:
                    return ResolveEcm(text, listenerName, out direction);

                default:
                    direction = CombatEventDirection.Unknown;
                    return new Entity(null, null, null);
            }
        }

        private static Entity ResolveScram(string text, string listenerName, out CombatEventDirection direction)
        {
            string actor = null;
            string target = null;

            // 英文：Warp scramble attempt from X to Y
            var english = Regex.Match(text, @"from\s+(?<a>.+?)\s+to\s+(?<b>.+)$", RegexOptions.IgnoreCase);
            if (english.Success)
            {
                actor = english.Groups["a"].Value;
                target = english.Groups["b"].Value;
            }

            // 旧式中文：… 来自 X … 对/至 Y
            if (actor == null)
            {
                var chinese = Regex.Match(text, @"来自\s*(?<a>.+?)\s*(?:对|至|向)\s*(?<b>.+)$");
                if (chinese.Success)
                {
                    actor = chinese.Groups["a"].Value;
                    target = chinese.Groups["b"].Value;
                }
            }

            // 实测中文：A - 试图跃迁扰频 B（A 前可能有"来自"）
            if (actor == null)
            {
                var verb = FirstIndexOfAny(text, "试图跃迁扰频", "试图跃迁扰断", "跃迁扰频", "跃迁扰断",
                    "warp scramble", "warp disruption");
                if (verb >= 0)
                {
                    var left = CleanName(text.Substring(0, verb));
                    left = CleanName(AfterPhrase(left, "来自") ?? AfterPhrase(left, "from") ?? left);
                    actor = left;
                    target = CleanName(text.Substring(verb + VerbLength(text, verb)));
                }
            }

            target = CleanName(target);
            actor = CleanName(actor);

            if (target != null && (IsYou(target) || IsListenerPart(target, listenerName)))
            {
                direction = CombatEventDirection.Incoming;
                return ParseEntity(actor);
            }

            if (actor != null && (IsYou(actor) || IsListenerPart(actor, listenerName)))
            {
                direction = CombatEventDirection.Outgoing;
                return ParseEntity(target);
            }

            // 第三方互相扰断：不是我们，按出向丢弃（不显示"反跳"）
            direction = CombatEventDirection.Outgoing;
            return ParseEntity(target ?? actor);
        }

        private static Entity ResolveEcm(string text, string listenerName, out CombatEventDirection direction)
        {
            if (Regex.IsMatch(text, @"干扰\s*你|jammed\s+you", RegexOptions.IgnoreCase))
            {
                var jamIndex = text.IndexOf("干扰", StringComparison.Ordinal);
                var before = jamIndex > 0 ? text.Substring(0, jamIndex) : text;
                var possessive = before.LastIndexOf('的');
                direction = CombatEventDirection.Incoming;
                return ParseEntity(possessive >= 0 ? before.Substring(0, possessive) : before);
            }

            // 旧式/英文被动：「你被 X 干扰」
            var passive = Regex.Match(text, @"被\s*(?<actor>.+?)\s*(?:干扰|jammed)", RegexOptions.IgnoreCase);
            if (passive.Success)
            {
                direction = CombatEventDirection.Incoming;
                return ParseEntity(passive.Groups["actor"].Value);
            }

            var to = AfterPhrase(text, "对");
            if (to != null)
            {
                var target = CleanName(to.Split(new[] { " -" }, StringSplitOptions.None)[0]);
                var entity = ParseEntity(target);
                direction = IsYou(target) || IsListener(entity.Pilot, listenerName)
                    ? CombatEventDirection.Incoming
                    : CombatEventDirection.Outgoing;
                return entity;
            }

            var by = AfterPhrase(text, "by");
            if (by != null)
            {
                direction = CombatEventDirection.Incoming;
                return ParseEntity(by);
            }

            direction = CombatEventDirection.Unknown;
            return new Entity(null, null, null);
        }

        private static bool IsYou(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var text = value.Trim().TrimEnd('！', '!', '-').Trim();
            return CombatLogVocabulary.IsYou(text);
        }

        private static bool IsListener(string pilot, string listenerName)
            => !string.IsNullOrWhiteSpace(pilot)
               && !string.IsNullOrWhiteSpace(listenerName)
               && string.Equals(pilot, listenerName, StringComparison.OrdinalIgnoreCase);

        private static bool IsListenerPart(string value, string listenerName)
            => IsListener(value, listenerName)
               || (!string.IsNullOrWhiteSpace(value)
                   && !string.IsNullOrWhiteSpace(listenerName)
                   && value.IndexOf(listenerName, StringComparison.OrdinalIgnoreCase) >= 0);

        /// <summary>取短语后面的实体文本：到 " - " 或结尾。</summary>
        private static string AfterPhrase(string text, string phrase)
        {
            var index = text.IndexOf(phrase, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return null;
            }

            var rest = text.Substring(index + phrase.Length).Trim();
            if (rest.Length == 0)
            {
                return null;
            }

            // 到 " - "（模块分隔）或 " -"（如 ECM 的「… -干扰」）为止
            var cut = rest.IndexOf(" - ", StringComparison.Ordinal);
            if (cut < 0)
            {
                cut = rest.IndexOf(" -", StringComparison.Ordinal);
            }

            if (cut > 0)
            {
                rest = rest.Substring(0, cut);
            }

            return CleanName(rest);
        }

        private static Entity ParseEntity(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new Entity(null, null, null);
            }

            var corpMatch = CorpRegex.Match(raw);
            var corp = corpMatch.Success ? CleanName(corpMatch.Groups[1].Value) : null;
            var shipMatch = ShipRegex.Match(raw);
            var ship = shipMatch.Success ? CleanName(shipMatch.Groups[1].Value) : null;
            var brackets = CorpRegex.Matches(raw);
            var bracketCount = brackets.Count;

            var clean = BracketOrParenRegex.Replace(raw, " ").Trim();
            clean = MultiSpaceRegex.Replace(clean, " ");
            var words = clean.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(CleanName)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToArray();

            string pilot;
            if (bracketCount >= 2 && words.Length <= 1)
            {
                // 舰船 [军团] [角色]（第三方反跳行就是这种）
                ship ??= words.Length == 1 ? words[0] : null;
                pilot = CleanName(brackets[brackets.Count - 1].Groups[1].Value);
                if (corp == null && brackets.Count >= 1)
                {
                    corp = CleanName(brackets[0].Groups[1].Value);
                }
            }
            else if (words.Length >= 2 && string.IsNullOrEmpty(ship) && bracketCount >= 1)
            {
                // 角色 [军团] 舰船
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

        private static int FirstIndexOfAny(string text, params string[] words)
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

        private static int VerbLength(string text, int index)
        {
            foreach (var word in new[] { "试图跃迁扰频", "试图跃迁扰断", "跃迁扰频", "跃迁扰断", "warp scramble", "warp disruption" })
            {
                if (string.Compare(text, index, word, 0, word.Length, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return word.Length;
                }
            }

            return 0;
        }

        /// <summary>英文按词边界匹配，中文按包含匹配。</summary>
        private static int IndexOfWord(string text, string word)
        {
            if (string.IsNullOrEmpty(word) || string.IsNullOrEmpty(text))
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
