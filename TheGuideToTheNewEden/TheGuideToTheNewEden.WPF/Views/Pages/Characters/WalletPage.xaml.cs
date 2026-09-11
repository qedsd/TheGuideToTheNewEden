using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Services.Characters;
using TheGuideToTheNewEden.WPF.ViewModels.Characters;

namespace TheGuideToTheNewEden.WPF.Views.Pages.Characters;

/// <summary>
/// 钱包页：个人/军团 × 流水/交易四个页签（内容与 WinUI3 对齐），
/// 使用标准 DataGrid 展示，流水按 ESI 分页。
/// </summary>
public partial class WalletPage : Page, ICharacterSubPage
{
    private readonly CharacterContext _context;
    private readonly WalletPageViewModel _viewModel;
    private bool _loaded;

    public WalletPage(CharacterContext context)
    {
        InitializeComponent();

        _context = context;
        _viewModel = new WalletPageViewModel(context);
        DataContext = _viewModel;

        // 个人-流水翻页
        CharacterJournalPager.PageChanged += async (_, page) =>
        {
            CharacterJournalPager.Page = page;
            _viewModel.CharacterJournal.Page = page;
            await _viewModel.LoadCharacterJournalAsync(forceRefresh: true);
        };

        // 军团-流水翻页
        CorpJournalPager.PageChanged += async (_, page) =>
        {
            CorpJournalPager.Page = page;
            _viewModel.CorpJournal.Page = page;
            await _viewModel.LoadCorpJournalAsync(forceRefresh: true);
        };

        // 军团-流水钱包 division 变化：回到第一页重取
        CorpJournalDivision.ValueChanged += async (_, _) =>
        {
            _viewModel.CorpJournal.Division = CorpJournalDivision.Value ?? 1;
            _viewModel.CorpJournal.Page = 1;
            CorpJournalPager.Page = 1;
            await _viewModel.LoadCorpJournalAsync(forceRefresh: true);
        };

        // 军团-交易钱包 division 变化：重取
        CorpTransactionDivision.ValueChanged += async (_, _) =>
        {
            _viewModel.CorpTransactions.Division = CorpTransactionDivision.Value ?? 1;
            await _viewModel.LoadCorpTransactionsAsync(forceRefresh: true);
        };

        // 页签首次选中时才加载（避免一次性打 4 个接口）
        Tabs.SelectionChanged += async (_, _) => await LoadSelectedTabIfNeededAsync();

        Loaded += async (_, _) => await LoadAsync();
    }

    /// <inheritdoc />
    public async Task RefreshAsync(bool forceRefresh = true)
    {
        // 重新加载当前页签：流水回到第一页，并绕过缓存（不重建页面实例）。
        switch (Tabs.SelectedIndex)
        {
            case 0:
                _viewModel.CharacterJournal.Page = 1;
                CharacterJournalPager.Page = 1;
                await _viewModel.LoadCharacterJournalAsync(forceRefresh);
                break;
            case 1:
                await _viewModel.LoadCharacterTransactionsAsync(forceRefresh);
                break;
            case 2:
                _viewModel.CorpJournal.Page = 1;
                CorpJournalPager.Page = 1;
                await _viewModel.LoadCorpJournalAsync(forceRefresh);
                break;
            case 3:
                await _viewModel.LoadCorpTransactionsAsync(forceRefresh);
                break;
        }
    }

    /// <summary>首次加载：刷新军团 ID 后加载当前（默认第一个）页签。</summary>
    private async Task LoadAsync()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;

        // 军团钱包需要军团 ID，优先用总览接口回填。
        var overview = await CharacterOverviewService.GetAsync(_context);
        if (overview is { CorporationId: > 0 })
        {
            _viewModel.CorporationId = overview.CorporationId;
        }

        await LoadSelectedTabIfNeededAsync();
    }

    private async Task LoadSelectedTabIfNeededAsync()
    {
        switch (Tabs.SelectedIndex)
        {
            case 0 when !_viewModel.CharacterJournal.Loaded:
                await _viewModel.LoadCharacterJournalAsync(forceRefresh: false);
                break;
            case 1 when !_viewModel.CharacterTransactions.Loaded:
                await _viewModel.LoadCharacterTransactionsAsync(forceRefresh: false);
                break;
            case 2 when !_viewModel.CorpJournal.Loaded:
                await _viewModel.LoadCorpJournalAsync(forceRefresh: false);
                break;
            case 3 when !_viewModel.CorpTransactions.Loaded:
                await _viewModel.LoadCorpTransactionsAsync(forceRefresh: false);
                break;
        }
    }
}
