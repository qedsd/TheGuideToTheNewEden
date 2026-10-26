using TheGuideToTheNewEden.Core.Services.DB;

namespace TheGuideToTheNewEden.WPF.Services.Industry;

/// <summary>行星页的名称显示：优先本地化库，英文回退；行星类型按当前界面语言显示。</summary>
public static class PlanetaryNameLocalizer
{
    private static readonly string[] PlanetTypeCodes =
    {
        "Temperate", "Ice", "Gas", "Oceanic", "Lava", "Barren", "Storm", "Plasma",
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
        if (string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        // SDE 里可能是 "Temperate"，也可能是 "Planet (Temperate)"
        var code = name.Trim();
        var open = code.IndexOf('(');
        if (open >= 0 && code.EndsWith(")", StringComparison.Ordinal))
        {
            code = code.Substring(open + 1, code.Length - open - 2).Trim();
        }

        foreach (var candidate in PlanetTypeCodes)
        {
            if (string.Equals(candidate, code, StringComparison.OrdinalIgnoreCase))
            {
                return LanguageService.Get("PlanetType_" + candidate);
            }
        }
        return name;
    }
}
