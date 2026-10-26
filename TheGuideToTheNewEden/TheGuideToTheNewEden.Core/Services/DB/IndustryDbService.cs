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

        /// <summary>主库记录的 SDE buildNumber；用于判断本地库是否落后于下载目录。</summary>
        public static int? GetSdeBuildNumber()
        {
            var db = Db;
            if (db == null) return null;
            try
            {
                var value = db.Ado.GetInt("select BuildNumber from sdeInfo where Id = 'sde'");
                return value > 0 ? value : (int?)null;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return null;
            }
        }

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

        // ───────────────────── 工业辅助数据 ─────────────────────

        /// <summary>全部物品分类（用于按物类 rig 选择）。</summary>
        public static List<InvCategory> GetCategories()
        {
            var db = Db;
            if (db == null)
            {
                return new List<InvCategory>();
            }
            try
            {
                return db.Queryable<InvCategory>()
                    .OrderBy(p => p.CategoryName)
                    .ToList()
                    .Where(p => p.CategoryID > 0
                        && !string.IsNullOrWhiteSpace(p.CategoryName)
                        && !p.CategoryName.StartsWith("#"))
                    .ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return new List<InvCategory>();
            }
        }

        /// <summary>全部发明解密器（Decryptors / Generic / Salvage 组），带 1112/1113/1114/1124 属性。</summary>
        public static List<IndustryDecryptorInfo> GetDecryptorTypes()
        {
            var result = new List<IndustryDecryptorInfo>();
            var db = Db;
            if (db == null)
            {
                return result;
            }
            try
            {
                var groups = db.Queryable<InvGroup>().ToList()
                    .Where(p => p.GroupName != null
                        && (p.GroupName.StartsWith("Decryptors", StringComparison.OrdinalIgnoreCase)
                            || p.GroupName.Equals("Generic Decryptor", StringComparison.OrdinalIgnoreCase)
                            || p.GroupName.Equals("Salvage Decryptors", StringComparison.OrdinalIgnoreCase)))
                    .Select(p => p.GroupID)
                    .Distinct()
                    .ToList();
                if (groups.Count == 0)
                {
                    return result;
                }

                var types = db.Queryable<InvType>()
                    .Where(p => groups.Contains(p.GroupID))
                    .OrderBy(p => p.TypeName)
                    .ToList()
                    .Where(p => p.TypeID > 0 && !string.IsNullOrWhiteSpace(p.TypeName) && !p.TypeName.StartsWith("#"))
                    .ToList();
                foreach (var type in types)
                {
                    result.Add(BuildDecryptorInfo(type.TypeID, type.TypeName));
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
            return result;
        }

        /// <summary>读取单个解密器的工业属性；不存在返回 null。</summary>
        public static IndustryDecryptorInfo GetDecryptorInfo(int typeId)
        {
            if (typeId <= 0)
            {
                return null;
            }
            var info = GetTypeInfo(typeId);
            return info == null ? null : BuildDecryptorInfo(typeId, info.TypeName);
        }

        private static IndustryDecryptorInfo BuildDecryptorInfo(int typeId, string name)
        {
            return new IndustryDecryptorInfo
            {
                TypeID = typeId,
                Name = name,
                ProbabilityMultiplier = GetDogmaValue(typeId, 1112) ?? 1.0,
                MeModifier = (int)Math.Round(GetDogmaValue(typeId, 1113) ?? 0),
                TeModifier = (int)Math.Round(GetDogmaValue(typeId, 1114) ?? 0),
                RunModifier = (int)Math.Round(GetDogmaValue(typeId, 1124) ?? 0),
            };
        }

        /// <summary>可压缩物品 → 压缩后物品；不可压缩返回 null。</summary>
        public static CompressibleType GetCompressibleType(int typeId)
        {
            var db = Db;
            if (db == null || typeId <= 0)
            {
                return null;
            }
            try
            {
                return db.Queryable<CompressibleType>().First(p => p.Id == typeId);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return null;
            }
        }

        /// <summary>
        /// 全部可制造/可反应产物（用于常驻物品树）。一次 JOIN 取回，避免逐物品反查。
        /// </summary>
        public static List<IndustryProductEntry> GetAllManufacturableProducts()
        {
            var db = Db;
            if (db == null)
            {
                return new List<IndustryProductEntry>();
            }
            try
            {
                const string sql = @"
WITH first_bp AS (
    SELECT ProductTypeID, MIN(BlueprintTypeID) AS BlueprintTypeID
    FROM blueprintProducts
    WHERE ActivityID IN (1, 11)
    GROUP BY ProductTypeID
)
SELECT bp.ProductTypeID AS ProductTypeId,
       pt.Name AS ProductName,
       bp.BlueprintTypeID AS BlueprintTypeId,
       bt.Name AS BlueprintName,
       bp.ActivityID AS ActivityId,
       COALESCE(b.MaxProductionLimit, 0) AS MaxProductionLimit,
       g.Id AS GroupId,
       g.Name AS GroupName,
       c.Id AS CategoryId,
       c.Name AS CategoryName
FROM first_bp f
JOIN blueprintProducts bp ON bp.ProductTypeID = f.ProductTypeID AND bp.BlueprintTypeID = f.BlueprintTypeID
JOIN types pt ON pt.Id = bp.ProductTypeID
JOIN types bt ON bt.Id = bp.BlueprintTypeID
JOIN groups g ON g.Id = pt.GroupID
JOIN categories c ON c.Id = g.CategoryID
LEFT JOIN blueprints b ON b.BlueprintTypeID = bp.BlueprintTypeID
WHERE pt.Published = 1
  AND pt.Id > 0
  AND pt.Name NOT LIKE '#%'
UNION ALL
SELECT pm.type_id AS ProductTypeId,
       pt.Name AS ProductName,
       0 AS BlueprintTypeId,
       '' AS BlueprintName,
       100 AS ActivityId,
       0 AS MaxProductionLimit,
       g.Id AS GroupId,
       g.Name AS GroupName,
       c.Id AS CategoryId,
       c.Name AS CategoryName
FROM planetSchematicsTypeMap pm
JOIN types pt ON pt.Id = pm.type_id
JOIN groups g ON g.Id = pt.GroupID
JOIN categories c ON c.Id = g.CategoryID
WHERE pm.is_input = 0
  AND pt.Published = 1
  AND pt.Id > 0
  AND pt.Name NOT LIKE '#%'
  AND EXISTS (SELECT 1 FROM planetSchematics s WHERE s.schematic_id = pm.schematic_id)
ORDER BY CategoryName, GroupName, ProductName";
                return db.Ado.SqlQuery<IndustryProductEntry>(sql);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return new List<IndustryProductEntry>();
            }
        }

        // ───────────────────── 官方工业元数据 ─────────────────────

        /// <summary>官方工业活动定义（反应官方 ID 为 9；应用内部仍用 11 兼容旧库）。</summary>
        public static List<IndustryActivityDefinition> GetIndustryActivities()
        {
            var db = Db;
            if (db == null) return new List<IndustryActivityDefinition>();
            try
            {
                return db.Queryable<IndustryActivityDefinition>().OrderBy(p => p.Id).ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return new List<IndustryActivityDefinition>();
            }
        }

        /// <summary>装配线及其分类/组倍率明细。</summary>
        public static List<IndustryAssemblyLineInfo> GetIndustryAssemblyLines()
        {
            var db = Db;
            if (db == null) return new List<IndustryAssemblyLineInfo>();
            try
            {
                var lines = db.Queryable<IndustryAssemblyLine>().OrderBy(p => p.Id).ToList();
                var details = db.Queryable<IndustryAssemblyLineDetail>().ToList()
                    .GroupBy(p => p.AssemblyLineID)
                    .ToDictionary(p => p.Key, p => p.ToList());
                return lines.Select(p => new IndustryAssemblyLineInfo
                {
                    Line = p,
                    Details = details.TryGetValue(p.Id, out var rows) ? rows : new List<IndustryAssemblyLineDetail>(),
                }).ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return new List<IndustryAssemblyLineInfo>();
            }
        }

        /// <summary>设施类型可用的装配线 ID（解析 SDE 的逗号分隔列）。</summary>
        public static List<int> GetInstallationAssemblyLineIds(int installationTypeId)
        {
            var db = Db;
            if (db == null || installationTypeId <= 0) return new List<int>();
            try
            {
                var row = db.Queryable<IndustryInstallationType>()
                    .Where(p => p.InstallationTypeID == installationTypeId)
                    .First();
                if (row == null || string.IsNullOrWhiteSpace(row.AssemblyLineIDs)) return new List<int>();
                return row.AssemblyLineIDs
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => int.TryParse(p, out var id) ? id : 0)
                    .Where(p => p > 0)
                    .Distinct()
                    .ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return new List<int>();
            }
        }

        /// <summary>结构/装备类型在某活动上使用的 dogma 属性来源。</summary>
        public static List<IndustryModifierSource> GetIndustryModifierSources(int sourceTypeId)
        {
            var db = Db;
            if (db == null || sourceTypeId <= 0) return new List<IndustryModifierSource>();
            try
            {
                return db.Queryable<IndustryModifierSource>()
                    .Where(p => p.SourceTypeID == sourceTypeId)
                    .OrderBy(p => p.ActivityID)
                    .OrderBy(p => p.ModifierType)
                    .ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return new List<IndustryModifierSource>();
            }
        }

        /// <summary>
        /// 解析结构的官方加成：modifierSources 给出活动/类型/属性 ID，
        /// typeDogma 给出属性值（如 Raitaru 材料 0.99、时间 0.85、费用 0.97）。
        /// </summary>
        public static IndustryStructureModifiers GetStructureModifiers(int structureTypeId)
        {
            var db = Db;
            if (db == null || structureTypeId <= 0) return null;
            try
            {
                var sources = GetIndustryModifierSources(structureTypeId);
                if (sources.Count == 0) return null;
                var attributeIds = sources.Select(p => p.DogmaAttributeID).Distinct().ToList();
                var values = db.Queryable<TypeDogma>()
                    .Where(p => p.TypeID == structureTypeId && attributeIds.Contains(p.AttributeID))
                    .ToList()
                    .GroupBy(p => p.AttributeID)
                    .ToDictionary(p => p.Key, p => p.First().Value);
                var result = new IndustryStructureModifiers
                {
                    StructureTypeId = structureTypeId,
                    StructureName = InvTypeService.QueryType(structureTypeId)?.TypeName ?? structureTypeId.ToString(),
                };
                foreach (var group in sources.GroupBy(p => p.ActivityID))
                {
                    var modifier = new IndustryActivityModifier();
                    foreach (var source in group)
                    {
                        if (!values.TryGetValue(source.DogmaAttributeID, out var value) || value <= 0)
                        {
                            continue;
                        }
                        switch (source.ModifierType)
                        {
                            case IndustryModifierType.Cost:
                                modifier.Cost *= value;
                                break;
                            case IndustryModifierType.Time:
                                modifier.Time *= value;
                                break;
                            case IndustryModifierType.Material:
                                modifier.Material *= value;
                                break;
                        }
                    }
                    result.Activities[group.Key] = modifier;
                }
                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return null;
            }
        }

        /// <summary>可作为建筑档案结构选项的类型（来自 industryModifierSources，按名称排序）。</summary>
        public static List<IndustryStructureOption> GetIndustryStructureOptions()
        {
            var db = Db;
            if (db == null) return new List<IndustryStructureOption>();
            try
            {
                var ids = db.Queryable<IndustryModifierSource>()
                    .Select(p => p.SourceTypeID)
                    .Distinct()
                    .ToList();
                var result = new List<IndustryStructureOption>();
                foreach (var id in ids)
                {
                    var type = InvTypeService.QueryType(id);
                    // 只列结构本体（Upwell 工程复合体/精炼厂），不把结构 rig 当成建筑。
                    if (type == null || (type.GroupID != 1404 && type.GroupID != 1406)) continue;
                    var modifiers = GetStructureModifiers(id);
                    if (modifiers == null) continue;
                    modifiers.Activities.TryGetValue(IndustryActivity.Manufacturing, out var manufacturing);
                    result.Add(new IndustryStructureOption
                    {
                        TypeId = id,
                        Name = modifiers.StructureName,
                        MaterialMultiplier = manufacturing?.Material ?? 1.0,
                        TimeMultiplier = manufacturing?.Time ?? 1.0,
                        CostMultiplier = manufacturing?.Cost ?? 1.0,
                    });
                }
                return result.OrderBy(p => p.Name).ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return new List<IndustryStructureOption>();
            }
        }

        /// <summary>官方目标过滤器（分类/组集合）。</summary>
        public static List<IndustryTargetFilterInfo> GetIndustryTargetFilters()
        {
            var db = Db;
            if (db == null) return new List<IndustryTargetFilterInfo>();
            try
            {
                var filters = db.Queryable<IndustryTargetFilterDefinition>().OrderBy(p => p.Id).ToList();
                var categories = db.Queryable<IndustryTargetFilterCategory>().ToList()
                    .GroupBy(p => p.FilterID)
                    .ToDictionary(p => p.Key, p => p.Select(x => x.CategoryID).ToHashSet());
                var groups = db.Queryable<IndustryTargetFilterGroup>().ToList()
                    .GroupBy(p => p.FilterID)
                    .ToDictionary(p => p.Key, p => p.Select(x => x.GroupID).ToHashSet());
                return filters.Select(p => new IndustryTargetFilterInfo
                {
                    Id = p.Id,
                    Name = p.Name,
                    CategoryIds = categories.TryGetValue(p.Id, out var c) ? c : new HashSet<int>(),
                    GroupIds = groups.TryGetValue(p.Id, out var g) ? g : new HashSet<int>(),
                }).ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return new List<IndustryTargetFilterInfo>();
            }
        }

        /// <summary>按分类/组判断是否命中目标过滤器。</summary>
        public static bool MatchesTargetFilter(int filterId, int categoryId, int groupId)
        {
            if (filterId <= 0) return false;
            var filter = GetIndustryTargetFilters().FirstOrDefault(p => p.Id == filterId);
            if (filter == null) return false;
            return filter.CategoryIds.Contains(categoryId) || filter.GroupIds.Contains(groupId);
        }

        /// <summary>按产物反查行星 schematic（planetSchematicsTypeMap 的 is_input=0）。</summary>
        public static IndustryPlanetaryRecipe GetPlanetaryRecipeByProduct(int productTypeId)
        {
            var db = Db;
            if (db == null || productTypeId <= 0)
            {
                return null;
            }
            try
            {
                var output = db.Queryable<PlanetSchematicTypeMap>()
                    .Where(p => p.TypeId == productTypeId && !p.IsInput)
                    .OrderBy(p => p.SchematicId)
                    .First();
                if (output == null || output.SchematicId <= 0)
                {
                    return null;
                }

                var schematic = db.Queryable<PlanetSchematic>()
                    .Where(p => p.SchematicId == output.SchematicId)
                    .First();
                var maps = db.Queryable<PlanetSchematicTypeMap>()
                    .Where(p => p.SchematicId == output.SchematicId)
                    .ToList();
                return new IndustryPlanetaryRecipe
                {
                    SchematicId = output.SchematicId,
                    ProductTypeID = productTypeId,
                    Name = schematic?.SchematicName ?? string.Empty,
                    CycleTime = schematic?.CycleTime ?? 0,
                    OutputQuantity = output.Quantity,
                    Inputs = maps
                        .Where(p => p.IsInput && p.Quantity > 0)
                        .Select(p => new IndustryPlanetaryInput
                        {
                            TypeID = p.TypeId,
                            Quantity = p.Quantity,
                        })
                        .ToList(),
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return null;
            }
        }

        /// <summary>全部行星开发 schematic（以输出产物为一条配方）。</summary>
        public static List<IndustryPlanetaryRecipe> GetAllPlanetaryRecipes()
        {
            var db = Db;
            if (db == null)
            {
                return new List<IndustryPlanetaryRecipe>();
            }
            try
            {
                var schematics = db.Queryable<PlanetSchematic>().ToList()
                    .ToDictionary(p => p.SchematicId);
                var maps = db.Queryable<PlanetSchematicTypeMap>().ToList()
                    .GroupBy(p => p.SchematicId)
                    .ToDictionary(p => p.Key, p => p.ToList());

                var result = new List<IndustryPlanetaryRecipe>();
                foreach (var group in maps)
                {
                    schematics.TryGetValue(group.Key, out var schematic);
                    var output = group.Value.FirstOrDefault(p => !p.IsInput && p.Quantity > 0);
                    if (output == null)
                    {
                        continue;
                    }
                    result.Add(new IndustryPlanetaryRecipe
                    {
                        SchematicId = group.Key,
                        ProductTypeID = output.TypeId,
                        Name = schematic?.SchematicName ?? string.Empty,
                        CycleTime = schematic?.CycleTime ?? 0,
                        OutputQuantity = output.Quantity,
                        Inputs = group.Value
                            .Where(p => p.IsInput && p.Quantity > 0)
                            .Select(p => new IndustryPlanetaryInput
                            {
                                TypeID = p.TypeId,
                                Quantity = p.Quantity,
                            })
                            .ToList(),
                    });
                }
                return result
                    .OrderBy(p => p.SchematicId)
                    .ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return new List<IndustryPlanetaryRecipe>();
            }
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
                    if (item.ID <= 0
                        || string.IsNullOrWhiteSpace(item.Name)
                        || item.Name.StartsWith("#")
                        || item.Type != TheGuideToTheNewEden.Core.Enums.DataBaseItemType.InvType
                        || !seen.Add(item.ID))
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
                    string groupName = info?.GroupName;
                    if (TheGuideToTheNewEden.Core.Config.NeedLocalization && DBService.LocalDbReady && info != null)
                    {
                        var localGroup = LocalDbService.TranInvGroup(info.GroupID);
                        if (!string.IsNullOrWhiteSpace(localGroup?.GroupName))
                        {
                            groupName = localGroup.GroupName;
                        }
                    }

                    result.Add(new IndustryProductEntry
                    {
                        ProductTypeId = item.ID,
                        ProductName = item.Name,
                        BlueprintTypeId = blueprintTypeId.Value,
                        BlueprintName = InvTypeService.QueryType(blueprintTypeId.Value)?.TypeName,
                        ActivityId = activityId,
                        MaxProductionLimit = GetBlueprint(blueprintTypeId.Value)?.MaxProductionLimit ?? 0,
                        GroupId = info?.GroupID ?? 0,
                        GroupName = groupName,
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
