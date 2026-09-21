using System.Globalization;

namespace TheGuideToTheNewEden.WPF.Helpers;

/// <summary>星图相关的文本格式化（安全等级等），全项目共用一份口径。</summary>
public static class MapTextHelper
{
    /// <summary>
    /// 安全等级显示口径（与 EVE 客户端一致）：
    /// <list type="bullet">
    /// <item>0 ≤ sec &lt; 0.05（含 0.0 与"恰好 0"）→ 显示 <c>0.0</c>（不显示 0.04 这种伪低安）；</item>
    /// <item><b>负数按实际显示</b>（如 -0.1 / -1.0）—— 之前这里被当成 0.0，负安等星系全被显示成 0.0；</item>
    /// <item>其余按正常小数显示。</item>
    /// </list>
    /// </summary>
    /// <param name="security">星系安全等级。</param>
    /// <param name="decimals">小数位（1 = <c>0.0</c>，2 = <c>0.00</c>）。</param>
    public static string FormatSecurity(double security, int decimals = 1)
    {
        var format = decimals <= 1 ? "0.0" : "0.00";
        var zero = decimals <= 1 ? "0.0" : "0.00";

        // 负数：按实际数值显示（-0.04 这类极小负值仍归入 0.0，避免出现 "-0.0" 这种怪值）
        if (security < 0)
        {
            return security <= -0.05 ? security.ToString(format, CultureInfo.InvariantCulture) : zero;
        }

        return security < 0.05 ? zero : security.ToString(format, CultureInfo.InvariantCulture);
    }
}
