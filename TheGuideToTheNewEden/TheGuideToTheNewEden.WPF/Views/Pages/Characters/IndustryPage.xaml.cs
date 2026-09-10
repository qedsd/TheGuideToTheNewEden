using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Services.Characters;

namespace TheGuideToTheNewEden.WPF.Views.Pages.Characters;

/// <summary>工业页：角色工业任务列表。</summary>
public partial class IndustryPage : Page
{
    private readonly CharacterContext _context;
    private bool _loaded;

    public IndustryPage(CharacterContext context)
    {
        InitializeComponent();

        _context = context;
        RefreshButton.Click += async (_, _) => await LoadAsync(forceRefresh: true);
        Loaded += async (_, _) =>
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;
            await LoadAsync();
        };
    }

    private async Task LoadAsync(bool forceRefresh = false)
    {
        var jobs = await CharacterIndustryService.GetAsync(_context, forceRefresh);
        JobGrid.ItemsSource = jobs;
    }
}