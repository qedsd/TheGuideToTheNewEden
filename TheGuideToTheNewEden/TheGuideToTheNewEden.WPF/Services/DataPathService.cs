using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using TheGuideToTheNewEden.WPF.Helpers;

namespace TheGuideToTheNewEden.WPF.Services;

/// <summary>数据目录相对"产品根目录"的位置。</summary>
public enum DataPathMode
{
    /// <summary>按大版本隔离（默认）：<c>%LocalAppData%\TheGuideToTheNewEden\V4</c>。</summary>
    Versioned = 0,

    /// <summary>沿用旧版本目录（与旧版本共用）：<c>%LocalAppData%\TheGuideToTheNewEden</c>。</summary>
    Legacy = 1,

    /// <summary>用户自选目录。</summary>
    Custom = 2,
}

/// <summary>
/// 用户数据目录（设置 / 令牌 / 缓存 / 各功能配置）的解析与迁移。
/// <para>
/// 背景（阶段 186）：4.0 起配置结构与旧版本不再保证兼容，而旧版本一直把数据直接放在
/// <c>%LocalAppData%\TheGuideToTheNewEden</c>。两个版本共用同一份配置时，
/// **新版本写过的文件旧版本可能读不了，新版本也可能读到旧版本留下的旧格式**，互相冲突。
/// 因此默认改为按大版本隔离到 <c>TheGuideToTheNewEden\V4</c>，
/// 并在首次运行时询问用户：使用新目录（可把旧配置复制过来）还是沿用旧目录。
/// </para>
/// <para>
/// 选择结果落在**根目录**的 <c>datapath.json</c>（不随数据目录走），所以任何模式下都能在
/// 启动最早期读到；改目录后需要重启才生效（<see cref="SettingsService.DataPath"/> 是启动期一次性解析的）。
/// </para>
/// </summary>
public static class DataPathService
{
    private const string ProductFolderName = "TheGuideToTheNewEden";
    private const string BootstrapFileName = "datapath.json";

    private static readonly object Sync = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static bool _initialized;

