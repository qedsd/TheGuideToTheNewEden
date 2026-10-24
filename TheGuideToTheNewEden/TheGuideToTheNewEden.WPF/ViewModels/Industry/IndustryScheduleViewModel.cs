using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using TheGuideToTheNewEden.Core.Models.Indusrty;

namespace TheGuideToTheNewEden.WPF.ViewModels.Industry;

public sealed class IndustryScheduleMaterialViewModel
{
    public IndustryScheduleMaterialViewModel(IndustryScheduleMaterial model)
    {
        Name = model.Name;
        QuantityText = model.Quantity.ToString("N0");
        StateKey = model.StateKey;
        StateText = model.StateText;
    }

    public string Name { get; }
    public string QuantityText { get; }
    public string StateKey { get; }
    public string StateText { get; }
}

/// <summary>
/// 甘特时间轴：按任务起止切成时间段，长任务 / 长空闲段用不等距压缩，
/// 总宽度限制在 1600–3600px；刻度按像素间距自适应，不再逐小时展开。
/// </summary>
public sealed class IndustryScheduleTimeScale
{
    public const double BaselinePixelsPerHour = 80;
    private const double BusyMinWidth = 6;
    private const double IdleMinWidth = 2;
    private const double TargetMinWidth = 1600;
    private const double TargetMaxWidth = 3600;

    private readonly List<Segment> _segments;

    private IndustryScheduleTimeScale(List<Segment> segments, double totalHours, double pixelsPerHour)
    {
        _segments = segments;
        TotalHours = totalHours;
        PixelsPerHour = pixelsPerHour;
    }

    public double PixelsPerHour { get; }
    public double TotalHours { get; }
    public double TimelineWidth => _segments.Count == 0 ? 0 : _segments[^1].EndPixel;
    public bool HasCompression => _segments.Any(p => p.IsCompressed);

    public static IndustryScheduleTimeScale Create(
        IEnumerable<IndustryScheduleJob> jobs,
        double pixelsPerHour,
        double? totalHours = null)
    {
        var list = jobs?.Where(p => p.EndHours > p.StartHours).ToList() ?? new List<IndustryScheduleJob>();
        var total = Math.Max(12, totalHours ?? 0);
        if (list.Count > 0)
        {
            total = Math.Max(total, list.Max(p => p.EndHours));
        }
        total = Math.Max(0.1, total);

        var boundaries = new List<double> { 0, total };
        foreach (var job in list)
        {
            var start = Math.Clamp(job.StartHours, 0, total);
            var end = Math.Clamp(job.EndHours, 0, total);
            if (end - start < 0.01) continue;
            boundaries.Add(start);
            boundaries.Add(end);
        }
        boundaries.Sort();

        var knots = new List<double>();
        foreach (var boundary in boundaries)
        {
            if (knots.Count == 0 || boundary - knots[^1] > 0.001)
            {
                knots.Add(boundary);
            }
        }
        if (knots.Count < 2)
        {
            return new IndustryScheduleTimeScale(new List<Segment>(), total, pixelsPerHour);
        }

        var intervals = new List<(double Start, double End, bool Busy, double Duration)>();
        for (var i = 0; i < knots.Count - 1; i++)
        {
            var start = knots[i];
            var end = knots[i + 1];
            var duration = end - start;
            if (duration <= 0.001) continue;
            var busy = list.Any(p => p.StartHours < end - 0.001 && p.EndHours > start + 0.001);
            intervals.Add((start, end, busy, duration));
        }
        if (intervals.Count == 0)
        {
            return new IndustryScheduleTimeScale(new List<Segment>(), total, pixelsPerHour);
        }

        var targetWidth = Math.Clamp(intervals.Count * 28.0, TargetMinWidth, TargetMaxWidth);
        var minWidths = intervals.Select(p => p.Busy ? BusyMinWidth : IdleMinWidth).ToArray();
        var minSum = minWidths.Sum();
        var minScale = minSum > targetWidth ? targetWidth / minSum : 1;
        var baseWidths = minWidths.Select(p => p * minScale).ToArray();
        var remaining = Math.Max(0, targetWidth - baseWidths.Sum());
        var weights = intervals
            .Select(p => Math.Sqrt(p.Duration) * (p.Busy ? 1.0 : 0.3))
            .ToArray();
        var weightSum = weights.Sum();
        var baseline = Math.Max(1, pixelsPerHour);

        var segments = new List<Segment>(intervals.Count);
        var pixel = 0.0;
        for (var i = 0; i < intervals.Count; i++)
        {
            var width = baseWidths[i] + (weightSum > 0 ? remaining * weights[i] / weightSum : 0);
            var isCompressed = intervals[i].Duration >= 4
                && width < intervals[i].Duration * baseline * 0.6;
            segments.Add(new Segment(
                intervals[i].Start,
                intervals[i].End,
                pixel,
                pixel + width,
                isCompressed));
            pixel += width;
        }

        return new IndustryScheduleTimeScale(segments, total, targetWidth / total);
    }

