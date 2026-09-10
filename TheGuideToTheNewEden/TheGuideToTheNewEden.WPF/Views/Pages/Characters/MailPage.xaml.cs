using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Services.Characters;
using TheGuideToTheNewEden.WPF.Views.Windows;

namespace TheGuideToTheNewEden.WPF.Views.Pages.Characters;

/// <summary>邮件页：左侧标签、右侧邮件列表，选中后打开详情窗口。</summary>
public partial class MailPage : Page
{
    private readonly CharacterContext _context;
    private bool _loaded;

    public MailPage(CharacterContext context)
    {
        InitializeComponent();

        _context = context;
        LabelList.SelectionChanged += async (_, _) => await LoadHeadersAsync();
        MailList.SelectionChanged += async (_, _) => await OpenSelectedAsync();

        Loaded += async (_, _) =>
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;
            await LoadLabelsAsync();
        };
    }

    private async Task LoadLabelsAsync()
    {
        var labels = await CharacterMailService.GetLabelsAsync(_context) ?? [];

        // 顶部插入"全部邮件"伪标签
        var items = new List<MailLabelView>
        {
            new() { LabelId = 0, Name = FindString("Characters.Mail.All"), UnreadCount = 0 },
        };
        items.AddRange(labels);

        LabelList.ItemsSource = items;
        LabelList.SelectedIndex = 0;
    }

    private async Task LoadHeadersAsync()
    {
        var labelId = LabelList.SelectedItem is MailLabelView { LabelId: > 0 } label ? label.LabelId : (long?)null;
        var headers = await CharacterMailService.GetHeadersAsync(_context, labelId);
        MailList.ItemsSource = headers;
    }

    private async Task OpenSelectedAsync()
    {
        if (MailList.SelectedItem is not MailHeaderView header)
        {
            return;
        }

        var window = new MailDetailWindow(_context, header.MailId) { Owner = Window.GetWindow(this) };
        window.Show();

        if (!header.IsRead)
        {
        }

        MailList.SelectedItem = null;
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}