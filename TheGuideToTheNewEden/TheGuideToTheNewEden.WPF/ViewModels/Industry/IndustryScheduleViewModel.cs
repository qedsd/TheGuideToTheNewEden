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

/// <summary>甘特时间轴：明显长于其他任务的任务中段折叠，避免一根条占满屏幕。</summary>
public sealed class IndustryScheduleTimeScale
{
    public IndustryScheduleTimeScale(
        double pixelsPerHour,
        double breakStart = 0,
        double breakEnd = 0,
        double compressedHours = 0)
    {
        PixelsPerHour = pixelsPerHour;
        BreakStart = Math.Max(0, breakStart);
        BreakEnd = Math.Max(BreakStart, breakEnd);
        CompressedHours = Math.Max(0, compressedHours);
    }

    public double PixelsPerHour { get; }
    public double BreakStart { get; }
    public double BreakEnd { get; }
    public double CompressedHours { get; }
    public bool HasBreak => BreakEnd - BreakStart >= 1 && CompressedHours > 0;
    public double BreakWidth => CompressedHours * PixelsPerHour;
    public string BreakText => HasBreak ? $"{Format(BreakStart)} … {Format(BreakEnd)}" : string.Empty;

    public static IndustryScheduleTimeScale Create(IEnumerable<IndustryScheduleJob> jobs, double pixelsPerHour)
    {
        var list = jobs?.ToList() ?? new List<IndustryScheduleJob>();
        var longest = list.OrderByDescending(p => p.DurationHours).FirstOrDefault();
        if (longest is null || list.Count < 2)
        {
            return new IndustryScheduleTimeScale(pixelsPerHour);
        }

        var maxOther = list.Where(p => !ReferenceEquals(p, longest))
            .Select(p => p.DurationHours)
            .DefaultIfEmpty(0)
            .Max();
        if (longest.DurationHours < 20 || longest.DurationHours < maxOther * 2.5)
        {
            return new IndustryScheduleTimeScale(pixelsPerHour);
        }

        var head = Math.Min(10, Math.Max(2, longest.DurationHours * 0.2));
        var breakStart = longest.StartHours + head;
        var breakEnd = longest.EndHours;
        if (breakEnd - breakStart < 4)
        {
            return new IndustryScheduleTimeScale(pixelsPerHour);
        }

        return new IndustryScheduleTimeScale(pixelsPerHour, breakStart, breakEnd, 2);
    }

    public double Map(double hours)
    {
        var value = Math.Max(0, hours);
        if (!HasBreak || value <= BreakStart)
        {
            return value * PixelsPerHour;
        }

        if (value >= BreakEnd)
        {
            return BreakStart * PixelsPerHour + BreakWidth + (value - BreakEnd) * PixelsPerHour;
        }

        var ratio = (value - BreakStart) / (BreakEnd - BreakStart);
        return BreakStart * PixelsPerHour + ratio * BreakWidth;
    }

    private static string Format(double hours) => hours >= 1 ? $"{hours:0.#}h" : $"{hours * 60:0}m";
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
        HasCompressedGap = timeScale.HasBreak
            && model.StartHours <= timeScale.BreakStart
            && model.EndHours >= timeScale.BreakEnd;
        CompressedGapText = HasCompressedGap ? timeScale.BreakText : string.Empty;
        StageOrder = model.Stage switch
        {
            "发明" => 0,
            "反应" => 1,
            "组件" => 2,
            _ => 3,
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
        + (HasCompressedGap ? $"\n时间轴压缩：{CompressedGapText}" : string.Empty);

    public List<IndustryScheduleMaterialViewModel> Inputs { get; }
    public List<IndustryScheduleMaterialViewModel> Outputs { get; }
    public string OutputText => string.Join("  ", Outputs.Select(p => $"{p.Name} {p.QuantityText} {p.StateText}"));
    public double Left { get; }
    public double Width { get; }
    public double DisplayLeft { get; set; }
    public bool HasCompressedGap { get; }
    public string CompressedGapText { get; }

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
        var timeScale = IndustryScheduleTimeScale.Create(schedule.Jobs, PixelsPerHour);
        TotalHours = Math.Max(12, Math.Ceiling(schedule.MakespanHours + 1));
        TimelineWidth = timeScale.Map(TotalHours);
        var ticks = new List<IndustryScheduleTickViewModel>();
        for (var hour = 0; hour <= (int)Math.Ceiling(TotalHours); hour++)
        {
            if (timeScale.HasBreak && hour > timeScale.BreakStart && hour < timeScale.BreakEnd)
            {
                continue;
            }
            ticks.Add(new IndustryScheduleTickViewModel
            {
                Left = timeScale.Map(hour),
                Label = hour + "h",
            });
        }
        if (timeScale.HasBreak)
        {
            ticks.Add(new IndustryScheduleTickViewModel
            {
                Left = timeScale.Map(timeScale.BreakStart) + timeScale.BreakWidth / 2,
                Label = "…",
            });
        }
        TimelineTicks = new ObservableCollection<IndustryScheduleTickViewModel>(
            ticks.OrderBy(p => p.Left));

        var jobs = schedule.Jobs
            .Select(p => new IndustryScheduleJobViewModel(p, timeScale))
            .OrderBy(p => p.Model.StartHours)
            .ThenBy(p => p.TypeName)
            .ToList();
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
            jobs.OrderBy(p => p.StageOrder)
                .ThenBy(p => p.Model.StartHours)
                .ThenBy(p => p.TypeName)
                .Select(p => new IndustryScheduleStepViewModel(p)));
        StepsView = CollectionViewSource.GetDefaultView(Steps);
        StepsView.GroupDescriptions.Clear();
        StepsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(IndustryScheduleStepViewModel.Stage)));

        BuildGraph(jobs);

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
            SelectJob(jobs.FirstOrDefault(p => p.StageOrder >= 3) ?? jobs[0]);
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
