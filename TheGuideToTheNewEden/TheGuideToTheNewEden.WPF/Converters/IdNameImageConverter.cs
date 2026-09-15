using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.WPF.Controls;
using TheGuideToTheNewEden.WPF.Helpers;

namespace TheGuideToTheNewEden.WPF.Converters;

/// <summary>
/// <see cref="IdName"/> → 实体图片（角色头像 / 军团徽标 / 联盟徽标 / 物品图标，由类别分派）。
///
/// <para>
/// <b>注意</b>：转换器返回 null 后绑定不会自动重算，因此这里只能给"已在缓存中"的图片，
/// 首次遇到新地址会返回 null 并后台预热，需要重绘时才能显示。列表/表格类界面
/// **请改用 <see cref="Controls.AsyncImage"/> 附加属性**（异步加载完成直写 <c>Image.Source</c>，
/// 不受绑定刷新限制），本转换器仅保留给个别非列表场景。
/// </para>
/// </summary>
public sealed class IdNameImageConverter : IValueConverter
{
    /// <summary>图片尺寸（像素），默认 32。</summary>
    public int Size { get; set; } = 32;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not IdName idName || idName.Id <= 0)
        {
            return null;
        }

        var url = GameImageHelper.BuildEntityImageUrl(idName.GetCategory(), idName.Id, Size);
        return url is null ? null : AsyncImageCache.TryGet(url);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
