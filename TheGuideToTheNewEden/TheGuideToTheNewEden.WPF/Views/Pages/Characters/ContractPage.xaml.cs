using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Services.Characters;
using TheGuideToTheNewEden.WPF.ViewModels.Characters;

namespace TheGuideToTheNewEden.WPF.Views.Pages.Characters;

/// <summary>
/// 合同页：个人/军团两个页签（内容与 WinUI3 ContractPage 对齐），
/// 使用标准 DataGrid 展示，合同按 ESI 分页。页面实例被 Frame 长期托管，切页签只懒加载一次。
/// </summary>
public partial class ContractPage : Page, ICharacterSubPage
{
    private readonly ContractPageViewModel _viewModel;
    private bool _loaded;

    public ContractPage(CharacterContext context)
    {
        InitializeComponent();

        _viewModel = new ContractPageViewModel(context);
        DataContext = _viewModel;

        // 个人合同翻页
        CharacterPager.PageChanged += async (_, page) =>
        {
            CharacterPager.Page = page;
            _viewModel.CharacterContracts.Page = page;
            await _viewModel.LoadCharacterAsync(forceRefresh: true);
        };

        // 军团合同翻页
        CorpPager.PageChanged += async (_, page) =>
        {
            CorpPager.Page = page;
            _viewModel.CorpContracts.Page = page;
            await _viewModel.LoadCorpAsync(forceRefresh: true);
        };

        // 页签首次选中时才加载（避免一次性打两个接口）
        Tabs.SelectionChanged += async (_, _) => await LoadSelectedTabIfNeededAsync();

        Loaded += async (_, _) => await LoadAsync();
    }

    /// <inheritdoc />
    public async Task RefreshAsync(bool forceRefresh = true)
    {
        // 重新加载当前页签：回到第一页并绕过缓存（不重建页面实例）。
        switch (Tabs.SelectedIndex)
        {
            case 0:
                _viewModel.CharacterContracts.Page = 1;
                CharacterPager.Page = 1;
                await _viewModel.LoadCharacterAsync(forceRefresh);
                break;
            case 1:
                _viewModel.CorpContracts.Page = 1;
                CorpPager.Page = 1;
                await _viewModel.LoadCorpAsync(forceRefresh);
                break;
        }
    }

    /// <summary>首次加载：加载当前（默认第一个）页签。</summary>
    private async Task LoadAsync()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        await LoadSelectedTabIfNeededAsync();
    }

    private async Task LoadSelectedTabIfNeededAsync()
    {
        switch (Tabs.SelectedIndex)
        {
            case 0 when !_viewModel.CharacterContracts.Loaded:
                await _viewModel.LoadCharacterAsync(forceRefresh: false);
                break;
            case 1 when !_viewModel.CorpContracts.Loaded:
                await _viewModel.LoadCorpAsync(forceRefresh: false);
                break;
        }
    }
}
