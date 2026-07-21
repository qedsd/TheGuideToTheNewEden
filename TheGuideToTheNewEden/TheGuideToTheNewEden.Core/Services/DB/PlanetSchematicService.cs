using System.Collections.Generic;
using System.Linq;
using TheGuideToTheNewEden.Core.DBModels;
namespace TheGuideToTheNewEden.Core.Services.DB
{
    public static class PlanetSchematicService
    {
        private static Dictionary<int, SchematicInfo> _cache;
        public static SchematicInfo GetSchematic(int id) { EnsureCache(); SchematicInfo s; return _cache.TryGetValue(id, out s) ? s : null; }
        public static List<SchematicInfo> GetAll() { EnsureCache(); return _cache.Values.ToList(); }
        private static void EnsureCache()
        {
            if (_cache != null) return;
            _cache = new Dictionary<int, SchematicInfo>();
            try
            {
                var list = DBService.StaticDb?.Queryable<PlanetSchematic>().ToList();
                if (list != null && list.Count > 0)
                {
                    var types = DBService.StaticDb?.Queryable<PlanetSchematicTypeMap>().ToList();
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
                    if (_cache.Count > 0) return;
                }
            }
            catch { }
            Add(74, "Reactive Metals", 1800, new[] { 2267 }, new[] { 3000 }, new[] { 3691 }, new[] { 20 });
            Add(75, "Toxic Metals", 1800, new[] { 2270 }, new[] { 3000 }, new[] { 3693 }, new[] { 20 });
            Add(76, "Industrial Fibers", 1800, new[] { 2272 }, new[] { 3000 }, new[] { 3695 }, new[] { 20 });
            Add(77, "Bacteria", 1800, new[] { 2274 }, new[] { 3000 }, new[] { 3697 }, new[] { 20 });
            Add(78, "Proteins", 1800, new[] { 2278 }, new[] { 3000 }, new[] { 3699 }, new[] { 20 });
            Add(79, "Silicon", 1800, new[] { 2278 }, new[] { 3000 }, new[] { 3701 }, new[] { 20 });
            Add(80, "Water", 1800, new[] { 2270 }, new[] { 3000 }, new[] { 3703 }, new[] { 20 });
            Add(81, "Nanites", 3600, new[] { 3697, 3701 }, new[] { 40, 40 }, new[] { 3721 }, new[] { 5 });
            Add(82, "Consumer Electronics", 3600, new[] { 3701, 3699 }, new[] { 40, 40 }, new[] { 3723 }, new[] { 5 });
            Add(83, "Mechanical Parts", 3600, new[] { 3697, 3699 }, new[] { 40, 40 }, new[] { 3725 }, new[] { 5 });
            Add(84, "Superconductors", 3600, new[] { 3705, 3729 }, new[] { 40, 40 }, new[] { 3727 }, new[] { 5 });
            Add(85, "Transmitter", 1800, new[] { 2288 }, new[] { 3000 }, new[] { 3729 }, new[] { 20 });
            Add(86, "Electrolytes", 1800, new[] { 2290 }, new[] { 3000 }, new[] { 3705 }, new[] { 20 });
        }
        private static void Add(int id, string name, int cycle, int[] ii, int[] iq, int[] oi, int[] oq)
        {
            var info = new SchematicInfo { SchematicId = id, SchematicName = name, CycleTime = cycle, Inputs = new List<SchematicIO>(), Outputs = new List<SchematicIO>() };
            for (int i = 0; i < ii.Length; i++) info.Inputs.Add(new SchematicIO { TypeId = ii[i], Quantity = iq[i] });
            for (int i = 0; i < oi.Length; i++) info.Outputs.Add(new SchematicIO { TypeId = oi[i], Quantity = oq[i] });
            _cache[id] = info;
        }
    }
    public class SchematicInfo
    {
        public int SchematicId { get; set; }
        public string SchematicName { get; set; }
        public int CycleTime { get; set; }
        public List<SchematicIO> Inputs { get; set; }
        public List<SchematicIO> Outputs { get; set; }
        public string Tier
        {
            get
            {
                if (Outputs == null || Outputs.Count == 0) return "?";
                int t = Outputs[0].TypeId;
                if (t >= 3691 && t <= 3719) return "P1";
                if (t >= 3721 && t <= 3767) return "P2";
                if (t >= 3770 && t <= 3820) return "P3";
                if (t >= 3825 && t <= 3830) return "P4";
                return "?";
            }
        }
    }
    public class SchematicIO { public int TypeId { get; set; } public int Quantity { get; set; } public string TypeName { get; set; } }
}