    public double Map(double hours)
    {
        if (_segments.Count == 0)
        {
            return Math.Max(0, hours) * BaselinePixelsPerHour;
        }

        var value = Math.Clamp(hours, 0, TotalHours);
        foreach (var segment in _segments)
        {
            if (value <= segment.EndHour + 0.0001)
            {
                var duration = segment.EndHour - segment.StartHour;
                if (duration <= 0.0001) return segment.EndPixel;
                var ratio = (value - segment.StartHour) / duration;
                return segment.StartPixel + ratio * segment.Width;
            }
        }
        return TimelineWidth;
    }

    /// <summary>任务条视觉宽度是否明显短于真实时长，需要显示压缩省略标记。</summary>
    public bool IsJobCompressed(IndustryScheduleJob job)
    {
        if (job.DurationHours < 4) return false;
        var width = Map(job.EndHours) - Map(job.StartHours);
        return width < job.DurationHours * BaselinePixelsPerHour * 0.5;
    }

    public IReadOnlyList<IndustryScheduleTickViewModel> CreateTicks(double minSpacing = 110)
    {
        var ticks = new List<IndustryScheduleTickViewModel>();
        if (_segments.Count == 0) return ticks;

        var spacing = Math.Max(40, minSpacing);
        ticks.Add(new IndustryScheduleTickViewModel { Left = 0, Hours = 0, Label = "0h" });
        var lastHour = 0.0;
        var target = spacing;
        var guard = 0;
        while (target < TimelineWidth - 16 && guard++ < 300)
        {
            var hour = Math.Round(HourAtPixel(target) * 2) / 2;
            if (hour <= lastHour + 0.01)
            {
                target += spacing;
                continue;
            }

            var left = Map(hour);
            if (left - ticks[^1].Left >= spacing * 0.55)
            {
                ticks.Add(new IndustryScheduleTickViewModel
                {
                    Left = left,
                    Hours = hour,
                    Label = FormatTick(hour),
                });
                lastHour = hour;
                target = left + spacing;
            }
            else
            {
                target += spacing;
            }
        }

        if (TimelineWidth - ticks[^1].Left > spacing * 0.45)
        {
            ticks.Add(new IndustryScheduleTickViewModel
            {
                Left = TimelineWidth,
                Hours = TotalHours,
                Label = FormatTick(TotalHours),
            });
        }
        return ticks;
    }

    private double HourAtPixel(double pixel)
    {
        var value = Math.Clamp(pixel, 0, TimelineWidth);
        foreach (var segment in _segments)
        {
            if (value <= segment.EndPixel + 0.0001)
            {
                if (segment.Width <= 0.0001) return segment.StartHour;
                var ratio = (value - segment.StartPixel) / segment.Width;
                return segment.StartHour + ratio * (segment.EndHour - segment.StartHour);
            }
        }
        return TotalHours;
    }

    private static string FormatTick(double hours)
        => hours >= 1000 ? $"{hours / 1000:0.#}kh" : $"{hours:0.#}h";

    private readonly record struct Segment(
        double StartHour,
        double EndHour,
        double StartPixel,
        double EndPixel,
        bool IsCompressed)
    {
        public double Width => EndPixel - StartPixel;
    }
}

