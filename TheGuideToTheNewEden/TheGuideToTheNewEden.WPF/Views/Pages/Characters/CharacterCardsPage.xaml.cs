using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Characters;
using TheGuideToTheNewEden.WPF.Services.Home;
using TheGuideToTheNewEden.WPF.ViewModels.Characters;

namespace TheGuideToTheNewEden.WPF.Views.Pages.Characters;

/// <summary>
/// 角色卡片页：汇总 + 卡片网格 / 紧凑列表（可切换） + 添加/移除/打开。
/// </summary>
public partial class CharacterCardsPage : Page
{
    /// <summary>角色页视图形态（false = 卡片网格，true = 紧凑列表）；存 settings.json，重启后沿用。</summary>
    private const string ListViewSettingKey = "Characters.ListView";

    private readonly CharactersViewModel _viewModel = new();

    /// <summary>当前登录等待的取消源；仅用于页面内遮罩的「取消」按钮。</summary>
    private CancellationTokenSource? _loginCts;

    private CharacterCardViewModel? _dragCard;
    private Point _dragStart;
    private bool _suppressClick;

    /// <summary>当前是否为列表视图（角色多时用它扫读，单行仍保留头像/状态/SP/ISK/LP/技能队列）。</summary>
    private bool _listView;

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
        RowList.ItemsSource = _viewModel.Cards;

        RefreshButton.Click += async (_, _) => await ReloadAsync(forceRefresh: true);
        LoginButton.Click += async (_, _) => await AddCharacterAsync();
        AddCharacterButton.Click += async (_, _) => await AddCharacterAsync();

        _viewModel.PropertyChanged += (_, _) => UpdateEmptyState();

        // 视图形态是纯界面偏好：沿用上次的选择（默认卡片网格）
        ApplyViewMode(SettingsService.GetBool(ListViewSettingKey), persist: false);

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
        => _viewModel.Cards.FirstOrDefault(p => p.CharacterId == characterId);

    private void UpdateEmptyState()
    {
        var empty = _viewModel.IsEmpty;
        EmptyState.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        SummaryCard.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        DragHint.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        ViewHost.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;

        // 空态下两种视图都不显示，否则按当前视图二选一
        CardList.Visibility = empty || _listView ? Visibility.Collapsed : Visibility.Visible;
        RowScrollHost.Visibility = empty || !_listView ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>切换卡片 / 列表视图：同步两个按钮的选中态与两个容器，必要时持久化。</summary>
    private void ApplyViewMode(bool listView, bool persist)
    {
        _listView = listView;

        // 两个 ToggleButton 互斥：点已选中的那个时 WPF 会先取消选中，这里按状态回填
        CardViewToggle.IsChecked = !listView;
        ListViewToggle.IsChecked = listView;

        UpdateEmptyState();

        if (persist)
        {
            SettingsService.SetBool(ListViewSettingKey, listView);
        }
    }

    private void CardViewToggle_Click(object sender, RoutedEventArgs e) => ApplyViewMode(listView: false, persist: true);

    private void ListViewToggle_Click(object sender, RoutedEventArgs e) => ApplyViewMode(listView: true, persist: true);

    private void Card_Click(object sender, RoutedEventArgs e)
    {
        // 拖拽结束后 Button 仍可能补一个 Click，不能让它同时打开工作区
        if (_suppressClick)
        {
            _suppressClick = false;
            e.Handled = true;
            return;
        }

        if (sender is not FrameworkElement { Tag: CharacterCardViewModel card })
        {
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

    // ---------- 拖拽排序（卡片网格与列表共用同一套逻辑） ----------

    private void CardList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 新的一次按下先清掉上次拖拽可能残留的"抑制点击"
        _suppressClick = false;

        var button = FindAncestor<Button>(e.OriginalSource as DependencyObject);
        if (button?.Tag is CharacterCardViewModel card)
        {
            _dragCard = card;
            _dragStart = e.GetPosition(ViewHost);
        }
        else
        {
            // "···"按钮落在按钮自身（Tag 不是角色）上，不参与拖拽
            _dragCard = null;
        }
    }

    private void CardList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragCard is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var position = e.GetPosition(ViewHost);
        if (Math.Abs(position.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(position.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var card = _dragCard;
        _dragCard = null;
        _suppressClick = true;
        var data = new DataObject(typeof(CharacterCardViewModel), card);
        DragDrop.DoDragDrop(ViewHost, data, DragDropEffects.Move);
        e.Handled = true;
    }

    private void CardList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => _dragCard = null;

    private void CardList_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(CharacterCardViewModel))
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void CardList_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(CharacterCardViewModel))
            || e.Data.GetData(typeof(CharacterCardViewModel)) is not CharacterCardViewModel source)
        {
            return;
        }

        var cards = _viewModel.Cards;
        var sourceIndex = cards.IndexOf(source);
        if (sourceIndex < 0 || cards.Count <= 1)
        {
            return;
        }

        var hit = FindCardButton(e.OriginalSource as DependencyObject);
        var target = hit?.Card;
        if (target is not null && ReferenceEquals(target, source))
        {
            return;
        }

        var targetIndex = target is null ? -1 : cards.IndexOf(target);
        int finalIndex;
        if (targetIndex < 0)
        {
            // 拖到空白处：放到最后一个角色之后
            finalIndex = cards.Count - 1;
        }
        else
        {
            // 删除源之后的列表里目标的新下标；卡片按左右半边、列表行按上下半边决定插前/插后
            var targetAfterRemoval = targetIndex > sourceIndex ? targetIndex - 1 : targetIndex;
            var after = false;
            if (hit is { } cardHit)
            {
                var drop = e.GetPosition(cardHit.Button);
                after = _listView
                    ? drop.Y > cardHit.Button.ActualHeight / 2
                    : drop.X > cardHit.Button.ActualWidth / 2;
            }

            finalIndex = Math.Clamp(
                targetAfterRemoval + (after ? 1 : 0),
                0,
                cards.Count - 1);
        }

        if (finalIndex != sourceIndex)
        {
            _viewModel.MoveCard(sourceIndex, finalIndex);
        }

        e.Handled = true;
    }

    private static T? FindAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        var current = source;
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = GetParent(current);
        }

        return null;
    }

    /// <summary>向上找真正带角色 DataContext 的卡片 Button（跳过卡片里的"···"按钮）。</summary>
    private static CardHit? FindCardButton(DependencyObject? source)
    {
        var current = source;
        while (current is not null)
        {
            if (current is Button button && button.Tag is CharacterCardViewModel card)
            {
                return new CardHit(button, card);
            }

            current = GetParent(current);
        }

        return null;
    }

    private static DependencyObject? GetParent(DependencyObject child)
    {
        if (child is Visual)
        {
            return VisualTreeHelper.GetParent(child);
        }

        if (child is FrameworkContentElement contentElement)
        {
            return contentElement.Parent;
        }

        return LogicalTreeHelper.GetParent(child);
    }

    private readonly record struct CardHit(Button Button, CharacterCardViewModel Card);

    /// <summary>
    /// 把该角色固定到主页（P3 参数化磁贴：<c>characters + 角色 ID</c>）——
    /// 点主页磁贴会直接打开这个角色的工作区标签。
    /// </summary>
    private void PinCharacter_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not CharacterCardViewModel card)
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