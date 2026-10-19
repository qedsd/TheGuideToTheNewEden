using CommunityToolkit.Mvvm.ComponentModel;

namespace TheGuideToTheNewEden.WPF.ViewModels.GamePreview;

/// <summary>
/// "分组顺序"列表里的一行：分组内的一个角色名。
/// 与 <see cref="SwitchOrderEntry"/> 不同，分组顺序按**角色名**排序（不是窗口标题），
/// 因此窗口标题变化（切换角色）不会让排序失效。
/// </summary>
public sealed class GroupOrderEntry : ObservableObject
{
    private int _index;
    private bool _canMoveUp;
    private bool _canMoveDown;

    public GroupOrderEntry(string name, bool isRunning)
    {
        Name = name;
        IsRunning = isRunning;
    }

    public string Name { get; }

    /// <summary>构建列表时该角色是否正在预览（列表刷新时更新）。</summary>
    public bool IsRunning { get; }

    /// <summary>1-based 序号。</summary>
    public int Index
    {
        get => _index;
        internal set => SetProperty(ref _index, value);
    }

    public bool CanMoveUp
    {
        get => _canMoveUp;
        internal set => SetProperty(ref _canMoveUp, value);
    }

    public bool CanMoveDown
    {
        get => _canMoveDown;
        internal set => SetProperty(ref _canMoveDown, value);
    }

    internal void UpdatePosition(int zeroBasedIndex, int count)
    {
        Index = zeroBasedIndex + 1;
        CanMoveUp = zeroBasedIndex > 0;
        CanMoveDown = zeroBasedIndex < count - 1;
    }
}