public sealed class IndustryScheduleJobViewModel : INotifyPropertyChanged
{
    public IndustryScheduleJobViewModel(IndustryScheduleJob model, IndustryScheduleTimeScale timeScale)
    {
        Model = model;
        Inputs = model.Inputs.Select(p => new IndustryScheduleMaterialViewModel(p)).ToList();
        Outputs = model.Outputs.Select(p => new IndustryScheduleMaterialViewModel(p)).ToList();
        Left = Math.Max(0, timeScale.Map(model.StartHours));
        Width = Math.Max(10, timeScale.Map(model.EndHours) - Left);
        DisplayLeft = Left;
        HasCompressedGap = timeScale.IsJobCompressed(model);
        StageOrder = model.Stage switch
        {
            "发明" => 0,
            "反应" => 1,
            "组件" => 2,
            "行星" => 3,
            _ => 4,
        };
    }

    public IndustryScheduleJob Model { get; }
    public string Key => Model.Key;
    public string TypeName => Model.TypeName;
    public string ActivityKey => Model.ActivityKey;
    public string ActivityText => Model.ActivityText;
    public string Stage => Model.Stage;
    public int StageOrder { get; }
    public string RunsText => Model.IsInvention ? $"{Model.Runs:N0} 次尝试" : $"{Model.Runs:N0} 流程";
    public string CharacterName => Model.CharacterName;
    public string LineText => string.IsNullOrEmpty(Model.CharacterName) ? "—" : $"{Model.CharacterName} · {Model.LineId}";
    public string StartText => FormatHours(Model.StartHours);
    public string EndText => FormatHours(Model.EndHours);
    public string DurationText => FormatHours(Model.DurationHours);
    public string TimeText => $"{StartText} → {EndText}";
    public string TimeRangeToolTip => $"开始 {StartText} · 结束 {EndText} · 耗时 {DurationText}";
    public string JobCostText => Model.JobCost > 0 ? Model.JobCost.ToString("N0") : "—";
    public string TimeMultiplierText => $"×{Model.TimeMultiplier:0.00}";
    public string ToolTip => $"{TypeName}\n{ActivityText} · {Model.Runs:N0} 流程 · {TimeText}\n{LineText} · {DurationText}"
        + (IsInProgress ? "\n在制任务：占用当前槽位，完成前不会开始后续任务" : string.Empty)
        + (HasCompressedGap ? "\n条形按时间轴压缩显示；真实起止见上" : string.Empty);

    public bool IsInProgress => Model.IsInProgress;
    public string BarText => IsInProgress ? "在制 · " + TypeName : TypeName;

    public List<IndustryScheduleMaterialViewModel> Inputs { get; }
    public List<IndustryScheduleMaterialViewModel> Outputs { get; }
    public string OutputText => string.Join("  ", Outputs.Select(p => $"{p.Name} {p.QuantityText} {p.StateText}"));
    public double Left { get; }
    public double Width { get; }
    public double DisplayLeft { get; set; }
    public bool HasCompressedGap { get; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private static string FormatHours(double hours)
    {
        if (hours <= 0) return "0h";
        var span = TimeSpan.FromHours(hours);
        if (span.TotalHours >= 1) return $"{(int)span.TotalHours}h {span.Minutes:00}m";
        return $"{span.Minutes}m";
    }
}

public sealed class IndustryScheduleLineViewModel
{
    public IndustryScheduleLineViewModel(IndustryScheduleLine model, IEnumerable<IndustryScheduleJobViewModel> jobs)
    {
        Id = model.Id;
        Kind = model.Kind;
        ActivityKey = model.Kind switch
        {
            "发明" => "invention",
            "反应" => "reaction",
            "行星" => "planetary",
            _ => "mfg",
        };
        Jobs = new ObservableCollection<IndustryScheduleJobViewModel>(jobs.OrderBy(p => p.Model.StartHours));
        var previousRight = 0.0;
        foreach (var job in Jobs)
        {
            job.DisplayLeft = job.Left;
            if (previousRight > 0 && job.DisplayLeft > previousRight && job.DisplayLeft - previousRight <= 24)
            {
                job.DisplayLeft = previousRight;
            }
            previousRight = job.DisplayLeft + job.Width;
        }
        BusyHours = model.BusyHours;
    }

