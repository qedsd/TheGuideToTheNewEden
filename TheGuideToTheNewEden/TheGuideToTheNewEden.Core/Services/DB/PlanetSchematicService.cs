using System.Collections.Generic;
using System.Linq;
using TheGuideToTheNewEden.Core.DBModels;
namespace TheGuideToTheNewEden.Core.Services.DB
{
    public static class PlanetSchematicService
    {
        private static Dictionary<int, SchematicInfo> _cache;
        private static Dictionary<int, string> _tierCache; // typeId → "P0".."P4"

        public static SchematicInfo GetSchematic(int id) 
        { 
            EnsureCache(); 
            SchematicInfo s; 
            return _cache.TryGetValue(id, out s) ? s : null; 
        }
        public static List<SchematicInfo> GetAll() 
        { 
            EnsureCache(); 
            return _cache.Values.ToList(); 
        }

        /// <summary>返回指定 typeId 的 PI 等级 (P0-P4)，非 PI 物品返回 null</summary>
        public static string GetProductTier(int typeId)
        {
            EnsureCache();
            return _tierCache.TryGetValue(typeId, out var tier) ? tier : null;
        }

        /// <summary>返回所有 PI 物品及其等级</summary>
        public static Dictionary<int, string> GetAllProductTiers()
        {
            EnsureCache();
            return new Dictionary<int, string>(_tierCache);
        }

        /// <summary>获取指定类型的输入配方信息（哪些配方把它作为输入、每种需要多少个）</summary>
        public static List<SchematicIO> GetInputsForType(int typeId)
        {
            EnsureCache();
            var list = new List<SchematicIO>();
            foreach (var sch in _cache.Values)
            {
                if (sch.Inputs != null)
                {
                    var io = sch.Inputs.FirstOrDefault(i => i.TypeId == typeId);
                    if (io != null) list.Add(io);
                }
                if (sch.Outputs != null)
                {
                    var io = sch.Outputs.FirstOrDefault(o => o.TypeId == typeId);
                    if (io != null) list.Add(io);
                }
            }
            return list;
        }

        private static void EnsureCache()
        {
            if (_cache != null) return;
            _cache = new Dictionary<int, SchematicInfo>();
            _tierCache = new Dictionary<int, string>();
            try
            {
                var list = DBService.MainDb?.Queryable<PlanetSchematic>().ToList();
                if (list != null && list.Count > 0)
                {
                    var types = DBService.MainDb?.Queryable<PlanetSchematicTypeMap>().ToList();
                    foreach (var s in list)
                    {
                        var info = new SchematicInfo { SchematicId = s.SchematicId, SchematicName = s.SchematicName, CycleTime = s.CycleTime };
                        if (types != null)
                        {
                            var t = types.Where(x => x.SchematicId == s.SchematicId).ToList();
                            info.Inputs = t.Where(x => x.IsInput).Select(x => new SchematicIO { TypeId = x.TypeId, Quantity = x.Quantity }).ToList();
                            info.Outputs = t.Where(x => !x.IsInput).Select(x => new SchematicIO { TypeId = x.TypeId, Quantity = x.Quantity }).ToList();
                        }
                        _cache[s.SchematicId] = info;
                    }
                }
            }
            catch { }
            ComputeTiers();
        }

        private static void Add(int id, string name, int cycle, int[] ii, int[] iq, int[] oi, int[] oq)
        {
            var info = new SchematicInfo { SchematicId = id, SchematicName = name, CycleTime = cycle, Inputs = new List<SchematicIO>(), Outputs = new List<SchematicIO>() };
            for (int i = 0; i < ii.Length; i++) info.Inputs.Add(new SchematicIO { TypeId = ii[i], Quantity = iq[i] });
            for (int i = 0; i < oi.Length; i++) info.Outputs.Add(new SchematicIO { TypeId = oi[i], Quantity = oq[i] });
            _cache[id] = info;
        }

