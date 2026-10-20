using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Settings;
using TheGuideToTheNewEden.WPF.Services.Translation;

namespace TheGuideToTheNewEden.WPF.Views.UserControls.Settings;

/// <summary>
/// 设置 → AI → 「翻译」：**翻译专属**设置——术语库（开关 / 条数上限 / 状态与重新载入）、
/// 翻译行为（单次请求最大字符数 / 结果缓存与清空）、翻译用的系统提示词。
/// <para>
/// 阶段 125 从原来的「模型与翻译」里拆出来（模型端点搬去 <see cref="AiModelSettingView"/>，
/// 助手与翻译共用那’一份）。本视图自己带「保存」；「重新载入术语库 / 清空缓存」是即时动作。
/// </para>
/// </summary>
public partial class AiTranslationSettingView : UserControl
{
    public AiTranslationSettingView()
    {
        InitializeComponent();
        Load();
        RefreshCacheStatus();
        Loaded += (_, _) => RefreshGlossaryStatus(autoLoad: true);
    }

    /// <summary>把设置读进控件（"恢复默认"后由宿主页显式调用）。</summary>
    public void Load()
    {
        var setting = TranslationSettingService.Ai;

        UseGlossaryToggle.IsChecked = setting.UseGlossary;
        GlossaryLimitBox.Value = setting.GlossaryLimit;
        ChunkSizeBox.Value = setting.MaxChunkChars;
        ContextTokensBox.Value = setting.ContextMaxTokens;
        CacheToggle.IsChecked = setting.Cache;
        SystemPromptBox.Text = setting.SystemPrompt;
    }

    /// <summary>把控件里的值写回设置。</summary>
    public void Save()
    {
        try
        {
            var current = TranslationSettingService.Ai.Clone();

            current.UseGlossary = UseGlossaryToggle.IsChecked == true;
            current.GlossaryLimit = (int)(GlossaryLimitBox.Value ?? current.GlossaryLimit);
            current.MaxChunkChars = (int)(ChunkSizeBox.Value ?? current.MaxChunkChars);
            current.ContextMaxTokens = (int)(ContextTokensBox.Value ?? current.ContextMaxTokens);
            current.Cache = CacheToggle.IsChecked == true;
            current.SystemPrompt = SystemPromptBox.Text;

            TranslationSettingService.SetAi(current);
            RefreshCacheStatus();
            PageNotifyService.Success(FindString("AssistantSettingPage_Saved"));
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

    private void OnSaveClick(object sender, RoutedEventArgs e) => Save();

    private void OnRestorePromptClick(object sender, RoutedEventArgs e)
    {
        SystemPromptBox.Text = string.Empty;
        Save();
        PageNotifyService.Info(FindString("AiSettingPage_PromptRestored"));
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
