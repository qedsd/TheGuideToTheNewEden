using System.Globalization;
using System.Windows;

namespace TheGuideToTheNewEden.WPF.Services;

/// <summary>
/// 语言服务：通过替换 Application 资源字典中的语言字典实现运行时切换。
/// 页面与菜单文本需使用 DynamicResource 绑定。
/// </summary>
public static class LanguageService
{
    public const string DefaultLanguage = "zh-CN";

    public static string Value { get; private set; } = DefaultLanguage;

    public static event EventHandler<string>? LanguageChanged;

    public static void Initialize()
    {
        var saved = SettingsService.GetValue(SettingsService.LanguageKey);
        SetLanguage(string.IsNullOrWhiteSpace(saved) ? DefaultLanguage : saved);
    }

    public static void SetLanguage(string language)
    {
        Value = language;

        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var old = dictionaries.FirstOrDefault(d =>
            d.Source?.OriginalString.Contains("Resources/Languages") == true);

        if (old is not null)
        {
            dictionaries.Remove(old);
        }

        dictionaries.Add(new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Resources/Languages/{language}.xaml", UriKind.Absolute),
        });

        CultureInfo.CurrentUICulture = new CultureInfo(language);
        CultureInfo.CurrentCulture = new CultureInfo(language);

        SettingsService.SetValue(SettingsService.LanguageKey, language);
        SettingsService.Save();

        LanguageChanged?.Invoke(null, language);
    }
}