    /// <summary>产品根目录（旧版本的数据目录）：<c>%LocalAppData%\TheGuideToTheNewEden</c>。</summary>
    public static string RootPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        ProductFolderName);

    /// <summary>旧版本一直使用的数据目录（= 根目录）。</summary>
    public static string LegacyDataPath => RootPath;

    /// <summary>按大版本隔离时使用的子目录名，例如 <c>V4</c>。</summary>
    public static string VersionFolderName { get; } = ResolveVersionFolderName();

    /// <summary>默认数据目录：<c>%LocalAppData%\TheGuideToTheNewEden\V4</c>。</summary>
    public static string DefaultDataPath { get; } = Path.Combine(RootPath, VersionFolderName);

    /// <summary>选择结果文件：<c>%LocalAppData%\TheGuideToTheNewEden\datapath.json</c>。</summary>
    public static string BootstrapFile { get; } = Path.Combine(RootPath, BootstrapFileName);

    /// <summary>当前生效的数据目录（启动期解析一次，运行期不再改变）。</summary>
    public static string Current { get; private set; } = DefaultDataPath;

    /// <summary>当前目录的归属。</summary>
    public static DataPathMode Mode { get; private set; } = DataPathMode.Versioned;

    /// <summary>首次运行且检测到旧版本配置：需要用户先做选择（<see cref="ApplyFirstRunChoice"/>）。</summary>
    public static bool NeedsDecision { get; private set; }

    /// <summary>旧版本目录里是否有可沿用的配置。</summary>
    public static bool HasLegacyData { get; private set; }

    /// <summary>是否与旧版本共用同一目录（有冲突风险）。</summary>
    public static bool IsSharedWithLegacy => SamePath(Current, LegacyDataPath);

    /// <summary>是否使用按大版本隔离的默认目录。</summary>
    public static bool IsDefault => SamePath(Current, DefaultDataPath);

    /// <summary>当前目录的类型说明（语言键）。</summary>
    public static string ModeKey => ModeKeyFor(Mode);

    /// <summary>指定归属方式的说明（语言键）。</summary>
    public static string ModeKeyFor(DataPathMode mode) => mode switch
    {
        DataPathMode.Legacy => "Setting_DataPath_ModeLegacy",
        DataPathMode.Custom => "Setting_DataPath_ModeCustom",
        _ => "Setting_DataPath_ModeDefault",
    };

    /// <summary>
    /// 解析数据目录（幂等）。必须在 <see cref="SettingsService.Initialize"/> / <c>Core.Log.Init</c> 之前调用，
    /// 因为这些模块都会在启动期把 <see cref="Current"/> 固化进路径。
    /// </summary>
    public static void Initialize()
    {
        lock (Sync)
        {
            if (_initialized)
            {
                return;
            }
            _initialized = true;

            try
            {
                Directory.CreateDirectory(RootPath);
            }
            catch (Exception ex)
            {
                Core.Log.Warn($"创建数据根目录失败：{ex.Message}");
            }

            HasLegacyData = DetectLegacyData();

            var saved = ReadBootstrap();
            if (!string.IsNullOrWhiteSpace(saved))
            {
                ApplyPath(saved!);
                return;
            }

            Current = DefaultDataPath;
            Mode = DataPathMode.Versioned;

            if (HasLegacyData)
            {
                // 有旧配置但没有选择记录：先不落盘、不建目录，等用户在选择框里决定。
                NeedsDecision = true;
                return;
            }

            EnsureDirectory(Current);
            WriteBootstrap(Current, Mode);
        }
    }

    /// <summary>
    /// 首次运行选择框的结果：<paramref name="useLegacyFolder"/> = 沿用旧目录；否则使用新目录，
    /// 并按 <paramref name="copyLegacyConfig"/> 决定是否把旧配置/登录信息复制进新目录。
    /// </summary>
    public static void ApplyFirstRunChoice(bool useLegacyFolder, bool copyLegacyConfig)
    {
        lock (Sync)
        {
            if (useLegacyFolder)
            {
                SetPath(LegacyDataPath, DataPathMode.Legacy, copyFrom: null);
            }
            else
            {
                SetPath(DefaultDataPath, DataPathMode.Versioned,
                    copyFrom: copyLegacyConfig ? LegacyDataPath : null);
            }
            NeedsDecision = false;
        }
    }

    /// <summary>
    /// 在设置页切换数据目录（**只写选择记录，本次运行仍用旧目录**，下次启动生效）。
    /// <paramref name="copyCurrentConfig"/> = 把当前目录的配置与登录信息复制到新目录（缓存目录不复制）。
    /// </summary>
    public static bool SetDataPath(string path, bool copyCurrentConfig)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        lock (Sync)
        {
            var normalized = NormalizePath(path);
            if (string.IsNullOrEmpty(normalized))
            {
                return false;
            }

            var mode = SamePath(normalized, DefaultDataPath) ? DataPathMode.Versioned
                : SamePath(normalized, LegacyDataPath) ? DataPathMode.Legacy
                : DataPathMode.Custom;

            return SetPath(normalized, mode, copyCurrentConfig ? Current : null, applyNow: false);
        }
    }

    /// <summary>恢复默认目录（下次启动生效）。</summary>
    public static bool ResetToDefault(bool copyCurrentConfig)
        => SetDataPath(DefaultDataPath, copyCurrentConfig);

    /// <summary>某个目录相对当前运行目录的归属（用于设置页展示即将生效的模式）。</summary>
    public static DataPathMode GetModeFor(string path)
    {
        var normalized = NormalizePath(path);
        return SamePath(normalized, DefaultDataPath) ? DataPathMode.Versioned
            : SamePath(normalized, LegacyDataPath) ? DataPathMode.Legacy
            : DataPathMode.Custom;
    }

    /// <summary>当前写盘的选择记录（用于设置页显示"下次启动生效"的目录）；读不到时为 <see cref="Current"/>。</summary>
    public static string PendingDataPath => ReadBootstrap() ?? Current;

    /// <summary>把某个数据目录里的"配置与登录信息"复制到另一个目录（不含可重建的缓存）。</summary>
    public static bool CopyUserData(string sourcePath, string targetPath)
    {
        // 目标在源目录内部时不复制：边写边枚举会把新目录也当成待复制内容（甚至无限递归）
        var sourceRoot = NormalizePath(sourcePath);
        var targetRoot = NormalizePath(targetPath);
        if (string.IsNullOrEmpty(sourceRoot)
            || string.IsNullOrEmpty(targetRoot)
            || SamePath(sourceRoot, targetRoot)
            || targetRoot.StartsWith(sourceRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var configSource = Path.Combine(sourceRoot, "Configs");
            var configTarget = Path.Combine(targetRoot, "Configs");
            Directory.CreateDirectory(configTarget);

            if (Directory.Exists(configSource))
            {
                // 顶层文件：settings.json / Auth.json / 各功能配置
                foreach (var file in Directory.EnumerateFiles(configSource))
                {
                    var name = Path.GetFileName(file);
                    var destination = Path.Combine(configTarget, name);
                    if (File.Exists(destination))
                    {
                        continue;
                    }
                    File.Copy(file, destination, overwrite: false);
                }

                // 用户自己的小数据目录（购物记录 / 配置同步备份）；大缓存不复制
                foreach (var folder in new[] { "ShoppingRecords", ".backups" })
                {
                    CopyDirectory(Path.Combine(configSource, folder), Path.Combine(configTarget, folder));
                }
            }

            // EVE 客户端设置的备份（配置同步模块）
            CopyDirectory(Path.Combine(sourceRoot, "EveSettings"), Path.Combine(targetRoot, "EveSettings"));
            return true;
        }
        catch (Exception ex)
        {
            Core.Log.Warn($"复制旧配置失败：{ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 落盘目录选择；<paramref name="applyNow"/> = true 时同时切换本次运行的 <see cref="Current"/>
    /// （只有启动阶段的选择框这么做，设置页改动留到下次启动）。
    /// </summary>
    private static bool SetPath(string path, DataPathMode mode, string? copyFrom, bool applyNow = true)
    {
        var normalized = NormalizePath(path);
        if (string.IsNullOrEmpty(normalized))
        {
            return false;
        }

        if (!EnsureDirectory(normalized))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(copyFrom) && !SamePath(copyFrom, normalized))
        {
            CopyUserData(copyFrom!, normalized);
        }

        WriteBootstrap(normalized, mode);
        if (applyNow)
        {
            Current = normalized;
            Mode = mode;
        }
        Core.Log.Info($"数据目录已设置为：{normalized}（{mode}，{(applyNow ? "本次运行生效" : "下次启动生效")}）");
        return true;
    }

    private static void ApplyPath(string saved)
    {
        var normalized = NormalizePath(saved);
        if (string.IsNullOrEmpty(normalized))
        {
            normalized = DefaultDataPath;
        }

        // 启动阶段不复制数据：只确保目录存在；万一目录被删/不可写就退回默认目录
        if (!EnsureDirectory(normalized))
        {
            normalized = DefaultDataPath;
            EnsureDirectory(normalized);
        }

        Current = normalized;
        Mode = SamePath(normalized, DefaultDataPath) ? DataPathMode.Versioned
            : SamePath(normalized, LegacyDataPath) ? DataPathMode.Legacy
            : DataPathMode.Custom;
    }

    private static bool EnsureDirectory(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            return true;
        }
        catch (Exception ex)
        {
            Core.Log.Error($"数据目录不可用：{path}（{ex.Message}）");
            return false;
        }
    }

    /// <summary>旧版本是否留下了配置：根目录的 <c>Configs</c>（有内容）、<c>settings.wpf.json</c> 或根级 <c>Auth.json</c>。</summary>
    private static bool DetectLegacyData()
    {
        try
        {
            if (File.Exists(Path.Combine(RootPath, "settings.wpf.json"))
                || File.Exists(Path.Combine(RootPath, "Auth.json")))
            {
                return true;
            }

            var configs = Path.Combine(RootPath, "Configs");
            if (!Directory.Exists(configs))
            {
                return false;
            }

            return Directory.EnumerateFileSystemEntries(configs).Any();
        }
        catch (Exception ex)
        {
            Core.Log.Warn($"检测旧版本配置失败：{ex.Message}");
            return false;
        }
    }

    private static string? ReadBootstrap()
    {
        try
        {
            if (!File.Exists(BootstrapFile))
            {
                return null;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(BootstrapFile));
            return document.RootElement.TryGetProperty("DataPath", out var value)
                ? value.GetString()
                : null;
        }
        catch (Exception ex)
        {
            Core.Log.Warn($"读取数据目录选择失败：{ex.Message}");
            return null;
        }
    }

    private static void WriteBootstrap(string path, DataPathMode mode)
    {
        try
        {
            Directory.CreateDirectory(RootPath);
            var payload = new DataPathBootstrap
            {
                DataPath = path,
                Mode = mode.ToString(),
                Updated = DateTime.Now,
            };
            File.WriteAllText(BootstrapFile, JsonSerializer.Serialize(payload, JsonOptions));
        }
        catch (Exception ex)
        {
            Core.Log.Warn($"保存数据目录选择失败：{ex.Message}");
        }
    }

    /// <summary>把用户输入的路径规范化：去引号/空白，可展开环境变量；必须是绝对路径。</summary>
    private static string NormalizePath(string path)
    {
        var value = path.Trim().Trim('"');
        try
        {
            value = Environment.ExpandEnvironmentVariables(value);
        }
        catch
        {
            // 展开失败就按原样用
        }

        try
        {
            return Path.GetFullPath(value).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool SamePath(string? left, string? right)
        => !string.IsNullOrEmpty(left)
           && !string.IsNullOrEmpty(right)
           && string.Equals(
               left.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
               right.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
               StringComparison.OrdinalIgnoreCase);

    private static void CopyDirectory(string source, string target)
    {
        if (!Directory.Exists(source))
        {
            return;
        }

        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            var destination = Path.Combine(target, Path.GetFileName(file));
            if (File.Exists(destination))
            {
                continue;
            }
            File.Copy(file, destination, overwrite: false);
        }
        foreach (var folder in Directory.EnumerateDirectories(source))
        {
            CopyDirectory(folder, Path.Combine(target, Path.GetFileName(folder)));
        }
    }

    /// <summary>数据目录按"主版本号"隔离：4.0.x → <c>V4</c>。</summary>
    private static string ResolveVersionFolderName()
    {
        if (TryGetMajor(AppVersion.Current, out var major) || TryGetMajor(typeof(DataPathService).Assembly.GetName().Version?.ToString(), out major))
        {
            return "V" + major;
        }
        return "V0";
    }

    private static bool TryGetMajor(string? version, out int major)
    {
        major = 0;
        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        var text = version.Trim();
        var dot = text.IndexOf('.');
        if (dot > 0)
        {
            text = text[..dot];
        }
        return int.TryParse(text, out major) && major > 0;
    }

    private sealed class DataPathBootstrap
    {
        public string DataPath { get; set; } = string.Empty;
        public string Mode { get; set; } = string.Empty;
        public DateTime Updated { get; set; }
    }
}
