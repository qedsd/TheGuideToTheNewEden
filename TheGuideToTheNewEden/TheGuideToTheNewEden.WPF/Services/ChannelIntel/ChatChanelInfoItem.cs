using System.ComponentModel;
using System.Runtime.CompilerServices;
using TheGuideToTheNewEden.Core.Models;

namespace TheGuideToTheNewEden.WPF.Services.ChannelIntel;

/// <summary>
/// 频道列表项：Core 的 <see cref="ChatChanelInfo"/> + 界面勾选状态（对齐 WinUI 版 Models/ChatChanelInfo）。
/// </summary>
public sealed class ChatChanelInfoItem : INotifyPropertyChanged
{
    public Core.Models.ChatChanelInfo Info { get; }

    public ChatChanelInfoItem(Core.Models.ChatChanelInfo info)
    {
        Info = info;
    }

    public string ChannelID => Info.ChannelID;

    public string ChannelName => Info.ChannelName;

    public DateTime SessionStarted => Info.SessionStarted;

    private bool _isChecked;

    /// <summary>是否勾选为预警频道（TwoWay，随设置持久化）。</summary>
    public bool IsChecked
    {
        get => _isChecked;
        set => Set(ref _isChecked, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
