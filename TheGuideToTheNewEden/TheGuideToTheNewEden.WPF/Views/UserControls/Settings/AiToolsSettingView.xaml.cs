using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Assistant;

namespace TheGuideToTheNewEden.WPF.Views.UserControls.Settings;

/// <summary>
/// 设置 → AI → 「工具开关」：按需声明开关 + 44 个工具逐个启用/停用（按套餐分组列出）。
/// <para>阶段 124 从 <c>AiAssistantSettingPage</c> 拆出来，作为二级菜单的一个分节；本视图自己带「保存」。</para>
/// </summary>
public partial class AiToolsSettingView : UserControl
{
    /// <summary>工具清单里的一个开关项。</summary>
    public sealed class ToolToggleItem : INotifyPropertyChanged
    {
        private bool _isEnabled;

        public ToolToggleItem(string name, string displayName, bool isEnabled)
        {
            Name = name;
            DisplayName = displayName;
            _isEnabled = isEnabled;
        }

        /// <summary>面向模型的英文工具名（存储用的就是它）。</summary>
        public string Name { get; }

        /// <summary>界面显示名（本地化）。</summary>
        public string DisplayName { get; }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled == value)
                {
                    return;
                }

                _isEnabled = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>一个工具分组（分组 = 按需声明的套餐，顺序取自 <see cref="ToolRegistry.Categories"/>）。</summary>
    public sealed class ToolGroupItem
    {
        public required string Title { get; init; }

        public required List<ToolToggleItem> Items { get; init; }
    }

    private readonly ObservableCollection<ToolGroupItem> _toolGroups = [];
    private readonly List<ToolToggleItem> _allToolItems = [];
    private readonly Dictionary<string, CheckBox> _pinnedChecks = new(StringComparer.Ordinal);

    public AiToolsSettingView()
    {
        InitializeComponent();
        ToolGroups.ItemsSource = _toolGroups;
        // 只在**构造时**读一次设置并建清单（切来切去不丢未保存的编辑）
        Load();
    }

    /// <summary>读设置并重建工具清单（"恢复默认"后由宿主页显式调用）。</summary>
    public void Load()
    {
        ToolRouteToggle.IsChecked = AiAssistantSettingService.AutoRouteTools;
        BuildToolGroups();
        BuildPinnedBundles();
    }

    /// <summary>重建"手动钉选套餐"复选框（阶段 150；Esi 是核心常驻，不作为套餐出现）。</summary>
    private void BuildPinnedBundles()
    {
        PinnedBundlesPanel.Children.Clear();
        _pinnedChecks.Clear();

        foreach (var category in ToolRegistry.Categories.Where(category => category != "Esi"))
        {
            var check = new CheckBox
            {
                Content = FindString($"AssistantSettingPage_ToolsGroup_{category}"),
                Tag = category,
                Margin = new Thickness(0, 0, 14, 0),
                VerticalAlignment = VerticalAlignment.Center,
                IsChecked = AiAssistantSettingService.PinnedBundles.Contains(category),
            };

            _pinnedChecks[category] = check;
            PinnedBundlesPanel.Children.Add(check);
        }
    }

    /// <summary>按分组重建工具清单（反映最新注册表与开关状态）。</summary>
    private void BuildToolGroups()
    {
        _toolGroups.Clear();
        _allToolItems.Clear();

        foreach (var category in ToolRegistry.Categories)
        {
            var items = ToolRegistry.Product
                .Where(t => ToolRegistry.CategoryOf(t.Name) == category)
                .Select(t => new ToolToggleItem(t.Name, FindString(t.DisplayNameKey), AiAssistantSettingService.IsToolEnabled(t.Name)))
                .ToList();
            if (items.Count == 0)
            {
                continue;
            }

            _allToolItems.AddRange(items);
            _toolGroups.Add(new ToolGroupItem
            {
                Title = FindString($"AssistantSettingPage_ToolsGroup_{category}"),
                Items = items,
            });
        }

        UpdateToolCount();
    }

    /// <summary>保存本分节（禁用集合 + 按需声明开关）。</summary>
    public void Save()
    {
        AiAssistantSettingService.SaveDisabledTools(_allToolItems.Where(i => !i.IsEnabled).Select(i => i.Name));
        AiAssistantSettingService.SaveRouting(ToolRouteToggle.IsChecked == true);
        AiAssistantSettingService.SavePinnedBundles(
            _pinnedChecks.Where(pair => pair.Value.IsChecked == true).Select(pair => pair.Key));

        UpdateToolCount();
        PageNotifyService.Success(FindString("AssistantSettingPage_Saved"));
    }

    private void OnSaveClick(object sender, RoutedEventArgs e) => Save();

    private void OnEnableAllToolsClick(object sender, RoutedEventArgs e)
    {
        foreach (var item in _allToolItems)
        {
            item.IsEnabled = true;
        }

        UpdateToolCount();
    }

    private void OnDisableAllToolsClick(object sender, RoutedEventArgs e)
    {
        foreach (var item in _allToolItems)
        {
            item.IsEnabled = false;
        }

        UpdateToolCount();
    }

    private void UpdateToolCount()
    {
        var disabled = _allToolItems.Count(i => !i.IsEnabled);
        ToolCountText.Text = string.Format(
            FindString("AssistantSettingPage_Tools_Count"), disabled, _allToolItems.Count);
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}
