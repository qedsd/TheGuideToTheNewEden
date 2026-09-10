using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace TheGuideToTheNewEden.WPF.Views.Pages;

/// <summary>
/// 功能页面占位基类：一个居中的图标 + 标题 + 描述。
/// 后续各功能页在此替换为真实实现。
/// </summary>
public abstract class PlaceholderPage : System.Windows.Controls.Page
{
    protected PlaceholderPage(string titleKey, SymbolRegular symbol)
    {
        var grid = new Grid();

        var stack = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var icon = new SymbolIcon
        {
            Symbol = symbol,
            FontSize = 42,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 16),
        };
        stack.Children.Add(icon);

        var title = new System.Windows.Controls.TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8),
        };
        title.SetResourceReference(System.Windows.Controls.TextBlock.TextProperty, titleKey);
        stack.Children.Add(title);

        var description = new System.Windows.Controls.TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Opacity = 0.7,
        };
        description.SetResourceReference(System.Windows.Controls.TextBlock.TextProperty, "Placeholder.Description");
        stack.Children.Add(description);

        grid.Children.Add(stack);
        Content = grid;
    }
}