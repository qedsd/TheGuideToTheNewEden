using System.Globalization;
using System.Windows.Media;

namespace TheGuideToTheNewEden.WPF.Services.GamePreview;

/// <summary><see cref="System.Drawing.Color"/> 与 WPF 画刷/十六进制字符串之间的转换。</summary>
internal static class PreviewColorHelper
{
    internal static SolidColorBrush ToBrush(System.Drawing.Color color)
    {
        var brush = new SolidColorBrush(ToMediaColor(color));
        brush.Freeze();
        return brush;
    }

    internal static Color ToMediaColor(System.Drawing.Color color)
        => Color.FromArgb(color.A, color.R, color.G, color.B);

    /// <summary>前景色取黑或白，保证在给定底色上可读。</summary>
    internal static SolidColorBrush ContrastForeground(System.Drawing.Color background)
    {
        var luminance = ((0.299 * background.R) + (0.587 * background.G) + (0.114 * background.B)) / 255.0;
        return luminance > 0.6 ? Brushes.Black : Brushes.White;
    }

    /// <summary>
    /// 转成 <c>#RRGGBB</c>；<b>带透明度时输出 <c>#AARRGGBB</c></b>（否则半透明色在任何"显示→回写"
    /// 往返中都会静默变成全不透明，例如角色名叠加的背景色）。
    /// </summary>
    internal static string ToHex(System.Drawing.Color color) => color.A == 255
        ? $"#{color.R:X2}{color.G:X2}{color.B:X2}"
        : $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";

    internal static System.Drawing.Color FromHex(string? text, System.Drawing.Color fallback)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }

        var value = text.Trim().TrimStart('#');
        if (value.Length == 3)
        {
            value = string.Concat(value.Select(c => new string(c, 2)));
        }

        if (value.Length is not (6 or 8))
        {
            return fallback;
        }

        if (!uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed))
        {
            return fallback;
        }

        return value.Length == 6
            ? System.Drawing.Color.FromArgb(255, (byte)(parsed >> 16), (byte)(parsed >> 8), (byte)parsed)
            : System.Drawing.Color.FromArgb((byte)(parsed >> 24), (byte)(parsed >> 16), (byte)(parsed >> 8), (byte)parsed);
    }
}
