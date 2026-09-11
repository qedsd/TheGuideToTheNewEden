using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Services.Characters;
using TheGuideToTheNewEden.WPF.ViewModels.Characters;

namespace TheGuideToTheNewEden.WPF.Views.Pages.Characters;

/// <summary>
/// 技能页：技能点汇总 + 技能队列 + 按技能组折叠的技能列表（含搜索）。
/// 内容项与 WinUI3 的 SkillPage 对齐，队列状态复用工作区/总览的展示约定。
/// </summary>
public partial class SkillPage : Page, ICharacterSubPage
{
    private readonly SkillPageViewModel _viewModel;

    public SkillPage(CharacterContext context)
    {
        InitializeComponent();

        _viewModel = new SkillPageViewModel(context);
        DataContext = _viewModel;

        Loaded += async (_, _) => await LoadAsync();
    }

    /// <summary>工作区右上角刷新按钮调用（forceRefresh=true 时绕过缓存），不重建页面实例。</summary>
    public async Task RefreshAsync(bool forceRefresh = true) => await LoadAsync(forceRefresh);

    private async Task LoadAsync(bool forceRefresh = false) => await _viewModel.LoadAsync(forceRefresh);
}
