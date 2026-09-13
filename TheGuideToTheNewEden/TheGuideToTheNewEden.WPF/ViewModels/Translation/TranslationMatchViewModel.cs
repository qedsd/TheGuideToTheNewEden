using System.Collections.Concurrent;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media.Imaging;
using TheGuideToTheNewEden.Core.Enums;
using TheGuideToTheNewEden.Core.Models;
using TheGuideToTheNewEden.WPF.Helpers;
using TheGuideToTheNewEden.WPF.Services.Translation;

namespace TheGuideToTheNewEden.WPF.ViewModels.Translation;

/// <summary>
/// 一条翻译结果的展示包装（<see cref="TranslationItem"/> 是 Core 的纯数据模型，
/// 这里补上界面需要的本地化文本、布尔标记与物品图标，避免在 XAML 里堆转换器）。
/// 本地化文本在构造时取好，因此切换语言后需要重建（由页面 VM 负责）。
/// </summary>
public sealed class TranslationMatchViewModel : INotifyPropertyChanged
{
    /// <summary>已下载的物品图标（冻结后可跨线程/跨列表复用，避免来回点击重复下载）。</summary>
    private static readonly ConcurrentDictionary<int, BitmapImage> IconCache = new();

    private static readonly HttpClient Http = new();

    private readonly string _noTranslation;

    public TranslationMatchViewModel(TranslationItem item)
    {
        Item = item;
        _noTranslation = FindString("TranslationPage_NoTranslation");
        if (IsInvType && IconCache.TryGetValue(Item.ID, out var cached))
        {
            _icon = cached;
        }
    }

    public TranslationItem Item { get; }

    /// <summary>EVE 类型 ID（物品才用于取图标）。</summary>
    public int Id => Item.ID;

    public string Query => Item.Query ?? string.Empty;

    public string Translation => string.IsNullOrWhiteSpace(Item.Translation) ? _noTranslation : Item.Translation;

    /// <summary>译文库里是否存在该名词（不存在时显示"无译文"）。</summary>
    public bool HasTranslation => !string.IsNullOrWhiteSpace(Item.Translation);

    /// <summary>名词类型（物品 / 星域 / 星系 / 空间站）。</summary>
    public string TypeName => FindString(TypeKey(Item.DataBaseItemType));

    /// <summary>只有物品有图标。</summary>
    public bool IsInvType => Item.DataBaseItemType == DataBaseItemType.InvType;

    /// <summary>形如「英文 → 中文」。</summary>
    public string DirectionText
        => $"{FindString(TranslationLanguageHelper.LanguageKey(Item.From))} → {FindString(TranslationLanguageHelper.LanguageKey(Item.To))}";

    public string QueryDescription => Item.QueryDescription ?? string.Empty;

    public bool HasQueryDescription => !string.IsNullOrWhiteSpace(Item.QueryDescription);

    public string TranslationDescription => Item.TranslationDescription ?? string.Empty;

    public bool HasTranslationDescription => !string.IsNullOrWhiteSpace(Item.TranslationDescription);

    private BitmapImage? _icon;

    /// <summary>物品图标（异步下载后回填，见 <see cref="LoadIconAsync"/>）。</summary>
    public BitmapImage? Icon
    {
        get => _icon;
        private set
        {
            if (ReferenceEquals(_icon, value))
            {
                return;
            }

            _icon = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 下载物品图标并回填（失败保持为空，不影响结果）。
    /// 先取字节、再在同一个线程池线程上解码并 <c>Freeze()</c>：既避开 <see cref="BitmapImage"/> 的线程亲缘性
    /// （REFACTORING.md §9 第 15 条），也绕开 WPF 走 WinINet 缓存下载的老路径（与估价页同一做法）。
    /// </summary>
    public async Task LoadIconAsync()
    {
        if (!IsInvType || _icon is not null)
        {
            return;
        }

        if (IconCache.TryGetValue(Item.ID, out var cached))
        {
            Icon = cached;
            return;
        }

        var url = GameImageHelper.BuildTypeImageUrl(Item.ID);
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
            bitmap.Freeze();
            IconCache[Item.ID] = bitmap;
            Icon = bitmap;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    private static string TypeKey(DataBaseItemType type) => type switch
    {
        DataBaseItemType.InvType => "TranslationPage_Type_InvType",
        DataBaseItemType.MapRegion => "TranslationPage_Type_MapRegion",
        DataBaseItemType.MapSolarSystem => "TranslationPage_Type_MapSolarSystem",
        DataBaseItemType.StaStation => "TranslationPage_Type_StaStation",
        _ => "TranslationPage_Type_InvType",
    };

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
