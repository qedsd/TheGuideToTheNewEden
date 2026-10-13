using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Home;

namespace TheGuideToTheNewEden.WPF.ViewModels.Home;

/// <summary>收支构成里的一行（名称按 <see cref="NameKey"/> 现取，语言切换后 <c>Refresh</c> 即可）。</summary>
public sealed class HomeWalletSliceViewModel : INotifyPropertyChanged
{
    public HomeWalletSliceViewModel(HomeWalletSlice slice, bool isIncome)
    {
        Slice = slice;
        IsIncome = isIncome;
    }

    public HomeWalletSlice Slice { get; }

    public bool IsIncome { get; }

    public string Name => Resolve(Slice.NameKey);

    public string PercentText => $"{Slice.Percent:F2}%";

    public double Percent => Slice.Percent;

    public string AmountText => Slice.Amount.ToString("N2");

    public void Refresh() => OnPropertyChanged(nameof(Name));

    public event PropertyChangedEventHandler? PropertyChanged;

    private static string Resolve(string key)
        => Application.Current?.TryFindResource(key) as string ?? key;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// 主页「我的钱包」卡：余额求和 + 收支构成 + 区间切换。
/// <para>
/// 取数交给 <see cref="HomeWalletSummaryService"/>（有界翻页 + 5 分钟缓存 + 逐角色容错），
/// 这里只负责状态与文案。四种状态：加载中 / 无角色（引导）/ 全部失败（提示）/ 有数据。
/// </para>
/// </summary>
public sealed class HomeWalletCardViewModel : INotifyPropertyChanged
{
    private static readonly int[] RangeValues = [7, 30, 0];

    private HomeWalletSummary? _summary;
    private bool _isLoading;
    private int _rangeIndex;
    private bool _loaded;

    public HomeWalletCardViewModel()
    {
        _rangeIndex = Math.Max(0, Array.IndexOf(RangeValues, HomeShortcutService.WalletRangeDays));
    }

    public ObservableCollection<HomeWalletSliceViewModel> IncomeSlices { get; } = [];

