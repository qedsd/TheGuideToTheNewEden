using CommunityToolkit.Mvvm.ComponentModel;
using TheGuideToTheNewEden.Core.Models.GamePreviews;

namespace TheGuideToTheNewEden.WPF.ViewModels.GamePreview;

/// <summary>
/// "切换顺序"页签里的一行：一个窗口标题在切换顺序中的位置。
/// <para>
/// 顺序本身仍存在 <see cref="PreviewSetting.ProcessOrder"/>（窗口标题列表，与旧配置/WinUI 兼容）；
/// 这个类只是把标题和"当前是否真的发现该客户端"组合起来，供列表展示与上/下移按钮使用。
/// </para>
/// </summary>
public sealed class SwitchOrderEntry : ObservableObject
{
    private int _index;
    private bool _canMoveUp;
    private bool _canMoveDown;

    public SwitchOrderEntry(string windowTitle, ProcessInfo? process)
    {
        WindowTitle = windowTitle;
        Process = process;
        CharacterName = process?.GetCharacterName() ?? ExtractCharacterName(windowTitle);
    }

    /// <summary>标题（与 <see cref="PreviewSetting.ProcessOrder"/> 中的字符串一致）。</summary>
    public string WindowTitle { get; }

    /// <summary>当前发现的进程；窗口已关闭时为 null（列表里显示"未发现"）。</summary>
    public ProcessInfo? Process { get; }

    /// <summary>角色名（未识别时回退窗口标题）。</summary>
    public string CharacterName { get; }

    public bool IsPresent => Process is not null;

    public bool IsRunning => Process?.Running ?? false;

    /// <summary>1-based 序号（列表左侧显示）。</summary>
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

    private static string ExtractCharacterName(string windowTitle)
    {
        var index = windowTitle.IndexOf('-');
        return index > -1 && index < windowTitle.Length - 1
            ? windowTitle[(index + 1)..].Trim()
            : windowTitle;
    }
}
