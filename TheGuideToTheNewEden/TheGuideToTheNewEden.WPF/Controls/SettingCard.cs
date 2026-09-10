using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace TheGuideToTheNewEden.WPF.Controls;

/// <summary>
/// Win11 设置行：左侧图标 + 标题（可带描述），右侧为操作控件（Content）。
/// 外观由 App 资源中的隐式样式 SettingCardStyles.xaml 提供。
/// </summary>
public class SettingCard : ContentControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(SettingCard),
        new PropertyMetadata(string.Empty));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description),
        typeof(string),
        typeof(SettingCard),
        new PropertyMetadata(string.Empty));

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon),
        typeof(SymbolRegular),
        typeof(SettingCard),
        new PropertyMetadata(SymbolRegular.Empty));

    public SymbolRegular Icon
    {
        get => (SymbolRegular)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }
}