    public string Id { get; }
    public string Kind { get; }
    public string ActivityKey { get; }
    public ObservableCollection<IndustryScheduleJobViewModel> Jobs { get; }
    public double BusyHours { get; }
    public bool HasJobs => Jobs.Count > 0;
    public bool IsIdle => Jobs.Count == 0;
    public IndustryScheduleJobViewModel? FirstJob => Jobs.FirstOrDefault();
    public string QueueText => Jobs.Count > 1 ? $"+{Jobs.Count - 1}" : string.Empty;
    public string QueueToolTip => Jobs.Count > 1 ? $"该产线还有 {Jobs.Count - 1} 个任务排队" : string.Empty;
    public string LineLabel => $"{Id} · {Kind}";
    public string BusyText => BusyHours > 0 ? $"{BusyHours:0.0}h" : "空闲";
}

public sealed class IndustryScheduleLineGroupViewModel
{
    public IndustryScheduleLineGroupViewModel(
        string title,
        string kindKey,
        IReadOnlyList<IndustryScheduleLineViewModel> lines)
    {
        Title = title;
        KindKey = kindKey;
        Lines = lines;
    }

    public string Title { get; }
    public string KindKey { get; }
    public IReadOnlyList<IndustryScheduleLineViewModel> Lines { get; }
}

public sealed class IndustryScheduleCharacterViewModel
{
    public IndustryScheduleCharacterViewModel(
        IndustryScheduleCharacter model,
        IEnumerable<IndustryScheduleLineViewModel> lines)
    {
        Name = model.CharacterName;
        SourceText = string.IsNullOrWhiteSpace(model.Source) ? "技能" : model.Source;
        CapabilitiesText = $"M{model.ManufacturingSlots} · I{model.InventionSlots} · R{model.ReactionSlots} · ×{model.ManufacturingTimeMultiplier:0.00}";
        UtilizationText = $"{model.UtilizationPercent:0}%";
        UsedSlotText = $"{model.UsedSlotHours:0.0} / {model.AvailableSlotHours:0.0} 槽时";
        Lines = new ObservableCollection<IndustryScheduleLineViewModel>(lines);
        ActiveLines = Lines.Where(p => p.HasJobs).ToList();
        LineGroups = ActiveLines
            .GroupBy(p => p.Kind)
            .OrderBy(g => g.Key switch
            {
                "制造" => 0,
                "发明" => 1,
                "反应" => 2,
                _ => 3,
            })
            .Select(g => new IndustryScheduleLineGroupViewModel(
                $"{g.Key} ({g.Count()})",
                g.First().ActivityKey,
                g.ToList()))
            .ToList();
    }

    public string Name { get; }
    public string SourceText { get; }
    public string CapabilitiesText { get; }
    public string UtilizationText { get; }
    public string UsedSlotText { get; }
    public ObservableCollection<IndustryScheduleLineViewModel> Lines { get; }
    public IReadOnlyList<IndustryScheduleLineViewModel> ActiveLines { get; }
    public IReadOnlyList<IndustryScheduleLineGroupViewModel> LineGroups { get; }
    public bool HasActiveLines => ActiveLines.Count > 0;
}

public sealed class IndustryScheduleStepViewModel : INotifyPropertyChanged
{
    public IndustryScheduleStepViewModel(IndustryScheduleJobViewModel job)
    {
        Job = job;
        Stage = job.Stage;
        StageOrder = job.StageOrder;
    }

