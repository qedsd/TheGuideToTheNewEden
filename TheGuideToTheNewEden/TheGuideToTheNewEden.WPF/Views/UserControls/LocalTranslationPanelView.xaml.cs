using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TheGuideToTheNewEden.WPF.ViewModels.Translation;

namespace TheGuideToTheNewEden.WPF.Views.UserControls;

/// <summary>
/// 「本地词库」页签：左侧输入与匹配列表、右侧译文详情（离线 SDE 中英词库，输入即查、不计费）。
/// <para>
/// DataContext 由宿主 <see cref="TranslationPanelView"/> 赋值为 <see cref="LocalTranslationViewModel"/>，
/// 本控件<b>不自己造 VM</b>——这样页面与"弹窗"两个实例各自持有独立状态，互不干扰。
/// </para>
/// </summary>
public partial class LocalTranslationPanelView : UserControl
{
    public LocalTranslationPanelView()
    {
        InitializeComponent();
        Loaded += (_, _) => ViewModel?.Init();
        Unloaded += (_, _) => ViewModel?.Dispose();
    }

    private LocalTranslationViewModel? ViewModel => DataContext as LocalTranslationViewModel;

    private void OnTranslateClick(object sender, RoutedEventArgs e) => _ = ViewModel?.SearchAsync();

    private void OnClearClick(object sender, RoutedEventArgs e) => ViewModel?.Clear();

    private void OnCopyClick(object sender, RoutedEventArgs e) => ViewModel?.CopyTranslation();

    private void OnCopyQueryClick(object sender, RoutedEventArgs e) => ViewModel?.CopyQuery();

    private void OnCopyTranslationClick(object sender, RoutedEventArgs e) => ViewModel?.CopyTranslation();

    /// <summary>右键点在匹配列表的某一行时先选中它，保证右键菜单作用于鼠标下的那条。</summary>
    private void OnMatchesPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
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

    /// <summary>
    /// 回车立即查询；<b>Shift+回车</b>保留为换行（输入框是多行的，便于粘贴整段文本）。
    /// 用 PreviewKeyDown：多行 TextBox 会在 KeyDown 的类处理器里把回车当换行吞掉。
    /// </summary>
    private void OnInputPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || (Keyboard.Modifiers & ModifierKeys.Shift) != 0)
        {
            return;
        }

        e.Handled = true;
        _ = ViewModel?.SearchAsync();
    }

    private void OnPopWindowClick(object sender, RoutedEventArgs e) => TranslationPopup.ShowLocal(this);
}
