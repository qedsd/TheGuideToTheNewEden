using System;
using System.Collections.Generic;
using System.Linq;
using TheGuideToTheNewEden.Core.Enums;
using TheGuideToTheNewEden.Core.Services.DB;

namespace TheGuideToTheNewEden.WPF.Services.Industry;

/// <summary>星系下拉项。</summary>
public sealed class PlanetarySystemOption
{
    public int SolarSystemId { get; init; }
    public string Name { get; init; } = string.Empty;
    public override string ToString() => Name;
}

/// <summary>行星下拉项：带本地库里的真实半径（km）与行星类型。</summary>
public sealed class PlanetaryPlanetOption
{
    public int PlanetId { get; init; }
    public string Name { get; init; } = string.Empty;
    public int TypeId { get; init; }
    public string PlanetType { get; init; } = string.Empty;
    public double RadiusKm { get; init; }
    public int SolarSystemId { get; init; }
    public string SolarSystemName { get; init; } = string.Empty;

    /// <summary>下拉里的"不指定"占位项（不提供半径）。</summary>
    public bool IsNone => PlanetId <= 0;
    public bool HasRadius => RadiusKm > 0;

    public string DisplayText => IsNone
        ? LanguageService.Get("IndustryPlanetary_PlanetNotSpecified")
        : HasRadius
            ? $"{Name} · {PlanetType} · {RadiusKm:N0} km"
            : $"{Name} · {PlanetType}";

    /// <summary>候选行星列表里的副标题：星系 · 类型 · 半径。</summary>
    public string DetailText => HasRadius
        ? $"{SolarSystemName} · {PlanetType} · {RadiusKm:N0} km"
        : $"{SolarSystemName} · {PlanetType}";

    public static PlanetaryPlanetOption None() => new()
    {
        PlanetId = 0,
        Name = LanguageService.Get("IndustryPlanetary_PlanetNotSpecified"),
    };
}

/// <summary>
/// 行星计划用到的本地库查询：星系搜索 + 星系内的行星（含真实半径）。
/// 半径来自 CCP SDE 的 <c>mapPlanets.radius</c>（构建时写入 <c>mapDenormalizes.Radius</c>，单位米）。
/// </summary>
public static class PlanetaryPlanetCatalog
{
    /// <summary>按（中/英）名称模糊搜索星系，最多返回 <paramref name="limit"/> 条。</summary>
    public static List<PlanetarySystemOption> SearchSystems(string text, int limit = 20)
    {
        var keyword = text?.Trim() ?? string.Empty;
        if (keyword.Length == 0)
        {
            return new List<PlanetarySystemOption>();
        }

        try
        {
            return MapSolarSystemService.Search(keyword)
                .Where(p => p.Type == DataBaseItemType.MapSolarSystem)
                .Where(p => p.ID > 0 && !string.IsNullOrWhiteSpace(p.Name))
                // 主库与本地化库会各命中一次：按 ID 去重，优先本地化名称。
                .GroupBy(p => p.ID)
                .Select(g => g.OrderByDescending(p => p.IsLocal).First())
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .Take(limit)
                .Select(p => new PlanetarySystemOption { SolarSystemId = p.ID, Name = p.Name })
                .ToList();
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return new List<PlanetarySystemOption>();
        }
    }

    /// <summary>某星系的全部行星（按天体编号排序），首项是"不指定"占位。</summary>
    public static List<PlanetaryPlanetOption> GetPlanets(PlanetarySystemOption? system)
    {
        var options = new List<PlanetaryPlanetOption>();
        if (system is null || system.SolarSystemId <= 0)
        {
            return options;
        }

        options.Add(PlanetaryPlanetOption.None());
        try
        {
            var planets = MapDenormalizeService.QueryPlanetsBySolarSystemID(system.SolarSystemId);
            if (planets.Count == 0)
            {
                return options;
            }

            var typeNames = new Dictionary<int, string>();
            try
            {
                // 用主库英文类型名（"Planet (Temperate)"）解析领域名，保证与计划结果里的「温带」用词一致；
                // 本地化库把它译成「温和」，直接拿本地名会与计划口径不一致。
                foreach (var typeId in planets.Select(p => p.TypeID).Distinct())
                {
                    var name = InvTypeService.QueryType(typeId, local: false)?.TypeName;
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        typeNames[typeId] = name;
                    }
                }
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
            }

            foreach (var planet in planets)
            {
                typeNames.TryGetValue(planet.TypeID, out var typeName);
                options.Add(new PlanetaryPlanetOption
                {
                    PlanetId = planet.ItemID,
                    Name = planet.ItemName,
                    TypeId = planet.TypeID,
                    PlanetType = ResolvePlanetType(typeName),
                    RadiusKm = planet.RadiusKm,
                    SolarSystemId = system.SolarSystemId,
                    SolarSystemName = system.Name,
                });
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
        return options;
    }

    /// <summary>按行星 ID 取下拉项（载入已保存计划时还原选择用）；找不到返回 null。</summary>
    public static PlanetaryPlanetOption? GetPlanet(long planetId)
    {
        if (planetId <= 0)
        {
            return null;
        }

        try
        {
            var planet = MapDenormalizeService.QueryPlanet(planetId);
            if (planet is null)
            {
                return null;
            }

            string? typeName = null;
            try
            {
                // 同 GetPlanets：用主库英文名解析领域名，避免本地化译名（温和）与计划口径（温带）不一致。
                typeName = InvTypeService.QueryType(planet.TypeID, local: false)?.TypeName;
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
            }

            var systemName = string.Empty;
            try
            {
                systemName = MapSolarSystemService.Query(planet.SolarSystemID)?.SolarSystemName ?? string.Empty;
            }
            catch (Exception ex)
            {
                Core.Log.Error(ex);
            }

            return new PlanetaryPlanetOption
            {
                PlanetId = planet.ItemID,
                Name = planet.ItemName,
                TypeId = planet.TypeID,
                PlanetType = ResolvePlanetType(typeName),
                RadiusKm = planet.RadiusKm,
                SolarSystemId = planet.SolarSystemID,
                SolarSystemName = systemName,
            };
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return null;
        }
    }

    /// <summary>"Planet (Temperate)" / "行星（温带）" → 中文领域名「温带」。</summary>
    public static string ResolvePlanetType(string? typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return string.Empty;
        }

        var text = typeName.Trim();
        var open = text.IndexOf('(');
        var close = text.LastIndexOf(')');
        if (open < 0)
        {
            open = text.IndexOf('（');
            close = text.LastIndexOf('）');
        }
        var raw = open >= 0 && close > open ? text.Substring(open + 1, close - open - 1) : text;
        return PlanetaryNameLocalizer.PlanetType(raw.Trim());
    }
}
