using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace TheGuideToTheNewEden.WPF.Services;

/// <summary>
/// 语言服务：通过替换 Application 资源字典中的语言字典实现运行时切换。
/// 页面与菜单文本需使用 DynamicResource 绑定。
///
/// 换字典只能让 <c>DynamicResource</c> 自动跟着变。界面里还有大量文案是**绑定求值时才查资源**的
/// （典型：模型上的计算属性 <c>WormholeListItem.PhenomenaName</c>、<c>ActiveEntityItem.LastActiveText</c>，
/// 以及带转换器的绑定）——WPF 的绑定只在"源发通知 / DataContext 变化"时重读源，
/// 光换字典它们会一直停在旧语言，直到重启（用户反馈"切英文没法全部生效，重启才生效"）。
/// 因此 <see cref="SetLanguage"/> 末尾统一重算一次绑定，见 <see cref="RefreshAllBindings"/>。
/// </summary>
public static class LanguageService
{
    public const string DefaultLanguage = "zh-CN";

    /// <summary>本次语言已重算过绑定的元素（弱引用，元素被回收时记录自动消失，不会累积）。</summary>
    private static readonly ConditionalWeakTable<DependencyObject, object> RefreshedElements = new();

    /// <summary>只作"已刷新"的标记值（ConditionalWeakTable 的值统一用它）。</summary>
    private static readonly object RefreshMark = new();

    private static bool _loadedRefreshHooked;

    /// <summary>启动阶段结束（<see cref="Initialize"/> 跑完）。</summary>
    private static bool _initialized;

    /// <summary>运行期切换过语言：此后"重新上树"的元素才需要补刷（启动阶段元素刚建、文案本来就是新的，省一份记录）。</summary>
    private static bool _refreshOnLoaded;

    public static string Value { get; private set; } = DefaultLanguage;

    public static event EventHandler<string>? LanguageChanged;

    public static void Initialize()
    {
        HookLoadedRefresh();

        var saved = SettingsService.GetValue(SettingsService.LanguageKey);
        SetLanguage(string.IsNullOrWhiteSpace(saved) ? DefaultLanguage : saved);
        _initialized = true;
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

        // 先让订阅者（VM）重建它们"生成时就烤进对象"的文案，再统一重算绑定
        LanguageChanged?.Invoke(null, language);

        _refreshOnLoaded |= _initialized;
        RefreshAllBindings();
    }

    // ==================================================================
    //  换语言后的绑定重算
    // ==================================================================

    /// <summary>
    /// 换语言后把现有界面上的绑定重算一遍：
    /// ① 已显示的元素走一遍可视树；② 之后**重新上树**的元素（<c>NavigationCacheMode="Required"</c> 的缓存页、
    /// 虚拟化容器、重开的 Flyout/Popup）由 <see cref="HookLoadedRefresh"/> 的类处理器按本次语言补刷
    /// —— 缓存页在切走时脱离可视树、不参与①，切回来时只认"语言版本"补刷一次。
    /// </summary>
    private static void RefreshAllBindings()
    {
        RefreshedElements.Clear();

        var windows = Application.Current?.Windows;
        if (windows is null)
        {
            return;
        }

        foreach (Window window in windows)
        {
            RefreshTree(window);
        }
    }

    private static void RefreshTree(DependencyObject element)
    {
        if (MarkRefreshed(element))
        {
            RefreshBindings(element);
        }

        var count = VisualTreeHelper.GetChildrenCount(element);
        for (var index = 0; index < count; index++)
        {
            RefreshTree(VisualTreeHelper.GetChild(element, index));
        }
    }

    /// <summary>把元素记成"本次语言已刷"；已经记过或元素为空返回 false。</summary>
    private static bool MarkRefreshed(DependencyObject? element)
    {
        if (element is null || RefreshedElements.TryGetValue(element, out _))
        {
            return false;
        }

        RefreshedElements.Add(element, RefreshMark);
        return true;
    }

    /// <summary>
    /// 重算单个元素上的绑定：只认"本地值里是绑定表达式"的依赖属性（绑定的目标值就是本地值），
    /// 逐个 <c>UpdateTarget()</c> —— 按源重新走一遍路径、重新跑转换器，而且**只写目标、不回写源**。
    /// </summary>
    private static void RefreshBindings(DependencyObject element)
    {
        if (element is not FrameworkElement frameworkElement)
        {
            return;
        }

        // 焦点元素可能正持有"尚未提交"的输入（TextBox 默认 LostFocus 才回写源）：
        // 对它 UpdateTarget 会把源上的旧值写回目标 = 抹掉用户正在输入的内容，跳过。
        if (frameworkElement.IsKeyboardFocused)
        {
            return;
        }

        var values = frameworkElement.GetLocalValueEnumerator();
        while (values.MoveNext())
        {
            if (values.Current.Value is not BindingExpressionBase expression)
            {
                continue;
            }

            // OneWayToSource 没有"从源取值"这回事
            if (expression.ParentBindingBase is Binding { Mode: BindingMode.OneWayToSource })
            {
                continue;
            }

            try
            {
                expression.UpdateTarget();
            }
            catch (System.Exception ex)
            {
                // 单个转换器/取值器抛错不该打断整轮刷新（例如受限环境下会抛的 TypeImageConverter）
                Core.Log.Warn($"[Language] 重算绑定失败：{ex.Message}");
            }
        }
    }

    /// <summary>
    /// 给所有 <see cref="FrameworkElement"/> 的 Loaded 挂一个类处理器：元素**重新上树**时按语言版本补刷。
    /// 这一步是"缓存页"能跟着换语言的关键——缓存页切走时已脱离可视树，光靠 <see cref="RefreshAllBindings"/> 的
    /// 那一遍走不到它，否则要等重启才刷新。
    /// </summary>
    private static void HookLoadedRefresh()
    {
        if (_loadedRefreshHooked)
        {
            return;
        }

        _loadedRefreshHooked = true;
        EventManager.RegisterClassHandler(
            typeof(FrameworkElement),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) => OnElementLoaded(sender as DependencyObject)));
    }

    /// <summary>元素重新上树：只在"运行期切换过语言"之后才需要补刷（启动阶段元素刚建、文案本来就是新的）。</summary>
    private static void OnElementLoaded(DependencyObject? element)
    {
        if (!_refreshOnLoaded || element is null || !MarkRefreshed(element))
        {
            return;
        }

        RefreshBindings(element);
    }
}
