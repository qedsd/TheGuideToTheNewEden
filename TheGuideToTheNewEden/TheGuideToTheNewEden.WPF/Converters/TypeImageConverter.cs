using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using TheGuideToTheNewEden.WPF.Helpers;

namespace TheGuideToTheNewEden.WPF.Converters;

/// <summary>
/// 物品类型 ID → 图片（<c>{Binding TypeId, Converter={StaticResource ...}}</c>）。
/// 采用 <see cref="BitmapImage.UriSource"/> 的默认按需下载（**不要**设 <c>CacheOption=OnLoad</c>，
/// 那会在绑定时同步下载、列表长时卡 UI）；WIC 在进程内按 URI 缓存，重复物品不会重复下载。
/// 下载失败/离线时返回 null，Image 保持空白，不影响布局。
/// </summary>
public sealed class TypeImageConverter : IValueConverter
{
    /// <summary>图片尺寸（像素），默认 64。</summary>
    public int Size { get; set; } = 64;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var typeId = value switch
        {
            int i => (long)i,
            long l => l,
            _ => 0L,
        };

        var url = GameImageHelper.BuildTypeImageUrl(typeId, Size);
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
