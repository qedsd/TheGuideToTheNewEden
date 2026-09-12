using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Ui.Controls;

namespace TheGuideToTheNewEden.WPF.Controls;

/// <summary>右下角通知条目（供 <see cref="MessageHost"/> 的列表绑定）。</summary>
public sealed class MessageItem
{
    public required string Text { get; init; }

    public required SymbolRegular Symbol { get; init; }

    /// <summary>图标前景色（按资源键在创建时解析一次；通知是短生命周期的，不跟随主题切换）。</summary>
    public Brush? Foreground { get; init; }
}

/// <summary>
/// 右下角通知栈（对应 WinUI 版的 InfoBar 即时消息）：淡入滑入、超时自动消失、可手动关闭。
/// 由 <see cref="Services.PageNotifyService"/> 统一驱动，放在 MainWindow 里全局复用。
/// </summary>
public partial class MessageHost : UserControl
{
    private const int MaxVisible = 5;

    private readonly ObservableCollection<MessageItem> _items = [];

    public MessageHost()
    {
        InitializeComponent();
        ItemsHost.ItemsSource = _items;
    }

    /// <summary>推入一条通知，<paramref name="milliseconds"/> 后自动移除。</summary>
    public void Show(string text, SymbolRegular symbol, string brushKey, int milliseconds)
    {
        var item = new MessageItem
        {
            Text = text,
            Symbol = symbol,
            Foreground = Application.Current?.TryFindResource(brushKey) as Brush,
        };

        _items.Insert(0, item);
        while (_items.Count > MaxVisible)
        {
            _items.RemoveAt(_items.Count - 1);
        }

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            _items.Remove(item);
        };
        timer.Start();
    }

    public void Clear() => _items.Clear();

    private void OnDismissClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: MessageItem item })
        {
            _items.Remove(item);
        }
    }
}