    public IndustryScheduleJobViewModel Job { get; }
    public string Stage { get; }
    public int StageOrder { get; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class IndustryScheduleCandidateViewModel
{
    public long CharacterId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DurationText { get; set; } = string.Empty;
    public string CapacityText { get; set; } = string.Empty;
    public string TimeText { get; set; } = string.Empty;
    public bool IsCurrent { get; set; }
    public bool IsPinned { get; set; }
    public bool CanReassign => !IsCurrent;
}

public sealed class IndustryScheduleTickViewModel
{
    public double Left { get; set; }
    public double Hours { get; set; }
    public string Label { get; set; } = string.Empty;
}

public sealed class IndustryScheduleGraphNodeViewModel : INotifyPropertyChanged
{
    public IndustryScheduleGraphNodeViewModel(IndustryScheduleJobViewModel job, double x, double y)
    {
        Job = job;
        X = x;
        Y = y;
    }

    public IndustryScheduleJobViewModel Job { get; }
    public double X { get; }
    public double Y { get; }
    public double Width => 190;
    public double Height => 66;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class IndustryScheduleGraphEdgeViewModel
{
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; }
    public double Y2 { get; set; }
    public bool IsCritical { get; set; }
}

/// <summary>排产结果的展示模型：排程甘特 / 步骤物料 / 工位 / 关系图共用选中与候选角色。</summary>
public sealed class IndustryScheduleViewModel : INotifyPropertyChanged
{
    public const double PixelsPerHour = 80;

    private readonly Dictionary<string, IndustryScheduleJobViewModel> _jobMap;
    private IndustryScheduleJobViewModel? _selectedJob;
    private IndustryScheduleStepViewModel? _selectedStep;
    private bool _syncingSelection;

    public IndustryScheduleViewModel(IndustrySchedule schedule)
    {
        Schedule = schedule;
        TotalHours = Math.Max(12, Math.Ceiling(schedule.MakespanHours + 1));
        TimelineStart = DateTime.Now;
        var timeScale = IndustryScheduleTimeScale.Create(schedule.Jobs, PixelsPerHour, TotalHours);
        TimelineWidth = timeScale.TimelineWidth;
        HasTimelineCompression = timeScale.HasCompression;
        TimelineHint = HasTimelineCompression
            ? "时间轴按长任务 / 空闲段压缩；悬浮任务条查看真实起止"
            : string.Empty;
        var ticks = timeScale.CreateTicks();
        if (TotalHours > 24)
        {
            foreach (var tick in ticks)
            {
                tick.Label = TimelineStart.AddHours(tick.Hours).ToString("MM-dd HH:mm");
            }
        }
        TimelineTicks = new ObservableCollection<IndustryScheduleTickViewModel>(ticks);

        var jobs = schedule.Jobs
            .Select(p => new IndustryScheduleJobViewModel(p, timeScale))
            .OrderBy(p => p.Model.StartHours)
            .ThenBy(p => p.TypeName)
            .ToList();
        var planJobs = jobs.Where(p => !p.Model.IsInProgress).ToList();
        _jobMap = jobs.GroupBy(p => p.Key).ToDictionary(g => g.Key, g => g.First());

        Characters = new ObservableCollection<IndustryScheduleCharacterViewModel>();
        foreach (var character in schedule.Characters)
        {
            var lineViewModels = new List<IndustryScheduleLineViewModel>();
            foreach (var line in character.Lines)
            {
                var lineJobs = new List<IndustryScheduleJobViewModel>();
                foreach (var key in line.JobKeys)
                {
                    if (_jobMap.TryGetValue(key, out var lineJob))
                    {
                        lineJobs.Add(lineJob);
                    }
                }
                lineViewModels.Add(new IndustryScheduleLineViewModel(line, lineJobs));
            }
            Characters.Add(new IndustryScheduleCharacterViewModel(character, lineViewModels));
        }
        foreach (var character in Characters.Where(p => p.HasActiveLines))
        {
            ActiveCharacters.Add(character);
        }

        Steps = new ObservableCollection<IndustryScheduleStepViewModel>(
            planJobs.OrderBy(p => p.StageOrder)
                .ThenBy(p => p.Model.StartHours)
                .ThenBy(p => p.TypeName)
                .Select(p => new IndustryScheduleStepViewModel(p)));
        StepsView = CollectionViewSource.GetDefaultView(Steps);
        StepsView.GroupDescriptions.Clear();
        StepsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(IndustryScheduleStepViewModel.Stage)));

        BuildGraph(planJobs);

        MakespanText = FormatHours(schedule.MakespanHours);
        TotalJobCostText = schedule.TotalJobCost > 0 ? schedule.TotalJobCost.ToString("N0") + " ISK" : "—";
        var manufacturingSlots = schedule.Characters.Sum(p => p.ManufacturingSlots);
        var usedManufacturing = schedule.Characters.SelectMany(p => p.Lines)
            .Count(p => p.Kind == "制造" && p.JobKeys.Count > 0);
        ManufacturingUsageText = $"{usedManufacturing}/{manufacturingSlots} · 峰值 {PeakConcurrency(schedule, 1)}";
        var inventionUsed = schedule.Characters.SelectMany(p => p.Lines).Count(p => p.Kind == "发明" && p.JobKeys.Count > 0);
        var inventionSlots = schedule.Characters.Sum(p => p.InventionSlots);
        var reactionUsed = schedule.Characters.SelectMany(p => p.Lines).Count(p => p.Kind == "反应" && p.JobKeys.Count > 0);
        var reactionSlots = schedule.Characters.Sum(p => p.ReactionSlots);
        SpecialUsageText = $"发明 {inventionUsed}/{inventionSlots} · 反应 {reactionUsed}/{reactionSlots}";
        ObjectiveText = schedule.Objective switch
        {
            IndustryScheduleObjective.Cost => "最低成本",
            IndustryScheduleObjective.Balanced => "平衡",
            _ => "最短完成",
        };
        var bottleneck = schedule.Jobs
            .Where(p => p.IsCritical)
            .OrderByDescending(p => p.DurationHours)
            .FirstOrDefault();
        BottleneckText = bottleneck is null ? "—" : $"{bottleneck.TypeName} · {FormatHours(bottleneck.DurationHours)}";
        CharacterSummaryText = string.Join(" · ", schedule.Characters.Select(p =>
            $"{p.CharacterName} M{p.ManufacturingSlots}/I{p.InventionSlots}/R{p.ReactionSlots}"));
        CriticalCountText = $"{schedule.CriticalJobKeys.Count} 个关键任务";

        if (jobs.Count > 0)
        {
            SelectJob(planJobs.FirstOrDefault(p => p.StageOrder >= 3)
                ?? planJobs.FirstOrDefault()
                ?? jobs[0]);
        }
    }

