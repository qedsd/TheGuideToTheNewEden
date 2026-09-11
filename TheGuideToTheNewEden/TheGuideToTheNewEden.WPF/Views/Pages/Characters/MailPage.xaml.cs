using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Services.Characters;
using TheGuideToTheNewEden.WPF.ViewModels.Characters;
using TheGuideToTheNewEden.WPF.Views.Windows;

namespace TheGuideToTheNewEden.WPF.Views.Pages.Characters;

/// <summary>
/// 邮件页：左侧标签列表（含未读数与"全部邮件"）、右侧邮件列表，
/// 选中邮件后打开详情窗口，并在详情成功加载后标记为已读。
/// 页面实例由工作区长期托管，刷新走 <see cref="RefreshAsync"/>，不重建实例。
/// </summary>
public partial class MailPage : Page, ICharacterSubPage
{
    private readonly CharacterContext _context;
    private readonly MailPageViewModel _viewModel;
    private bool _syncingSelection;

    public MailPage(CharacterContext context)
    {
        InitializeComponent();

        _context = context;
        _viewModel = new MailPageViewModel(context);
        DataContext = _viewModel;

        LabelList.ItemsSource = _viewModel.Labels;
        MailList.ItemsSource = _viewModel.Headers;

        Loaded += async (_, _) => await LoadAsync();
    }

    /// <summary>工作区右上角刷新入口：绕过标签缓存重新加载标签与当前邮件列表。</summary>
    public async Task RefreshAsync(bool forceRefresh = true)
    {
        await LoadAsync(forceRefresh);
    }

    private async Task LoadAsync(bool forceRefresh = false)
    {
        await _viewModel.LoadLabelsAsync(forceRefresh);
        SyncLabelSelection();
        await _viewModel.LoadHeadersAsync(_viewModel.SelectedLabelId);
    }

    /// <summary>把视图模型的选中标签同步回 ListBox（不触发一次多余的加载）。</summary>
    private void SyncLabelSelection()
    {
        _syncingSelection = true;
        try
        {
            LabelList.SelectedItem = _viewModel.SelectedLabel;
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    private async void LabelList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingSelection || LabelList.SelectedItem is not MailLabelItem label)
        {
            return;
        }

        _viewModel.SelectedLabel = label;
        // 注意用 SelectedLabelId（"全部邮件"伪标签 LabelId=0 会被归一化为 null=不过滤），
        // 直接传 label.LabelId 会把 0 当成"标签 0"去过滤，结果恒为空。
        await _viewModel.LoadHeadersAsync(_viewModel.SelectedLabelId);
    }

    private void MailList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MailList.SelectedItem is not MailHeaderItem header)
        {
            return;
        }

        OpenDetail(header);

        // 清空选中，便于再次点击同一封邮件。
        MailList.SelectedItem = null;
    }

    private void OpenDetail(MailHeaderItem header)
    {
        var window = new MailDetailWindow(_context, header.MailId, header.IsUnread)
        {
            Owner = Window.GetWindow(this),
        };

        // 详情成功加载并标记已读后，刷新列表中的未读标记。
        window.MarkedRead += (_, _) => _viewModel.MarkRead(header.MailId);
        window.Show();
    }
}
