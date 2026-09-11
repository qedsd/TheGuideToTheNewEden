using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using TheGuideToTheNewEden.WPF.Services.Characters;
using TheGuideToTheNewEden.WPF.ViewModels.Characters;

namespace TheGuideToTheNewEden.WPF.Views.Pages.Characters;

/// <summary>
/// 角色工作区：左侧信息栏（身份/资产、技能队列、ZKB）+ 右侧子页（实例常驻，切换不丢状态）。
/// </summary>
public partial class CharacterWorkspacePage : Page
{
    private readonly CharacterWorkspaceViewModel _viewModel;

    public CharacterWorkspacePage(CharacterCardViewModel card)
    {
        InitializeComponent();

        _viewModel = new CharacterWorkspaceViewModel(card);
        DataContext = _viewModel;

        BuildSubTabs();
        Loaded += async (_, _) => await _viewModel.LoadAsync();
    }

    private void BuildSubTabs()
    {
        var context = new CharacterContext(_viewModel.Card.Character);

        SubTabs.Items.Add(CreateTab("Characters.Tab.Overview", new OverviewPage(context)));
        SubTabs.Items.Add(CreateTab("Characters.Tab.Skill", new SkillPage(context)));
        SubTabs.Items.Add(CreateTab("Characters.Tab.Clone", new ClonePage(context)));
        SubTabs.Items.Add(CreateTab("Characters.Tab.Wallet", new WalletPage(context)));
        SubTabs.Items.Add(CreateTab("Characters.Tab.Mail", new MailPage(context)));
        SubTabs.Items.Add(CreateTab("Characters.Tab.Contract", new ContractPage(context)));
        SubTabs.Items.Add(CreateTab("Characters.Tab.Industry", new IndustryPage(context)));
    }

    private static TabItem CreateTab(string headerKey, object content)
    {
        return new TabItem
        {
            Header = FindString(headerKey),
            Content = content is Page page ? HostInFrame(page) : content,
        };
    }

    private static Frame HostInFrame(Page page) => new()
    {
        Content = page,
        NavigationUIVisibility = NavigationUIVisibility.Hidden,
    };

    private async void RefreshLeftButton_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoadAsync(forceRefresh: true);
    }

    /// <summary>刷新当前子页：优先调用页面的 RefreshAsync（不重建实例），否则退回 Frame.Refresh()。</summary>
    private void RefreshPageButton_Click(object sender, RoutedEventArgs e)
    {
        if (SubTabs.SelectedItem is not TabItem { Content: Frame frame })
        {
            return;
        }

        if (frame.Content is ICharacterSubPage subPage)
        {
            _ = subPage.RefreshAsync();
            return;
        }

        frame.Refresh();
    }

    private void ZkbButton_Click(object sender, RoutedEventArgs e)
    {
        Services.Navigation.Navigate(typeof(Pages.ZKBPage));
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}
