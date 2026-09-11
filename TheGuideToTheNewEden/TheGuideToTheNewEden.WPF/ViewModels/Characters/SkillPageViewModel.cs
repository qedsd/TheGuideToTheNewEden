using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using TheGuideToTheNewEden.WPF.Services.Characters;

namespace TheGuideToTheNewEden.WPF.ViewModels.Characters;

/// <summary>
/// 技能页视图模型（与 WinUI3 SkillPage 的信息项对齐）：
/// 技能点汇总（总技能点 / 未分配）、技能队列（进行中 / 等待中 / 已完成 / 已暂停）、
/// 按技能组展开的技能列表（技能名 / 已掌握等级 / 已训练技能点），并支持按技能名搜索过滤。
/// </summary>
public sealed class SkillPageViewModel : INotifyPropertyChanged
{
    private readonly CharacterContext _context;

    /// <summary>未过滤的完整分组，搜索时在其基础上生成 <see cref="Groups"/>。</summary>
    private readonly List<SkillGroupItem> _allGroups = [];

    private string _totalSkillPointsText = "-";
    private string _unallocatedSkillPointsText = "-";
    private string _searchText = string.Empty;
    private bool _isLoading;
    private bool _hasQueue;
    private bool _hasGroups;

    public SkillPageViewModel(CharacterContext context)
    {
        _context = context;
    }

    /// <summary>当前展示（经搜索过滤）的技能组。</summary>
    public ObservableCollection<SkillGroupItem> Groups { get; } = [];

    /// <summary>技能队列条目，按队列位置排序。</summary>
    public ObservableCollection<SkillQueueEntry> Queue { get; } = [];

    /// <summary>总技能点（已格式化）。</summary>
    public string TotalSkillPointsText
    {
        get => _totalSkillPointsText;
        private set => Set(ref _totalSkillPointsText, value);
    }

    /// <summary>未分配技能点（已格式化）。</summary>
    public string UnallocatedSkillPointsText
    {
        get => _unallocatedSkillPointsText;
        private set => Set(ref _unallocatedSkillPointsText, value);
    }

