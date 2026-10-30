using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.Core.DBModels;
using TheGuideToTheNewEden.Core.Models.KB;

namespace TheGuideToTheNewEden.WPF.Views.UserControls.KB;

/// <summary>
/// 单个过滤维度：模式（不过滤 / 只保留 / 排除）+ 一个实体列表。
/// 列表内实体是 OR；维度之间由 <see cref="ZkbStreamFilter"/> 做 AND。
/// </summary>
public partial class KbFilterPairControl : UserControl
{
    private bool _syncingMode;

    public KbFilterPairControl()
    {
        InitializeComponent();
        ModeCombo.SelectionChanged += OnModeSelectionChanged;
        UpdateModeCombo();
    }

    /// <summary>过滤模式（双向绑定到配置）。</summary>
    public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(
        nameof(Mode),
        typeof(ZkbFilterMode),
        typeof(KbFilterPairControl),
        new FrameworkPropertyMetadata(
            ZkbFilterMode.None,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnModeChanged));

    public ZkbFilterMode Mode
    {
        get => (ZkbFilterMode)GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    /// <summary>当前维度的过滤实体列表。</summary>
    public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(
        nameof(Items),
        typeof(ObservableCollection<IdName>),
        typeof(KbFilterPairControl),
        new PropertyMetadata(null));

    public ObservableCollection<IdName>? Items
    {
        get => (ObservableCollection<IdName>?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    /// <summary>限定可选的实体类别。</summary>
    public static readonly DependencyProperty CategoriesProperty = DependencyProperty.Register(
        nameof(Categories), typeof(IdName.CategoryEnum[]), typeof(KbFilterPairControl), new PropertyMetadata(null));

    public IdName.CategoryEnum[]? Categories
    {
        get => (IdName.CategoryEnum[]?)GetValue(CategoriesProperty);
        set => SetValue(CategoriesProperty, value);
    }

    /// <summary>搜索框占位提示。</summary>
    public static readonly DependencyProperty PlaceholderTextProperty = DependencyProperty.Register(
        nameof(PlaceholderText), typeof(string), typeof(KbFilterPairControl), new PropertyMetadata(null));

    public string? PlaceholderText
    {
        get => (string?)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    private static void OnModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((KbFilterPairControl)d).UpdateModeCombo();

    private void UpdateModeCombo()
    {
        if (ModeCombo is null)
        {
            return;
        }

        _syncingMode = true;
        ModeCombo.SelectedIndex = (int)Mode;
        _syncingMode = false;
    }

    private void OnModeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingMode || ModeCombo.SelectedIndex < 0)
        {
            return;
        }

        Mode = (ZkbFilterMode)ModeCombo.SelectedIndex;
    }

    private void OnItemSelected(IdName item) => Add(Items, item);

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
        if (sender is FrameworkElement { DataContext: IdName item } && Items is not null)
        {
            Items.Remove(item);
        }
    }
}
