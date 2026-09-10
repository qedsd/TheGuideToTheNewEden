using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace TheGuideToTheNewEden.WPF.Views.Pages.Settings;

/// <summary>软件更新：检查 GitHub 最新版本、下载安装包并启动安装程序。</summary>
public partial class UpdateSettingPage : Page
{
    private readonly HttpClient _httpClient = new();
    private Octokit.Release? _latestRelease;
    private string? _installerPath;

    public UpdateSettingPage()
    {
        InitializeComponent();

        CurrentVersionText.Text = GetCurrentVersion();

        CheckButton.Click += async (_, _) => await CheckAsync();
        DownloadButton.Click += async (_, _) => await DownloadAsync();
        InstallButton.Click += (_, _) => Install();
    }

    private static string GetCurrentVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "Unknown";
    }

    private static string FindString(string key)
    {
        return Application.Current?.TryFindResource(key) as string ?? key;
    }

    private async Task CheckAsync()
    {
        CheckButton.IsEnabled = false;
        DownloadButton.IsEnabled = false;
        StatusText.Text = FindString("Settings.Update.Checking");

        try
        {
            var release = await Core.Helpers.GithubHelper.GetLastReleaseInfoAsync();
            var latest = release.TagName.TrimStart('v', 'V');
            var current = GetCurrentVersion().Split('-', '+')[0];
            _latestRelease = release;

            if (Version.TryParse(latest, out var latestVersion)
                && Version.TryParse(current, out var currentVersion)
                && latestVersion > currentVersion)
            {
                StatusText.Text = $"{FindString("Settings.Update.NewAvailable")} {latest}";
                DownloadButton.IsEnabled = release.Assets.Count > 0;
            }
            else
            {
                StatusText.Text = FindString("Settings.Update.UpToDate");
            }
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            StatusText.Text = $"{FindString("Settings.Update.Failed")}: {ex.Message}";
        }
        finally
        {
            CheckButton.IsEnabled = true;
        }
    }

    private async Task DownloadAsync()
    {
        if (_latestRelease is null)
        {
            return;
        }

        var asset = _latestRelease.Assets.FirstOrDefault(a =>
                        a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                        || a.Name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
                    ?? _latestRelease.Assets.FirstOrDefault();

        if (asset is null)
        {
            return;
        }

        DownloadButton.IsEnabled = false;
        CheckButton.IsEnabled = false;

        try
        {
            var target = Path.Combine(Path.GetTempPath(), asset.Name);
            StatusText.Text = FindString("Settings.Update.Downloading");

            using var response = await _httpClient.GetAsync(asset.BrowserDownloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength ?? -1L;
            await using (var source = await response.Content.ReadAsStreamAsync())
            await using (var destination = File.Create(target))
            {
                var buffer = new byte[81920];
                long read = 0;
                int count;
                while ((count = await source.ReadAsync(buffer)) > 0)
                {
                    await destination.WriteAsync(buffer.AsMemory(0, count));
                    read += count;
                    if (total > 0)
                    {
                        StatusText.Text = $"{FindString("Settings.Update.Downloading")} {read * 100 / total}%";
                    }
                }
            }

            _installerPath = target;
            StatusText.Text = FindString("Settings.Update.Downloaded");
            InstallButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            StatusText.Text = ex.Message;
            DownloadButton.IsEnabled = true;
        }
        finally
        {
            CheckButton.IsEnabled = true;
        }
    }

    private void Install()
    {
        if (string.IsNullOrEmpty(_installerPath) || !File.Exists(_installerPath))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(_installerPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Core.Log.Error(ex);
            System.Windows.MessageBox.Show(
                ex.Message,
                FindString("Settings.Update.Install"),
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
    }
}