    public ObservableCollection<HomeWalletSliceViewModel> OutcomeSlices { get; } = [];

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(ShowContent));
            }
        }
    }

    /// <summary>区间下拉选中项（0=近 7 天 / 1=近 30 天 / 2=全部）；改动即落盘并重新取数。</summary>
    public int RangeIndex
    {
        get => _rangeIndex;
        set
        {
            if (value < 0 || value >= RangeValues.Length || !SetProperty(ref _rangeIndex, value))
            {
                return;
            }

            HomeShortcutService.WalletRangeDays = RangeValues[value];
            OnPropertyChanged(nameof(NoteText));
            _ = LoadAsync(forceRefresh: false);
        }
    }

    /// <summary>区间下拉的显示项（语言切换后重建）。</summary>
    public List<string> RangeLabels =>
    [
        Resolve("Home.Wallet.Range7"),
        Resolve("Home.Wallet.Range30"),
        Resolve("Home.Wallet.RangeAll"),
    ];

    public bool HasData => _summary is { CharacterCount: > 0 };

    /// <summary>还没有任何登录角色（引导态）。</summary>
    public bool HasNoCharacter => _summary is null && !IsLoading;

    /// <summary>有角色但一个都没取到（令牌失效 / 网络问题）。</summary>
    public bool HasAllFailed => _summary is { CharacterCount: 0 };

    public bool HasPartialFailure => _summary is { FailedCount: > 0, CharacterCount: > 0 };

    public bool IsTruncated => _summary is { Truncated: true };

    /// <summary>收入 / 支出构成是否各有内容（没有的一列直接不显示）。</summary>
    public bool HasIncomeSlices => IncomeSlices.Count > 0;

    public bool HasOutcomeSlices => OutcomeSlices.Count > 0;

    /// <summary>有余额但区间/窗口内没有流水（例如角色长期未登录，journal 保留窗口已过）→ 显示提示而非"全部失败"。</summary>
    public bool HasNoSlices => IncomeSlices.Count == 0 && OutcomeSlices.Count == 0;

    public bool ShowContent => HasData;

    /// <summary>「总余额（4 个角色，缓存 5 分钟）」。</summary>
    public string TotalLabel => string.Format(Resolve("Home.Wallet.Total"), _summary?.CharacterCount ?? 0);

    public string BalanceText => _summary is null ? "—" : _summary.Balance.ToString("N2");

    public string IncomeText => _summary is null
        ? "—"
        : "+" + _summary.Income.ToString("N2");

    public string OutcomeText => _summary is null
        ? "—"
        : "-" + _summary.Outcome.ToString("N2");

    public string FailedText => string.Format(Resolve("Home.Wallet.PartialFailed"), _summary?.FailedCount ?? 0);

    /// <summary>底部口径说明。</summary>
    public string NoteText
    {
        get
        {
            var range = HomeShortcutService.WalletRangeDays switch
            {
                30 => Resolve("Home.Wallet.Range30"),
                0 => Resolve("Home.Wallet.RangeAll"),
                _ => Resolve("Home.Wallet.Range7"),
            };

            return _summary is { Truncated: true }
                ? string.Format(Resolve("Home.Wallet.NoteTruncated"), range)
                : string.Format(Resolve("Home.Wallet.Note"), range);
        }
    }

    /// <summary>首次进入主页时调用（已加载过且未强制刷新时直接复用缓存）。</summary>
    public async Task LoadAsync(bool forceRefresh = false)
    {
        if (_loaded && !forceRefresh)
        {
            return;
        }

        _loaded = true;
        IsLoading = true;
        OnPropertyChanged(nameof(HasNoCharacter));
        OnPropertyChanged(nameof(HasAllFailed));

        try
        {
            var range = HomeShortcutService.WalletRangeDays switch
            {
                30 => HomeWalletRange.Days30,
                0 => HomeWalletRange.All,
                _ => HomeWalletRange.Days7,
            };

            _summary = await HomeWalletSummaryService.GetAsync(range, forceRefresh);
        }
        catch (Exception ex)
        {
            Core.Log.Error($"[主页] 钱包卡取数失败：{ex}");
            _summary = null;
        }
        finally
        {
            IsLoading = false;
        }

        RebuildSlices();
        NotifyAll();
    }

    public void RefreshTexts()
    {
        foreach (var slice in IncomeSlices)
        {
            slice.Refresh();
        }

        foreach (var slice in OutcomeSlices)
        {
            slice.Refresh();
        }

        OnPropertyChanged(nameof(RangeLabels));
        NotifyAll();
    }

    private void RebuildSlices()
    {
        IncomeSlices.Clear();
        OutcomeSlices.Clear();

        if (_summary is null)
        {
            return;
        }

        foreach (var slice in _summary.IncomeSlices)
        {
            IncomeSlices.Add(new HomeWalletSliceViewModel(slice, isIncome: true));
        }

        foreach (var slice in _summary.OutcomeSlices)
        {
            OutcomeSlices.Add(new HomeWalletSliceViewModel(slice, isIncome: false));
        }
    }

    private void NotifyAll()
    {
        OnPropertyChanged(nameof(HasData));
        OnPropertyChanged(nameof(ShowContent));
        OnPropertyChanged(nameof(HasNoCharacter));
        OnPropertyChanged(nameof(HasAllFailed));
        OnPropertyChanged(nameof(HasPartialFailure));
        OnPropertyChanged(nameof(IsTruncated));
        OnPropertyChanged(nameof(HasIncomeSlices));
        OnPropertyChanged(nameof(HasOutcomeSlices));
        OnPropertyChanged(nameof(HasNoSlices));
        OnPropertyChanged(nameof(TotalLabel));
        OnPropertyChanged(nameof(BalanceText));
        OnPropertyChanged(nameof(IncomeText));
        OnPropertyChanged(nameof(OutcomeText));
        OnPropertyChanged(nameof(FailedText));
        OnPropertyChanged(nameof(NoteText));
    }

    private static string Resolve(string key)
        => Application.Current?.TryFindResource(key) as string ?? key;

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
