namespace TheGuideToTheNewEden.WPF.Helpers;

/// <summary>
/// ISK 数值的缩写格式化（K / M / B / T），与角色工作区 ZKB 卡片、WinUI 的 ISKNormalizeConverter 同款观感，
/// 但用数值阈值判断而不是 WinUI 那种"按字符串长度 + 小数点切分"的做法
/// （后者对负数、科学计数法、无小数点的超大数都会算错）。
/// </summary>
public static class IskFormatHelper
{
    /// <summary>缩写成人类可读的 ISK 文本，例如 1234567890 → "1.23B"。</summary>
    public static string Format(double value)
    {
        var abs = Math.Abs(value);
        return abs switch
        {
            >= 1_000_000_000_000 => $"{value / 1_000_000_000_000:0.##}T",
            >= 1_000_000_000 => $"{value / 1_000_000_000:0.##}B",
            >= 1_000_000 => $"{value / 1_000_000:0.##}M",
            >= 1_000 => $"{value / 1_000:0.##}K",
            _ => $"{value:0.##}",
        };
    }
}
