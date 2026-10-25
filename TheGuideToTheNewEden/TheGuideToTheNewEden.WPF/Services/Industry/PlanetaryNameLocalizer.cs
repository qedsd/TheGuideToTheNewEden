using TheGuideToTheNewEden.Core.Services.DB;

namespace TheGuideToTheNewEden.WPF.Services.Industry;

/// <summary>行星页的名称显示：优先本地化库，英文回退；行星类型用中文领域名。</summary>
public static class PlanetaryNameLocalizer
{
    private static readonly Dictionary<string, string> PlanetTypeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Temperate"] = "温带",
        ["Ice"] = "冰原",
        ["Gas"] = "气态",
        ["Oceanic"] = "海洋",
        ["Lava"] = "熔岩",
        ["Barren"] = "贫瘠",
        ["Storm"] = "风暴",
        ["Plasma"] = "等离子",
    };

    public static string TypeName(int typeId, string fallback)
    {
        if (!Core.Config.NeedLocalization || !DBService.LocalDbReady) return fallback;
        try
        {
            var local = LocalDbService.TranInvType(typeId);
            return string.IsNullOrWhiteSpace(local?.TypeName) ? fallback : local.TypeName;
        }
        catch
        {
            return fallback;
        }
    }

    public static string PlanetType(string name)
    {
        return !string.IsNullOrWhiteSpace(name) && PlanetTypeNames.TryGetValue(name, out var local)
            ? local
            : name;
    }
}
