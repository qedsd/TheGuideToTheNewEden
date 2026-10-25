using System;
using System.Collections.Generic;
using System.Linq;
using TheGuideToTheNewEden.Core.Models.Indusrty;
using TheGuideToTheNewEden.Core.Models.Planetary;
using TheGuideToTheNewEden.Core.Services.DB;

namespace TheGuideToTheNewEden.Core.Services.Planetary
{
    /// <summary>
    /// 行星配方目录：把 planetSchematics 转成按产物索引的 P0-P4 图。
    /// 层级由依赖深度推断，不依赖固定的 schematic ID 段。
    /// </summary>
    public sealed class PlanetaryRecipeCatalog
    {
        private readonly Dictionary<int, IndustryPlanetaryRecipe> _recipeByProduct;
        private readonly Dictionary<int, PlanetaryProduct> _products;
        private readonly Dictionary<int, int> _tierCache = new Dictionary<int, int>();
        private readonly Dictionary<int, List<PlanetaryProduct>> _productsByTier;
        private readonly Dictionary<int, List<PlanetaryProduct>> _p1ByP0;

        private PlanetaryRecipeCatalog(
            List<IndustryPlanetaryRecipe> recipes,
            Dictionary<int, IndustryTypeInfo> typeInfos)
        {
            _recipeByProduct = recipes
                .Where(p => p.ProductTypeID > 0 && p.OutputQuantity > 0)
                .GroupBy(p => p.ProductTypeID)
                .ToDictionary(p => p.Key, p => p.First());

            var allTypeIds = new HashSet<int>(_recipeByProduct.Keys);
            foreach (var recipe in _recipeByProduct.Values)
            {
                foreach (var input in recipe.Inputs)
                {
                    allTypeIds.Add(input.TypeID);
                }
            }

            _products = new Dictionary<int, PlanetaryProduct>();
            foreach (var typeId in allTypeIds)
            {
                typeInfos.TryGetValue(typeId, out var info);
                _recipeByProduct.TryGetValue(typeId, out var recipe);
                _products[typeId] = new PlanetaryProduct
                {
                    TypeId = typeId,
                    Name = info?.TypeName ?? ("Type " + typeId),
                    Volume = info?.Volume ?? 0,
                    Recipe = recipe,
                };
            }

            foreach (var product in _products.Values)
            {
                product.Tier = (PlanetaryProductTier)DetermineTier(product.TypeId, new HashSet<int>());
            }

            _productsByTier = _products.Values
                .GroupBy(p => (int)p.Tier)
                .ToDictionary(p => p.Key, p => p.ToList());

            _p1ByP0 = new Dictionary<int, List<PlanetaryProduct>>();
            foreach (var p1 in _products.Values.Where(p => p.Tier == PlanetaryProductTier.P1 && p.Recipe != null))
            {
                foreach (var input in p1.Recipe.Inputs)
                {
                    if (!_p1ByP0.TryGetValue(input.TypeID, out var list))
                    {
                        list = new List<PlanetaryProduct>();
                        _p1ByP0[input.TypeID] = list;
                    }
                    list.Add(p1);
                }
            }
        }

        public static PlanetaryRecipeCatalog Load()
        {
            var recipes = IndustryDbService.GetAllPlanetaryRecipes();
            var ids = new HashSet<int>();
            foreach (var recipe in recipes)
            {
                ids.Add(recipe.ProductTypeID);
                foreach (var input in recipe.Inputs)
                {
                    ids.Add(input.TypeID);
                }
            }
            var infos = IndustryDbService.GetTypeInfos(ids)
                .Where(p => p != null)
                .GroupBy(p => p.TypeID)
                .ToDictionary(p => p.Key, p => p.First());
            return new PlanetaryRecipeCatalog(recipes, infos);
        }

        public IReadOnlyCollection<PlanetaryProduct> Products => _products.Values;

        public IReadOnlyList<PlanetaryProduct> GetProducts(PlanetaryProductTier tier)
        {
            return _productsByTier.TryGetValue((int)tier, out var list)
                ? list.OrderBy(p => p.Name).ToList()
                : new List<PlanetaryProduct>();
        }

        public PlanetaryProduct GetProduct(int typeId)
        {
            return _products.TryGetValue(typeId, out var product) ? product : null;
        }

        public IndustryPlanetaryRecipe GetRecipe(int typeId)
        {
            return _recipeByProduct.TryGetValue(typeId, out var recipe) ? recipe : null;
        }

        /// <summary>某个 P0 资源可以产出哪些 P1（通常唯一）。</summary>
        public IReadOnlyList<PlanetaryProduct> GetP1ProductsForP0(int p0TypeId)
        {
            return _p1ByP0.TryGetValue(p0TypeId, out var list)
                ? list
                : new List<PlanetaryProduct>();
        }

        private int DetermineTier(int typeId, HashSet<int> visiting)
        {
            if (_tierCache.TryGetValue(typeId, out var cached))
            {
                return cached;
            }

            if (!_recipeByProduct.TryGetValue(typeId, out var recipe))
            {
                // 没有配方的物品是 P0 原料。
                _tierCache[typeId] = 0;
                return 0;
            }

            if (!visiting.Add(typeId))
            {
                return 0;
            }

            var inputTier = 0;
            foreach (var input in recipe.Inputs)
            {
                inputTier = Math.Max(inputTier, DetermineTier(input.TypeID, visiting));
            }
            visiting.Remove(typeId);

            var tier = Math.Max(1, Math.Min(4, inputTier + 1));
            _tierCache[typeId] = tier;
            return tier;
        }
    }
}
