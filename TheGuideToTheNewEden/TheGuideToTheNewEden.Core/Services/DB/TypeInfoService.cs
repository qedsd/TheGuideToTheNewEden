using System;
using System.Collections.Generic;
using System.Linq;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.Core.Services.DB
{
    /// <summary>物品详情里的一条 dogma 属性（已合并本地化显示名/单位/分组）。</summary>
    public sealed class TypeInfoAttribute
    {
        public int AttributeID { get; set; }
        /// <summary>内部名（如 <c>maxVelocity</c>），用于悬浮提示与排查。</summary>
        public string Name { get; set; }
        /// <summary>显示名（本地化库优先，缺了用主库英文）。</summary>
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public double Value { get; set; }
        public int UnitID { get; set; }
        /// <summary>本地化单位显示名（m / kg / %…）。</summary>
        public string UnitText { get; set; }
        public int CategoryID { get; set; }
        public string CategoryName { get; set; }
        public bool Published { get; set; }
        /// <summary>数值越大越好。</summary>
        public bool HighIsGood { get; set; }
        /// <summary>单位是百分比（值是 0.67 这类小数，显示时要 ×100）。</summary>
        public bool IsPercent => UnitText == "%";
        /// <summary>单位是 typeID（技能 / 物品引用）。</summary>
        public bool IsTypeReference => UnitText == "typeID";
    }

    /// <summary>物品详情里的一条技能要求（来自 <c>requiredSkill1..6</c> + 对应等级）。</summary>
    public sealed class TypeInfoSkill
    {
        public int SkillTypeID { get; set; }
        public string Name { get; set; }
        public int Level { get; set; }
    }

    /// <summary>物品详情数据（对应游戏里「物品信息」窗口）。</summary>
    public sealed class TypeInfo
    {
        public int TypeID { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int GroupID { get; set; }
        public string GroupName { get; set; }
        public int CategoryID { get; set; }
        public string CategoryName { get; set; }
        public int? MarketGroupID { get; set; }
        public string MarketGroupName { get; set; }
        public int MetaGroupID { get; set; }
        public string MetaGroupName { get; set; }
        /// <summary>科技等级（MetaLevel，游戏里的 0–14）。</summary>
        public int MetaLevel { get; set; }
        public double Volume { get; set; }
        public double PackagedVolume { get; set; }
        public double Mass { get; set; }
        public double Capacity { get; set; }
        public double Radius { get; set; }
        public double BasePrice { get; set; }
        public int PortionSize { get; set; }
        public bool Published { get; set; }
        public int IconID { get; set; }
        public List<TypeInfoAttribute> Attributes { get; } = new List<TypeInfoAttribute>();
        public List<TypeInfoSkill> Skills { get; } = new List<TypeInfoSkill>();
        /// <summary>是否有蓝图（能制造/反应出来）。</summary>
        public bool HasBlueprint { get; set; }
    }

    /// <summary>
    /// 物品详情查询（阶段 283）：把主库 <c>types</c> + <c>typeDogma</c> + <c>dogmaAttributes</c> +
    /// <c>dogmaUnits</c> + <c>dogmaAttributeCategories</c> 与本地化库 <c>zh.db</c> 的同名翻译表合并。
    /// 主库永远英文，中文只来自本地化库。
    /// </summary>
    public static class TypeInfoService
    {
        public static TypeInfo GetTypeInfo(int typeId)
        {
            if (!DBService.MainDbReady || typeId <= 0)
            {
                return null;
            }

            var type = DBService.MainDb.Queryable<InvTypeFull>().First(p => p.TypeID == typeId);
            if (type == null)
            {
                return null;
            }

            var info = new TypeInfo
            {
                TypeID = type.TypeID,
                Name = type.Name,
                Description = type.Description,
                GroupID = type.GroupID,
                MarketGroupID = type.MarketGroupID,
                MetaGroupID = type.MetaGroupID,
                MetaLevel = type.MetaLevel,
                Volume = type.Volume,
                PackagedVolume = type.PackagedVolume,
                Mass = type.Mass,
                Capacity = type.Capacity,
                Radius = type.Radius,
                BasePrice = type.BasePrice,
                PortionSize = type.PortionSize,
                Published = type.Published,
                IconID = type.IconID,
            };

            // 名称/描述/组/类目：本地化库优先
            TryLocalizeType(info);

            var values = DBService.MainDb.Queryable<TypeDogma>().Where(p => p.TypeID == typeId).ToList();
            if (values.Count == 0)
            {
                return info;
            }

            var attrIds = values.Select(p => p.AttributeID).Distinct().ToList();
            var defs = DBService.MainDb.Queryable<DogmaAttribute>().Where(p => attrIds.Contains(p.AttributeID)).ToList();
            if (defs.Count == 0)
            {
                return info;
            }

            var units = DBService.MainDb.Queryable<DogmaUnit>().ToList();
            var categories = DBService.MainDb.Queryable<DogmaAttributeCategory>().ToList();

            var localAttrs = SafeLocal(() => LocalDbService.TranDogmaAttributes(attrIds))
                .ToDictionary(p => p.AttributeID, p => p);
            var localUnits = SafeLocal(() => LocalDbService.TranDogmaUnits())
                .ToDictionary(p => p.UnitID, p => p);
            var localCategories = SafeLocal(() => LocalDbService.TranDogmaAttributeCategories())
                .ToDictionary(p => p.CategoryID, p => p);

            var unitMap = units.ToDictionary(p => p.UnitID, p => p);
            var categoryMap = categories.ToDictionary(p => p.CategoryID, p => p);

            foreach (var def in defs.OrderBy(p => p.AttributeCategoryID).ThenBy(p => p.AttributeID))
            {
                var value = values.First(p => p.AttributeID == def.AttributeID).Value;

                unitMap.TryGetValue(def.UnitID, out var unit);
                var unitText = localUnits.TryGetValue(def.UnitID, out var localUnit) && !string.IsNullOrWhiteSpace(localUnit.DisplayName)
                    ? localUnit.DisplayName
                    : unit?.DisplayName ?? string.Empty;

                categoryMap.TryGetValue(def.AttributeCategoryID, out var category);
                var categoryName = localCategories.TryGetValue(def.AttributeCategoryID, out var localCategory) && !string.IsNullOrWhiteSpace(localCategory.Name)
                    ? localCategory.Name
                    : category?.Name ?? string.Empty;

                var displayName = localAttrs.TryGetValue(def.AttributeID, out var localAttr) && !string.IsNullOrWhiteSpace(localAttr.DisplayName)
                    ? localAttr.DisplayName
                    : (!string.IsNullOrWhiteSpace(def.DisplayName) ? def.DisplayName : def.Name);

                var description = localAttrs.TryGetValue(def.AttributeID, out var localDesc) && !string.IsNullOrWhiteSpace(localDesc.Description)
                    ? localDesc.Description
                    : def.Description;

                info.Attributes.Add(new TypeInfoAttribute
                {
                    AttributeID = def.AttributeID,
                    Name = def.Name,
                    DisplayName = displayName,
                    Description = description,
                    Value = value,
                    UnitID = def.UnitID,
                    UnitText = unitText,
                    CategoryID = def.AttributeCategoryID,
                    CategoryName = categoryName,
                    Published = def.Published,
                    HighIsGood = def.HighIsGood,
                });
            }

            BuildSkills(info, values);

            info.HasBlueprint = DBService.MainDb.Queryable<BlueprintProduct>()
                .Any(p => p.ProductTypeID == typeId && p.ActivityID == IndustryActivity.Manufacturing);

            return info;
        }

        /// <summary>技能要求：<c>requiredSkill1..6</c> 的值是技能 TypeID，配套 <c>requiredSkillNLevel</c> 是等级。</summary>
        private static void BuildSkills(TypeInfo info, List<TypeDogma> values)
        {
            var levels = new Dictionary<int, int>();

            foreach (var item in values)
            {
                // requiredSkill1 / requiredSkill1Level
                var name = info.Attributes.FirstOrDefault(p => p.AttributeID == item.AttributeID)?.Name;
                if (string.IsNullOrEmpty(name) || !name.StartsWith("requiredSkill", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var suffix = name.Substring("requiredSkill".Length);
                if (suffix.EndsWith("Level", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(suffix.Substring(0, suffix.Length - "Level".Length), out var levelIndex))
                    {
                        levels[levelIndex] = (int)Math.Round(item.Value);
                    }
                }

            }

            // 收集 requiredSkillN 的 TypeID，再换成技能名
            var pairs = new List<(int Index, int SkillTypeId)>();
            foreach (var attr in info.Attributes.Where(p => p.Name != null
                         && p.Name.StartsWith("requiredSkill", StringComparison.OrdinalIgnoreCase)
                         && !p.Name.EndsWith("Level", StringComparison.OrdinalIgnoreCase)))
            {
                var suffix = attr.Name.Substring("requiredSkill".Length);
                if (int.TryParse(suffix, out var index) && attr.Value > 0)
                {
                    pairs.Add((index, (int)attr.Value));
                }
            }

            if (pairs.Count == 0)
            {
                return;
            }

            var ids = pairs.Select(p => p.SkillTypeId).Distinct().ToList();
            var types = InvTypeService.QueryTypes(ids);
            foreach (var pair in pairs.OrderBy(p => p.Index))
            {
                var skill = types.FirstOrDefault(p => p.TypeID == pair.SkillTypeId);
                info.Skills.Add(new TypeInfoSkill
                {
                    SkillTypeID = pair.SkillTypeId,
                    Name = skill?.TypeName ?? "#" + pair.SkillTypeId,
                    Level = levels.TryGetValue(pair.Index, out var level) ? level : 1,
                });
            }
        }

        private static void TryLocalizeType(TypeInfo info)
        {
            if (!DBService.LocalDbReady)
            {
                return;
            }

            try
            {
                var localType = LocalDbService.TranInvType(info.TypeID);
                if (localType != null)
                {
                    if (!string.IsNullOrWhiteSpace(localType.TypeName)) info.Name = localType.TypeName;
                    if (!string.IsNullOrWhiteSpace(localType.Description)) info.Description = localType.Description;
                }

                var groups = LocalDbService.TranInvGroups(new List<int> { info.GroupID });
                if (groups != null && groups.Count > 0 && !string.IsNullOrWhiteSpace(groups[0].GroupName))
                {
                    info.GroupName = groups[0].GroupName;
                }

                // 类目：主库组表给出 CategoryID，名称再走本地化
                var mainGroup = DBService.MainDb.Queryable<InvGroup>().First(p => p.GroupID == info.GroupID);
                if (mainGroup != null)
                {
                    info.CategoryID = mainGroup.CategoryID;
                    var category = LocalDbService.TranInvCategories(new List<int> { mainGroup.CategoryID });
                    if (category != null && category.Count > 0 && !string.IsNullOrWhiteSpace(category[0].CategoryName))
                    {
                        info.CategoryName = category[0].CategoryName;
                    }
                }

                if (info.MarketGroupID.HasValue)
                {
                    var marketGroup = LocalDbService.TranInvMarketGroup(info.MarketGroupID.Value);
                    if (marketGroup != null && !string.IsNullOrWhiteSpace(marketGroup.MarketGroupName))
                    {
                        info.MarketGroupName = marketGroup.MarketGroupName;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        private static List<T> SafeLocal<T>(Func<List<T>> query)
        {
            try
            {
                return query() ?? new List<T>();
            }
            catch (Exception ex)
            {
                Log.Warn(ex.Message);
                return new List<T>();
            }
        }
    }
}
