using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Services.Characters;
using TheGuideToTheNewEden.WPF.ViewModels.Characters;

namespace TheGuideToTheNewEden.WPF.Views.Pages.Characters;

/// <summary>
/// 角色卡片页：汇总 + 卡片网格 + 添加/移除/打开。
/// </summary>
public partial class CharacterCardsPage : Page
{
    private readonly CharactersViewModel _viewModel = new();

    /// <summary>点击角色卡片时触发（由壳页打开对应标签）。</summary>
    public event EventHandler<CharacterCardViewModel>? CharacterActivated;

    public CharacterCardsPage()
    {
        InitializeComponent();

        DataContext = _viewModel;
        CardList.ItemsSource = _viewModel.Cards;

        RefreshButton.Click += async (_, _) => await ReloadAsync(forceRefresh: true);
        LoginButton.Click += async (_, _) => await AddCharacterAsync();

        _viewModel.PropertyChanged += (_, _) => UpdateEmptyState();

        Loaded += async (_, _) => await ReloadAsync();
    }

    public Task ReloadAsync(bool forceRefresh = false)
    {
        return _viewModel.LoadAsync(forceRefresh).ContinueWith(_ => UpdateEmptyState(), TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void UpdateEmptyState()
    {
        var empty = _viewModel.IsEmpty;
        EmptyState.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        SummaryCard.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        CardList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Card_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: CharacterCardViewModel card })
        {
            return;
        }

        if (card.IsAdd)
        {
            _ = AddCharacterAsync();
            return;
        }

        CharacterActivated?.Invoke(this, card);
    }

    /// <summary>卡片右上角"···"按钮：弹出移除菜单（左键也能打开 ContextMenu）。</summary>
    private void MoreCharacter_Click(object sender, RoutedEventArgs e)
    {
        // 阻止冒泡到整卡，避免同时触发"打开工作区"
        e.Handled = true;

        if (sender is FrameworkElement { ContextMenu: { } menu } element)
        {
            menu.PlacementTarget = element;
            menu.IsOpen = true;
        }
    }

    private void RemoveCharacter_Click(object sender, RoutedEventArgs e)
    {
        // 菜单项的 DataContext 来自放置目标（卡片），因此这里从 DataContext 取
        if ((sender as FrameworkElement)?.DataContext is not CharacterCardViewModel card)
        {
            return;
        }

        // 阻止事件冒泡到卡片本身（避免同时触发"打开"）
        e.Handled = true;

        var confirm = MessageBox.Show(
            $"{FindString("Characters.Remove")}: {card.Name}?",
            FindString("Characters.Title"),
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.OK)
        {
            return;
        }

        _viewModel.Remove(card);
        UpdateEmptyState();
    }

    private async Task AddCharacterAsync()
    {
        var reloadNeeded = false;

        try
        {
            if (CharacterAuthService.IsSerenity)
            {
                // 国服没有可用回调，走"登出 → 打开授权页 → 粘贴网址 → 校验"向导（与 WinUI 一致）
                var wizard = new Views.Windows.SerenityAuthWindow
                {
                    Owner = Window.GetWindow(this),
                };
                wizard.ShowDialog();
                reloadNeeded = wizard.Result is not null;
            }
            else if (!CharacterAuthService.CredentialsAvailable)
            {
                MessageBox.Show(FindString("Characters.LoginFailed"), FindString("Characters.Login"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            else
            {
                reloadNeeded = await CharacterAuthService.LoginAsync() is not null;
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }

        if (reloadNeeded)
        {
            await ReloadAsync(forceRefresh: true);
        }
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}