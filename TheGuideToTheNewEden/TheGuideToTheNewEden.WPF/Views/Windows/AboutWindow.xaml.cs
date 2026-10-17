using System.Windows;
using TheGuideToTheNewEden.WPF.Helpers;
using Wpf.Ui.Controls;

namespace TheGuideToTheNewEden.WPF.Views.Windows;

/// <summary>
/// 关于窗口：版本、运行时、社区链接、第三方组件。
/// <para>
/// WinUI 版把「关于」放在主页的状态卡里、用 <c>ToolWindow</c> 承载一个 AboutPage；
/// WPF 侧做成独立的小窗口（单实例），主页品牌条的图钉/磁贴都可以指向它。
/// </para>
/// </summary>
public partial class AboutWindow : FluentWindow
{
    private static AboutWindow? _instance;

    public AboutWindow()
    {
        InitializeComponent();

        if (App.MainWindow is { } mainWindow)
        {
            Owner = mainWindow;
        }

        VersionText.Text = AppVersion.Format(AppName);
        RuntimeText.Text = $".NET {Environment.Version} · {Environment.OSVersion.VersionString}";
    }

    /// <summary>打开（或激活）单实例关于窗口。</summary>
    public static void ShowWindow()
    {
        if (_instance is { IsLoaded: true } existing)
        {
            if (existing.WindowState == WindowState.Minimized)
            {
                existing.WindowState = WindowState.Normal;
            }

            existing.Activate();
            return;
        }

        _instance = new AboutWindow();
        _instance.Closed += (_, _) => _instance = null;
        _instance.Show();
        _instance.Activate();
    }

    private static string AppName
        => Application.Current?.TryFindResource("AppDisplayName") as string ?? "TheGuideToTheNewEden";

    private void OnGithubClick(object sender, RoutedEventArgs e)
        => ShellHelper.TryOpen("https://github.com/qedsd/TheGuideToTheNewEden", out _);

    private void OnQqGroupClick(object sender, RoutedEventArgs e)
        => ShellHelper.TryOpen("https://jq.qq.com/?_wv=1027&k=m8Ttv1DX", out _);

    /// <summary>打开内置说明书（原来的"打开 PDF"入口，阶段 131 起改指手册窗口）。</summary>
    private void OnManualClick(object sender, RoutedEventArgs e) => ManualWindow.Show("getting-started");
}
