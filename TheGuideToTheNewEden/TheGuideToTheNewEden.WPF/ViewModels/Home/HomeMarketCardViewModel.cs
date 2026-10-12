using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Business;

namespace TheGuideToTheNewEden.WPF.ViewModels.Home;

/// <summary>
/// 「我的行情」里的一张物品卡。
/// <para>
/// 数据来自**历史统计**（<c>Configs/HistoryOrders/{region}/{type}.json</c>，按设置里的 TTL 走磁盘缓存），
/// 而不是实时订单：主页每次进入都去拉实时订单是十几次 ESI 请求，代价与收益不成比例。
/// 因此这里显示的是「近一日历史均价 / 销量 + 相对前一日的涨跌」，实时订单交给点击后的市场页。
/// </para>
/// </summary>
public sealed class HomeMarketItemViewModel : INotifyPropertyChanged
{
    public HomeMarketItemViewModel(int typeId, string? name)
    {
        TypeId = typeId;
        Name = string.IsNullOrWhiteSpace(name) ? typeId.ToString() : name!;
    }

    public int TypeId { get; }

    public string Name { get; }

    /// <summary>给 <c>ctl:AsyncImage.TypeId</c> 用（物品图标）。</summary>
    public long IconTypeId => TypeId;

    public bool HasData { get; private set; }

    public string AverageText { get; private set; } = "—";

    public string ChangeText { get; private set; } = string.Empty;

    public bool IsRising { get; private set; }

    public string VolumeText { get; private set; } = "—";

    public string VolumeChangeText { get; private set; } = string.Empty;

    public bool IsVolumeRising { get; private set; }

    /// <summary>用两天的历史（最后一天 vs 前一天）填空。</summary>
    public void Apply(IReadOnlyList<Core.Models.Market.Statistic> rows)
    {
        // 历史按日期升序取最后两天；不足两天时只显示数值、不显示涨跌。
        var ordered = rows.OrderBy(p => p.Date).ToList();
        if (ordered.Count == 0)
        {
            return;
        }

        var last = ordered[^1];
        var previous = ordered.Count > 1 ? ordered[^2] : null;

        var average = Convert.ToDouble(last.Average);
        AverageText = average.ToString("N2");
        VolumeText = Convert.ToDouble(last.Volume).ToString("N0");

        if (previous is not null)
        {
            var previousAverage = Convert.ToDouble(previous.Average);
            if (previousAverage > 0)
            {
                var change = (average - previousAverage) / previousAverage * 100;
                ChangeText = $"{change:+0.00;-0.00;0.00}%";
                IsRising = change >= 0;
            }

            var previousVolume = Convert.ToDouble(previous.Volume);
            var volume = Convert.ToDouble(last.Volume);
            if (previousVolume > 0)
            {
                var volumeChange = (volume - previousVolume) / previousVolume * 100;
                VolumeChangeText = $"{volumeChange:+0.00;-0.00;0.00}%";
                IsVolumeRising = volumeChange >= 0;
            }
        }

        HasData = true;
        OnPropertyChanged(nameof(HasData));
        OnPropertyChanged(nameof(AverageText));
        OnPropertyChanged(nameof(ChangeText));
        OnPropertyChanged(nameof(IsRising));
        OnPropertyChanged(nameof(VolumeText));
        OnPropertyChanged(nameof(VolumeChangeText));
        OnPropertyChanged(nameof(IsVolumeRising));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// 主页「我的行情」卡：显示收藏物品（<c>Configs/StaredMarketInvType.json</c>，与市场页共用）的行情。
/// 最多显示 <see cref="MaxCards"/> 张，其余通过「全部收藏」进市场页看；
/// 单个物品没有历史时那一张显示「—」，不影响其它卡（卡片级降级）。
/// </summary>
public sealed class HomeMarketCardViewModel : INotifyPropertyChanged
{
    public const int MaxCards = 6;

    private bool _isLoading;
    private bool _loaded;
    private int _totalFavorites;

    public ObservableCollection<HomeMarketItemViewModel> Items { get; } = [];

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (_isLoading == value)
            {
                return;
            }

            _isLoading = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasNoFavorite));
        }
    }

    public bool HasData => Items.Count > 0;

    /// <summary>收藏为空（引导态：去市场页收藏）。</summary>
    public bool HasNoFavorite => !IsLoading && Items.Count == 0;

    /// <summary>「全部收藏 ›」右侧提示（收藏总数超过显示上限时才有意义）。</summary>
    public string FavoriteCountText => _totalFavorites > Items.Count
        ? string.Format(Resolve("Home.Market.More"), _totalFavorites - Items.Count)
        : string.Empty;

    public bool HasMore => _totalFavorites > Items.Count;

    /// <summary>底部口径说明：数据源是历史统计（最近一日），实时订单在点击后的市场页看。</summary>
    public string NoteText => Resolve("Home.Market.Note");

    public async Task LoadAsync(bool forceRefresh = false)
    {
        if (_loaded && !forceRefresh)
        {
            return;
        }

        _loaded = true;
        IsLoading = true;

        try
        {
            var favorites = MarketStarService.Current.GetIds();
            _totalFavorites = favorites.Count;
            Items.Clear();

            if (favorites.Count == 0)
            {
                return;
            }

            var shown = favorites.Take(MaxCards).ToList();

            // 名称来自本地 SDE（不走网络）；本地库没载入时不影响出价，只退化成显示类型 ID。
            var names = new Dictionary<int, string>();
            try
            {
                foreach (var type in Core.Services.DB.LocalDbService.TranInvTypes(shown))
                {
                    names[type.TypeID] = type.TypeName;
                }
            }
            catch (Exception ex)
            {
                Core.Log.Warn($"[主页] 行情卡取物品名失败（本地库未载入？）：{ex.Message}");
            }

            var cards = shown
                .Select(id => new HomeMarketItemViewModel(id, names.GetValueOrDefault(id)))
                .ToList();

            var history = await MarketOrderService.Current.GetHistoryBatchAsync(
                shown, (int)MarketOrderService.DefaultMarketRegion);

            foreach (var card in cards)
            {
                if (history.TryGetValue(card.TypeId, out var rows))
                {
                    card.Apply(rows);
                }

                Items.Add(card);
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error($"[主页] 行情卡取数失败：{ex}");
        }
        finally
        {
            IsLoading = false;
        }

        OnPropertyChanged(nameof(HasData));
        OnPropertyChanged(nameof(HasNoFavorite));
        OnPropertyChanged(nameof(HasMore));
        OnPropertyChanged(nameof(FavoriteCountText));
        OnPropertyChanged(nameof(NoteText));
    }

    public void RefreshTexts() => OnPropertyChanged(nameof(NoteText));

    private static string Resolve(string key)
        => Application.Current?.TryFindResource(key) as string ?? key;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
