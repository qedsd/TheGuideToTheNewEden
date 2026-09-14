using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.Core.DBModels;

namespace TheGuideToTheNewEden.WPF.Views.UserControls.KB;

/// <summary>
/// 一组"排除项 / 包含项"过滤器编辑器（左边排除、右边包含）：搜索添加 + 列表删除。
/// 直接读写绑定的 <see cref="ObservableCollection{T}"/>（即 <c>ZKBStreamConfig</c> 里的集合），
/// 改动会即时反映到 <c>ZkbKillStreamHub</c> 的过滤快照上，无需断开重连。
/// </summary>
public partial class KbFilterPairControl : UserControl
{
    public KbFilterPairControl()
    {
        InitializeComponent();
    }

    /// <summary>排除项集合（命中即丢弃）。</summary>
    public static readonly DependencyProperty ExclusionsProperty = DependencyProperty.Register(
        nameof(Exclusions), typeof(ObservableCollection<IdName>), typeof(KbFilterPairControl), new PropertyMetadata(null));

    public ObservableCollection<IdName>? Exclusions
    {
        get => (ObservableCollection<IdName>?)GetValue(ExclusionsProperty);
        set => SetValue(ExclusionsProperty, value);
    }

    /// <summary>包含项集合（非空时必须命中之一）。</summary>
    public static readonly DependencyProperty InclusionsProperty = DependencyProperty.Register(
        nameof(Inclusions), typeof(ObservableCollection<IdName>), typeof(KbFilterPairControl), new PropertyMetadata(null));

    public ObservableCollection<IdName>? Inclusions
    {
        get => (ObservableCollection<IdName>?)GetValue(InclusionsProperty);
        set => SetValue(InclusionsProperty, value);
    }

    /// <summary>限定可选的实体类别。</summary>
    public static readonly DependencyProperty CategoriesProperty = DependencyProperty.Register(
        nameof(Categories), typeof(IdName.CategoryEnum[]), typeof(KbFilterPairControl), new PropertyMetadata(null));

    public IdName.CategoryEnum[]? Categories
    {
        get => (IdName.CategoryEnum[]?)GetValue(CategoriesProperty);
        set => SetValue(CategoriesProperty, value);
    }

    /// <summary>说明文字（为空则不显示）。</summary>
    public static readonly DependencyProperty TipProperty = DependencyProperty.Register(
        nameof(Tip), typeof(string), typeof(KbFilterPairControl), new PropertyMetadata(null));

    public string? Tip
    {
        get => (string?)GetValue(TipProperty);
        set => SetValue(TipProperty, value);
    }

    private void OnExclusionSelected(IdName item) => Add(Exclusions, item);

    private void OnInclusionSelected(IdName item) => Add(Inclusions, item);

    private static void Add(ObservableCollection<IdName>? target, IdName item)
    {
        if (target is null || item.Id <= 0)
        {
            return;
        }

        if (target.Any(p => p.Id == item.Id && p.GetCategory() == item.GetCategory()))
        {
            return;
        }

        target.Add(item);
    }

    private void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: IdName item } element)
        {
            return;
        }

        // 通过祖先找到这一行属于哪个列表
        if (FindOwner(element) is { } list)
        {
            list.Remove(item);
        }
    }

    private ObservableCollection<IdName>? FindOwner(DependencyObject element)
    {
        var current = element;
        while (current is not null)
        {
            if (current is ListBox { ItemsSource: ObservableCollection<IdName> list })
            {
                return list;
            }

            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
