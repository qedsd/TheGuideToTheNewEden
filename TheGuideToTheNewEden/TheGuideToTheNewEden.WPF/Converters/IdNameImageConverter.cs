using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.WPF.Helpers;

namespace TheGuideToTheNewEden.WPF.Converters;

/// <summary>
/// <see cref="IdName"/> → 实体图片（角色头像 / 军团徽标 / 联盟徽标 / 物品图标，由类别分派）。
/// 与 <see cref="TypeImageConverter"/> 同款按需下载；无法确定图片或下载失败时返回 null。
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
        if (url is null)
        {
            return null;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(url);
            image.EndInit();
            return image;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
