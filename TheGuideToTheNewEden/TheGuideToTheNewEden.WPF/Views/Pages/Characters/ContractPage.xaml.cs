using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Controls;
using TheGuideToTheNewEden.WPF.Services.Characters;

namespace TheGuideToTheNewEden.WPF.Views.Pages.Characters;

/// <summary>合同页：角色合同与军团合同（分页表格）。</summary>
public partial class ContractPage : Page
{
    private readonly CharacterContext _context;
    private long _corporationId;

    public ContractPage(CharacterContext context)
    {
        InitializeComponent();

        _context = context;
        _corporationId = context.Character.CorporationID;

        Tabs.Items.Add(BuildTab("Characters.Contract.Character", corporation: false));
        Tabs.Items.Add(BuildTab("Characters.Contract.Corp", corporation: true));

        Tabs.SelectionChanged += (_, _) =>
        {
            if (Tabs.SelectedItem is TabItem { Tag: TabState { Loaded: false } } tab)
            {
                _ = LoadAsync(tab, 1, forceRefresh: false);
            }
        };

        Loaded += async (_, _) =>
        {
            var overview = await CharacterOverviewService.GetAsync(_context);
            if (overview is not null && overview.CorporationId > 0)
            {
                _corporationId = overview.CorporationId;
            }

            if (Tabs.SelectedItem is TabItem tab)
            {
                await LoadAsync(tab, 1, forceRefresh: false);
            }
        };
    }

    private TabItem BuildTab(string headerKey, bool corporation)
    {
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            IsReadOnly = true,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HeadersVisibility = DataGridHeadersVisibility.Column,
        };

        AddColumn(grid, "Characters.Contract.Title", nameof(ContractView.Title), 2, null);
        AddColumn(grid, "Characters.Contract.Type", nameof(ContractView.TypeText), 1, null);
        AddColumn(grid, "Characters.Contract.Price", nameof(ContractView.Price), 0, "N2");
        AddColumn(grid, "Characters.Contract.Status", nameof(ContractView.Status), 0, null);
        AddColumn(grid, "Characters.Contract.StartLocation", nameof(ContractView.StartLocationName), 2, null);
        AddColumn(grid, "Characters.Contract.EndLocation", nameof(ContractView.EndLocationName), 2, null);
        AddColumn(grid, "Characters.Contract.Issued", nameof(ContractView.DateIssued), 0, "yyyy-MM-dd");

        var pager = new PagerControl { Page = 1, Margin = new Thickness(0, 8, 0, 0) };

        var panel = new DockPanel();
        DockPanel.SetDock(pager, Dock.Bottom);
        panel.Children.Add(pager);
        panel.Children.Add(grid);

        var tab = new TabItem
        {
            Header = FindString(headerKey),
            Content = panel,
            Tag = new TabState { IsCorporation = corporation, Pager = pager, Grid = grid },
        };

        pager.PageChanged += async (_, page) =>
        {
            var state = (TabState)tab.Tag;
            state.Pager.Page = page;
            await LoadAsync(tab, page, forceRefresh: true);
        };

        return tab;
    }

    private void AddColumn(DataGrid grid, string headerKey, string path, double starWidth, string? format)
    {
        var column = new DataGridTextColumn
        {
            Header = FindString(headerKey),
            Binding = new System.Windows.Data.Binding(path) { StringFormat = format },
            Width = starWidth > 0
                ? new DataGridLength(starWidth, DataGridLengthUnitType.Star)
                : DataGridLength.Auto,
        };

        grid.Columns.Add(column);
    }

    private async Task LoadAsync(TabItem tab, int page, bool forceRefresh)
    {
        if (tab.Tag is not TabState state)
        {
            return;
        }

        state.Loaded = true;
        var result = await CharacterContractService.GetAsync(_context, state.IsCorporation, page);
        state.Grid.ItemsSource = result?.Items;
        state.Pager.HasNext = result?.HasNextPage ?? false;
    }

    private sealed class TabState
    {
        public bool IsCorporation { get; init; }

        public bool Loaded { get; set; }

        public required PagerControl Pager { get; init; }

        public required DataGrid Grid { get; init; }
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}