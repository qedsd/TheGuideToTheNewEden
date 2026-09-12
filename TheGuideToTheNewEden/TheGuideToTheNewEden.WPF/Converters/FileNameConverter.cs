using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace TheGuideToTheNewEden.WPF.Converters;

/// <summary>文件绝对路径 → 不含目录与扩展名的文件名（购物记录列表展示用）。</summary>
public sealed class FileNameConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string path ? Path.GetFileNameWithoutExtension(path) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
