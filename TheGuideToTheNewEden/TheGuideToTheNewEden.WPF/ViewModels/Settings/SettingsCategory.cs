using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace TheGuideToTheNewEden.WPF.ViewModels.Settings;

/// <summary>设置首页的分类条目：图标 + 标题 + 描述 + 目标页面工厂。</summary>
public sealed class SettingsCategory : INotifyPropertyChanged
{
    public required SymbolRegular Icon { get; init; }

    public required string TitleKey { get; init; }

    public required string DescriptionKey { get; init; }

    /// <summary>目标子页类型（用作实例缓存的键）。</summary>
    public required Type PageType { get; init; }

    public required Func<Page> CreatePage { get; init; }

    public string Title => Resolve(TitleKey);

    public string Description => Resolve(DescriptionKey);

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>语言切换后刷新标题与描述文本。</summary>
    public void Refresh()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Description)));
    }

    private static string Resolve(string key)
    {
        return Application.Current?.TryFindResource(key) as string ?? key;
    }
}