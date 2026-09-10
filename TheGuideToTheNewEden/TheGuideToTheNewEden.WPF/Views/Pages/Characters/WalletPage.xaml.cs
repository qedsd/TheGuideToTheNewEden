using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Controls;
using TheGuideToTheNewEden.WPF.Services.Characters;

namespace TheGuideToTheNewEden.WPF.Views.Pages.Characters;

/// <summary>
/// 钱包页：角色/军团 × 流水/交易 四个页签，DataGrid + 通用分页控件。
/// 使用 WPF-UI DataGrid（不依赖 Syncfusion）。
/// </summary>
public partial class WalletPage : Page
{
    private readonly CharacterContext _context;
    private long _corporationId;
    private bool _loaded;

    public WalletPage(CharacterContext context)
    {
        InitializeComponent();

        _context = context;
        _corporationId = context.Character.CorporationID;

        Tabs.Items.Add(BuildJournalTab("Characters.Wallet.CharacterJournal", corporation: false));
        Tabs.Items.Add(BuildTransactionTab("Characters.Wallet.CharacterTransactions", corporation: false));
        Tabs.Items.Add(BuildJournalTab("Characters.Wallet.CorpJournal", corporation: true));
        Tabs.Items.Add(BuildTransactionTab("Characters.Wallet.CorpTransactions", corporation: true));

        // 页签首次选中时才加载（避免一次性打 4 个接口）
        Tabs.SelectionChanged += (_, _) =>
        {
            if (Tabs.SelectedItem is TabItem { Tag: TabState { Loaded: false } } tab)
            {
                _ = LoadTabAsync(tab);
            }
        };

        Loaded += async (_, _) =>
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;

            // 军团钱包需要军团 ID，优先用总览接口刷新一次
            var overview = await CharacterOverviewService.GetAsync(_context);
            if (overview is not null && overview.CorporationId > 0)
            {
                _corporationId = overview.CorporationId;
            }

            await LoadTabAsync((TabItem)Tabs.Items[0]);
        };
    }

    // ---------- 页签构建 ----------

    private TabItem BuildJournalTab(string headerKey, bool corporation)
    {
        var grid = CreateGrid(
            ("Characters.Wallet.Date", nameof(JournalRow.Date), "yyyy-MM-dd HH:mm", 150),
            ("Characters.Wallet.Amount", nameof(JournalRow.Amount), "N2", 130),
            ("Characters.Wallet.Balance", nameof(JournalRow.Balance), "N2", 140),
            ("Characters.Wallet.RefType", nameof(JournalRow.RefType), null, 160),
            ("Characters.Wallet.Description", nameof(JournalRow.Description), null, 0));

        return BuildTab(headerKey, corporation, grid);
    }

    private TabItem BuildTransactionTab(string headerKey, bool corporation)
    {
        var grid = CreateGrid(
            ("Characters.Wallet.Date", nameof(TransactionRow.Date), "yyyy-MM-dd HH:mm", 150),
            ("Characters.Wallet.Item", nameof(TransactionRow.TypeName), null, 0),
            ("Characters.Wallet.UnitPrice", nameof(TransactionRow.UnitPrice), "N2", 120),
            ("Characters.Wallet.Quantity", nameof(TransactionRow.Quantity), "N0", 80),
            ("Characters.Wallet.Total", nameof(TransactionRow.TotalPrice), "N2", 130),
            ("Characters.Wallet.IsBuy", nameof(TransactionRow.IsBuyText), null, 70));

        return BuildTab(headerKey, corporation, grid);
    }

    private TabItem BuildTab(string headerKey, bool corporation, DataGrid grid)
    {
        var pager = new PagerControl { Page = 1 };
        var divisionBox = new Wpf.Ui.Controls.NumberBox
        {
            Minimum = 1,
            Maximum = 7,
            Value = 1,
            Width = 120,
            SpinButtonPlacementMode = Wpf.Ui.Controls.NumberBoxSpinButtonPlacementMode.Compact,
            Margin = new Thickness(0, 0, 0, 8),
            Visibility = corporation ? Visibility.Visible : Visibility.Collapsed,
        };

        var panel = new DockPanel();
        DockPanel.SetDock(divisionBox, Dock.Top);
        panel.Children.Add(divisionBox);
        DockPanel.SetDock(pager, Dock.Bottom);
        pager.Margin = new Thickness(0, 8, 0, 0);
        panel.Children.Add(pager);
        panel.Children.Add(grid);

        var tab = new TabItem
        {
            Header = FindString(headerKey),
            Content = panel,
            Tag = new TabState { IsCorporation = corporation, IsJournal = grid.Tag is "journal", Pager = pager, Grid = grid, Division = divisionBox },
        };

        pager.PageChanged += async (_, page) =>
        {
            var state = (TabState)tab.Tag;
            state.Pager.Page = page;
            await LoadAsync(state, page, forceRefresh: page > 1);
        };

        divisionBox.ValueChanged += async (_, _) =>
        {
            var state = (TabState)tab.Tag;
            await LoadAsync(state, 1, forceRefresh: true);
        };

        return tab;
    }

    private DataGrid CreateGrid(params (string HeaderKey, string Path, string? Format, double Width)[] columns)
    {
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            IsReadOnly = true,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            Tag = columns.Any(c => c.Path == nameof(JournalRow.Amount)) ? "journal" : "transaction",
        };

        foreach (var (headerKey, path, format, width) in columns)
        {
            var column = new DataGridTextColumn
            {
                Header = FindString(headerKey),
                Binding = new System.Windows.Data.Binding(path) { StringFormat = format },
            };

            column.Width = width > 0 ? new DataGridLength(width) : new DataGridLength(1, DataGridLengthUnitType.Star);
            grid.Columns.Add(column);
        }

        return grid;
    }

    // ---------- 数据加载 ----------

    private async Task LoadTabAsync(TabItem tab)
    {
        if (tab.Tag is not TabState state)
        {
            return;
        }

        await LoadAsync(state, 1, forceRefresh: false);
    }

    private async Task LoadAsync(TabState state, int page, bool forceRefresh)
    {
        state.Loaded = true;

        if (state.IsJournal)
        {
            if (state.IsCorporation)
            {
                var rows = await CharacterWalletService.GetCorporationJournalAsync(
                    _context, _corporationId, (int)(state.Division.Value ?? 1), page);
                state.Grid.ItemsSource = rows?.Select(JournalRow.From).ToList();
                state.Pager.HasNext = rows is { Count: > 0 };
            }
            else
            {
                var result = await CharacterWalletService.GetCharacterJournalAsync(_context, page, forceRefresh);
                state.Grid.ItemsSource = result?.Items.Select(JournalRow.From).ToList();
                state.Pager.HasNext = result?.HasNextPage ?? false;
            }
        }
        else
        {
            if (state.IsCorporation)
            {
                var rows = await CharacterWalletService.GetCorporationTransactionsAsync(
                    _context, _corporationId, (int)(state.Division.Value ?? 1));
                state.Grid.ItemsSource = rows?.Select(TransactionRow.From).ToList();
                state.Pager.HasNext = false;
            }
            else
            {
                var result = await CharacterWalletService.GetCharacterTransactionsAsync(_context, page, forceRefresh);
                state.Grid.ItemsSource = result?.Items.Select(TransactionRow.From).ToList();
                state.Pager.HasNext = result?.HasNextPage ?? false;
            }
        }
    }

    // ---------- 展示用投影（避免把本地化/格式化塞进服务层 DTO） ----------

    private sealed class JournalRow
    {
        public DateTime Date { get; init; }

        public double Amount { get; init; }

        public double Balance { get; init; }

        public string? RefType { get; init; }

        public string? Description { get; init; }

        public static JournalRow From(WalletJournalRow row) => new()
        {
            Date = row.Date.ToLocalTime(),
            Amount = row.Amount,
            Balance = row.Balance,
            RefType = row.RefType,
            Description = row.Description,
        };
    }

    private sealed class TransactionRow
    {
        public DateTime Date { get; init; }

        public string? TypeName { get; init; }

        public double UnitPrice { get; init; }

        public int Quantity { get; init; }

        public double TotalPrice { get; init; }

        public string IsBuyText { get; init; } = string.Empty;

        public static TransactionRow From(WalletTransactionRow row) => new()
        {
            Date = row.Date.ToLocalTime(),
            TypeName = row.TypeName,
            UnitPrice = row.UnitPrice,
            Quantity = row.Quantity,
            TotalPrice = row.TotalPrice,
            IsBuyText = FindString(row.IsBuy ? "Characters.Wallet.Buy" : "Characters.Wallet.Sell"),
        };
    }

    private sealed class TabState
    {
        public bool IsCorporation { get; init; }

        public bool IsJournal { get; init; }

        public bool Loaded { get; set; }

        public required PagerControl Pager { get; init; }

        public required DataGrid Grid { get; init; }

        public required Wpf.Ui.Controls.NumberBox Division { get; init; }
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}