    public IndustrySchedule Schedule { get; }
    public ObservableCollection<IndustryScheduleCharacterViewModel> Characters { get; }
    public ObservableCollection<IndustryScheduleCharacterViewModel> ActiveCharacters { get; } = new();
    public ObservableCollection<IndustryScheduleStepViewModel> Steps { get; }
    public ICollectionView StepsView { get; }
    public ObservableCollection<IndustryScheduleGraphNodeViewModel> GraphNodes { get; } = new();
    public ObservableCollection<IndustryScheduleGraphEdgeViewModel> GraphEdges { get; } = new();
    public ObservableCollection<IndustryScheduleTickViewModel> TimelineTicks { get; }
    public ObservableCollection<IndustryScheduleCandidateViewModel> Candidates { get; } = new();

    public double PixelsPerHourValue => PixelsPerHour;
    public double TotalHours { get; }
    public double TimelineWidth { get; }
    public DateTime TimelineStart { get; }
    public string TimelineStartText => $"开始 {TimelineStart:yyyy-MM-dd HH:mm}";
    public bool HasTimelineCompression { get; }
    public string TimelineHint { get; }

    private double _ganttZoom = 1.0;
    public double GanttZoom
    {
        get => _ganttZoom;
        set
        {
            var zoom = Math.Clamp(value, 0.4, 3.0);
            if (Math.Abs(_ganttZoom - zoom) < 0.0001) return;
            _ganttZoom = zoom;
            OnPropertyChanged();
            OnPropertyChanged(nameof(GanttZoomText));
        }
    }

    public string GanttZoomText => $"{GanttZoom * 100:0}%";
    public bool HasSchedule => Schedule.Jobs.Count > 0;

    public string MakespanText { get; }
    public string TotalJobCostText { get; }
    public string ManufacturingUsageText { get; }
    public string SpecialUsageText { get; }
    public string BottleneckText { get; }
    public string ObjectiveText { get; }
    public string CharacterSummaryText { get; }
    public string CriticalCountText { get; }
    public double GraphWidth { get; private set; }
    public double GraphHeight { get; private set; }

    public IndustryScheduleJobViewModel? SelectedJob
    {
        get => _selectedJob;
        set => SelectJob(value);
    }

    public IndustryScheduleStepViewModel? SelectedStep
    {
        get => _selectedStep;
        set
        {
            if (_syncingSelection) return;
            if (value is not null)
            {
                SelectJob(value.Job);
            }
        }
    }

