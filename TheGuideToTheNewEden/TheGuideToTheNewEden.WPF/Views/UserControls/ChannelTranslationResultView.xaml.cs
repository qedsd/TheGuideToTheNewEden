using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.ViewModels.Channel;

namespace TheGuideToTheNewEden.WPF.Views.UserControls;

/// <summary>
/// 频道翻译结果视图：实时译文列表（新→旧），原文与译文都可拖选复制（富文本），
/// 译文超过五行折叠并带「展开 / 收起」，每条的复制图标在 meta 行最右侧；
/// 右键还能单独复制原文/译文。
/// 页面与"弹窗"（<c>ToolWindow</c> 承载）共用这一份标记，DataContext 由宿主传入同一个
/// <see cref="ChannelTranslationViewModel"/>，因此两边看到的内容完全一致。
/// </summary>
public partial class ChannelTranslationResultView : UserControl
{
    public ChannelTranslationResultView()
    {
        InitializeComponent();
    }

    /// <summary>右键点在某一行的消息上时先选中它，保证右键菜单作用于鼠标下的那条。</summary>
    private void OnResultPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox listBox || e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        if (ItemsControl.ContainerFromElement(listBox, source) is ListBoxItem item)
        {
            item.IsSelected = true;
        }
    }

    private void OnCopyOriginalClick(object sender, RoutedEventArgs e) => Copy(useTranslation: false);

    private void OnCopyTranslationClick(object sender, RoutedEventArgs e) => Copy(useTranslation: true);

    /// <summary>每条右下角的复制译文图标。</summary>
    private void OnCopyItemTranslationClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ChannelTranslationItemViewModel item })
        {
            CopyText(item.Translation);
        }
    }

    /// <summary>译文气泡的「展开 / 收起」。</summary>
    private void OnToggleTranslationClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ChannelTranslationItemViewModel item })
        {
            item.ToggleTranslationExpansion();
        }
    }

    private void Copy(bool useTranslation)
    {
        if (ResultList.SelectedItem is not ChannelTranslationItemViewModel item)
        {
            PageNotifyService.Warning(FindString("TranslationPage_NoSelection"));
            return;
        }

        // 复制时用清洗后的文本（粘贴出来的内容是纯文字，不带游戏内标记）
        CopyText(useTranslation && item.Success ? item.Translation : item.OriginalFull);
    }

    private static void CopyText(string text)
    {
        try
        {
            Clipboard.SetText(text);
            PageNotifyService.Success(FindString("TranslationPage_CopySuccess"));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
        }
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}
