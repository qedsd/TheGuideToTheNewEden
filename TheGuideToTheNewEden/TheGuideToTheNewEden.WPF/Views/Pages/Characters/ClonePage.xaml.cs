using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Services.Characters;

namespace TheGuideToTheNewEden.WPF.Views.Pages.Characters;

/// <summary>克隆页：家空间站、跳跃克隆、植入体。</summary>
public partial class ClonePage : Page
{
    private readonly CharacterContext _context;

    public ClonePage(CharacterContext context)
    {
        InitializeComponent();

        _context = context;
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var data = await CharacterCloneService.GetAsync(_context);
        if (data is null)
        {
            return;
        }

        HomeText.Text = data.HomeLocation ?? "-";
        LastStationChangeText.Text = data.LastStationChange?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "-";
        JumpCloneCountText.Text = data.JumpCloneCount.ToString();
        LastJumpText.Text = data.LastCloneJump?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "-";
        CloneList.ItemsSource = data.Clones;
    }
}