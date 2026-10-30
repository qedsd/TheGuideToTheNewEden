using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TheGuideToTheNewEden.WPF.Models.KB;

/// <summary>舰船分类树节点：叶子是具体舰船 TypeID，父节点勾选会级联到全部子节点。</summary>
public sealed class ShipFilterNode : INotifyPropertyChanged
{
    private bool? _isChecked = false;
    private bool _updating;

    public string Name { get; init; } = string.Empty;
    public int TypeId { get; init; }
    public bool IsLeaf { get; init; }
    public ObservableCollection<ShipFilterNode> Children { get; } = [];
    public ShipFilterNode? Parent { get; init; }

    public event Action<ShipFilterNode, bool?>? CheckedChanged;

    public bool? IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value)
            {
                return;
            }

            _isChecked = value;
            OnPropertyChanged();

            if (value != null && Children.Count > 0)
            {
                _updating = true;
                foreach (var child in Children)
                {
                    child.IsChecked = value;
                }
                _updating = false;
            }

            Parent?.RefreshFromChildren();

            if (IsLeaf)
            {
                CheckedChanged?.Invoke(this, value);
            }
        }
    }

    internal void RefreshFromChildren()
    {
        if (_updating || Children.Count == 0)
        {
            return;
        }

        bool? state;
        var selected = Children.Count(p => p.IsChecked == true);
        var partial = Children.Any(p => p.IsChecked == null);
        if (selected == 0 && !partial)
        {
            state = false;
        }
        else if (selected == Children.Count)
        {
            state = true;
        }
        else
        {
            state = null;
        }

        if (_isChecked != state)
        {
            _isChecked = state;
            OnPropertyChanged(nameof(IsChecked));
            Parent?.RefreshFromChildren();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
