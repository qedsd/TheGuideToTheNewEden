using System;
using System.Collections.Generic;
using System.Linq;
using TheGuideToTheNewEden.Core.Services.DB;

namespace TheGuideToTheNewEden.Core.Services.Industry
{
    public sealed class OreProcessingOutput
    {
        public int TypeID { get; set; }
        public string Name { get; set; } = string.Empty;
        public long Quantity { get; set; }
        public double UnitVolume { get; set; }
        public double TotalVolume => UnitVolume * Quantity;
    }

    /// <summary>回收与压缩的独立计算（不属于制造 BOM）。</summary>
    public static class IndustryOreCalculator
    {
        /// <summary>按 SDE typeMaterials / portionSize 计算回收产出；yield 为 0..1。</summary>
        public static List<OreProcessingOutput> Reprocess(int typeId, long quantity, double yield)
        {
            var outputs = new List<OreProcessingOutput>();
            if (typeId <= 0 || quantity <= 0) return outputs;

            var type = InvTypeService.QueryType(typeId);
            int portion = Math.Max(1, type?.PortionSize ?? 1);
            long portions = quantity / portion;
            if (portions <= 0) return outputs;

            double factor = Math.Clamp(yield, 0, 1);
            var materials = IndustryDbService.GetTypeMaterials(typeId);
            if (materials == null || materials.Count == 0) return outputs;

            var ids = materials.Select(p => p.MaterialTypeID).Distinct().ToList();
            var names = InvTypeService.QueryTypes(ids).ToDictionary(p => p.TypeID, p => p.TypeName);
            foreach (var material in materials)
            {
                long amount = (long)Math.Floor(material.Quantity * (double)portions * factor);
                if (amount <= 0) continue;
                outputs.Add(new OreProcessingOutput
                {
                    TypeID = material.MaterialTypeID,
                    Name = names.TryGetValue(material.MaterialTypeID, out var name) ? name : material.MaterialTypeID.ToString(),
                    Quantity = amount,
                    UnitVolume = InvTypeService.QueryType(material.MaterialTypeID)?.Volume ?? 0,
                });
            }
            return outputs;
        }

        /// <summary>压缩映射；SDE 压缩为 1:1 单位，体积由压缩后类型决定。</summary>
        public static OreProcessingOutput Compress(int typeId, long quantity)
        {
            if (typeId <= 0 || quantity <= 0) return null;
            var mapping = IndustryDbService.GetCompressibleType(typeId);
            if (mapping == null) return null;
            var type = InvTypeService.QueryType(mapping.CompressedTypeID);
            return new OreProcessingOutput
            {
                TypeID = mapping.CompressedTypeID,
                Name = type?.TypeName ?? mapping.CompressedTypeID.ToString(),
                Quantity = quantity,
                UnitVolume = type?.Volume ?? 0,
            };
        }
    }
}