    /// <summary>搜索关键字；变化时即时过滤技能列表。</summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (Set(ref _searchText, value))
            {
                ApplyFilter();
            }
        }
    }

    /// <summary>是否正在加载。</summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set => Set(ref _isLoading, value);
    }

    /// <summary>技能队列是否有内容（为空时显示占位提示）。</summary>
    public bool HasQueue
    {
        get => _hasQueue;
        private set => Set(ref _hasQueue, value);
    }

    /// <summary>是否有可展示的技能分组（为空时显示占位提示）。</summary>
    public bool HasGroups
    {
        get => _hasGroups;
        private set => Set(ref _hasGroups, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>加载技能数据（forceRefresh=true 时绕过 30 分钟 TTL 缓存）。</summary>
    public async Task LoadAsync(bool forceRefresh = false)
    {
        IsLoading = true;
        try
        {
            var data = await CharacterSkillService.GetAsync(_context, forceRefresh);
            if (data is null)
            {
                return;
            }

            TotalSkillPointsText = data.TotalSkillPoints.ToString("N0");
            UnallocatedSkillPointsText = data.UnallocatedSkillPoints.ToString("N0");

            _allGroups.Clear();
            foreach (var group in data.Groups)
            {
                var item = new SkillGroupItem(
                    group.GroupName,
                    group.Skills.Select(s => new SkillItem(s.Name, s.Level, s.SkillPoints)).ToList());
                if (item.Skills.Count > 0)
                {
                    _allGroups.Add(item);
                }
            }

            Queue.Clear();
            foreach (var queued in data.Queue)
            {
                Queue.Add(SkillQueueEntry.Create(queued));
            }

            HasQueue = Queue.Count > 0;
            ApplyFilter();
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>按 <see cref="SearchText"/> 过滤技能组；无关键字时展示全部分组。</summary>
    public void ApplyFilter()
    {
        var keyword = _searchText?.Trim();

        Groups.Clear();

        if (string.IsNullOrEmpty(keyword))
        {
            foreach (var group in _allGroups)
            {
                Groups.Add(group);
            }
        }
        else
        {
            foreach (var group in _allGroups)
            {
                var matched = group.Skills
                    .Where(s => s.Name.Contains(keyword, StringComparison.CurrentCultureIgnoreCase))
                    .ToList();
                if (matched.Count > 0)
                {
                    Groups.Add(new SkillGroupItem(group.GroupName, matched));
                }
            }
        }

        HasGroups = Groups.Count > 0;
    }

    private bool Set<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }

    /// <summary>读取语言资源；缺失时退回键名（与页面其他部分一致）。</summary>
    internal static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}

/// <summary>技能组（Expander 一行）。</summary>
public sealed class SkillGroupItem
{
    public SkillGroupItem(string groupName, List<SkillItem> skills)
    {
        GroupName = groupName;
        Skills = skills;
    }

    /// <summary>技能组名称。</summary>
    public string GroupName { get; }

    /// <summary>组内技能（已训练的技能；搜索时仅保留匹配项）。</summary>
    public List<SkillItem> Skills { get; }
}

/// <summary>单个技能行：名称 / 已掌握等级 / 已训练技能点。</summary>
public sealed class SkillItem
{
    public SkillItem(string name, int level, long skillPoints)
    {
        Name = name;
        Level = level;
        SkillPoints = skillPoints;
    }

    public string Name { get; }

    /// <summary>已掌握的等级。</summary>
    public int Level { get; }

    /// <summary>已训练的技能点。</summary>
    public long SkillPoints { get; }
}

/// <summary>
/// 技能队列条目。状态判定与 WinUI/Core 的 <c>SkillQueueItem</c> 一致：
/// 缺任一时间为已暂停；结束时间已过为已完成；开始时间未到为等待中；其余为进行中。
/// </summary>
public sealed class SkillQueueEntry
{
    private SkillQueueEntry()
    {
    }

    public string SkillName { get; init; } = string.Empty;

    /// <summary>本条训练完成后达到的技能等级。</summary>
    public int FinishedLevel { get; init; }

    public bool IsRunning { get; init; }

    public bool IsWaiting { get; init; }

    public bool IsFinished { get; init; }

    public bool IsPause { get; init; }

    /// <summary>状态图标（Segoe MDL2 Assets 字形）。</summary>
    public string StateGlyph { get; init; } = string.Empty;

    /// <summary>状态文字（进行中 / 等待中 / 已完成 / 已暂停）。</summary>
    public string StateText { get; init; } = string.Empty;

    /// <summary>剩余时间文本；已暂停或无结束时间时为空。</summary>
    public string RemainTimeText { get; init; } = string.Empty;

    /// <summary>开始时间（本地时间，yyyy.MM.dd HH:mm）。</summary>
    public string StartText { get; init; } = string.Empty;

    /// <summary>结束时间（本地时间，yyyy.MM.dd HH:mm）。</summary>
    public string FinishText { get; init; } = string.Empty;

    /// <summary>由服务层 DTO 计算队列状态与显示文本。</summary>
    public static SkillQueueEntry Create(SkillQueueView item)
    {
        var now = DateTime.UtcNow;
        var start = item.Start;
        var finish = item.Finish;

        var isPause = start is null || finish is null;
        var isFinished = finish is not null && finish.Value < now;
        var isWaiting = start is not null && finish is not null && start.Value > now;
        var isRunning = !(isPause || isFinished || isWaiting);

        string glyph;
        string stateKey;

        if (isPause)
        {
            glyph = "\uE004";
            stateKey = "CharacterPage_Skill_Pause";
        }
        else if (isFinished)
        {
            glyph = "\uE001";
            stateKey = "CharacterPage_Skill_Finished";
        }
        else if (isWaiting)
        {
            glyph = "\uE07F";
            stateKey = "CharacterPage_Skill_Waiting";
        }
        else
        {
            glyph = "\uE1F5";
            stateKey = "CharacterPage_Skill_Running";
        }

        var remain = string.Empty;
        if (!isPause && finish is not null)
        {
            // 与 WinUI 一致：进行中按"结束时间 - 现在"，其余按"结束时间 - 开始时间"。
            var span = isRunning && start is not null ? finish.Value - now : finish.Value - start!.Value;
            remain = span.Days >= 1
                ? $"{span.Days}d {span.Hours}h {span.Minutes}min"
                : $"{span.Hours}h {span.Minutes}min";
        }

        return new SkillQueueEntry
        {
            SkillName = item.SkillName,
            FinishedLevel = item.FinishedLevel,
            IsRunning = isRunning,
            IsWaiting = isWaiting,
            IsFinished = isFinished,
            IsPause = isPause,
            StateGlyph = glyph,
            StateText = SkillPageViewModel.FindString(stateKey),
            RemainTimeText = remain,
            StartText = start is null ? string.Empty : ToLocalText(start.Value),
            FinishText = finish is null ? string.Empty : ToLocalText(finish.Value),
        };
    }

    private static string ToLocalText(DateTime value)
    {
        var local = value.Kind switch
        {
            DateTimeKind.Utc => value.ToLocalTime(),
            DateTimeKind.Local => value,
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime(),
        };
        return local.ToString("yyyy.MM.dd HH:mm");
    }
}
