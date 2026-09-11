using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using TheGuideToTheNewEden.WPF.Services.Characters;

namespace TheGuideToTheNewEden.WPF.ViewModels.Characters;

/// <summary>左侧标签项（含未读数徽标）；LabelId 为 0 表示"全部邮件"伪标签。</summary>
public sealed class MailLabelItem : INotifyPropertyChanged
{
    private int _unreadCount;

    public long LabelId { get; init; }

    public string Name { get; init; } = string.Empty;

    public int UnreadCount
    {
        get => _unreadCount;
        set
        {
            if (Set(ref _unreadCount, value))
            {
                OnPropertyChanged(nameof(HasUnread));
                OnPropertyChanged(nameof(UnreadBadgeText));
            }
        }
    }

    public bool HasUnread => _unreadCount > 0;

    /// <summary>徽标文字（超过 99 显示 99+，避免撑破 16px 圆点）。</summary>
    public string UnreadBadgeText => _unreadCount > 99 ? "99+" : _unreadCount.ToString();

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(name!);
        return true;
    }
}

/// <summary>邮件列表项：发件人 / 主题 / 日期 / 已读状态。</summary>
public sealed class MailHeaderItem : INotifyPropertyChanged
{
    private bool _isRead;

    public MailHeaderItem(MailHeaderView source)
    {
        MailId = source.MailId;
        Subject = source.Subject;
        FromName = source.FromName;
        Date = source.Date;
        _isRead = source.IsRead;
    }

    public long MailId { get; }

    public string Subject { get; }

    public string FromName { get; }

    public DateTime Date { get; }

    /// <summary>打开详情后会被置为已读，需要通知界面刷新未读标记。</summary>
    public bool IsRead
    {
        get => _isRead;
        set
        {
            if (Set(ref _isRead, value))
            {
                OnPropertyChanged(nameof(IsUnread));
            }
        }
    }

    public bool IsUnread => !_isRead;

    /// <summary>ESI 时间为 UTC，统一转本地时间显示。</summary>
    public string DateText => Date == DateTime.MinValue ? "-" : Date.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(name!);
        return true;
    }
}

/// <summary>
/// 邮件页视图模型：标签列表（含"全部邮件"与未读数）+ 当前标签的邮件列表。
/// 数据访问统一走 <see cref="CharacterMailService"/>（标签 2 分钟 TTL 缓存）。
/// </summary>
public sealed class MailPageViewModel : INotifyPropertyChanged
{
    private readonly CharacterContext _context;
    private MailLabelItem? _selectedLabel;
    private bool _isLoading;

    public MailPageViewModel(CharacterContext context)
    {
        _context = context;
    }

    public ObservableCollection<MailLabelItem> Labels { get; } = [];

    public ObservableCollection<MailHeaderItem> Headers { get; } = [];

    /// <summary>当前选中标签；LabelId=0 表示"全部邮件"。</summary>
    public MailLabelItem? SelectedLabel
    {
        get => _selectedLabel;
        set
        {
            if (Set(ref _selectedLabel, value))
            {
                OnPropertyChanged(nameof(SelectedLabelId));
            }
        }
    }

    public long? SelectedLabelId => _selectedLabel is { LabelId: > 0 } label ? label.LabelId : null;

    public bool IsLoading
    {
        get => _isLoading;
        private set => Set(ref _isLoading, value);
    }

    public bool HasLabels => Labels.Count > 0;

    /// <summary>非加载中且列表为空：显示"无邮件"占位。</summary>
    public bool IsMailListEmpty => !_isLoading && Headers.Count == 0;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>加载标签（保留当前选中）与当前标签的邮件列表。</summary>
    public async Task LoadAsync(bool forceRefresh = false)
    {
        await LoadLabelsAsync(forceRefresh);
        await LoadHeadersAsync(SelectedLabelId);
    }

    /// <summary>加载标签；首次会自动选中第一项（"全部邮件"）。</summary>
    public async Task LoadLabelsAsync(bool forceRefresh)
    {
        IsLoading = true;
        try
        {
            var previousId = SelectedLabel?.LabelId ?? 0;
            var labels = await CharacterMailService.GetLabelsAsync(_context, forceRefresh) ?? [];

            Labels.Clear();

            // 顶部插入"全部邮件"伪标签（LabelId=0 → 不限制标签）
            Labels.Add(new MailLabelItem { LabelId = 0, Name = FindString("Characters.Mail.All") });
            foreach (var label in labels)
            {
                Labels.Add(new MailLabelItem
                {
                    LabelId = label.LabelId,
                    Name = label.Name,
                    UnreadCount = label.UnreadCount,
                });
            }

            OnPropertyChanged(nameof(HasLabels));

            SelectedLabel = Labels.FirstOrDefault(l => l.LabelId == previousId) ?? Labels.FirstOrDefault();
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>加载指定标签的邮件头列表（labelId 为空或 0 均表示"全部邮件"，不过滤）。</summary>
    public async Task LoadHeadersAsync(long? labelId)
    {
        // "全部邮件"伪标签的 LabelId 是 0；直接把 0 传给 ESI 会被当成"标签 0"过滤，结果恒为空。
        var effectiveLabelId = labelId is > 0 ? labelId : null;
        IsLoading = true;
        try
        {
            var headers = await CharacterMailService.GetHeadersAsync(_context, effectiveLabelId) ?? [];

            Headers.Clear();
            foreach (var header in headers)
            {
                Headers.Add(new MailHeaderItem(header));
            }

            OnPropertyChanged(nameof(IsMailListEmpty));
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>详情窗口成功打开后把对应列表项标记为已读。</summary>
    public void MarkRead(long mailId)
    {
        var item = Headers.FirstOrDefault(h => h.MailId == mailId);
        if (item is not null)
        {
            item.IsRead = true;
        }
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;

    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(name!);
        return true;
    }
}
