using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using TheGuideToTheNewEden.WPF.Services.Characters;

namespace TheGuideToTheNewEden.WPF.ViewModels.Characters;

/// <summary>
/// 钱包页视图模型：个人/军团 × 流水/交易四个页签各自持有列表与分页状态。
/// 只负责取数与展示投影，不直接触碰控件；分页/页签事件由页面转调。
/// </summary>
public sealed class WalletPageViewModel
{
    private long _corporationId;

    public WalletPageViewModel(CharacterContext context)
    {
        Context = context;
        _corporationId = context.Character.CorporationID;

        CharacterJournal = new JournalTabViewModel();
        CharacterTransactions = new TransactionTabViewModel();
        CorpJournal = new JournalTabViewModel();
        CorpTransactions = new TransactionTabViewModel();
    }

    public CharacterContext Context { get; }

    /// <summary>军团钱包所需的军团 ID（优先由总览接口回填）。</summary>
    public long CorporationId
    {
        get => _corporationId;
        set => _corporationId = value;
    }

    public JournalTabViewModel CharacterJournal { get; }

    public TransactionTabViewModel CharacterTransactions { get; }

    public JournalTabViewModel CorpJournal { get; }

    public TransactionTabViewModel CorpTransactions { get; }

    /// <summary>个人-流水。</summary>
    public async Task LoadCharacterJournalAsync(bool forceRefresh)
    {
        var result = await CharacterWalletService.GetCharacterJournalAsync(Context, CharacterJournal.Page, forceRefresh);
        CharacterJournal.SetItems(result?.Items.Select(ToJournalItem));
        CharacterJournal.HasNext = result?.HasNextPage ?? false;
        CharacterJournal.Loaded = true;
    }

    /// <summary>个人-交易（ESI 按 fromId 单次返回，无分页）。</summary>
    public async Task LoadCharacterTransactionsAsync(bool forceRefresh)
    {
        CharacterTransactions.Page = 1;
        var result = await CharacterWalletService.GetCharacterTransactionsAsync(Context, page: 1, forceRefresh);
        CharacterTransactions.SetItems(result?.Items.Select(ToTransactionItem));
        CharacterTransactions.HasNext = false;
        CharacterTransactions.Loaded = true;
    }

    /// <summary>军团-流水。</summary>
    public async Task LoadCorpJournalAsync(bool forceRefresh)
    {
        _ = forceRefresh;
        var rows = await CharacterWalletService.GetCorporationJournalAsync(
            Context, _corporationId, Division(CorpJournal), CorpJournal.Page);
        CorpJournal.SetItems(rows?.Select(ToJournalItem));
        CorpJournal.HasNext = rows is { Count: > 0 };
        CorpJournal.Loaded = true;
    }

    /// <summary>军团-交易（单次返回，无分页）。</summary>
    public async Task LoadCorpTransactionsAsync(bool forceRefresh)
    {
        _ = forceRefresh;
        CorpTransactions.Page = 1;
        var rows = await CharacterWalletService.GetCorporationTransactionsAsync(
            Context, _corporationId, Division(CorpTransactions));
        CorpTransactions.SetItems(rows?.Select(ToTransactionItem));
        CorpTransactions.HasNext = false;
        CorpTransactions.Loaded = true;
    }

    private static int Division(WalletTabViewModel tab) =>
        Math.Clamp((int)Math.Round(tab.Division), 1, 7);

    // ---------- 展示投影（颜色/本地化不进服务层 DTO） ----------

    private static WalletJournalItemView ToJournalItem(WalletJournalRow row) => new()
    {
        Date = Localize(row.Date),
        Amount = row.Amount,
        Balance = row.Balance,
        Description = row.Description,
        RefType = row.RefType,
        Reason = row.Reason,
    };

    private static WalletTransactionItemView ToTransactionItem(WalletTransactionRow row) => new()
    {
        Date = Localize(row.Date),
        TypeName = row.TypeName,
        UnitPrice = row.UnitPrice,
        Quantity = row.Quantity,
        TotalPrice = row.TotalPrice,
        ClientName = row.ClientName,
        LocationName = row.LocationName,
        IsBuy = row.IsBuy,
        IsBuyText = Resolve(row.IsBuy ? "WalletPage_TransactionBuy" : "WalletPage_TransactionSell"),
    };

    /// <summary>ESI 时间统一转成本地时间后再显示（与 WinUI 列格式一致）。</summary>
    private static DateTime Localize(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value.ToLocalTime(),
        DateTimeKind.Local => value,
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime(),
    };

    private static string Resolve(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}

/// <summary>页签公共状态：页码、是否有下一页、是否已加载、军团钱包 division。</summary>
public abstract class WalletTabViewModel : INotifyPropertyChanged
{
    private int _page = 1;
    private bool _hasNext;
    private bool _loaded;
    private double _division = 1;

    /// <summary>当前页码（从 1 开始）。</summary>
    public int Page
    {
        get => _page;
        set => Set(ref _page, value);
    }

    /// <summary>是否可能还有下一页（ESI 分页无法预知总页数）。</summary>
    public bool HasNext
    {
        get => _hasNext;
        set => Set(ref _hasNext, value);
    }

    /// <summary>该页签是否已经取过数据（懒加载用）。</summary>
    public bool Loaded
    {
        get => _loaded;
        set => Set(ref _loaded, value);
    }

    /// <summary>军团钱包 division（1-7）。</summary>
    public double Division
    {
        get => _division;
        set => Set(ref _division, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

/// <summary>流水页签。</summary>
public sealed class JournalTabViewModel : WalletTabViewModel
{
    private ObservableCollection<WalletJournalItemView> _items = new();

    public ObservableCollection<WalletJournalItemView> Items
    {
        get => _items;
        set => Set(ref _items, value);
    }

    public void SetItems(IEnumerable<WalletJournalItemView>? items) =>
        Items = items is null ? new() : new(items);
}

/// <summary>交易页签。</summary>
public sealed class TransactionTabViewModel : WalletTabViewModel
{
    private ObservableCollection<WalletTransactionItemView> _items = new();

    public ObservableCollection<WalletTransactionItemView> Items
    {
        get => _items;
        set => Set(ref _items, value);
    }

    public void SetItems(IEnumerable<WalletTransactionItemView>? items) =>
        Items = items is null ? new() : new(items);
}

/// <summary>钱包流水行视图。</summary>
public sealed class WalletJournalItemView
{
    public DateTime Date { get; init; }

    public double Amount { get; init; }

    public double Balance { get; init; }

    public string? Description { get; init; }

    public string? RefType { get; init; }

    public string? Reason { get; init; }

    /// <summary>是否为支出（XAML 据此把金额显示成主题的201c上涨/下跌201d色）。</summary>
    public bool IsAmountNegative => Amount < 0;
}

/// <summary>钱包交易行视图。</summary>
public sealed class WalletTransactionItemView
{
    public DateTime Date { get; init; }

    public string? TypeName { get; init; }

    public double UnitPrice { get; init; }

    public int Quantity { get; init; }

    public double TotalPrice { get; init; }

    public string? ClientName { get; init; }

    public string? LocationName { get; init; }

    public bool IsBuy { get; init; }

    /// <summary>买入/卖出文本。</summary>
    public string IsBuyText { get; init; } = string.Empty;

}
