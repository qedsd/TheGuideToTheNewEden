using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.Indusrty;

namespace TheGuideToTheNewEden.Core.Services.DB
{
    /// <summary>
    /// 工业数据查询（蓝图 / 活动 / 材料 / 产物 / 技能 / 回收 / dogma）。
    /// <para>
    /// 数据来自主库（SDE 构建产出的 <c>blueprints</c> / <c>blueprintActivities</c> /
    /// <c>blueprintMaterials</c> / <c>blueprintProducts</c> / <c>blueprintSkills</c> /
    /// <c>typeMaterials</c> / <c>typeDogma</c> 表）。
    /// </para>
    /// <para>
    /// 主库未载入时统一返回空集合 / null，不抛异常（与其它 Core DB 服务的约定一致）。
    /// 所有方法都是同步的；BOM 递归请优先使用批量重载（一次查一页，避免逐条查库）。
    /// </para>
    /// </summary>
    public static class IndustryDbService
    {
        private static SqlSugarClient Db => DBService.MainDb;

        /// <summary>工业数据是否可用（主库已载入）。</summary>
        public static bool IsAvailable => Db != null;

        private static List<int> Normalize(IEnumerable<int> ids)
        {
            return ids == null ? null : ids.Where(p => p > 0).Distinct().ToList();
        }

        // ───────────────────────── 蓝图头 ─────────────────────────

        public static Blueprint GetBlueprint(int blueprintTypeId)
        {
            var db = Db;
            if (db == null || blueprintTypeId <= 0)
            {
                return null;
            }
            try
            {
                return db.Queryable<Blueprint>().First(p => p.BlueprintTypeID == blueprintTypeId);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return null;
            }
        }

        public static List<Blueprint> GetBlueprints(IEnumerable<int> blueprintTypeIds)
        {
            var ids = Normalize(blueprintTypeIds);
            var db = Db;
            if (db == null || ids == null || ids.Count == 0)
            {
                return new List<Blueprint>();
            }
            try
            {
                return db.Queryable<Blueprint>().Where(p => ids.Contains(p.BlueprintTypeID)).ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return new List<Blueprint>();
            }
        }

        // ─────────────────── 产物 → 蓝图 反查 ───────────────────

        /// <summary>
        /// 反查生产某产物的蓝图：优先制造（1），其次反应（11）。
        /// 查不到（原矿 / 行星产物等没有蓝图的物品）返回 null。
        /// </summary>
        public static int? GetBlueprintTypeIdByProduct(int productTypeId, int activityId = IndustryActivity.Manufacturing)
        {
            var db = Db;
            if (db == null || productTypeId <= 0)
            {
                return null;
            }
            try
            {
                return db.Queryable<BlueprintProduct>()
                    .Where(p => p.ProductTypeID == productTypeId && p.ActivityID == activityId)
                    .OrderBy(p => p.BlueprintTypeID)
                    .Select(p => (int?)p.BlueprintTypeID)
                    .First();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return null;
            }
        }

