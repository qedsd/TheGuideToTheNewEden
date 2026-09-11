using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.ViewModels.Characters;

namespace TheGuideToTheNewEden.WPF.Views.Pages.Characters;

/// <summary>
/// 角色功能壳页：一个"全部角色"固定标签 + 每个角色一个可关闭标签。
/// 标签内容常驻，切换不丢失状态。
/// </summary>
public partial class CharactersShellPage : Page
{
    private readonly CharacterCardsPage _cardsPage = new();
    private readonly Dictionary<long, TabItem> _characterTabs = [];

    public CharactersShellPage()
    {
        InitializeComponent();

        _cardsPage.CharacterActivated += (_, card) => OpenCharacter(card);

        // 运行时切换游戏服务器：另一个服务器的角色标签已失效，全部关掉并重载卡片。
        CoreInitializer.GameServerChanged += OnGameServerChanged;

        Tabs.Items.Add(CreateTabItem(BuildTextHeader(FindString("Characters.Title")), HostInFrame(_cardsPage)));

        Loaded += async (_, _) => await _cardsPage.ReloadAsync();
    }

    /// <summary>标签外观走全局隐式 TabItem 样式；标签内容统一用 Frame 承载。</summary>
    private TabItem CreateTabItem(object header, UIElement content)
    {
        return new TabItem
        {
            Header = header,
            // Page 只能由 Window/Frame 承载，标签内容统一用 Frame 托管
            Content = content is Page page ? HostInFrame(page) : content,
        };
    }

    private void OnGameServerChanged(object? sender, EventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => OnGameServerChanged(sender, e));
            return;
        }

        foreach (var tab in _characterTabs.Values.ToList())
        {
            Tabs.Items.Remove(tab);
        }

        _characterTabs.Clear();
        Tabs.SelectedItem = Tabs.Items.Count > 0 ? Tabs.Items[0] : null;

        _ = _cardsPage.ReloadAsync(forceRefresh: true);
    }

    /// <summary>打开（或切换到）指定角色的标签。</summary>
    public void OpenCharacter(CharacterCardViewModel card)
    {
        if (_characterTabs.TryGetValue(card.CharacterId, out var existing))
        {
            Tabs.SelectedItem = existing;
            return;
        }

        var tab = CreateTabItem(BuildCharacterHeader(card), BuildWorkspace(card));

        _characterTabs[card.CharacterId] = tab;
        Tabs.Items.Add(tab);
        Tabs.SelectedItem = tab;
    }

    private static TextBlock BuildTextHeader(string text)
    {
        return new TextBlock
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    private static Frame HostInFrame(Page page) => new()
    {
        Content = page,
        NavigationUIVisibility = NavigationUIVisibility.Hidden,
    };

    private StackPanel BuildCharacterHeader(CharacterCardViewModel card)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };

        panel.Children.Add(new TextBlock
        {
            Text = card.Name,
            VerticalAlignment = VerticalAlignment.Center,
        });

        var close = new Button
        {
            Margin = new Thickness(8, 0, 0, 0),
            Padding = new Thickness(2),
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Content = new Wpf.Ui.Controls.SymbolIcon
            {
                Symbol = Wpf.Ui.Controls.SymbolRegular.Dismiss24,
                FontSize = 10,
            },
            ToolTip = FindString("Characters.Remove"),
        };

        close.Click += (_, _) => CloseCharacter(card);
        panel.Children.Add(close);

        return panel;
    }

    private void CloseCharacter(CharacterCardViewModel card)
    {
        if (!_characterTabs.Remove(card.CharacterId, out var tab))
        {
            return;
        }

        Tabs.Items.Remove(tab);
        Tabs.SelectedItem = Tabs.Items.Count > 0 ? Tabs.Items[0] : null;
    }

    /// <summary>每个角色标签承载一个工作区（左侧信息栏 + 右侧子页）。</summary>
    private static UIElement BuildWorkspace(CharacterCardViewModel card) =>
        HostInFrame(new CharacterWorkspacePage(card));

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}
