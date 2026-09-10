using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Services.Characters;

namespace TheGuideToTheNewEden.WPF.Views.Pages.Characters;

/// <summary>技能页：按技能组折叠展示 + 名称搜索（WinUI 版没有搜索）。</summary>
public partial class SkillPage : Page
{
    private readonly CharacterContext _context;
    private List<SkillGroupView> _groups = [];

    public SkillPage(CharacterContext context)
    {
        InitializeComponent();

        _context = context;
        SearchBox.TextChanged += (_, _) => ApplyFilter();
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var skills = await CharacterSkillService.GetAsync(_context);
        if (skills is null)
        {
            return;
        }

        _groups = skills.Groups;
        TotalText.Text =
            $"{FindString("Characters.SkillPoints")}: {skills.TotalSkillPoints:N0}"
            + $"   {FindString("Characters.Unallocated")}: {skills.UnallocatedSkillPoints:N0}";

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var keyword = SearchBox.Text?.Trim();
        if (string.IsNullOrEmpty(keyword))
        {
            GroupList.ItemsSource = _groups;
            return;
        }

        GroupList.ItemsSource = _groups
            .Select(group => new SkillGroupView
            {
                GroupName = group.GroupName,
                Skills = group.Skills
                    .Where(skill => skill.Name.Contains(keyword, StringComparison.CurrentCultureIgnoreCase))
                    .ToList(),
            })
            .Where(group => group.Skills.Count > 0)
            .ToList();
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}