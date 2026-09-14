using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TheGuideToTheNewEden.WPF.Models.KB;

namespace TheGuideToTheNewEden.WPF.Views.UserControls.KB;

/// <summary>
/// 排名卡片（最贵击杀 / 最高击杀 / 超期击杀共用）：排名 + 头像/图标 + 名称 + 副标题 + 击杀数/估价。
/// 点击通过 <see cref="Clicked"/> 交给宿主，由宿主决定打开 KB 详情还是实体统计。
/// </summary>
public partial class KillRankCard : UserControl
{
    public KillRankCard()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty ItemProperty = DependencyProperty.Register(
        nameof(Item), typeof(KillCardItem), typeof(KillRankCard), new PropertyMetadata(null, OnItemChanged));

    public KillCardItem? Item
    {
        get => (KillCardItem?)GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }

    /// <summary>点击卡片。</summary>
    public event Action<KillCardItem>? Clicked;

    private static void OnItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not KillRankCard card || e.NewValue is not KillCardItem item)
        {
            return;
        }

        card.KillsRow.Visibility = item.Kills > 0 ? Visibility.Visible : Visibility.Collapsed;
        card.ValueRow.Visibility = string.IsNullOrEmpty(item.ValueText) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnClicked(object sender, MouseButtonEventArgs e)
    {
        if (Item is not null)
        {
            Clicked?.Invoke(Item);
        }
    }
}
