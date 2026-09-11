using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Services.Characters;
using TheGuideToTheNewEden.WPF.ViewModels.Characters;

namespace TheGuideToTheNewEden.WPF.Views.Pages.Characters;

/// <summary>
/// 工业页：角色工业任务列表（列与 WinUI3 工业页对齐）。
/// 页面实例被 Frame 长期托管，刷新通过 <see cref="RefreshAsync"/> 绕过缓存重载，不重建实例。
/// </summary>
public partial class IndustryPage : Page, ICharacterSubPage
{
    private readonly IndustryPageViewModel _viewModel;
    private bool _loaded;

    public IndustryPage(CharacterContext context)
    {
        InitializeComponent();

        _viewModel = new IndustryPageViewModel(context);
        DataContext = _viewModel;

        Loaded += async (_, _) => await LoadAsync();
    }

    /// <inheritdoc />
    public async Task RefreshAsync(bool forceRefresh = true)
    {
        await _viewModel.LoadAsync(forceRefresh);
    }

    /// <summary>首次加载走缓存（若近期已取过数据则直接复用）。</summary>
    private async Task LoadAsync()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        await _viewModel.LoadAsync(forceRefresh: false);
    }
}
