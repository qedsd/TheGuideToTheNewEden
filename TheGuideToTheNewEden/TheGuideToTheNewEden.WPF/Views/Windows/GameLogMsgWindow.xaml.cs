using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using TheGuideToTheNewEden.WPF.Helpers;

namespace TheGuideToTheNewEden.WPF.Views.Windows;

/// <summary>
/// 频道监控的消息弹窗（对齐 WinUI 版 <c>Wins/GameLogMsgWindow</c>，WinUI 类名拼写为 GaemLogMsgWindow）：
/// 置顶小窗（仅关闭按钮，关闭即隐藏），新增消息加粗、上一条恢复正常，自动滚到底部；
/// 底部"前置游戏"按钮把对应 EVE 客户端窗口切到前台。
/// </summary>
public partial class GameLogMsgWindow : Wpf.Ui.Controls.FluentWindow
{
    private Paragraph? _lastParagraph;

    /// <summary>窗口被隐藏（用户关闭/手动隐藏）：通知服务停声音、清系统通知。</summary>
    public event EventHandler? OnHided;

    /// <summary>点击"前置游戏"按钮。</summary>
    public event EventHandler? OnShowGameButtonClick;

    public string ListenerName { get; }

    private bool _allowClose;

    public GameLogMsgWindow(string listenerName, string? displayTitle = null)
    {
        ListenerName = listenerName;
        InitializeComponent();
        TitleBar.Title = displayTitle ?? $"{FindString("ChannelMonitorPage")} - {listenerName}";
    }

    /// <summary>关闭即隐藏（对齐 WinUI 的 SetCloseToHide）：窗口要复用，直接关掉会导致下次 Show() 抛异常。</summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            HideWindow();
        }

        base.OnClosing(e);
    }

    /// <summary>真正销毁窗口（通知服务 Remove 时调用）。</summary>
    public void CloseWindow()
    {
        _allowClose = true;
        Close();
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;

    /// <summary>显示窗口并追加一条消息。</summary>
    public void Show(string content)
    {
        AppendMessage(content);
        if (!IsVisible)
        {
            Show();
        }
    }

    public void AppendMessage(string content)
    {
        var paragraph = new Paragraph { Margin = new Thickness(0, 1, 0, 1) };
        paragraph.Inlines.Add(new Run(content)
        {
            Foreground = GetResourceBrush("TextFillColorPrimaryBrush", Brushes.Black),
            FontWeight = FontWeights.Bold,
        });
        if (_lastParagraph is not null)
        {
            foreach (var run in _lastParagraph.Inlines.OfType<Run>())
            {
                run.FontWeight = FontWeights.Normal;
            }
        }

        _lastParagraph = paragraph;
        MsgBox.Document.Blocks.Add(paragraph);
        MsgBox.ScrollToEnd();
    }

    public void Clear()
    {
        MsgBox.Document.Blocks.Clear();
        _lastParagraph = null;
    }

    /// <summary>隐藏窗口（不关闭，等下一条消息再显示），并通知停声音/清系统通知。</summary>
    public void HideWindow()
    {
        if (IsVisible)
        {
            Hide();
            OnHided?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        Clear();
    }

    private void OnShowGameClick(object sender, RoutedEventArgs e)
        => OnShowGameButtonClick?.Invoke(this, EventArgs.Empty);

    private static Brush GetResourceBrush(string key, Brush fallback)
        => Application.Current?.TryFindResource(key) as Brush ?? fallback;
}
