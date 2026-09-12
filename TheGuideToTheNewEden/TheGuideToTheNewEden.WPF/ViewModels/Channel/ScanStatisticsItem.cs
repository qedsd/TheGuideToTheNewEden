using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.WPF.Services.Settings;

namespace TheGuideToTheNewEden.WPF.ViewModels.Channel;

/// <summary>
/// 频道统计里的一项军团/联盟人数统计：实体 + 人数 + 图标（异步加载，规则与 WinUI
/// <c>GameImageConverter</c> 及角色总览页一致，含国服图片域名）。
/// </summary>
public sealed class ScanStatisticsItem : INotifyPropertyChanged
{
    private static readonly HttpClient Http = new();

    /// <summary>军团或联盟（IdName）。</summary>
    public IdName Entity { get; init; } = new();

    /// <summary>人数（剔除忽略成员后的实际数量，取决于设置）。</summary>
    public int Count { get; init; }

    /// <summary>true = 联盟，false = 军团（决定图标地址）。</summary>
    public bool IsAlliance { get; init; }

    public string Name => Entity.Name;

    private ImageSource? _logo;

    /// <summary>军团/联盟徽标（下载失败保持为空，界面只显示文字）。</summary>
    public ImageSource? Logo
    {
        get => _logo;
        private set => Set(ref _logo, value);
    }

    public async Task LoadLogoAsync()
    {
        var url = BuildLogoUrl(Entity.Id, IsAlliance);
        if (url is null)
        {
            return;
        }

        try
        {
            var bytes = await Http.GetByteArrayAsync(url).ConfigureAwait(false);
            using var stream = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze(); // 冻结后跨线程绑定安全（见 REFACTORING.md §9 第 15 条）
            Logo = bitmap;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    /// <summary>徽标地址：国际服 images.evetech.net，国服 image.evepc.163.com（与总览页同规则）。</summary>
    private static string? BuildLogoUrl(long id, bool isAlliance)
    {
        if (id <= 0)
        {
            return null;
        }

        if (GameServerSelectorService.Value == Core.Enums.GameServerType.Serenity)
        {
            return $"https://image.evepc.163.com/{(isAlliance ? "alliances" : "corporations")}/{id}_64.png";
        }

        return isAlliance
            ? $"https://images.evetech.net/alliances/{id}/logo?size=64"
            : $"https://images.evetech.net/corporations/{id}/logo?size=64";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
