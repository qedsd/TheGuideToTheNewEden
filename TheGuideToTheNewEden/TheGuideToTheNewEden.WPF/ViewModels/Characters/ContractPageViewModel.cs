using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TheGuideToTheNewEden.WPF.Services.Characters;

namespace TheGuideToTheNewEden.WPF.ViewModels.Characters;

/// <summary>
/// 合同页视图模型：个人/军团两个页签各自持有列表与分页状态，
/// 字段与 WinUI3 的合同表格列一一对应（颜色语义 WinUI 版没有，故不添加）。
/// </summary>
public sealed class ContractPageViewModel
{
    public ContractPageViewModel(CharacterContext context)
    {
        Context = context;
        CharacterContracts = new ContractTabViewModel();
        CorpContracts = new ContractTabViewModel();
    }

    public CharacterContext Context { get; }

    /// <summary>个人合同页签。</summary>
    public ContractTabViewModel CharacterContracts { get; }

    /// <summary>军团合同页签。</summary>
    public ContractTabViewModel CorpContracts { get; }

    /// <summary>个人合同。</summary>
    public async Task LoadCharacterAsync(bool forceRefresh)
    {
        var result = await CharacterContractService.GetAsync(
            Context, corporation: false, CharacterContracts.Page, forceRefresh);
        CharacterContracts.SetItems(result?.Items.Select(ToItem));
        CharacterContracts.HasNext = result?.HasNextPage ?? false;
        CharacterContracts.Loaded = true;
    }

    /// <summary>军团合同。</summary>
    public async Task LoadCorpAsync(bool forceRefresh)
    {
        var result = await CharacterContractService.GetAsync(
            Context, corporation: true, CorpContracts.Page, forceRefresh);
        CorpContracts.SetItems(result?.Items.Select(ToItem));
        CorpContracts.HasNext = result?.HasNextPage ?? false;
        CorpContracts.Loaded = true;
    }

    /// <summary>投影为行视图：日期统一转本地时间后显示（与 WinUI 列的格式一致）。</summary>
    private static ContractItemView ToItem(ContractView row) => new()
    {
        ContractId = row.ContractId,
        Title = row.Title,
        TypeText = row.TypeText,
        Price = row.Price,
        IssuerName = row.IssuerName,
        DateIssued = Localize(row.DateIssued),
        DateExpired = row.DateExpired is null ? null : Localize(row.DateExpired.Value),
        AcceptorName = row.AcceptorName,
        StartLocationName = row.StartLocationName,
        Status = row.Status,
        ForCorporation = row.ForCorporation,
        Buyout = row.Buyout,
        Volume = row.Volume,
        Reward = row.Reward,
        Collateral = row.Collateral,
        EndLocationName = row.EndLocationName,
        DaysToComplete = row.DaysToComplete,
    };

    private static DateTime Localize(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value.ToLocalTime(),
        DateTimeKind.Local => value,
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime(),
    };
}

/// <summary>页签公共状态：页码、是否有下一页、是否已加载。</summary>
public sealed class ContractTabViewModel : INotifyPropertyChanged
{
    private int _page = 1;
    private bool _hasNext;
    private bool _loaded;
    private ObservableCollection<ContractItemView> _items = new();

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

    public ObservableCollection<ContractItemView> Items
    {
        get => _items;
        set => Set(ref _items, value);
    }

    public void SetItems(IEnumerable<ContractItemView>? items) =>
        Items = items is null ? new() : new(items);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

/// <summary>合同行视图（列与 WinUI3 ContractPage 一致）。</summary>
public sealed class ContractItemView
{
    public long ContractId { get; init; }

    public string Title { get; init; } = string.Empty;

    public string TypeText { get; init; } = string.Empty;

    public double Price { get; init; }

    public string IssuerName { get; init; } = string.Empty;

    public DateTime DateIssued { get; init; }

    public DateTime? DateExpired { get; init; }

    public string AcceptorName { get; init; } = string.Empty;

    public string StartLocationName { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    /// <summary>是否为军团合同（与 WinUI 的 GridTextColumn 一样直接显示布尔值）。</summary>
    public bool ForCorporation { get; init; }

    public double Buyout { get; init; }

    public double Volume { get; init; }

    public double Reward { get; init; }

    public double Collateral { get; init; }

    public string EndLocationName { get; init; } = string.Empty;

    public int? DaysToComplete { get; init; }
}