    public void SelectJob(IndustryScheduleJobViewModel? job)
    {
        if (job is not null && _selectedJob?.Key == job.Key)
        {
            return;
        }

        _syncingSelection = true;
        try
        {
            foreach (var item in _jobMap.Values)
            {
                item.IsSelected = false;
            }
            foreach (var step in Steps)
            {
                step.IsSelected = false;
            }
            foreach (var node in GraphNodes)
            {
                node.IsSelected = false;
            }

            _selectedJob = job;
            if (job is not null)
            {
                job.IsSelected = true;
                _selectedStep = Steps.FirstOrDefault(p => p.Job.Key == job.Key);
                if (_selectedStep is not null)
                {
                    _selectedStep.IsSelected = true;
                }
                var node = GraphNodes.FirstOrDefault(p => p.Job.Key == job.Key);
                if (node is not null)
                {
                    node.IsSelected = true;
                }
            }
            else
            {
                _selectedStep = null;
            }
        }
        finally
        {
            _syncingSelection = false;
        }

        BuildCandidates();
        OnPropertyChanged(nameof(SelectedJob));
        OnPropertyChanged(nameof(SelectedStep));
    }

    public void SelectJobByKey(string? key)
    {
        if (!string.IsNullOrEmpty(key) && _jobMap.TryGetValue(key, out var job))
        {
            SelectJob(job);
        }
    }

    private void BuildCandidates()
    {
        Candidates.Clear();
        var job = _selectedJob?.Model;
        if (job is null) return;

        foreach (var character in Schedule.Characters)
        {
            var slots = job.ActivityId switch
            {
                8 => character.InventionSlots,
                11 => character.ReactionSlots,
                _ => character.ManufacturingSlots,
            };
            if (slots <= 0) continue;

            var multiplier = job.ActivityId switch
            {
                8 => character.InventionTimeMultiplier,
                11 => character.ReactionTimeMultiplier,
                _ => character.ManufacturingTimeMultiplier,
            };
            var duration = Math.Max(0.01, _selectedJob!.Model.BaseHours * multiplier);
            var isCurrent = character.CharacterId == job.CharacterId;
            Candidates.Add(new IndustryScheduleCandidateViewModel
            {
                CharacterId = character.CharacterId,
                Name = character.CharacterName,
                DurationText = FormatHours(duration),
                CapacityText = $"{SlotsName(job.ActivityId)} {slots} 槽",
                TimeText = $"×{multiplier:0.00}",
                IsCurrent = isCurrent,
                IsPinned = isCurrent && job.IsPinned,
            });
        }
    }

