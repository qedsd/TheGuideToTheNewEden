using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;

namespace TheGuideToTheNewEden.DevTools.UpdatePublisher;

public partial class MainWindow : Window
{
    private readonly string _repoRoot;
    private readonly string _scriptPath;
    private Process? _process;
    private bool _running;
    private bool _cancelRequested;

    private static string SettingsFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TheGuideToTheNewEden",
        "DevTools",
        "update-publisher.json");

    public MainWindow()
    {
        InitializeComponent();

        (_repoRoot, _scriptPath) = LocateScript();
        ScriptPathText.Text = string.IsNullOrWhiteSpace(_scriptPath)
            ? "未找到 tools\\packaging\\make-update-package.ps1（请把本工具放在仓库目录内运行）"
            : $"脚本：{_scriptPath}";

        LoadSettings();
    }

    // ==================================================================
    //  位置与设置
    // ==================================================================

    private static (string RepoRoot, string ScriptPath) LocateScript()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && directory is not null; i++, directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "tools", "packaging", "make-update-package.ps1");
            if (File.Exists(candidate))
            {
                return (directory.FullName, candidate);
            }
        }

        var current = Directory.GetCurrentDirectory();
        var fallback = Path.Combine(current, "tools", "packaging", "make-update-package.ps1");
        return File.Exists(fallback)
            ? (current, fallback)
            : (current, string.Empty);
    }

    private void LoadSettings()
    {
        PublisherSettings settings;
        try
        {
            settings = File.Exists(SettingsFile)
                ? JsonSerializer.Deserialize<PublisherSettings>(File.ReadAllText(SettingsFile, Encoding.UTF8)) ?? new PublisherSettings()
                : new PublisherSettings();
        }
        catch
        {
            settings = new PublisherSettings();
        }

        VersionBox.Text = settings.Version;
        TagBox.Text = settings.Tag;
        OutputDirBox.Text = settings.OutputDir;
        GiteeBaseUrlBox.Text = settings.GiteeBaseUrl;
        FullNameBox.Text = settings.FullName;
        ExePathBox.Text = settings.ExePath;
        GitHubRepoBox.Text = string.IsNullOrWhiteSpace(settings.GitHubRepo) ? "qedsd/TheGuideToTheNewEden" : settings.GitHubRepo;
        GiteeRepoBox.Text = string.IsNullOrWhiteSpace(settings.GiteeRepo) ? "qedsd/TheGuideToTheNewEden" : settings.GiteeRepo;
        MaxPartBox.Text = settings.MaxPartMB is >= 10 and <= 99 ? settings.MaxPartMB.ToString() : "90";
        SkipPublishCheck.IsChecked = settings.SkipPublish;
        NotesBox.Text = settings.Notes;
    }

    private void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
            var settings = new PublisherSettings
            {
                Version = VersionBox.Text.Trim(),
                Tag = TagBox.Text.Trim(),
                OutputDir = OutputDirBox.Text.Trim(),
                GiteeBaseUrl = GiteeBaseUrlBox.Text.Trim(),
                FullName = FullNameBox.Text.Trim(),
                ExePath = ExePathBox.Text.Trim(),
                GitHubRepo = GitHubRepoBox.Text.Trim(),
                GiteeRepo = GiteeRepoBox.Text.Trim(),
                MaxPartMB = int.TryParse(MaxPartBox.Text.Trim(), out var max) ? max : 90,
                SkipPublish = SkipPublishCheck.IsChecked == true,
                Notes = NotesBox.Text,
            };
            File.WriteAllText(
                SettingsFile,
                JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        catch (Exception ex)
        {
            AppendLog($"[设置] 保存失败：{ex.Message}");
        }
    }

    // ==================================================================
    //  运行脚本
    // ==================================================================

    private async void OnPublishClick(object sender, RoutedEventArgs e)
    {
        await RunAsync(SkipPublishCheck.IsChecked == true);
    }

    private async void OnGenerateClick(object sender, RoutedEventArgs e)
    {
        await RunAsync(skipPublish: true);
    }

    private async Task RunAsync(bool skipPublish)
    {
        if (_running)
        {
            return;
        }

        if (!File.Exists(_scriptPath))
        {
            MessageBox.Show(
                this,
                "找不到 make-update-package.ps1。\n请把本工具放在仓库目录内运行，或从仓库根目录启动。",
                "更新发布工具",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(MaxPartBox.Text.Trim(), out var maxPart) || maxPart is < 10 or > 99)
        {
            MessageBox.Show(this, "分片大小必须是 10–99 之间的整数（Gitee 单附件上限 100 MB）。", "更新发布工具", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var customExe = ExePathBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(customExe))
        {
            // 指定了 exe 就不再编译，避免脚本先发布又把指定文件忽略掉。
            skipPublish = true;
        }

        SaveSettings();

        var tempDir = Path.Combine(Path.GetTempPath(), "TheGuideToTheNewEden.UpdatePublisher", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var notesPath = Path.Combine(tempDir, "notes.md");
        var wrapperPath = Path.Combine(tempDir, "run.ps1");

        try
        {
            var notes = NotesBox.Text ?? string.Empty;
            var hasNotes = !string.IsNullOrWhiteSpace(notes);

            var builder = new StringBuilder();
            builder.AppendLine("$ErrorActionPreference = 'Stop'");
            builder.AppendLine("[Console]::OutputEncoding = [System.Text.Encoding]::UTF8");
            builder.AppendLine("$OutputEncoding = [System.Text.Encoding]::UTF8");
            builder.Append("& ").Append(PsQuote(_scriptPath));

            AppendArg(builder, "-Version", VersionBox.Text.Trim());
            AppendArg(builder, "-Tag", TagBox.Text.Trim());
            AppendArg(builder, "-OutputDir", OutputDirBox.Text.Trim());
            AppendArg(builder, "-GiteeBaseUrl", GiteeBaseUrlBox.Text.Trim());
            AppendArg(builder, "-FullName", FullNameBox.Text.Trim());
            AppendArg(builder, "-GitHubRepo", GitHubRepoBox.Text.Trim());
            AppendArg(builder, "-GiteeRepo", GiteeRepoBox.Text.Trim());
            builder.Append(" -MaxPartMB ").Append(maxPart);

            if (skipPublish)
            {
                builder.Append(" -SkipPublish");
            }

            if (!string.IsNullOrWhiteSpace(customExe))
            {
                AppendArg(builder, "-ExePath", customExe);
            }

            if (hasNotes)
            {
                AppendArg(builder, "-NotesFile", notesPath);
            }

            builder.AppendLine();
            builder.AppendLine("exit $LASTEXITCODE");
            File.WriteAllText(wrapperPath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            if (hasNotes)
            {
                // 脚本按 UTF-8 读 notes；这里不写 BOM，避免脚本把 BOM 当成正文。
                File.WriteAllText(notesPath, notes, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }

            AppendLog("============================================================");
            AppendLog($"[启动] {DateTime.Now:HH:mm:ss}  {(skipPublish ? "仅生成（跳过编译）" : "一键发布并生成")}");
            AppendLog($"[脚本] {_scriptPath}");
            if (!string.IsNullOrWhiteSpace(VersionBox.Text))
            {
                AppendLog($"[版本] {VersionBox.Text.Trim()}");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = ResolvePowerShell(),
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{wrapperPath}\"",
                WorkingDirectory = _repoRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            _process.OutputDataReceived += (_, args) =>
            {
                if (args.Data is not null)
                {
                    AppendLog(args.Data);
                }
            };
            _process.ErrorDataReceived += (_, args) =>
            {
                if (args.Data is not null)
                {
                    AppendLog("! " + args.Data);
                }
            };

            SetRunningState(true);
            _cancelRequested = false;
            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            await _process.WaitForExitAsync();
            _process.WaitForExit();

            var exitCode = _process.ExitCode;
            if (_cancelRequested)
            {
                AppendLog("[结束] 已停止");
                StatusText.Text = "已停止";
            }
            else
            {
                AppendLog($"[结束] 退出码 {exitCode}");
                StatusText.Text = exitCode == 0 ? "完成" : $"失败（退出码 {exitCode}）";
            }
        }
        catch (Exception ex)
        {
            AppendLog($"! {ex}");
            StatusText.Text = "运行失败";
        }
        finally
        {
            SetRunningState(false);
            _process?.Dispose();
            _process = null;

            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch
            {
                // 临时目录清理失败不影响结果。
            }
        }
    }

    private static void AppendArg(StringBuilder builder, string name, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        builder.Append(' ').Append(name).Append(' ').Append(PsQuote(value));
    }

    private static string PsQuote(string value) => "'" + value.Replace("'", "''") + "'";

    private static string ResolvePowerShell()
    {
        var system = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var path = Path.Combine(system, "WindowsPowerShell", "v1.0", "powershell.exe");
        return File.Exists(path) ? path : "powershell.exe";
    }

    // ==================================================================
    //  按钮
    // ==================================================================

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_process is { HasExited: false })
            {
                _cancelRequested = true;
                AppendLog("[停止] 正在结束脚本进程…");
                StatusText.Text = "正在停止…";
                _process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            AppendLog($"! 停止失败：{ex.Message}");
        }
    }

    private void OnBrowseOutputDir(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择发布物输出目录",
            InitialDirectory = Directory.Exists(ResolveOutputDir()) ? ResolveOutputDir() : _repoRoot,
        };
        if (dialog.ShowDialog(this) == true)
        {
            OutputDirBox.Text = dialog.FolderName;
        }
    }

    private void OnBrowseExePath(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择要发布的 exe",
            Filter = "可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(this) == true)
        {
            ExePathBox.Text = dialog.FileName;
            SkipPublishCheck.IsChecked = true;
        }
    }

    private void OnImportNotes(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "导入更新内容",
            Filter = "Markdown / 文本 (*.md;*.txt)|*.md;*.txt|所有文件 (*.*)|*.*",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            NotesBox.Text = File.ReadAllText(dialog.FileName, Encoding.UTF8);
            StatusText.Text = $"已导入 {Path.GetFileName(dialog.FileName)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "导入失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnClearNotes(object sender, RoutedEventArgs e) => NotesBox.Clear();

    private void OnClearLog(object sender, RoutedEventArgs e) => LogBox.Clear();

    private void OnOpenOutput(object sender, RoutedEventArgs e)
    {
        var directory = ResolveOutputDir();
        if (!Directory.Exists(directory))
        {
            MessageBox.Show(this, $"目录还不存在：\n{directory}", "打开发布目录", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{directory}\"") { UseShellExecute = true });
    }

    private void OnOpenManifest(object sender, RoutedEventArgs e)
    {
        var path = Path.Combine(ResolveOutputDir(), "update.json");
        if (!File.Exists(path))
        {
            MessageBox.Show(this, $"还没有生成 update.json：\n{path}", "打开 update.json", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private string ResolveOutputDir()
    {
        var value = OutputDirBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return Path.Combine(_repoRoot, "update-package");
        }

        return Path.IsPathRooted(value) ? value : Path.GetFullPath(Path.Combine(_repoRoot, value));
    }

    // ==================================================================
    //  状态与日志
    // ==================================================================

    private void SetRunningState(bool running)
    {
        _running = running;
        PublishButton.IsEnabled = !running;
        GenerateButton.IsEnabled = !running;
        CancelButton.IsEnabled = running;
        StatusText.Text = running ? "运行中…" : StatusText.Text;
    }

    private void AppendLog(string text)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(() => AppendLog(text)));
            return;
        }

        LogBox.AppendText(text + Environment.NewLine);
        LogBox.ScrollToEnd();
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_running)
        {
            var result = MessageBox.Show(
                this,
                "脚本仍在运行，确定停止并退出吗？",
                "更新发布工具",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }

            try
            {
                _process?.Kill(entireProcessTree: true);
            }
            catch
            {
                // 退出时忽略。
            }
        }

        SaveSettings();
    }
}

public sealed class PublisherSettings
{
    public string Version { get; set; } = string.Empty;

    public string Tag { get; set; } = string.Empty;

    public string OutputDir { get; set; } = string.Empty;

    public string GiteeBaseUrl { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string ExePath { get; set; } = string.Empty;

    public string GitHubRepo { get; set; } = "qedsd/TheGuideToTheNewEden";

    public string GiteeRepo { get; set; } = "qedsd/TheGuideToTheNewEden";

    public int MaxPartMB { get; set; } = 90;

    public bool SkipPublish { get; set; }

    public string Notes { get; set; } = string.Empty;
}