        /// <summary>批量反查产物 → 蓝图（同一产物命中多张蓝图时取 BlueprintTypeID 最小者）。</summary>
        public static Dictionary<int, int> GetBlueprintTypeIdByProducts(IEnumerable<int> productTypeIds, int activityId = IndustryActivity.Manufacturing)
        {
            var result = new Dictionary<int, int>();
            var ids = Normalize(productTypeIds);
            var db = Db;
            if (db == null || ids == null || ids.Count == 0)
            {
                return result;
            }
            try
            {
                var rows = db.Queryable<BlueprintProduct>()
                    .Where(p => ids.Contains(p.ProductTypeID) && p.ActivityID == activityId)
                    .OrderBy(p => p.BlueprintTypeID)
                    .Select(p => new { p.ProductTypeID, p.BlueprintTypeID })
                    .ToList();
                foreach (var row in rows)
                {
                    if (!result.ContainsKey(row.ProductTypeID))
                    {
                        result[row.ProductTypeID] = row.BlueprintTypeID;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
            return result;
        }

        // ───────────────────────── 活动 ─────────────────────────

        public static List<BlueprintActivity> GetActivities(int blueprintTypeId)
        {
            var db = Db;
            if (db == null || blueprintTypeId <= 0)
            {
                return new List<BlueprintActivity>();
            }
            try
            {
                return db.Queryable<BlueprintActivity>().Where(p => p.BlueprintTypeID == blueprintTypeId).ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return new List<BlueprintActivity>();
            }
        }

        public static Dictionary<int, List<BlueprintActivity>> GetActivities(IEnumerable<int> blueprintTypeIds)
        {
            var result = new Dictionary<int, List<BlueprintActivity>>();
            var ids = Normalize(blueprintTypeIds);
            var db = Db;
            if (db == null || ids == null || ids.Count == 0)
            {
                return result;
            }
            try
            {
                var rows = db.Queryable<BlueprintActivity>().Where(p => ids.Contains(p.BlueprintTypeID)).ToList();
                foreach (var group in rows.GroupBy(p => p.BlueprintTypeID))
                {
                    result[group.Key] = group.ToList();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
            return result;
        }

        // ───────────────────────── 材料 ─────────────────────────

        public static List<BlueprintMaterial> GetMaterials(int blueprintTypeId, int activityId = IndustryActivity.Manufacturing)
        {
            var db = Db;
            if (db == null || blueprintTypeId <= 0)
            {
                return new List<BlueprintMaterial>();
            }
            try
            {
                return db.Queryable<BlueprintMaterial>()
                    .Where(p => p.BlueprintTypeID == blueprintTypeId && p.ActivityID == activityId)
                    .ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return new List<BlueprintMaterial>();
            }
        }

        public static Dictionary<int, List<BlueprintMaterial>> GetMaterials(IEnumerable<int> blueprintTypeIds, int activityId = IndustryActivity.Manufacturing)
        {
            var result = new Dictionary<int, List<BlueprintMaterial>>();
            var ids = Normalize(blueprintTypeIds);
            var db = Db;
            if (db == null || ids == null || ids.Count == 0)
            {
                return result;
            }
            try
            {
                var rows = db.Queryable<BlueprintMaterial>()
                    .Where(p => ids.Contains(p.BlueprintTypeID) && p.ActivityID == activityId)
                    .ToList();
                foreach (var group in rows.GroupBy(p => p.BlueprintTypeID))
                {
                    result[group.Key] = group.ToList();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
            return result;
        }

        // ───────────────────────── 产物 ─────────────────────────

        public static List<BlueprintProduct> GetProducts(int blueprintTypeId, int activityId = IndustryActivity.Manufacturing)
        {
            var db = Db;
            if (db == null || blueprintTypeId <= 0)
            {
                return new List<BlueprintProduct>();
            }
            try
            {
                return db.Queryable<BlueprintProduct>()
                    .Where(p => p.BlueprintTypeID == blueprintTypeId && p.ActivityID == activityId)
                    .ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return new List<BlueprintProduct>();
            }
        }

        public static Dictionary<int, List<BlueprintProduct>> GetProducts(IEnumerable<int> blueprintTypeIds, int activityId = IndustryActivity.Manufacturing)
        {
            var result = new Dictionary<int, List<BlueprintProduct>>();
            var ids = Normalize(blueprintTypeIds);
            var db = Db;
            if (db == null || ids == null || ids.Count == 0)
            {
                return result;
            }
            try
            {
                var rows = db.Queryable<BlueprintProduct>()
                    .Where(p => ids.Contains(p.BlueprintTypeID) && p.ActivityID == activityId)
                    .ToList();
                foreach (var group in rows.GroupBy(p => p.BlueprintTypeID))
                {
                    result[group.Key] = group.ToList();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
            return result;
        }

        // ───────────────────────── 技能 ─────────────────────────

        public static List<BlueprintSkill> GetSkills(int blueprintTypeId, int activityId = IndustryActivity.Manufacturing)
        {
            var db = Db;
            if (db == null || blueprintTypeId <= 0)
            {
                return new List<BlueprintSkill>();
            }
            try
            {
                return db.Queryable<BlueprintSkill>()
                    .Where(p => p.BlueprintTypeID == blueprintTypeId && p.ActivityID == activityId)
                    .ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return new List<BlueprintSkill>();
            }
        }

        public static Dictionary<int, List<BlueprintSkill>> GetSkills(IEnumerable<int> blueprintTypeIds, int activityId = IndustryActivity.Manufacturing)
        {
            var result = new Dictionary<int, List<BlueprintSkill>>();
            var ids = Normalize(blueprintTypeIds);
            var db = Db;
            if (db == null || ids == null || ids.Count == 0)
            {
                return result;
            }
            try
            {
                var rows = db.Queryable<BlueprintSkill>()
                    .Where(p => ids.Contains(p.BlueprintTypeID) && p.ActivityID == activityId)
                    .ToList();
                foreach (var group in rows.GroupBy(p => p.BlueprintTypeID))
                {
                    result[group.Key] = group.ToList();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
            return result;
        }

        // ───────────────────────── 回收 / dogma ─────────────────────────

        public static List<TypeMaterial> GetTypeMaterials(int typeId)
        {
            var db = Db;
            if (db == null || typeId <= 0)
            {
                return new List<TypeMaterial>();
            }
            try
            {
                return db.Queryable<TypeMaterial>().Where(p => p.TypeID == typeId).ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return new List<TypeMaterial>();
            }
        }

        public static Dictionary<int, List<TypeMaterial>> GetTypeMaterials(IEnumerable<int> typeIds)
        {
            var result = new Dictionary<int, List<TypeMaterial>>();
            var ids = Normalize(typeIds);
            var db = Db;
            if (db == null || ids == null || ids.Count == 0)
            {
                return result;
            }
            try
            {
                var rows = db.Queryable<TypeMaterial>().Where(p => ids.Contains(p.TypeID)).ToList();
                foreach (var group in rows.GroupBy(p => p.TypeID))
                {
                    result[group.Key] = group.ToList();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
            return result;
        }

        public static List<TypeDogma> GetTypeDogma(int typeId)
        {
            var db = Db;
            if (db == null || typeId <= 0)
            {
                return new List<TypeDogma>();
            }
            try
            {
                return db.Queryable<TypeDogma>().Where(p => p.TypeID == typeId).ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return new List<TypeDogma>();
            }
        }

        /// <summary>读取某物品的单个 dogma 属性值；不存在返回 null。</summary>
        public static double? GetDogmaValue(int typeId, int attributeId)
        {
            var db = Db;
            if (db == null || typeId <= 0 || attributeId <= 0)
            {
                return null;
            }
            try
            {
                return db.Queryable<TypeDogma>()
                    .Where(p => p.TypeID == typeId && p.AttributeID == attributeId)
                    .Select(p => (double?)p.Value)
                    .First();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return null;
            }
        }

        // ───────────────────── 物品摘要（types + groups） ─────────────────────

        public static IndustryTypeInfo GetTypeInfo(int typeId)
        {
            var list = GetTypeInfos(new[] { typeId });
            return list.Count > 0 ? list[0] : null;
        }

        public static List<IndustryTypeInfo> GetTypeInfos(IEnumerable<int> typeIds)
        {
            var result = new List<IndustryTypeInfo>();
            var ids = Normalize(typeIds);
            var db = Db;
            if (db == null || ids == null || ids.Count == 0)
            {
                return result;
            }
            try
            {
                var types = db.Queryable<InvType>().Where(p => ids.Contains(p.TypeID)).ToList();
                if (types.Count == 0)
                {
                    return result;
                }
                var groupIds = types.Select(p => p.GroupID).Distinct().ToList();
                var groups = db.Queryable<InvGroup>().Where(p => groupIds.Contains(p.GroupID)).ToList()
                    .ToDictionary(p => p.GroupID);
                foreach (var type in types)
                {
                    groups.TryGetValue(type.GroupID, out var group);
                    result.Add(new IndustryTypeInfo
                    {
                        TypeID = type.TypeID,
                        TypeName = type.TypeName,
                        GroupID = type.GroupID,
                        GroupName = group?.GroupName,
                        CategoryID = group?.CategoryID ?? 0,
                        Volume = type.Volume,
                        PackagedVolume = type.PackagedVolume
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
            return result;
        }

        // ───────────────────── 蓝图浏览器 ─────────────────────

        /// <summary>
        /// 搜索"可制造（或可反应）的产物"：先按名称模糊搜索物品（含本地化名），再反查生产它的蓝图。
        /// 用于蓝图浏览器。
        /// </summary>
        public static List<IndustryProductEntry> SearchManufacturableProducts(string query, int limit = 50)
        {
            var result = new List<IndustryProductEntry>();
            if (string.IsNullOrWhiteSpace(query) || limit <= 0)
            {
                return result;
            }
            var db = Db;
            if (db == null)
            {
                return result;
            }

            try
            {
                // InvTypeService.Search 已合并主库英文名与本地化库中文名。
                var items = InvTypeService.Search(query);
                if (items == null)
                {
                    return result;
                }

                var seen = new HashSet<int>();
                foreach (var item in items)
                {
                    if (item.Type != TheGuideToTheNewEden.Core.Enums.DataBaseItemType.InvType || !seen.Add(item.ID))
                    {
                        continue;
                    }

                    int activityId = IndustryActivity.Manufacturing;
                    var blueprintTypeId = GetBlueprintTypeIdByProduct(item.ID, IndustryActivity.Manufacturing);
                    if (!blueprintTypeId.HasValue)
                    {
                        activityId = IndustryActivity.Reaction;
                        blueprintTypeId = GetBlueprintTypeIdByProduct(item.ID, IndustryActivity.Reaction);
                    }
                    if (!blueprintTypeId.HasValue)
                    {
                        continue;
                    }

                    var info = GetTypeInfo(item.ID);
                    result.Add(new IndustryProductEntry
                    {
                        ProductTypeId = item.ID,
                        ProductName = item.Name,
                        BlueprintTypeId = blueprintTypeId.Value,
                        BlueprintName = InvTypeService.QueryType(blueprintTypeId.Value)?.TypeName,
                        ActivityId = activityId,
                        MaxProductionLimit = GetBlueprint(blueprintTypeId.Value)?.MaxProductionLimit ?? 0,
                        GroupId = info?.GroupID ?? 0,
                        GroupName = info?.GroupName,
                        CategoryId = info?.CategoryID ?? 0,
                    });

                    if (result.Count >= limit)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
            return result;
        }
    }
}
