using System.Globalization;
using System.Windows;
using System.Windows.Data;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.WPF.Services.KB;

namespace TheGuideToTheNewEden.WPF.Converters;

/// <summary>
/// 值 → <see cref="IdName.CategoryEnum"/> 的本地化文本（如 1 → "角色"）。
/// 接受 <see cref="IdName"/>（取其 Category）或直接的 <c>int</c> 类别值。
/// </summary>
public sealed class CategoryEnumToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var category = value switch
        {
            IdName idName => idName.GetCategory(),
            int i => (IdName.CategoryEnum)i,
            IdName.CategoryEnum e => e,
            _ => (IdName.CategoryEnum?)null,
        };

        if (category is null)
        {
            return string.Empty;
        }

        var key = ZkbMapping.ToCategoryLocalizationKey(category.Value);
        return Application.Current?.TryFindResource(key) as string ?? category.Value.ToString();
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
