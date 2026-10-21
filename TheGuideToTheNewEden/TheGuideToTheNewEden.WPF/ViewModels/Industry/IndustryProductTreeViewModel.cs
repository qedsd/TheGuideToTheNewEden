using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TheGuideToTheNewEden.WPF.ViewModels.Industry;

/// <summary>常驻物品树的分类节点（SDE categories）。</summary>
public sealed class IndustryProductTreeCategory : INotifyPropertyChanged
{
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ObservableCollection<IndustryProductTreeGroup> Groups { get; } = new();

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set => Set(ref _isExpanded, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}

/// <summary>常驻物品树的物品组节点（SDE groups）。</summary>
public sealed class IndustryProductTreeGroup : INotifyPropertyChanged
{
    public int GroupId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ObservableCollection<IndustryProductTreeItem> Items { get; } = new();

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set => Set(ref _isExpanded, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}

/// <summary>常驻物品树的产物节点。</summary>
public sealed class IndustryProductTreeItem : INotifyPropertyChanged
{
    public int TypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string BlueprintName { get; set; } = string.Empty;
    public int ActivityId { get; set; }
    public string ActivityText => ActivityId == 11 ? "反应" : "制造";
    public bool IsExpanded { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
}
