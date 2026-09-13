using System.Windows;
using System.Windows.Controls;
using TheGuideToTheNewEden.WPF.Services;
using TheGuideToTheNewEden.WPF.Services.Translation;

namespace TheGuideToTheNewEden.WPF.Views.Pages.Settings;

/// <summary>
/// 术语表设置：① 用户自定义术语（优先级高于 SDE，注入提示词/后校验/本地库源都会用）；
/// ② AI 新词候选（翻译时遇到、术语库没覆盖的专有名词，人工确认后加入术语表——**不自动写入**）；
/// ③ 术语库状态与重新载入。所有改动即时落盘。
/// </summary>
public partial class GlossarySettingPage : Page
{
    private bool _loading = true;

    public GlossarySettingPage()
    {
        InitializeComponent();

        GlossaryService.Changed += OnGlossaryChanged;
        UserGlossaryService.Changed += OnGlossaryChanged;
        GlossaryCandidateService.Changed += OnGlossaryChanged;
        Unloaded += OnUnloaded;

        RefreshLists();
        RefreshStatus();
        _loading = false;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        GlossaryService.Changed -= OnGlossaryChanged;
        UserGlossaryService.Changed -= OnGlossaryChanged;
        GlossaryCandidateService.Changed -= OnGlossaryChanged;
    }

    /// <summary>术语/候选变化（可能来自翻译线程）→ 切回 UI 线程刷新。</summary>
    private void OnGlossaryChanged(object? sender, EventArgs e)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            RefreshLists();
            RefreshStatus();
        }
        else
        {
            dispatcher.BeginInvoke(new Action(() =>
            {
                RefreshLists();
                RefreshStatus();
            }));
        }
    }

    private void RefreshLists()
    {
        TermList.ItemsSource = UserGlossaryService.Terms;
        CandidateList.ItemsSource = GlossaryCandidateService.Items;
        EmptyTermsText.Visibility = UserGlossaryService.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyCandidatesText.Visibility = GlossaryCandidateService.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        CandidateStatusText.Text = string.Format(FindString("GlossarySettingPage_CandidateCount"), GlossaryCandidateService.Count);
    }

    private void RefreshStatus()
    {
        StatusText.Text = string.Format(FindString("GlossarySettingPage_StatusValue"), GlossaryService.Count, UserGlossaryService.Count);
    }

    private void OnAddTermClick(object sender, RoutedEventArgs e)
    {
        var english = EnglishBox.Text?.Trim() ?? string.Empty;
        var chinese = ChineseBox.Text?.Trim() ?? string.Empty;

        if (english.Length == 0)
        {
            TermStatusText.Text = FindString("GlossarySettingPage_NeedEnglish");
            return;
        }

        if (chinese.Length == 0)
        {
            TermStatusText.Text = FindString("GlossarySettingPage_NeedChinese");
            return;
        }

        if (UserGlossaryService.Add(english, chinese))
        {
            TermStatusText.Text = string.Format(FindString("GlossarySettingPage_Added"), english, chinese);
            EnglishBox.Text = string.Empty;
            ChineseBox.Text = string.Empty;
            PageNotifyService.Success(TermStatusText.Text);
        }
        else
        {
            TermStatusText.Text = FindString("GlossarySettingPage_AddFailed");
        }
    }

    private void OnRemoveTermClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: UserGlossaryTerm term })
        {
            return;
        }

        if (UserGlossaryService.Remove(term.English))
        {
            TermStatusText.Text = string.Format(FindString("GlossarySettingPage_Removed"), term.English);
        }
    }

    /// <summary>把候选填进输入框，等用户补中文名（模型反推的对应关系不可靠，必须人工确认）。</summary>
    private void OnUseCandidateClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: GlossaryCandidate candidate })
        {
            return;
        }

        EnglishBox.Text = candidate.English;
        ChineseBox.Text = string.Empty;
        ChineseBox.Focus();
        TermStatusText.Text = string.Format(FindString("GlossarySettingPage_FillChinese"), candidate.English);
    }

    private void OnIgnoreCandidateClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: GlossaryCandidate candidate })
        {
            GlossaryCandidateService.Ignore(candidate.English);
        }
    }

    private void OnClearCandidatesClick(object sender, RoutedEventArgs e)
    {
        GlossaryCandidateService.Clear();
        PageNotifyService.Success(FindString("GlossarySettingPage_Cleared"));
    }

    private async void OnReloadClick(object sender, RoutedEventArgs e)
    {
        ReloadButton.IsEnabled = false;
        StatusText.Text = FindString("GlossarySettingPage_Reloading");
        try
        {
            var result = await GlossaryService.ReloadAsync();
            StatusText.Text = string.Format(
                FindString("GlossarySettingPage_Reloaded"),
                result.Count,
                UserGlossaryService.Count,
                $"{result.Elapsed.TotalSeconds:F1}s");
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            StatusText.Text = ex.Message;
        }
        finally
        {
            ReloadButton.IsEnabled = true;
        }
    }

    private static string FindString(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}
