using System;
using System.Collections.Generic;
using System.Linq;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.CombatLog;
using TheGuideToTheNewEden.Core.Services.DB;

namespace TheGuideToTheNewEden.Core.Services.CombatLog
{
    /// <summary>一个战斗分类的静态定义（展示文案用语言键，见各页面的 Resources/Languages）。</summary>
    public sealed class CombatCategoryDefinition
    {
        public CombatCategoryDefinition(string key, int priority, bool hasValue, string unit, bool defaultEnabled, bool sticky = false)
        {
            Key = key;
            Priority = priority;
            HasValue = hasValue;
            Unit = unit;
            DefaultEnabled = defaultEnabled;
            Sticky = sticky;
        }

        /// <summary>稳定 key（设置落盘、provider 输出都用它）。</summary>
        public string Key { get; }

        /// <summary>排序优先级（数值小在上：反跳最要紧）。</summary>
        public int Priority { get; }

        /// <summary>是否有数值（毁电/吸电/遥修/传电/承伤）。</summary>
        public bool HasValue { get; }

        /// <summary>数值单位（GJ / HP）；承伤另有 DPS。</summary>
        public string Unit { get; }

        /// <summary>默认是否显示。</summary>
        public bool DefaultEnabled { get; }

        /// <summary>
        /// 一次性状态（阶段 358）：反跳 / ECM 这类只有"第一次作用上"才会写日志的事件，
        /// 对面不停止就不会再刷日志，因此不能按超时消失——保留到用户手动清除或停止监控。
        /// </summary>
        public bool Sticky { get; }
    }

    /// <summary>战斗分类清单（阶段 357；阶段 358 增加"一次性状态"标记）。</summary>
    public static class CombatCategories
    {
        public const string Scram = "scram";
        public const string Neut = "neut";
        public const string Nos = "nos";
        public const string Ecm = "ecm";
        public const string Rep = "rep";
        public const string CapTransfer = "cap_transfer";
        public const string Damage = "damage";

        /// <summary>全部分类（按优先级升序）。</summary>
        public static IReadOnlyList<CombatCategoryDefinition> All { get; } = new List<CombatCategoryDefinition>
        {
            new CombatCategoryDefinition(Scram, 10, false, null, true, sticky: true),
            new CombatCategoryDefinition(Neut, 20, true, "GJ", true),
            new CombatCategoryDefinition(Ecm, 30, false, null, true, sticky: true),
            new CombatCategoryDefinition(Rep, 40, true, "HP", true),
            new CombatCategoryDefinition(Nos, 50, true, "GJ", true),
            new CombatCategoryDefinition(CapTransfer, 60, true, "GJ", true),
            new CombatCategoryDefinition(Damage, 90, true, "HP", true),
        };

