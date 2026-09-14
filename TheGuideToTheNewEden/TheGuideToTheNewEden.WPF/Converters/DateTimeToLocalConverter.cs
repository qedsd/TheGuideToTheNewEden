using System.Globalization;
using System.Windows.Data;

namespace TheGuideToTheNewEden.WPF.Converters;

/// <summary>
/// <see cref="DateTime"/> → 本地时间字符串。ZKB/ESI 返回的是 UTC，而 JSON 反序列化后的
/// <c>Kind</c> 可能是 Utc / Local / Unspecified（不确定时按 UTC 处理），因此统一归一化到本地时间再格式化，
/// 避免出现"时间差了 8 小时"的问题（对齐 WinUI 的 UTCToLocalTimeConverter）。
/// </summary>
public sealed class DateTimeToLocalConverter : IValueConverter
{
    /// <summary>格式串，可用 ConverterParameter 覆盖。</summary>
    public string Format { get; set; } = "g";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTime dateTime)
        {
            return string.Empty;
        }

        var local = dateTime.Kind switch
        {
            DateTimeKind.Utc => dateTime.ToLocalTime(),
            DateTimeKind.Local => dateTime,
            _ => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc).ToLocalTime(),
        };

        var format = parameter as string ?? Format;
        return local.ToString(format, culture);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
