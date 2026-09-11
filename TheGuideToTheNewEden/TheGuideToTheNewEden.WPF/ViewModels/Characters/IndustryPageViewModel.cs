using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TheGuideToTheNewEden.WPF.Services.Characters;

namespace TheGuideToTheNewEden.WPF.ViewModels.Characters;

/// <summary>
/// 工业页视图模型：把 <see cref="CharacterIndustryService"/> 返回的任务投影成表格行。
/// 只负责取数与展示投影（本地化时间/周期文本），不直接触碰控件。
/// </summary>
public sealed class IndustryPageViewModel : INotifyPropertyChanged
{
    private readonly CharacterContext _context;
    private ObservableCollection<IndustryJobItemView> _jobs = new();
    private bool _loaded;

    public IndustryPageViewModel(CharacterContext context)
    {
        _context = context;
    }

    /// <summary>工业任务行集合。</summary>
    public ObservableCollection<IndustryJobItemView> Jobs
    {
        get => _jobs;
        private set => Set(ref _jobs, value);
    }

    /// <summary>是否已经取过数据（懒加载用）。</summary>
    public bool Loaded
    {
        get => _loaded;
        private set => Set(ref _loaded, value);
    }

    /// <summary>加载工业任务（forceRefresh=true 时绕过 5 分钟缓存）。</summary>
    public async Task LoadAsync(bool forceRefresh)
    {
        var jobs = await CharacterIndustryService.GetAsync(_context, forceRefresh);
        Jobs = jobs is null ? new() : new(jobs.Select(ToItem));
        Loaded = true;
    }

    // ---------- 展示投影（时间本地化，周期格式化） ----------

    private static IndustryJobItemView ToItem(IndustryJobView job) => new()
    {
        BlueprintName = job.BlueprintName,
        ProductName = job.ProductName,
        StatusText = job.StatusText,
        Runs = job.Runs,
        Probability = job.Probability,
        Cost = job.Cost,
        StartDate = Localize(job.StartDate),
        EndDate = Localize(job.EndDate),
        Duration = TimeSpan.FromSeconds(job.DurationSeconds),
        LocationName = job.LocationName,
    };

    /// <summary>ESI 时间统一转成本地时间后再显示（与其它子页一致）。</summary>
    private static DateTime? Localize(DateTime? value)
    {
        if (value is null)
        {
            return null;
        }

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value.ToLocalTime(),
            DateTimeKind.Local => value.Value,
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc).ToLocalTime(),
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

/// <summary>工业任务表格行（列与 WinUI3 工业页一一对应）。</summary>
public sealed class IndustryJobItemView
{
    public string BlueprintName { get; init; } = string.Empty;

    public string ProductName { get; init; } = string.Empty;

    public string StatusText { get; init; } = string.Empty;

    public int Runs { get; init; }

    public double Probability { get; init; }

    public DateTime? StartDate { get; init; }

    /// <summary>项目周期，对应 WinUI 版的 Span 列。</summary>
    public TimeSpan Duration { get; init; }

    public DateTime? EndDate { get; init; }

    public double Cost { get; init; }

    public string LocationName { get; init; } = string.Empty;

    /// <summary>周期文本：与 WinUI 直接绑定 TimeSpan 时的默认格式一致。</summary>
    public string DurationText => Duration.ToString();
}