    private void BuildGraph(List<IndustryScheduleJobViewModel> jobs)
    {
        var byKey = jobs.ToDictionary(p => p.Key);
        var dependents = jobs.ToDictionary(p => p.Key, _ => new List<IndustryScheduleJobViewModel>());
        foreach (var job in jobs)
        {
            foreach (var dependencyKey in job.Model.Dependencies)
            {
                if (byKey.TryGetValue(dependencyKey, out var dependency))
                {
                    dependents[dependency.Key].Add(job);
                }
            }
        }

        var roots = jobs
            .Where(p => dependents[p.Key].Count == 0)
            .OrderByDescending(p => p.Model.IsTarget)
            .ThenBy(p => p.Model.StartHours)
            .ThenBy(p => p.TypeName)
            .ToList();
        if (roots.Count == 0)
        {
            roots = jobs.OrderBy(p => p.Model.StartHours).ThenBy(p => p.TypeName).ToList();
        }

        var placements = new Dictionary<string, NodePlacement>();
        var cursor = 20.0;
        var maxLevel = 0;

        double Layout(IndustryScheduleJobViewModel job, int level)
        {
            if (placements.TryGetValue(job.Key, out var existing))
            {
                return existing.SubtreeHeight;
            }

            maxLevel = Math.Max(maxLevel, level);
            var startY = cursor;
            var childrenHeight = 0.0;
            var childCount = 0;
            var children = job.Model.Dependencies
                .Select(key => byKey.TryGetValue(key, out var child) ? child : null)
                .Where(child => child is not null && child.Key != job.Key)
                .Select(child => child!)
                .DistinctBy(child => child.Key)
                .OrderBy(child => child.Model.StartHours)
                .ThenBy(child => child.TypeName)
                .ToList();

            foreach (var child in children)
            {
                if (placements.ContainsKey(child.Key))
                {
                    continue;
                }

                var childHeight = Layout(child, level + 1);
                childrenHeight += childHeight + (childCount > 0 ? 16 : 0);
                childCount++;
            }

            var subtreeHeight = Math.Max(66, childrenHeight);
            var nodeY = startY + Math.Max(0, (childrenHeight - 66) / 2);
            placements[job.Key] = new NodePlacement(job, level, nodeY, subtreeHeight);
            cursor = startY + subtreeHeight + 16;
            return subtreeHeight;
        }

        foreach (var root in roots)
        {
            if (!placements.ContainsKey(root.Key))
            {
                Layout(root, 0);
            }
        }

        while (placements.Count < jobs.Count)
        {
            var extra = jobs.FirstOrDefault(p => !placements.ContainsKey(p.Key));
            if (extra is null)
            {
                break;
            }
            Layout(extra, 0);
        }

        GraphWidth = 20 + (maxLevel + 1) * 230;
        var graphHeight = 40.0;
        foreach (var placement in placements.Values)
        {
            var x = 20 + (maxLevel - placement.Level) * 230;
            var node = new IndustryScheduleGraphNodeViewModel(placement.Job, x, placement.Y);
            GraphNodes.Add(node);
            graphHeight = Math.Max(graphHeight, placement.Y + node.Height + 20);
        }
        GraphHeight = graphHeight;

        foreach (var job in jobs)
        {
            var targetGraph = GraphNodes.FirstOrDefault(p => p.Job.Key == job.Key);
            if (targetGraph is null)
            {
                continue;
            }

            foreach (var dependencyKey in job.Model.Dependencies)
            {
                if (!byKey.TryGetValue(dependencyKey, out _))
                {
                    continue;
                }

                var sourceGraph = GraphNodes.FirstOrDefault(p => p.Job.Key == dependencyKey);
                if (sourceGraph is null)
                {
                    continue;
                }

                GraphEdges.Add(new IndustryScheduleGraphEdgeViewModel
                {
                    X1 = sourceGraph.X + sourceGraph.Width,
                    Y1 = sourceGraph.Y + sourceGraph.Height / 2,
                    X2 = targetGraph.X,
                    Y2 = targetGraph.Y + targetGraph.Height / 2,
                    IsCritical = sourceGraph.Job.Model.IsCritical && targetGraph.Job.Model.IsCritical,
                });
            }
        }
    }

    private sealed class NodePlacement
    {
        public NodePlacement(
            IndustryScheduleJobViewModel job,
            int level,
            double y,
            double subtreeHeight)
        {
            Job = job;
            Level = level;
            Y = y;
            SubtreeHeight = subtreeHeight;
        }

        public IndustryScheduleJobViewModel Job { get; }
        public int Level { get; }
        public double Y { get; }
        public double SubtreeHeight { get; }
    }

    private static int PeakConcurrency(IndustrySchedule schedule, int activityId)
    {
        var events = new List<(double Time, int Delta)>();
        foreach (var job in schedule.Jobs.Where(p => p.ActivityId == activityId))
        {
            events.Add((job.StartHours, 1));
            events.Add((job.EndHours, -1));
        }
        events.Sort((a, b) => a.Time == b.Time ? a.Delta.CompareTo(b.Delta) : a.Time.CompareTo(b.Time));
        var current = 0;
        var peak = 0;
        foreach (var item in events)
        {
            current += item.Delta;
            peak = Math.Max(peak, current);
        }
        return peak;
    }

    private static string SlotsName(int activityId)
    {
        return activityId switch
        {
            8 => "发明",
            11 => "反应",
            _ => "制造",
        };
    }

    private static string FormatHours(double hours)
    {
        if (hours <= 0) return "0h";
        var span = TimeSpan.FromHours(hours);
        if (span.TotalHours >= 1) return $"{(int)span.TotalHours}h {span.Minutes:00}m";
        return $"{span.Minutes}m";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
