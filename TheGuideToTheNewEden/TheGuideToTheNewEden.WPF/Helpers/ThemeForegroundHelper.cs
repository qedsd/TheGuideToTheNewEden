using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Views.UserControls.Map;

namespace TheGuideToTheNewEden.WPF.Helpers;

/// <summary>
/// 工具窗口的主题前景修正：独立 FluentWindow 里部分控件模板/局部画刷会停留在黑色，
/// 这里只把“实际为深色”的文字与控件前景绑定到窗口根视觉树的 TextElement.Foreground。
/// 安全等级色与主按钮对比度会被保留。
/// </summary>
public static class ThemeForegroundHelper
{
    public static void Attach(FrameworkElement root) => _ = new Tracker(root);

    private sealed class Tracker
    {
        private readonly FrameworkElement _root;
        private readonly RoutedEventHandler _elementLoadedHandler;
        private DependencyObject? _themeSource;

        public Tracker(FrameworkElement root)
        {
            _root = root;
            _elementLoadedHandler = OnElementLoaded;
            root.Loaded += OnRootLoaded;
            root.Unloaded += OnRootUnloaded;
            root.AddHandler(FrameworkElement.LoadedEvent, _elementLoadedHandler);
            ThemeService.ThemeChanged += OnThemeChanged;
        }

        private void OnRootLoaded(object sender, RoutedEventArgs e)
        {
            _themeSource = Window.GetWindow(_root)?.Content as DependencyObject ?? _root;
            ApplyReadableForegrounds(_root);
        }

        private void OnRootUnloaded(object sender, RoutedEventArgs e)
        {
            ThemeService.ThemeChanged -= OnThemeChanged;
            _root.RemoveHandler(FrameworkElement.LoadedEvent, _elementLoadedHandler);
        }

        private void OnThemeChanged()
        {
            _root.Dispatcher.BeginInvoke(() =>
            {
                _themeSource = Window.GetWindow(_root)?.Content as DependencyObject ?? _themeSource ?? _root;
                ApplyReadableForegrounds(_root);
            });
        }

        private void OnElementLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextBlock text)
            {
                FixTextForeground(text);
            }
            else if (sender is Wpf.Ui.Controls.Button button)
            {
                FixButtonForeground(button);
            }
            else if (sender is TextBox textBox)
            {
                FixControlForeground(textBox);
            }
        }

        private void ApplyReadableForegrounds(DependencyObject root)
        {
            if (root is TextBlock text)
            {
                FixTextForeground(text);
            }
            else if (root is Wpf.Ui.Controls.Button button)
            {
                FixButtonForeground(button);
            }
            else if (root is TextBox textBox)
            {
                FixControlForeground(textBox);
            }

            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                ApplyReadableForegrounds(VisualTreeHelper.GetChild(root, i));
            }
        }

        private void FixTextForeground(TextBlock text)
        {
            if (!IsDarkBrush(text.Foreground) || IsSecurityColor(text.Foreground))
            {
                return;
            }

            if (FindAncestor<Wpf.Ui.Controls.Button>(text) is
                { Appearance: Wpf.Ui.Controls.ControlAppearance.Primary })
            {
                return;
            }

            BindThemeForeground(text, TextBlock.ForegroundProperty);
        }

        private void FixButtonForeground(Wpf.Ui.Controls.Button button)
        {
            if (button.Appearance == Wpf.Ui.Controls.ControlAppearance.Primary
                || !IsDarkBrush(button.Foreground))
            {
                return;
            }

            BindThemeForeground(button, Control.ForegroundProperty);
        }

        private void FixControlForeground(TextBox textBox)
        {
            if (IsDarkBrush(textBox.Foreground))
            {
                BindThemeForeground(textBox, Control.ForegroundProperty);
            }
        }

        private void BindThemeForeground(FrameworkElement element, DependencyProperty property)
        {
            _themeSource ??= Window.GetWindow(_root)?.Content as DependencyObject ?? _root;
            element.SetBinding(property, new Binding("(TextElement.Foreground)")
            {
                Source = _themeSource,
            });
        }

        private static bool IsDarkBrush(Brush? brush)
        {
            if (brush is not SolidColorBrush solid || solid.Color.A < 40)
            {
                return false;
            }

            var color = solid.Color;
            return color.R < 80 && color.G < 80 && color.B < 80;
        }

        private static bool IsSecurityColor(Brush? brush)
        {
            if (brush is not SolidColorBrush solid)
            {
                return false;
            }

            var color = solid.Color;
            return StarMapCanvas.Palette.Any(p => p.Red == color.R && p.Green == color.G && p.Blue == color.B);
        }

        private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current is not null)
            {
                if (current is T target)
                {
                    return target;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }
    }
}
