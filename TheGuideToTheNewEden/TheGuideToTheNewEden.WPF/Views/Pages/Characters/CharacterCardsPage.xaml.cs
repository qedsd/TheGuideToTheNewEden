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

    private void RemoveCharacter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: CharacterCardViewModel card })
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
                // 国服没有可用回调，改为手动粘贴 code
                var code = PromptForText(FindString("Characters.Login"), FindString("Characters.SerenityPasteCode"));
                if (string.IsNullOrWhiteSpace(code))
                {
                    return;
                }

                reloadNeeded = await CharacterAuthService.CompleteSerenityAsync(code) is not null;
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

    /// <summary>简易文本输入对话框（阶段 D 会换成 Fluent 对话框）。</summary>
    private static string? PromptForText(string title, string prompt)
    {
        var window = new Window
        {
            Title = title,
            Width = 420,
            Height = 170,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Application.Current.MainWindow,
            ResizeMode = ResizeMode.NoResize,
        };

        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock { Text = prompt, TextWrapping = TextWrapping.Wrap });

        var input = new TextBox { Margin = new Thickness(0, 12, 0, 0) };
        panel.Children.Add(input);

        var ok = new Button { Content = "OK", Width = 88, Margin = new Thickness(0, 16, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
        string? result = null;
        ok.Click += (_, _) =>
        {
            result = input.Text;
            window.Close();
        };
        panel.Children.Add(ok);

        window.Content = panel;
        window.ShowDialog();
        return result;
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}