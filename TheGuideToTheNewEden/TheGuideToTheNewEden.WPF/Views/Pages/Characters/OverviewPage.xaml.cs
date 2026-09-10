using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.Core.Services.DB;
using TheGuideToTheNewEden.WPF.Services.Characters;

namespace TheGuideToTheNewEden.WPF.Views.Pages.Characters;

/// <summary>角色总览：军团/联盟、舰船与位置、在线状态、技能队列。</summary>
public partial class OverviewPage : Page
{
    private readonly CharacterContext _context;

    public OverviewPage(CharacterContext context)
    {
        InitializeComponent();

        _context = context;
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var overview = await CharacterOverviewService.GetAsync(_context);
        if (overview is not null)
        {
            CorporationText.Text = overview.CorporationName ?? "-";
            AllianceText.Text = overview.AllianceName ?? "-";
            ShipText.Text = string.IsNullOrEmpty(overview.ShipTypeName) ? "-" : overview.ShipTypeName;
            OnlineText.Text = overview.Online
                ? FindString("Characters.Online")
                : FindString("Characters.Offline");
            LastLoginText.Text = overview.LastLogin?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "-";

            LocationText.Text = overview.LocationId > 0
                ? await ResolveSystemNameAsync(overview.LocationId)
                : "-";
        }

        var skills = await CharacterSkillService.GetAsync(_context);
        var queue = skills?.Queue ?? [];
        QueueList.ItemsSource = queue;
        QueueEmptyText.Visibility = queue.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static async Task<string> ResolveSystemNameAsync(long solarSystemId)
    {
        try
        {
            var system = await MapSolarSystemService.QueryAsync((int)solarSystemId);
            return system?.SolarSystemName ?? solarSystemId.ToString();
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            return solarSystemId.ToString();
        }
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}