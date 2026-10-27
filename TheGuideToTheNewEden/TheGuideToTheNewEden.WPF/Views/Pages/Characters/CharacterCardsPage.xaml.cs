using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Characters;
using TheGuideToTheNewEden.WPF.Services.Home;
using TheGuideToTheNewEden.WPF.ViewModels.Characters;

namespace TheGuideToTheNewEden.WPF.Views.Pages.Characters;

/// <summary>
/// 角色卡片页：汇总 + 卡片网格 + 添加/移除/打开。
/// </summary>
public partial class CharacterCardsPage : Page
{
    private readonly CharactersViewModel _viewModel = new();

    /// <summary>当前登录等待的取消源；仅用于页面内遮罩的「取消」按钮。</summary>
    private CancellationTokenSource? _loginCts;

    /// <summary>点击角色卡片时触发（由壳页打开对应标签）。</summary>
    public event EventHandler<CharacterCardViewModel>? CharacterActivated;

    /// <summary>
    /// 卡片列表重载完成（含首次装入）。壳页用它兜住"要打开的角色卡片还没加载出来"这种情况。
    /// </summary>
    public event EventHandler? CardsReloaded;

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
        return _viewModel.LoadAsync(forceRefresh).ContinueWith(
            _ =>
            {
                UpdateEmptyState();
                CardsReloaded?.Invoke(this, EventArgs.Empty);
            },
            TaskScheduler.FromCurrentSynchronizationContext());
    }

    /// <summary>按角色 ID 找卡片（主页"角色 + 指定角色"快捷方式用）。</summary>
    public CharacterCardViewModel? FindCard(long characterId)
        => _viewModel.Cards.FirstOrDefault(p => !p.IsAdd && p.CharacterId == characterId);

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

    /// <summary>
    /// 把该角色固定到主页（P3 参数化磁贴：<c>characters + 角色 ID</c>）——
    /// 点主页磁贴会直接打开这个角色的工作区标签。
    /// </summary>
    private void PinCharacter_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not CharacterCardViewModel card || card.IsAdd)
        {
            return;
        }

        // 阻止事件冒泡到卡片本身（避免同时触发"打开"）
        e.Handled = true;

        if (HomeShortcutService.Add(
                "characters", card.CharacterId.ToString(CultureInfo.InvariantCulture), card.Name))
        {
            PageNotifyService.Success(string.Format(FindString("Home.Quick.Pinned"), card.Name));
        }
        else
        {
            PageNotifyService.Info(HomeShortcutService.IsFull
                ? string.Format(FindString("Home.Quick.LimitReached"), HomeShortcutService.MaxItems)
                : string.Format(FindString("Home.Quick.AlreadyPinned"), card.Name));
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
                // 国际服：起本地回环监听并等待浏览器回调。
                // 返回 null 的几种情况（回调地址没配好 / 端口被占用 / 用户拒绝 / 超时 / 换码失败）
                // 都要给用户反馈，否则表现为"点了添加角色没反应"（等待已在上限时间内自动结束）。
                var waitingText = FindString("Characters.WaitingForBrowser");
                var verifyingText = FindString("Characters.VerifyingAuthorization");

                using var cts = new CancellationTokenSource();
                _loginCts = cts;
                ShowLoginWaiting(waitingText, allowCancel: true);
                try
                {
                    var character = await CharacterAuthService.LoginAsync(
                        cts.Token,
                        stage =>
                        {
                            // 回调到达后进入"校验授权"：换令牌是网络请求，无法取消，因此去掉取消按钮，
                            // 避免用户点了取消却仍在校验、最后又成功造成误解。
                            ShowLoginWaiting(
                                stage == CharacterAuthService.CharacterLoginStage.Verifying
                                    ? verifyingText
                                    : waitingText,
                                allowCancel: stage != CharacterAuthService.CharacterLoginStage.Verifying);
                        });

                    reloadNeeded = character is not null;
                    if (character is not null)
                    {
                        PageNotifyService.Success($"{FindString("Characters.LoginSuccess")}: {character.CharacterName}");
                    }
                }
                finally
                {
                    _loginCts = null;
                    HideLoginWaiting();
                }

                if (!reloadNeeded)
                {
                    if (cts.IsCancellationRequested)
                    {
                        PageNotifyService.Info(FindString("Characters.AuthorizationCanceled"));
                    }
                    else
                    {
                        // 优先显示具体原因——只有"登录失败"四个字时，用户既不知道是拒绝授权还是配置有问题。
                        MessageBox.Show(
                            string.IsNullOrWhiteSpace(CharacterAuthService.LastFailure)
                                ? FindString("Characters.LoginFailed")
                                : CharacterAuthService.LastFailure,
                            FindString("Characters.Login"),
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            PageNotifyService.Error($"{FindString("Characters.LoginFailed")}: {ex.Message}");
        }

        if (reloadNeeded)
        {
            await ReloadAsync(forceRefresh: true);
        }
    }

    /// <summary>显示/更新页面内登录等待层；<paramref name="allowCancel"/> = 是否显示「取消」。</summary>
    private void ShowLoginWaiting(string message, bool allowCancel)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => ShowLoginWaiting(message, allowCancel));
            return;
        }

        LoginWaitText.Text = message;
        LoginWaitCancelButton.Visibility = allowCancel ? Visibility.Visible : Visibility.Collapsed;
        LoginWaitCancelButton.IsEnabled = allowCancel;
        LoginWaitSpinner.IsActive = true;
        LoginWaitMask.Visibility = Visibility.Visible;
    }

    private void HideLoginWaiting()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(HideLoginWaiting);
            return;
        }

        LoginWaitMask.Visibility = Visibility.Collapsed;
        LoginWaitSpinner.IsActive = false;
    }

    private void OnLoginWaitCancelClick(object sender, RoutedEventArgs e)
    {
        // 只取消"等浏览器"这一段；进入换令牌后按钮已被隐藏。
        LoginWaitCancelButton.IsEnabled = false;
        _loginCts?.Cancel();
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}