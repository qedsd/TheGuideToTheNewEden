using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Services.Characters;
using TheGuideToTheNewEden.WPF.ViewModels.Characters;

namespace TheGuideToTheNewEden.WPF.Views.Pages.Characters;

/// <summary>
/// 角色工作区：左侧信息栏 + 右侧子页（实例常驻，切换不丢状态）。
/// </summary>
public partial class CharacterWorkspacePage : Page
{
    private readonly CharacterCardViewModel _card;
    private readonly CharacterContext _context;

    public CharacterWorkspacePage(CharacterCardViewModel card)
    {
        InitializeComponent();

        _card = card;
        _context = new CharacterContext(card.Character);
        DataContext = card;

        BuildSubTabs();
        Loaded += async (_, _) => await LoadAsync();
    }

    private void BuildSubTabs()
    {
        SubTabs.Items.Add(CreateTab("Characters.Tab.Overview", new OverviewPage(_context)));
        SubTabs.Items.Add(CreateTab("Characters.Tab.Skill", new SkillPage(_context)));
        SubTabs.Items.Add(CreateTab("Characters.Tab.Clone", new ClonePage(_context)));
        SubTabs.Items.Add(CreateTab("Characters.Tab.Wallet", new WalletPage(_context)));
        SubTabs.Items.Add(CreateTab("Characters.Tab.Mail", new MailPage(_context)));
        SubTabs.Items.Add(CreateTab("Characters.Tab.Contract", new ContractPage(_context)));
        SubTabs.Items.Add(CreateTab("Characters.Tab.Industry", new IndustryPage(_context)));
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
        NavigationUIVisibility = System.Windows.Navigation.NavigationUIVisibility.Hidden,
    };

    private static UIElement BuildNotImplemented()
    {
        return new TextBlock
        {
            Text = FindString("Characters.NotImplemented"),
            Margin = new Thickness(32),
            Opacity = 0.7,
            TextWrapping = TextWrapping.Wrap,
        };
    }

    private async Task LoadAsync()
    {
        StatusText.Text = _card.StatusText;
        WalletText.Text = _card.WalletText;
        LoyaltyText.Text = _card.Loyalty.ToString("N0");
        SkillPointsText.Text = _card.SkillPoints.ToString("N0");

        var overview = await CharacterOverviewService.GetAsync(_context);
        if (overview is not null)
        {
            CorporationText.Text = overview.CorporationName ?? "-";
            AllianceText.Text = overview.AllianceName ?? "-";
            BirthdayText.Text = overview.Birthday?.ToLocalTime().ToString("yyyy-MM-dd") ?? "-";
            SecurityText.Text = overview.SecurityStatus.ToString("0.##");
            WalletText.Text = CharacterCardViewModel.FormatIsk(overview.WalletBalance);
            LoyaltyText.Text = overview.LoyaltyPoints.ToString("N0");
            StatusText.Text = _card.StatusText;
        }

        var skills = await CharacterSkillService.GetAsync(_context);
        if (skills is not null)
        {
            SkillPointsText.Text = skills.TotalSkillPoints.ToString("N0");
            UnallocatedText.Text = skills.UnallocatedSkillPoints.ToString("N0");

            var finish = skills.Queue.Select(p => p.Finish).Where(p => p is not null).DefaultIfEmpty(null).Max();
            QueueText.Text = $"{skills.Queue.Count}";
            QueueEndText.Text = finish is null
                ? FindString("Characters.NotTraining")
                : $"{FindString("Characters.QueueEnds")}: {finish.Value.ToLocalTime():yyyy-MM-dd HH:mm}";
        }
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}