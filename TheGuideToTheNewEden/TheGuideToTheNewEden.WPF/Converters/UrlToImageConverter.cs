using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using TheGuideToTheNewEden.WPF.Helpers;

namespace TheGuideToTheNewEden.WPF.Converters;

/// <summary>
/// 图片地址（string）→ <see cref="BitmapImage"/>。与 <see cref="TypeImageConverter"/> 一样
/// 走 <see cref="BitmapImage.UriSource"/> 的默认按需下载（**不要**设 <c>CacheOption=OnLoad</c>，
/// 那会在绑定时同步下载、列表长时卡 UI）；失败/离线返回 null，Image 保持空白。
/// </summary>
public sealed class UrlToImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string url || string.IsNullOrWhiteSpace(url))
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
