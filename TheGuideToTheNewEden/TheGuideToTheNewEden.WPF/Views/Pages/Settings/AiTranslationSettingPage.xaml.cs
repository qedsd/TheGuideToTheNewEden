using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Settings;
using TheGuideToTheNewEden.WPF.Services.Translation;
using TheGuideToTheNewEden.WPF.Services.Translation.Llm;

namespace TheGuideToTheNewEden.WPF.Views.Pages.Settings;

/// <summary>
/// AI 翻译设置：协议端点（OpenAI 兼容 / Azure / Anthropic / Gemini）、生成参数、
/// 术语库（SDE 中英词库命中注入）与结果缓存。改动即时保存；"测试连接"会先落盘再真实发一次请求。
/// </summary>
public partial class AiTranslationSettingPage : Page
{
    /// <summary>协议下拉项（Key 持久化，Name 本地化显示）。</summary>
    private sealed record ProtocolItem(string Key, string Name);

    /// <summary>思考模式下拉项。</summary>
    private sealed record ThinkingItem(string Key, string Name);

    private bool _loading = true;

    public AiTranslationSettingPage()
    {
        InitializeComponent();

        LoadProtocols();
        LoadThinkingModes();
        LoadSettings();
        RefreshCacheStatus();

        ProtocolBox.SelectionChanged += OnSettingChanged;
        ThinkingBox.SelectionChanged += OnSettingChanged;
        UseGlossaryToggle.Checked += OnToggleChanged;
        UseGlossaryToggle.Unchecked += OnToggleChanged;
        StreamToggle.Checked += OnToggleChanged;
        StreamToggle.Unchecked += OnToggleChanged;
        CacheToggle.Checked += OnToggleChanged;
        CacheToggle.Unchecked += OnToggleChanged;

        _loading = false;

        Loaded += (_, _) => RefreshGlossaryStatus(autoLoad: true);
    }

    private void LoadProtocols()
    {
        var items = ChatProtocolFactory.All
            .Select(p => new ProtocolItem(p.Key, FindString(p.DisplayNameKey)))
            .ToList();
        ProtocolBox.ItemsSource = items;
    }

    private void LoadThinkingModes()
    {
        ThinkingBox.ItemsSource = ThinkingModes.All
            .Select(key => new ThinkingItem(key, FindString($"AiSettingPage_Thinking_{key}")))
            .ToList();
    }

    private void LoadSettings()
    {
        var setting = TranslationSettingService.Ai;

        ProtocolBox.SelectedItem = ((List<ProtocolItem>)ProtocolBox.ItemsSource)
            .FirstOrDefault(p => p.Key == setting.Protocol) ?? ((List<ProtocolItem>)ProtocolBox.ItemsSource)[0];
        BaseUrlBox.Text = setting.BaseUrl;
        ApiKeyBox.Password = setting.ApiKey;
        ModelBox.Text = setting.Model;
        ApiVersionBox.Text = setting.ApiVersion;
        TemperatureBox.Value = setting.Temperature;
        MaxTokensBox.Value = setting.MaxTokens;
        TimeoutBox.Value = setting.TimeoutSeconds;
        UseGlossaryToggle.IsChecked = setting.UseGlossary;
        GlossaryLimitBox.Value = setting.GlossaryLimit;
        ChunkSizeBox.Value = setting.MaxChunkChars;
        ThinkingBox.SelectedItem = ((List<ThinkingItem>)ThinkingBox.ItemsSource)
            .FirstOrDefault(t => t.Key == setting.ThinkingMode) ?? ((List<ThinkingItem>)ThinkingBox.ItemsSource)[1];
        StreamToggle.IsChecked = setting.Stream;
        CacheToggle.IsChecked = setting.Cache;
        SystemPromptBox.Text = setting.SystemPrompt;

        UpdateModelPlaceholder();
    }

    private void OnSettingChanged(object sender, RoutedEventArgs e) => Save();

    private void OnToggleChanged(object sender, RoutedEventArgs e) => Save();

    private void OnNumberChanged(object sender, RoutedEventArgs e) => Save();