        /// <summary>
        /// 根据 PI 配方拓扑链自动计算 P0-P4 等级。
        /// P0 = 没有配方能生产它（仅能从行星采集）
        /// P1 = 仅由 P0 生产
        /// P2 = 由 P1 生产(也可能混合 P0)
        /// P3 = 由 P2 生产
        /// P4 = 由 P3 生产
        /// </summary>
        private static void ComputeTiers()
        {
            // 收集所有在配方中出现的 typeId
            var allInputTypes = new HashSet<int>();
            var allOutputTypes = new HashSet<int>();
            foreach (var sch in _cache.Values)
            {
                if (sch.Inputs != null)
                    foreach (var io in sch.Inputs)
                        allInputTypes.Add(io.TypeId);
                if (sch.Outputs != null)
                    foreach (var io in sch.Outputs)
                        allOutputTypes.Add(io.TypeId);
            }

            // 只出现在输入中、从未出现在输出中的 = P0（原始资源）
            foreach (var t in allInputTypes)
                if (!allOutputTypes.Contains(t))
                    _tierCache[t] = "P0";

            // 建立产出类型 → 配方 的映射
            var outputToSchematics = new Dictionary<int, List<SchematicInfo>>();
            foreach (var sch in _cache.Values)
            {
                if (sch.Outputs == null) continue;
                foreach (var io in sch.Outputs)
                {
                    if (!outputToSchematics.TryGetValue(io.TypeId, out var list))
                        outputToSchematics[io.TypeId] = list = new List<SchematicInfo>();
                    list.Add(sch);
                }
            }

            // 递归计算等级：输出类型的等级 = max(输入等级) + 1
            int ComputeTypeTier(int typeId, HashSet<int> visiting)
            {
                if (_tierCache.TryGetValue(typeId, out var cached))
                    return ParseTier(cached);

                // 如果没有配方生产它 → P0
                if (!outputToSchematics.TryGetValue(typeId, out var schematics))
                {
                    _tierCache[typeId] = "P0";
                    return 0;
                }

                // 防止循环依赖
                if (visiting.Contains(typeId))
                    return 0;
                visiting.Add(typeId);

                int maxInputTier = -1;
                foreach (var sch in schematics)
                {
                    if (sch.Inputs == null) continue;
                    foreach (var input in sch.Inputs)
                    {
                        int inputTier = ComputeTypeTier(input.TypeId, visiting);
                        if (inputTier > maxInputTier)
                            maxInputTier = inputTier;
                    }
                }

                visiting.Remove(typeId);

                int tier = maxInputTier + 1;
                _tierCache[typeId] = TierToString(tier);
                return tier;
            }

            foreach (var typeId in allOutputTypes)
            {
                if (!_tierCache.ContainsKey(typeId))
                    ComputeTypeTier(typeId, new HashSet<int>());
            }
        }

        private static int ParseTier(string t)
        {
            if (string.IsNullOrEmpty(t) || t.Length < 2) return -1;
            if (int.TryParse(t.Substring(1), out var n)) return n;
            return -1;
        }

        private static string TierToString(int tier)
        {
            return tier switch
            {
                0 => "P0",
                1 => "P1",
                2 => "P2",
                3 => "P3",
                4 => "P4",
                _ => $"P{tier}"
            };
        }
    }
    public class SchematicInfo
    {
        public int SchematicId { get; set; }
        public string SchematicName { get; set; }
        public int CycleTime { get; set; }
        public List<SchematicIO> Inputs { get; set; }
        public List<SchematicIO> Outputs { get; set; }

        /// <summary>
        /// 配方等级。取所有产出物品中的最高 P 等级。
        /// </summary>
        public string Tier
        {
            get
            {
                if (Outputs == null || Outputs.Count == 0) return "?";
                string maxTier = "P0";
                foreach (var o in Outputs)
                {
                    var t = PlanetSchematicService.GetProductTier(o.TypeId);
                    if (t != null && ParseTierNum(t) > ParseTierNum(maxTier))
                        maxTier = t;
                }
                return maxTier;
            }
        }

        private static int ParseTierNum(string t)
        {
            if (t.Length >= 2 && int.TryParse(t.Substring(1), out var n)) return n;
            return -1;
        }
    }
    public class SchematicIO 
    { 
        public int TypeId { get; set; } 
        public int Quantity { get; set; } 
        public string TypeName { get; set; }
    }
}