        public static CombatCategoryDefinition Find(string key)
            => string.IsNullOrWhiteSpace(key)
                ? null
                : All.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));

        public static bool IsKnown(string key) => Find(key) != null;
    }

    /// <summary>
    /// 装备 → 分类的反查（阶段 357）：
    /// <list type="number">
    ///   <item>装备名 → TypeID：先查本地化库（中文名），再查主库（英文名）；</item>
    ///   <item>TypeID → GroupID → 分类：首次使用时把主库英文组名 + 本地库中文组名各扫一遍，
    ///         用组名关键词生成 <c>GroupID → 分类</c>；</item>
    ///   <item>都查不到时退回事件类型对应的分类。</item>
    /// </list>
    /// 结果全部缓存（模块名重复率极高，查库只做一次）。
    /// </summary>
    public static class CombatCategoryResolver
    {
        private static readonly object Locker = new object();
        private static Dictionary<int, string> _groupCategories;
        private static readonly Dictionary<string, string> ModuleCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, (int TypeId, int GroupId)?> TypeCache = new Dictionary<string, (int, int)?>(StringComparer.OrdinalIgnoreCase);

        /// <summary>按分类配置的组名关键词（中英双语；组名命中即归入该分类）。</summary>
        private static readonly (string Category, string[] Keywords)[] GroupRules = new (string, string[])[]
        {
            (CombatCategories.Rep, new[]
            {
                "remote armor repairer", "remote shield transporter", "remote hull repairer", "remote repair",
                "远程装甲维修", "远程护盾传输", "远程结构维修", "远程维修",
            }),
            (CombatCategories.CapTransfer, new[]
            {
                "remote capacitor transmitter", "remote capacitor",
                "远程电容传输", "电容传输",
            }),
            (CombatCategories.Neut, new[]
            {
                "energy neutralizer", "energy neutraliz",
                "能量中和", "中和器",
            }),
            (CombatCategories.Nos, new[]
            {
                "energy nosferatu", "nosferatu", "energy vampire",
                "能量吸血", "吸血",
            }),
            (CombatCategories.Scram, new[]
            {
                "warp scrambler", "warp disruptor", "warp disruption field",
                "跃迁扰频", "跃迁扰断", "扰频器", "扰断器",
            }),
            (CombatCategories.Ecm, new[]
            {
                "ecm", "jammer", "electronic countermeasure",
                "电子对抗", "干扰器",
            }),
        };

        /// <summary>
        /// 给事件补上分类（同时回填 TypeID / GroupID）。返回分类 key；无法归类返回 null。
        /// </summary>
        public static string Resolve(CombatLogEvent ev)
        {
            if (ev == null)
            {
                return null;
            }

            // 中文日志：优先用 <localized hint="英文名"> 反查（中文名末尾的 * 已去掉，但仍可能查不到）
            var lookupName = !string.IsNullOrWhiteSpace(ev.ModuleHintName) ? ev.ModuleHintName : ev.ModuleName;
            if (!string.IsNullOrWhiteSpace(lookupName))
            {
                int typeId;
                int groupId;
                var category = ResolveByModule(lookupName, out typeId, out groupId);
                if (typeId > 0)
                {
                    ev.ModuleTypeId = typeId;
                    ev.ModuleGroupId = groupId;
                }

                if (!string.IsNullOrEmpty(category))
                {
                    ev.CategoryKey = category;
                    return category;
                }
            }

            string fallback;
            switch (ev.Kind)
            {
                case CombatEventKind.WarpScramble: fallback = CombatCategories.Scram; break;
                case CombatEventKind.CapacitorNeutralize: fallback = CombatCategories.Neut; break;
                case CombatEventKind.CapacitorDrain: fallback = CombatCategories.Nos; break;
                case CombatEventKind.EcmJam: fallback = CombatCategories.Ecm; break;
                case CombatEventKind.RemoteRepair: fallback = CombatCategories.Rep; break;
                case CombatEventKind.RemoteCapacitor: fallback = CombatCategories.CapTransfer; break;
                case CombatEventKind.Damage: fallback = CombatCategories.Damage; break;
                default: fallback = null; break;
            }

            ev.CategoryKey = fallback;
            return fallback;
        }

        private static string ResolveByModule(string moduleName, out int typeId, out int groupId)
        {
            typeId = 0;
            groupId = 0;
            var name = moduleName.Trim();
            if (name.Length == 0)
            {
                return null;
            }

            lock (Locker)
            {
                if (ModuleCache.TryGetValue(name, out var cached))
                {
                    (int TypeId, int GroupId)? cachedType;
                    if (TypeCache.TryGetValue(name, out cachedType) && cachedType.HasValue)
                    {
                        typeId = cachedType.Value.TypeId;
                        groupId = cachedType.Value.GroupId;
                    }

                    return cached;
                }
            }

            var resolvedType = LookupType(name);
            string category = null;
            if (resolvedType.HasValue)
            {
                typeId = resolvedType.Value.TypeId;
                groupId = resolvedType.Value.GroupId;
                string groupCategory;
                if (EnsureGroupMap().TryGetValue(groupId, out groupCategory))
                {
                    category = groupCategory;
                }
            }

            // 组里没匹配到（或查不到组）→ 直接用装备名关键词兜底
            if (category == null)
            {
                category = MatchCategoryByKeyword(name);
            }

            lock (Locker)
            {
                ModuleCache[name] = category;
                TypeCache[name] = resolvedType;
            }

            return category;
        }

        private static (int TypeId, int GroupId)? LookupType(string moduleName)
        {
            try
            {
                // 本地化库（中文名）
                if (DBService.LocalDbReady)
                {
                    var local = LocalDbService.QueryInvType(moduleName);
                    if (local != null && local.TypeID > 0)
                    {
                        var invType = InvTypeService.QueryType(local.TypeID, local: false);
                        if (invType != null)
                        {
                            return (invType.TypeID, invType.GroupID);
                        }
                    }
                }

                // 主库（英文名）。注意 QueryByName 在开启本地化时会用中文名覆盖 TypeName，
                // 这里只取第一条精确查询结果，不能再比较 TypeName。
                var type = InvTypeService.QueryByName(moduleName, isLike: false).FirstOrDefault();
                if (type != null)
                {
                    return (type.TypeID, type.GroupID);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }

            return null;
        }

        /// <summary>构建 GroupID → 分类（主库英文组名 + 本地库中文组名）。失败返回空表，不影响其它功能。</summary>
        private static Dictionary<int, string> EnsureGroupMap()
        {
            lock (Locker)
            {
                if (_groupCategories != null)
                {
                    return _groupCategories;
                }

                var map = new Dictionary<int, string>();
                try
                {
                    var groups = DBService.MainDb.Queryable<InvGroup>()
                        .Select(p => new { p.GroupID, p.GroupName })
                        .ToList();
                    foreach (var group in groups)
                    {
                        var category = MatchCategoryByKeyword(group.GroupName);
                        if (category != null)
                        {
                            map[group.GroupID] = category;
                        }
                    }

                    if (DBService.NeedLocalization && DBService.LocalDbReady)
                    {
                        var localGroups = DBService.LocalDb.Queryable<InvGroupBase>()
                            .Select(p => new { p.GroupID, p.GroupName })
                            .ToList();
                        foreach (var group in localGroups)
                        {
                            var category = MatchCategoryByKeyword(group.GroupName);
                            if (category != null)
                            {
                                map[group.GroupID] = category;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }

                _groupCategories = map;
                return map;
            }
        }

        /// <summary>组名/装备名关键词 → 分类。</summary>
        private static string MatchCategoryByKeyword(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            foreach (var rule in GroupRules)
            {
                foreach (var keyword in rule.Keywords)
                {
                    if (text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return rule.Category;
                    }
                }
            }

            return null;
        }
    }
}