    private void Save()
    {
        if (_loading)
        {
            return;
        }

        try
        {
            var current = TranslationSettingService.Ai.Clone();
            var selected = ProtocolBox.SelectedItem as ProtocolItem;

            current.Protocol = selected?.Key ?? current.Protocol;
            current.BaseUrl = BaseUrlBox.Text;
            current.ApiKey = ApiKeyBox.Password;
            current.Model = ModelBox.Text;
            current.ApiVersion = ApiVersionBox.Text;
            current.Temperature = TemperatureBox.Value ?? current.Temperature;
            current.MaxTokens = (int)(MaxTokensBox.Value ?? current.MaxTokens);
            current.TimeoutSeconds = (int)(TimeoutBox.Value ?? current.TimeoutSeconds);
            current.UseGlossary = UseGlossaryToggle.IsChecked == true;
            current.GlossaryLimit = (int)(GlossaryLimitBox.Value ?? current.GlossaryLimit);
            current.MaxChunkChars = (int)(ChunkSizeBox.Value ?? current.MaxChunkChars);
            current.ThinkingMode = (ThinkingBox.SelectedItem as ThinkingItem)?.Key ?? current.ThinkingMode;
            current.Stream = StreamToggle.IsChecked == true;
            current.Cache = CacheToggle.IsChecked == true;
            current.SystemPrompt = SystemPromptBox.Text;

            TranslationSettingService.SetAi(current);
            UpdateModelPlaceholder();
            RefreshCacheStatus();
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            MessageBox.Show(
                ex.Message,
                FindString("SettingPage_AiTranslation"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    /// <summary>模型名留空时按协议给出默认值提示（不写入设置，只做占位提示）。</summary>
    private void UpdateModelPlaceholder()
    {
        var protocol = ChatProtocolFactory.Get((ProtocolBox.SelectedItem as ProtocolItem)?.Key);
        ModelBox.PlaceholderText = protocol.DefaultModel;
        ApiVersionBox.PlaceholderText = AzureOpenAiChatProtocol.DefaultApiVersion;
    }

    private void OnRestoreBaseUrlClick(object sender, RoutedEventArgs e)
    {
        var protocol = ChatProtocolFactory.Get((ProtocolBox.SelectedItem as ProtocolItem)?.Key);
        BaseUrlBox.Text = protocol.DefaultBaseUrl;
        if (string.IsNullOrWhiteSpace(ModelBox.Text))
        {
            ModelBox.Text = protocol.DefaultModel;
        }

        Save();
    }

    private void OnRestorePromptClick(object sender, RoutedEventArgs e)
    {
        SystemPromptBox.Text = string.Empty;
        Save();
        TestStatusText.Text = FindString("AiSettingPage_PromptRestored");
    }

    private async void OnTestClick(object sender, RoutedEventArgs e)
    {
        Save();

        var endpoint = TranslationSettingService.AiEndpoint;
        if (!endpoint.IsConfigured)
        {
            TestStatusText.Text = FindString("AiSettingPage_NotConfigured");
            return;
        }

        TestButton.IsEnabled = false;
        TestStatusText.Text = FindString("AiSettingPage_Testing");
        try
        {
            var result = await ChatClient.TestAsync(endpoint);
            TestStatusText.Text = string.Format(FindString("AiSettingPage_TestOk"), result);
            PageNotifyService.Success(string.Format(FindString("AiSettingPage_TestOk"), result));
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            TestStatusText.Text = string.Format(FindString("AiSettingPage_TestFailed"), ex.Message);
            PageNotifyService.Error(string.Format(FindString("AiSettingPage_TestFailed"), ex.Message));
        }
        finally
        {
            TestButton.IsEnabled = true;
        }
    }

    private async void OnReloadGlossaryClick(object sender, RoutedEventArgs e)
    {
        await LoadGlossaryAsync(force: true);
    }

    private async void RefreshGlossaryStatus(bool autoLoad = false)
    {
        if (GlossaryService.IsReady)
        {
            GlossaryStatusText.Text = string.Format(FindString("AiSettingPage_GlossaryReady"), GlossaryService.Count, GlossaryService.LastBuildInfo);
            return;
        }

        GlossaryStatusText.Text = FindString("AiSettingPage_GlossaryNotLoaded");
        if (autoLoad)
        {
            await LoadGlossaryAsync(force: false);
        }
    }

    private async Task LoadGlossaryAsync(bool force)
    {
        ReloadGlossaryButton.IsEnabled = false;
        GlossaryStatusText.Text = FindString("AiSettingPage_GlossaryLoading");
        try
        {
            var result = force
                ? await GlossaryService.ReloadAsync()
                : await GlossaryService.EnsureLoadedAsync();
            GlossaryStatusText.Text = string.Format(
                FindString("AiSettingPage_GlossaryReady"),
                result.Count,
                $"{(result.Elapsed.TotalSeconds < 0.05 ? 0 : result.Elapsed.TotalSeconds):F1}s");
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            GlossaryStatusText.Text = string.Format(FindString("AiSettingPage_TestFailed"), ex.Message);
        }
        finally
        {
            ReloadGlossaryButton.IsEnabled = true;
        }
    }

    private void OnClearCacheClick(object sender, RoutedEventArgs e)
    {
        var removed = TranslationCache.Clear();
        RefreshCacheStatus();
        PageNotifyService.Success(string.Format(FindString("AiSettingPage_CacheCleared"), removed));
    }

    private void RefreshCacheStatus()
    {
        var count = TranslationCache.Count();
        var size = TranslationCache.SizeInBytes();
        CacheStatusText.Text = string.Format(
            FindString("AiSettingPage_CacheStatus"),
            count,
            size < 1024 * 1024 ? $"{size / 1024.0:F0} KB" : $"{size / 1024.0 / 1024.0:F1} MB");
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}
