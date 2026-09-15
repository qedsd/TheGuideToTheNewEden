using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.WPF.Helpers;

namespace TheGuideToTheNewEden.WPF.Controls;

/// <summary>
/// 给 <see cref="Image"/> 用的**异步图片附加属性**：绑定 URL / <see cref="IdName"/> / 物品类型 ID，
/// 图片在后台线程下载、解码并 <c>Freeze()</c>，完成后直写 <see cref="Image.Source"/>。
///
/// <para>
/// 列表、表格等**每行一张图**的界面必须用它：图像绑定在 UI 线程求值，走
/// <c>BitmapImage.UriSource</c> 会在 UI 线程同步下载（击杀者多的 KB 详情页、击杀列表页会卡一会儿）；
/// 而且转换器返回 null 后不会自动重算，做不了"先占位、后填充"。
/// </para>
///
/// <para>
/// <b>用法</b>：
/// <c>&lt;Image ctl:AsyncImage.Source="{Binding AvatarUrl}" /&gt;</c>、
/// <c>&lt;Image ctl:AsyncImage.IdName="{Binding Info.CharacterName}" ctl:AsyncImage.Size="32" /&gt;</c>、
/// <c>&lt;Image ctl:AsyncImage.TypeId="{Binding SKBDetail.Victim.ShipTypeId}" /&gt;</c>。
/// </para>
/// </summary>
public static class AsyncImage
{
    /// <summary>当前目标地址（供下载完成时校验容器是否已被复用）。</summary>
    private static readonly DependencyProperty UrlProperty = DependencyProperty.RegisterAttached(
        "Url",
        typeof(string),
        typeof(AsyncImage),
        new PropertyMetadata(null));

    /// <summary>直接绑图片 URL。</summary>
    public static readonly DependencyProperty SourceProperty = DependencyProperty.RegisterAttached(
        "Source",
        typeof(string),
        typeof(AsyncImage),
        new PropertyMetadata(null, OnUrlChanged));

    public static string? GetSource(DependencyObject obj) => (string?)obj.GetValue(SourceProperty);

    public static void SetSource(DependencyObject obj, string? value) => obj.SetValue(SourceProperty, value);

    /// <summary>实体（角色/军团/联盟/物品）→ 按类别分派头像/徽标/图标。</summary>
    public static readonly DependencyProperty IdNameProperty = DependencyProperty.RegisterAttached(
        "IdName",
        typeof(IdName),
        typeof(AsyncImage),
        new PropertyMetadata(null, OnIdNameChanged));

    public static IdName? GetIdName(DependencyObject obj) => (IdName?)obj.GetValue(IdNameProperty);

    public static void SetIdName(DependencyObject obj, IdName? value) => obj.SetValue(IdNameProperty, value);

    /// <summary>物品类型 ID → 图标。</summary>
    public static readonly DependencyProperty TypeIdProperty = DependencyProperty.RegisterAttached(
        "TypeId",
        typeof(long),
        typeof(AsyncImage),
        new PropertyMetadata(0L, OnTypeIdChanged));

    public static long GetTypeId(DependencyObject obj) => (long)obj.GetValue(TypeIdProperty);

    public static void SetTypeId(DependencyObject obj, long value) => obj.SetValue(TypeIdProperty, value);

    /// <summary>图片尺寸（配合 <see cref="IdNameProperty"/> / <see cref="TypeIdProperty"/>）。默认 32。</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.RegisterAttached(
        "Size",
        typeof(int),
        typeof(AsyncImage),
        new PropertyMetadata(32));

    public static int GetSize(DependencyObject obj) => (int)obj.GetValue(SizeProperty);

    public static void SetSize(DependencyObject obj, int value) => obj.SetValue(SizeProperty, value);

    private static void OnUrlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => Apply(d, e.NewValue as string);

    private static void OnIdNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var idName = e.NewValue as IdName;
        Apply(d, idName is null || idName.Id <= 0
            ? null
            : GameImageHelper.BuildEntityImageUrl(idName.GetCategory(), idName.Id, GetSize(d)));
    }

    private static void OnTypeIdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => Apply(d, GameImageHelper.BuildTypeImageUrl(GetTypeId(d), GetSize(d)));

    private static void Apply(DependencyObject d, string? url)
    {
        if (d is not Image image)
        {
            return;
        }

        image.SetValue(UrlProperty, url);

        if (string.IsNullOrWhiteSpace(url))
        {
            image.Source = null;
            return;
        }

        AsyncImageCache.Load(url, bitmap =>
        {
            // 容器可能已被列表虚拟化复用（地址已变），地址不符就丢弃这次结果
            if (bitmap is not null && ReferenceEquals(image.GetValue(UrlProperty) as string, url))
            {
                image.Source = bitmap;
            }
        });
    }
}
