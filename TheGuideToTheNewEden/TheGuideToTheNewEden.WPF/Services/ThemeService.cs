using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Appearance;

namespace TheGuideToTheNewEden.WPF.Services;

/// <summary>
/// 主题服务：浅色/深色切换与主题色（强调色）切换。
/// </summary>
public static class ThemeService
{
    /// <summary>主题色候选，供设置页展示为色块。</summary>
    public static readonly IReadOnlyList<Color> AccentColors =
    [
        Color.FromRgb(0x00, 0x78, 0xD4), // Windows 蓝
        Color.FromRgb(0xE3, 0x00, 0x8C), // 品红
        Color.FromRgb(0xCA, 0x50, 0x10), // 橙红
        Color.FromRgb(0x10, 0x7C, 0x10), // 绿
        Color.FromRgb(0x87, 0x63, 0x7C), // 灰紫
        Color.FromRgb(0x8A, 0x5C, 0x00), // 琥珀
        Color.FromRgb(0x00, 0x67, 0x5C), // 青绿
        Color.FromRgb(0x4F, 0x6B, 0xED), // 靛蓝
    ];

    public static ApplicationTheme Theme { get; private set; } = ApplicationTheme.Light;

    /// <summary>用户选择的强调色；未选择过时为 null（使用系统强调色）。</summary>
    public static Color? CurrentAccent { get; private set; }

    /// <summary>当前实际生效的强调色。</summary>
    public static Color AccentColor => CurrentAccent ?? ApplicationAccentColorManager.SystemAccent;

    /// <summary>
    /// 主题或强调色已变化。界面颜色走 <c>DynamicResource</c> 会自动更新，
    /// 但自绘/第三方渲染器（如 LiveCharts 的 SkiaSharp 画刷）拿不到主题资源，需要监听此事件重新着色。
    /// </summary>
    public static event Action? ThemeChanged;

    public static void Initialize()
    {
        var saved = SettingsService.GetValue(SettingsService.ThemeKey);
        var theme = saved is "Dark" or "dark" ? ApplicationTheme.Dark : ApplicationTheme.Light;

        Theme = theme;
        ApplicationThemeManager.Apply(theme, Wpf.Ui.Controls.WindowBackdropType.Mica, updateAccent: false);
        ApplySavedAccentColor();
    }

    public static void ApplyTheme(ApplicationTheme theme, bool applyAccent = true)
    {
        Theme = theme;

        // 切换主题时不要用系统色覆盖用户选择的强调色，保留用户设置。
        ApplicationThemeManager.Apply(theme, Wpf.Ui.Controls.WindowBackdropType.Mica, updateAccent: false);

        if (applyAccent)
        {
            ApplySavedAccentColor();
        }

        SettingsService.SetValue(SettingsService.ThemeKey, theme == ApplicationTheme.Dark ? "Dark" : "Light");
        SettingsService.Save();
        ThemeChanged?.Invoke();
    }

    private static void ApplySavedAccentColor()
    {
        var accent = SettingsService.GetColor(SettingsService.AccentColorKey);
        if (accent is { } color)
        {
            CurrentAccent = color;
            ApplicationAccentColorManager.Apply(color, Theme);
        }

        SyncSystemAccentBrushes();
    }

    public static void ToggleTheme()
    {
        ApplyTheme(Theme == ApplicationTheme.Light ? ApplicationTheme.Dark : ApplicationTheme.Light);
    }

    public static void ApplyAccentColor(Color color)
    {
        CurrentAccent = color;
        ApplicationAccentColorManager.Apply(color, Theme);
        SyncSystemAccentBrushes();
        SettingsService.SetColor(SettingsService.AccentColorKey, color);
        SettingsService.Save();
        ThemeChanged?.Invoke();
    }

    /// <summary>
    /// 把 <c>SystemAccentColorPrimary/Secondary/TertiaryBrush</c> 与 AccentColorManager 刚更新过的
    /// <c>SystemAccentColor*Color</c> 对齐。
    ///
    /// WPF-UI 的 <c>ApplicationAccentColorManager.Apply</c> 只写四个 <b>Color</b> 资源和
    /// <c>Accent*</c> 系画刷，<b>从不更新这三个画刷</b>——它们来自 <c>Resources/Accent.xaml</c> 的
    /// <c>StaticResource</c>，永远停留在编译时的系统默认蓝 <c>#0067C0</c>。本项目有 30+ 处引用
    /// 这三个画刷（KB/ZKB 页链接与强调、翻译选中高亮、通知条、虫洞与洞口详情标题……），
    /// 在自定义主题色下全部不变色（用户实机反馈：主题色是绿的、虫洞名字却是蓝的）。
    /// </summary>
    private static void SyncSystemAccentBrushes()
    {
        var resources = Application.Current.Resources;
        SyncSystemAccentBrush(resources, "SystemAccentColorPrimary");
        SyncSystemAccentBrush(resources, "SystemAccentColorSecondary");
        SyncSystemAccentBrush(resources, "SystemAccentColorTertiary");
    }

    private static void SyncSystemAccentBrush(ResourceDictionary resources, string colorKey)
    {
        if (resources[colorKey] is not Color color)
        {
            return;
        }

        var brush = new SolidColorBrush(color);
        brush.Freeze();
        resources[colorKey + "Brush"] = brush;
    }
}