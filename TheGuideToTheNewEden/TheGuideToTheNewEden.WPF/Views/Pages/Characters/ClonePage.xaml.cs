using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Services.Characters;
using TheGuideToTheNewEden.WPF.ViewModels.Characters;

namespace TheGuideToTheNewEden.WPF.Views.Pages.Characters;

/// <summary>
/// 克隆页：基地与上一次变更、克隆数量与上一次远克、当前激活克隆与各跳跃克隆的脑插。
/// 页面实例由工作区长期托管，刷新走 <see cref="RefreshAsync"/>，不重建实例。
/// </summary>
public partial class ClonePage : Page, ICharacterSubPage
{
    private readonly CharacterContext _context;
    private readonly ClonePageViewModel _viewModel = new();

    public ClonePage(CharacterContext context)
    {
        InitializeComponent();

        _context = context;
        DataContext = _viewModel;

        Loaded += async (_, _) => await LoadAsync();
    }

    /// <summary>工作区右上角刷新按钮入口：绕过缓存重新加载。</summary>
    public async Task RefreshAsync(bool forceRefresh = true)
    {
        await LoadAsync(forceRefresh);
    }

    private async Task LoadAsync(bool forceRefresh = false)
    {
        var data = await CharacterCloneService.GetAsync(_context, forceRefresh);
        if (data is null)
        {
            return;
        }

        _viewModel.Apply(data);
    }
}
