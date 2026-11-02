using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TheGuideToTheNewEden.WPF.Helpers;
using TheGuideToTheNewEden.WPF.Services.Settings;

namespace TheGuideToTheNewEden.WPF.Services.Update;

/// <summary>
/// 绿色版（单文件 exe）自更新服务，支持 GitHub / Gitee 多源与分片。
/// </summary>
/// <remarks>
/// <para>
/// 设计背景（阶段 262/263）：4.0 起程序以 <b>单个 exe</b> 发布（自包含 + 单文件 + 压缩，见
/// <c>win-x64-single-file.pubxml</c>），没有安装器、没有旁置 dll。更新必须由应用自己完成：
/// 下载新的单文件 exe、校验、替换当前 exe、重启。
/// </para>
/// <para>
/// 国内可达性（阶段 263）：GitHub 在部分网络下不可达，因此增加 **Gitee 镜像**。
/// Gitee 发行版单个附件上限 100 MB（免费/GVP 200 MB），而 exe 约 134 MB，
/// 所以 Gitee 源允许把 exe 切成多个 &lt;100 MB 的分片（<c>parts</c>），下载后按顺序拼接并校验整体 SHA-256。
/// 检查清单优先从 Gitee 仓库的 <c>update.json</c> 读取（小文件、国内快），失败再回退 GitHub。
/// </para>
/// <para>
/// 下载源是**有序列表**：清单里先列 Gitee 分片、再列 GitHub 单文件；某一条失败会自动尝试下一条。
/// 下载包放在数据目录 <c>Updates/&lt;version&gt;/</c>，可断点续传、可取消。
/// </para>
/// </remarks>
public static class UpdateService
{
    public const string Owner = "qedsd";
    public const string Repository = "TheGuideToTheNewEden";

    /// <summary>Gitee 镜像仓库（需与 GitHub 同名；若实际仓库不同，改这里即可）。</summary>
    public const string GiteeOwner = "qedsd";
    public const string GiteeRepository = "TheGuideToTheNewEden";

    /// <summary>发布方可以随单文件 exe 一起上传的更新清单（Gitee 仓库根目录也放一份）。</summary>
    public const string ManifestAssetName = "update.json";

    private const string GitHubApiUrl = $"https://api.github.com/repos/{Owner}/{Repository}/releases/latest";
    private const string GitHubLatestUrl = $"https://github.com/{Owner}/{Repository}/releases/latest";
    private const string GitHubReleaseDownloadUrl = $"https://github.com/{Owner}/{Repository}/releases/download";

    private static readonly HttpClient Http = CreateClient(followRedirects: true);
    private static readonly HttpClient HttpNoRedirect = CreateClient(followRedirects: false);

    private static readonly SemaphoreSlim CheckGate = new(1, 1);
    private static readonly object CacheSync = new();
    private static readonly Regex MarkdownBold = new(@"\*\*(.+?)\*\*", RegexOptions.Compiled);
    private static readonly Regex MarkdownLink = new(@"\[(.+?)\]\((.+?)\)", RegexOptions.Compiled);
    private static readonly Regex MarkdownInlineCode = new("`(.+?)`", RegexOptions.Compiled);
    private static readonly Regex PartSuffix = new(@"\.(part\d+|\d{3,4})$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static UpdateCheckResult? _cached;

    /// <summary>最近一次检查结果（含失败结果，供页面首次进入时直接显示）。</summary>
    public static UpdateCheckResult? Cached
    {
        get
        {
            lock (CacheSync)
            {
                return _cached;
            }
        }
    }

    /// <summary>自动检查的缓存有效期：有效期内重复打开页面 / 主页不再打接口。</summary>
    public static TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>失败结果的短暂缓存：离线/限流时避免每次导航都重试一轮网络请求。</summary>
    public static TimeSpan FailureCacheDuration { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>调试/探针用：非空时只读这一条清单地址（不参与正常发布）。</summary>
    public static string? ManifestUrlOverride { get; set; }

    /// <summary>当前进程实际运行的 exe 路径（单文件发布下是原始 exe，不是临时解压目录里的 apphost）。</summary>
    public static string? CurrentExecutablePath => Environment.ProcessPath;

    /// <summary>发布页地址（检查失败或没有可用资产时给用户手动下载）。</summary>
    public static string ReleasePageUrl => $"https://github.com/{Owner}/{Repository}/releases";

    /// <summary>Gitee 镜像仓库地址（国内手动下载入口）。</summary>
    public static string GiteeRepositoryUrl => $"https://gitee.com/{GiteeOwner}/{GiteeRepository}";

    private static bool IsCacheUsable(UpdateCheckResult? cached)
    {
        if (cached is null)
        {
            return false;
        }

        var age = DateTimeOffset.Now - cached.CheckedAt;
        return cached.Status == UpdateCheckStatus.Failed
            ? age < FailureCacheDuration
            : age < CacheDuration;
    }

    /// <summary>
    /// 清单地址，按优先级排列：Gitee 仓库根目录 → GitHub 最新发布资产。
    /// Gitee 的 raw 文件很小，国内可达性最好；失败会自然回退 GitHub。
    /// </summary>
    private static IEnumerable<string> GetManifestUrls()
    {
        if (!string.IsNullOrWhiteSpace(ManifestUrlOverride))
        {
            yield return ManifestUrlOverride!;
            yield break;
        }

        // 仓库默认分支可能是 master 或 main，两个都试；raw 文件不经过 Gitee API。
        yield return $"https://gitee.com/{GiteeOwner}/{GiteeRepository}/raw/master/{ManifestAssetName}";
        yield return $"https://gitee.com/{GiteeOwner}/{GiteeRepository}/raw/main/{ManifestAssetName}";
        yield return $"{GitHubLatestUrl}/download/{ManifestAssetName}";
        yield return $"{GitHubLatestUrl}/download/latest.json";
    }

    // ==================================================================
    //  检查
    // ==================================================================

    /// <summary>
    /// 检查更新。<paramref name="force"/> = false 时优先返回 <see cref="CacheDuration"/> 内的缓存，
    /// 避免主页与本页在几分钟内各打一次远程（GitHub 匿名 API 每小时只有 60 次）。
    /// </summary>
    public static async Task<UpdateCheckResult> CheckAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        var current = AppVersion.Current;

        if (!force && IsCacheUsable(Cached))
        {
            return Cached!;
        }

        await CheckGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!force && IsCacheUsable(Cached))
            {
                return Cached!;
            }

            UpdateCheckResult result;
            try
            {
                result = await FetchLatestAsync(current, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Core.Log.Warn($"[更新] 检查失败：{ex.Message}");
                result = UpdateCheckResult.Failed(current, new UpdateError(ClassifyNetworkError(ex), ex.Message));
            }

            lock (CacheSync)
            {
                // 失败结果也存，页面能显示"上次检查失败"；但下一次非强制调用会在 FailureCacheDuration 后重试。
                _cached = result;
            }

            LogResult(result);
            return result;
        }
        finally
        {
            CheckGate.Release();
        }
    }

    private static async Task<UpdateCheckResult> FetchLatestAsync(string current, CancellationToken cancellationToken)
    {
        var errors = new List<UpdateError>();

        // ① 清单（Gitee 优先）：体积小、可携带分片列表 / SHA-256 / 说明，且不消耗 GitHub API 配额。
        foreach (var manifestUrl in GetManifestUrls())
        {
            var info = await TryGetManifestAsync(manifestUrl, cancellationToken).ConfigureAwait(false);
            if (info is not null)
            {
                return BuildResult(info, current);
            }
        }

        // ② GitHub API：能拿到资产列表与 digest，但匿名只有 60 次/小时。
        var apiResult = await TryGetFromApiAsync(cancellationToken).ConfigureAwait(false);
        if (apiResult.Info is not null)
        {
            return BuildResult(apiResult.Info, current);
        }

        if (apiResult.Error is not null)
        {
            errors.Add(apiResult.Error);
        }

        // ③ releases/latest 重定向 + 约定文件名探测：完全绕开 API，仅在限流时兜底。
        var redirectInfo = await TryGetFromLatestRedirectAsync(cancellationToken).ConfigureAwait(false);
        if (redirectInfo is not null)
        {
            return BuildResult(redirectInfo, current);
        }

        var error = errors.FirstOrDefault(e => e.Kind != UpdateErrorKind.NoAsset)
                    ?? errors.FirstOrDefault()
                    ?? new UpdateError(UpdateErrorKind.Network, "无法获取任何版本信息");
        return UpdateCheckResult.Failed(current, error);
    }

    private static UpdateCheckResult BuildResult(UpdateInfo info, string current)
    {
        if (!SemanticVersion.TryParse(info.Version, out _))
        {
            return UpdateCheckResult.Failed(current, new UpdateError(UpdateErrorKind.BadManifest, $"版本号无法解析：{info.Version}"));
        }

        if (!SemanticVersion.IsNewer(info.Version, current))
        {
            return UpdateCheckResult.UpToDate(current);
        }

        return info.HasAsset
            ? UpdateCheckResult.Available(info, current)
            : UpdateCheckResult.NoAsset(info, current);
    }

    private static async Task<UpdateInfo?> TryGetManifestAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var timeout = CreateTimeout(cancellationToken, TimeSpan.FromSeconds(20));
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
            using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            return ParseManifest(json, url);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Core.Log.Info($"[更新] 读取清单失败（{url}）：{ex.Message}");
            return null;
        }
    }

    private static UpdateInfo? ParseManifest(string json, string manifestUrl)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var versionText = GetString(root, "version", "tag", "tag_name", "name");
            if (string.IsNullOrWhiteSpace(versionText))
            {
                return null;
            }

            var version = NormalizeTag(versionText);
            var tag = GetString(root, "tag", "tag_name") ?? $"v{version}";
            var fileName = GetString(root, "file", "fileName", "filename");
            var size = GetInt64(root, "size", "length", "bytes") ?? 0;
            var sha = NormalizeSha(GetString(root, "sha256", "hash", "digest"));
            var notes = GetString(root, "notes", "body", "changelog", "releaseNotes");
            var published = GetDateTimeOffset(root, "publishedAt", "published_at", "date");
            var releasePage = GetString(root, "releasePage", "html_url")
                              ?? $"https://github.com/{Owner}/{Repository}/releases/tag/{Uri.EscapeDataString(tag)}";

            var sources = ParseSources(root, fileName, size, sha);
            if (sources.Count == 0)
            {
                return new UpdateInfo
                {
                    Version = version,
                    Tag = tag,
                    FileName = ResolveFileName(fileName, null),
                    Size = size,
                    Sha256 = sha,
                    Notes = notes,
                    PublishedAt = published,
                    Sources = Array.Empty<UpdateDownloadSource>(),
                    ReleasePageUrl = releasePage,
                    Source = manifestUrl,
                };
            }

            var resolvedName = ResolveFileName(fileName, sources[0].Parts[0].Url);
            if (size <= 0)
            {
                size = sources[0].KnownSize;
            }

            if (sha is null && sources.Count == 1 && sources[0].Parts.Count == 1)
            {
                sha = sources[0].Parts[0].Sha256;
            }

            return new UpdateInfo
            {
                Version = version,
                Tag = tag,
                FileName = resolvedName,
                Size = size,
                Sha256 = sha,
                Notes = notes,
                PublishedAt = published,
                Sources = sources,
                ReleasePageUrl = releasePage,
                Source = manifestUrl,
            };
        }
        catch (JsonException ex)
        {
            Core.Log.Info($"[更新] 清单不是合法 JSON（{manifestUrl}）：{ex.Message}");
            return null;
        }
    }

    /// <summary>解析 sources 数组；兼容只有顶层 url/urls/parts 的旧清单。</summary>
    private static List<UpdateDownloadSource> ParseSources(JsonElement root, string? fileName, long size, string? sha)
    {
        var result = new List<UpdateDownloadSource>();

        if (root.TryGetProperty("sources", out var sourcesElement) && sourcesElement.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var sourceElement in sourcesElement.EnumerateArray())
            {
                if (sourceElement.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                index++;
                var name = GetString(sourceElement, "name", "label", "mirror") ?? $"源 {index}";
                result.AddRange(ParseSourceElement(sourceElement, name, fileName, size, sha));
            }
        }

        if (result.Count == 0)
        {
            // 旧格式：顶层 url / urls / file，一条源。
            result.AddRange(ParseSourceElement(root, "下载源", fileName, size, sha));
        }

        return result;
    }

    private static IEnumerable<UpdateDownloadSource> ParseSourceElement(
        JsonElement element,
        string name,
        string? fileName,
        long size,
        string? sha)
    {
        if (element.TryGetProperty("parts", out var partsElement) && partsElement.ValueKind == JsonValueKind.Array)
        {
            var parts = new List<UpdateFilePart>();
            foreach (var partElement in partsElement.EnumerateArray())
            {
                var url = GetString(partElement, "url", "downloadUrl", "browser_download_url")
                          ?? GetStringArray(partElement, "urls").FirstOrDefault();
                if (string.IsNullOrWhiteSpace(url))
                {
                    continue;
                }

                parts.Add(new UpdateFilePart
                {
                    Name = GetString(partElement, "name", "file", "filename") ?? ResolveFileName(fileName, url),
                    Url = url!,
                    Size = GetInt64(partElement, "size", "length", "bytes") ?? 0,
                    Sha256 = NormalizeSha(GetString(partElement, "sha256", "hash", "digest")),
                });
            }

            if (parts.Count > 0)
            {
                yield return new UpdateDownloadSource { Name = name, Parts = parts };
            }

            yield break;
        }

        var urls = GetStringArray(element, "urls", "url", "downloadUrl", "browser_download_url");
        for (var i = 0; i < urls.Count; i++)
        {
            var suffix = urls.Count > 1 ? $" #{i + 1}" : string.Empty;
            yield return new UpdateDownloadSource
            {
                Name = name + suffix,
                Parts =
                [
                    new UpdateFilePart
                    {
                        Name = ResolveFileName(fileName, urls[i]),
                        Url = urls[i],
                        Size = size,
                        Sha256 = sha,
                    },
                ],
            };
        }
    }

    private static string ResolveFileName(string? fileName, string? url)
    {
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            return SanitizeFileName(fileName!);
        }

        if (!string.IsNullOrWhiteSpace(url)
            && Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var name = Uri.UnescapeDataString(Path.GetFileName(uri.AbsolutePath));
            name = PartSuffix.Replace(name, string.Empty);
            if (!string.IsNullOrWhiteSpace(name))
            {
                return SanitizeFileName(name);
            }
        }

        return "TheGuideToTheNewEden.exe";
    }

    private static async Task<(UpdateInfo? Info, UpdateError? Error)> TryGetFromApiAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timeout = CreateTimeout(cancellationToken, TimeSpan.FromSeconds(25));
            using var request = new HttpRequestMessage(HttpMethod.Get, GitHubApiUrl);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                .ConfigureAwait(false);

            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
            {
                var remaining = response.Headers.TryGetValues("X-RateLimit-Remaining", out var values)
                    ? values.FirstOrDefault()
                    : null;
                return (null, new UpdateError(UpdateErrorKind.RateLimited, $"GitHub API 限流（remaining={remaining ?? "?"}）"));
            }

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var tag = GetString(root, "tag_name") ?? GetString(root, "name");
            if (string.IsNullOrWhiteSpace(tag))
            {
                return (null, new UpdateError(UpdateErrorKind.BadManifest, "发布缺少 tag_name"));
            }

            var version = NormalizeTag(tag!);
            var notes = GetString(root, "body");
            var published = GetDateTimeOffset(root, "published_at");
            var htmlUrl = GetString(root, "html_url") ?? ReleasePageUrl;

            string? bestName = null;
            string? bestUrl = null;
            long bestSize = 0;
            string? bestSha = null;
            var bestScore = int.MinValue;

            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = GetString(asset, "name");
                    var url = GetString(asset, "browser_download_url");
                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url))
                    {
                        continue;
                    }

                    var size = GetInt64(asset, "size") ?? 0;
                    var score = ScoreAsset(name!, size);
                    if (score <= bestScore)
                    {
                        continue;
                    }

                    bestScore = score;
                    bestName = name;
                    bestUrl = url;
                    bestSize = size;
                    bestSha = NormalizeSha(GetString(asset, "digest"));
                }
            }

            var sources = new List<UpdateDownloadSource>();
            if (bestUrl is not null)
            {
                sources.Add(new UpdateDownloadSource
                {
                    Name = "GitHub",
                    Parts =
                    [
                        new UpdateFilePart
                        {
                            Name = bestName ?? "TheGuideToTheNewEden.exe",
                            Url = bestUrl,
                            Size = bestSize,
                            Sha256 = bestSha,
                        },
                    ],
                });
            }

            return (new UpdateInfo
            {
                Version = version,
                Tag = tag,
                FileName = ResolveFileName(bestName, bestUrl),
                Size = bestSize,
                Sha256 = bestSha,
                Notes = notes,
                PublishedAt = published,
                Sources = sources,
                ReleasePageUrl = htmlUrl,
                Source = "GitHub API",
            }, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return (null, new UpdateError(ClassifyNetworkError(ex), ex.Message));
        }
    }

    private static async Task<UpdateInfo?> TryGetFromLatestRedirectAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timeout = CreateTimeout(cancellationToken, TimeSpan.FromSeconds(20));
            using var request = new HttpRequestMessage(HttpMethod.Get, GitHubLatestUrl);
            using var response = await HttpNoRedirect.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                .ConfigureAwait(false);

            var location = response.Headers.Location;
            if (location is null)
            {
                return null;
            }

            var tag = location.Segments.LastOrDefault()?.Trim('/');
            if (string.IsNullOrWhiteSpace(tag))
            {
                return null;
            }

            tag = Uri.UnescapeDataString(tag!);
            var version = NormalizeTag(tag);

            // 没有资产列表，只能按发布惯例探测文件名（每个候选只发 2 字节的 Range 请求）。
            var candidates = new List<string>
            {
                "TheGuideToTheNewEden.exe",
                $"TheGuideToTheNewEden_{version}.exe",
                $"TheGuideToTheNewEden-v{version}.exe",
                "TheGuideToTheNewEden_win-x64.exe",
                "TheGuideToTheNewEden_win-x64_single.exe",
                "TheGuideToTheNewEden_single.exe",
            };

            foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var url = $"{GitHubReleaseDownloadUrl}/{Uri.EscapeDataString(tag)}/{Uri.EscapeDataString(candidate)}";
                var size = await ProbeAssetSizeAsync(url, timeout.Token).ConfigureAwait(false);
                if (size > 0 || size == -1)
                {
                    return new UpdateInfo
                    {
                        Version = version,
                        Tag = tag,
                        FileName = candidate,
                        Size = Math.Max(0, size),
                        Sources =
                        [
                            new UpdateDownloadSource
                            {
                                Name = "GitHub",
                                Parts =
                                [
                                    new UpdateFilePart
                                    {
                                        Name = candidate,
                                        Url = url,
                                        Size = Math.Max(0, size),
                                    },
                                ],
                            },
                        ],
                        ReleasePageUrl = $"{ReleasePageUrl}/tag/{Uri.EscapeDataString(tag)}",
                        Source = "releases/latest 重定向",
                    };
                }
            }

            return new UpdateInfo
            {
                Version = version,
                Tag = tag,
                FileName = "TheGuideToTheNewEden.exe",
                Sources = Array.Empty<UpdateDownloadSource>(),
                ReleasePageUrl = $"{ReleasePageUrl}/tag/{Uri.EscapeDataString(tag)}",
                Source = "releases/latest 重定向",
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Core.Log.Info($"[更新] 读取 releases/latest 失败：{ex.Message}");
            return null;
        }
    }

    /// <summary>用 Range 请求探测资产：返回字节数；未知返回 -1；不存在返回 0。</summary>
    private static async Task<long> ProbeAssetSizeAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Range = new RangeHeaderValue(0, 0);
            using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return 0;
            }

            if (response.Content.Headers.ContentRange?.Length is { } length)
            {
                return length;
            }

            return response.Content.Headers.ContentLength is > 0 and <= 2
                ? -1
                : response.Content.Headers.ContentLength ?? -1;
        }
        catch
        {
            return 0;
        }
    }

    private static int ScoreAsset(string name, long size)
    {
        var lower = name.ToLowerInvariant();
        if (!lower.EndsWith(".exe", StringComparison.Ordinal))
        {
            return int.MinValue;
        }

        string[] rejects = ["winui", "setup", "installer", "install", "symbols", "x86", "arm64", "arm32", "portable_zip"];
        if (rejects.Any(lower.Contains))
        {
            return int.MinValue;
        }

        var score = 0;
        if (lower == "theguidetotheneweden.exe")
        {
            score += 1000;
        }

        if (lower.Contains("single"))
        {
            score += 300;
        }

        if (lower.Contains("win-x64") || lower.Contains("win64"))
        {
            score += 200;
        }

        if (lower.Contains("x64"))
        {
            score += 100;
        }

        // 单文件 exe 有 100 MB 以上；同分时优先大文件，避免选到启动器/引导器。
        score += (int)Math.Clamp(size / 1_000_000, 0, 200);
        return score;
    }

    // ==================================================================
    //  下载
    // ==================================================================

    /// <summary>更新包下载目录（放在数据目录下，纯绿色运行不污染 exe 所在目录）。</summary>
    public static string GetDownloadFolder(UpdateInfo info)
        => Path.Combine(SettingsService.DataPath, "Updates", SanitizeFileName(info.Version));

    private static string GetFinalPath(UpdateInfo info)
        => Path.Combine(GetDownloadFolder(info), SanitizeFileName(info.FileName));

    /// <summary>
    /// 下载更新包（支持多源回退、分片拼接、断点续传）。返回本地 exe 路径；失败抛 <see cref="UpdateException"/>。
    /// </summary>
    public static async Task<string> DownloadAsync(
        UpdateInfo info,
        IProgress<UpdateDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!info.HasAsset)
        {
            throw new UpdateException(UpdateErrorKind.NoAsset, "该发布没有可用的下载源");
        }

        var folder = GetDownloadFolder(info);
        Directory.CreateDirectory(folder);
        var finalPath = GetFinalPath(info);

        if (File.Exists(finalPath))
        {
            if (await ValidateFileAsync(finalPath, info, cancellationToken).ConfigureAwait(false))
            {
                progress?.Report(new UpdateDownloadProgress(new FileInfo(finalPath).Length, new FileInfo(finalPath).Length, 0));
                return finalPath;
            }

            TryDelete(finalPath);
        }

        EnsureFreeSpace(folder, info.Size > 0 ? info.Size : info.Sources.Max(s => s.KnownSize));

        UpdateException? lastError = null;

        for (var sourceIndex = 0; sourceIndex < info.Sources.Count; sourceIndex++)
        {
            var source = info.Sources[sourceIndex];
            var partPaths = new List<string>();
            var total = info.Size > 0 ? info.Size : source.KnownSize;

            try
            {
                var receivedBase = 0L;
                for (var partIndex = 0; partIndex < source.Parts.Count; partIndex++)
                {
                    var part = source.Parts[partIndex];
                    var partPath = Path.Combine(
                        folder,
                        $"{SanitizeFileName(info.FileName)}.s{sourceIndex}.p{partIndex}.part");

                    await DownloadPartAsync(part, partPath, receivedBase, total, source.Name, progress, cancellationToken)
                        .ConfigureAwait(false);

                    partPaths.Add(partPath);
                    receivedBase += new FileInfo(partPath).Length;
                }

                var assembled = await AssembleAsync(partPaths, folder, info.FileName, cancellationToken).ConfigureAwait(false);

                if (!await ValidateFileAsync(assembled, info, cancellationToken).ConfigureAwait(false))
                {
                    TryDelete(assembled);
                    throw new UpdateException(
                        info.Sha256 is not null ? UpdateErrorKind.HashMismatch : UpdateErrorKind.InvalidFile,
                        "下载的 exe 未通过校验");
                }

                if (!string.Equals(assembled, finalPath, StringComparison.OrdinalIgnoreCase))
                {
                    File.Move(assembled, finalPath, overwrite: true);
                }

                foreach (var partPath in partPaths)
                {
                    TryDelete(partPath);
                }

                return finalPath;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (UpdateException ex)
            {
                lastError = ex;
                Core.Log.Warn($"[更新] 下载源 {source.Name} 失败：{ex.Message}；尝试下一个源");
            }
            catch (Exception ex)
            {
                lastError = new UpdateException(UpdateErrorKind.DownloadFailed, ex.Message, ex);
                Core.Log.Warn($"[更新] 下载源 {source.Name} 异常：{ex.Message}；尝试下一个源");
            }
        }

        throw lastError ?? new UpdateException(UpdateErrorKind.DownloadFailed, "所有下载源都失败了");
    }

    private static async Task DownloadPartAsync(
        UpdateFilePart part,
        string partPath,
        long receivedBase,
        long total,
        string sourceName,
        IProgress<UpdateDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(part.Url) || !Uri.TryCreate(part.Url, UriKind.Absolute, out var uri))
        {
            throw new UpdateException(UpdateErrorKind.NoAsset, $"下载地址无效：{part.Url}");
        }

        var existing = File.Exists(partPath) ? new FileInfo(partPath).Length : 0;
        if (part.Size > 0 && existing > part.Size)
        {
            TryDelete(partPath);
            existing = 0;
        }

        if (existing > 0 && part.Size > 0 && existing == part.Size)
        {
            if (await ValidatePartAsync(partPath, part, cancellationToken).ConfigureAwait(false))
            {
                progress?.Report(new UpdateDownloadProgress(receivedBase + existing, total, 0, sourceName));
                return;
            }

            TryDelete(partPath);
            existing = 0;
        }

        using var timeout = CreateTimeout(cancellationToken, TimeSpan.FromMinutes(30));
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        if (existing > 0)
        {
            request.Headers.Range = new RangeHeaderValue(existing, null);
        }

        using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
            .ConfigureAwait(false);

        if (existing > 0 && response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            TryDelete(partPath);
            await DownloadPartAsync(part, partPath, receivedBase, total, sourceName, progress, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new UpdateException(UpdateErrorKind.DownloadFailed, $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        var append = existing > 0 && response.StatusCode == HttpStatusCode.PartialContent;

        await using var source = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
        await using var destination = new FileStream(
            partPath,
            append ? FileMode.Append : FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            81920,
            useAsync: true);

        var buffer = new byte[81920];
        var sessionBytes = 0L;
        var partReceived = append ? existing : 0;
        var stopwatch = Stopwatch.StartNew();
        var lastReport = TimeSpan.Zero;
        progress?.Report(new UpdateDownloadProgress(receivedBase + partReceived, total, 0, sourceName));

        while (true)
        {
            var read = await source.ReadAsync(buffer.AsMemory(), timeout.Token).ConfigureAwait(false);
            if (read <= 0)
            {
                break;
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), timeout.Token).ConfigureAwait(false);
            partReceived += read;
            sessionBytes += read;

            if (stopwatch.Elapsed - lastReport >= TimeSpan.FromMilliseconds(120))
            {
                lastReport = stopwatch.Elapsed;
                var speed = stopwatch.Elapsed.TotalSeconds > 0 ? sessionBytes / stopwatch.Elapsed.TotalSeconds : 0;
                progress?.Report(new UpdateDownloadProgress(receivedBase + partReceived, total, speed, sourceName));
            }
        }

        await destination.FlushAsync(timeout.Token).ConfigureAwait(false);
        // 校验前必须先释放写入句柄：FileShare.None 会让后面的 File.OpenRead 直接报"文件被占用"。
        await destination.DisposeAsync().ConfigureAwait(false);

        var finalSpeed = stopwatch.Elapsed.TotalSeconds > 0 ? sessionBytes / stopwatch.Elapsed.TotalSeconds : 0;
        progress?.Report(new UpdateDownloadProgress(receivedBase + partReceived, total, finalSpeed, sourceName));

        if (!await ValidatePartAsync(partPath, part, cancellationToken).ConfigureAwait(false))
        {
            var actualLength = File.Exists(partPath) ? new FileInfo(partPath).Length : -1;
            var actualSha = string.Empty;
            if (File.Exists(partPath))
            {
                await using var debugStream = File.OpenRead(partPath);
                actualSha = Convert.ToHexString(await SHA256.HashDataAsync(debugStream, cancellationToken).ConfigureAwait(false)).ToLowerInvariant();
            }

            Core.Log.Warn(
                $"[更新] 分片校验失败 {part.Name}：长度 {actualLength}/{part.Size}，SHA {actualSha}/{part.Sha256 ?? "(未提供)"}");
            TryDelete(partPath);
            throw new UpdateException(
                part.Sha256 is not null ? UpdateErrorKind.HashMismatch : UpdateErrorKind.SizeMismatch,
                $"分片校验失败：{part.Name}");
        }
    }

    private static async Task<bool> ValidatePartAsync(string path, UpdateFilePart part, CancellationToken cancellationToken)
    {
        try
        {
            var length = new FileInfo(path).Length;
            if (part.Size > 0 && length != part.Size)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(part.Sha256))
            {
                await using var stream = File.OpenRead(path);
                var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
                return string.Equals(Convert.ToHexString(hash), part.Sha256, StringComparison.OrdinalIgnoreCase);
            }

            return length > 0;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<string> AssembleAsync(
        IReadOnlyList<string> parts,
        string folder,
        string fileName,
        CancellationToken cancellationToken)
    {
        if (parts.Count == 1)
        {
            return parts[0];
        }

        var assembled = Path.Combine(folder, SanitizeFileName(fileName) + ".assembling");
        TryDelete(assembled);

        await using (var output = new FileStream(assembled, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            foreach (var part in parts)
            {
                await using var input = new FileStream(part, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
                await input.CopyToAsync(output, 81920, cancellationToken).ConfigureAwait(false);
            }
        }

        return assembled;
    }

    /// <summary>校验完整 exe：大小（若已知）、SHA-256（若提供）、PE 头。</summary>
    public static async Task<bool> ValidateFileAsync(string path, UpdateInfo info, CancellationToken cancellationToken = default)
    {
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists || file.Length <= 0)
            {
                return false;
            }

            if (info.Size > 0 && file.Length != info.Size)
            {
                Core.Log.Warn($"[更新] 文件大小不匹配：期望 {info.Size}，实际 {file.Length}");
                return false;
            }

            if (!await HasPortableExeHeaderAsync(path, cancellationToken).ConfigureAwait(false))
            {
                Core.Log.Warn("[更新] 文件不是有效的 Windows exe（缺少 MZ 头）");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(info.Sha256))
            {
                await using var stream = File.OpenRead(path);
                var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
                var actual = Convert.ToHexString(hash).ToLowerInvariant();
                if (!string.Equals(actual, info.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    Core.Log.Warn($"[更新] SHA-256 不匹配：期望 {info.Sha256}，实际 {actual}");
                    return false;
                }
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Core.Log.Warn($"[更新] 校验文件失败：{ex.Message}");
            return false;
        }
    }

    private static async Task<bool> HasPortableExeHeaderAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        var header = new byte[2];
        var read = await stream.ReadAsync(header.AsMemory(), cancellationToken).ConfigureAwait(false);
        return read == 2 && header[0] == (byte)'M' && header[1] == (byte)'Z';
    }

    /// <summary>如果该版本已经下载并通过大小检查，返回本地路径；完整校验在安装前再做一次。</summary>
    public static bool TryGetDownloaded(UpdateInfo info, out string? path)
    {
        path = null;
        if (!info.HasAsset)
        {
            return false;
        }

        var candidate = GetFinalPath(info);
        if (!File.Exists(candidate))
        {
            return false;
        }

        try
        {
            var length = new FileInfo(candidate).Length;
            if (info.Size > 0 && length != info.Size)
            {
                return false;
            }

            path = candidate;
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ==================================================================
    //  安装（替换当前 exe）
    // ==================================================================

    /// <summary>
    /// 用已下载的 exe 替换当前正在运行的程序。调用方在返回 <see cref="UpdateRelaunchMode.ByApp"/> 后
    /// 应调用 <c>App.RequestRestartAfterUpdate()</c>；返回 <see cref="UpdateRelaunchMode.ByHelper"/> 时直接退出进程即可，
    /// 由辅助脚本等待退出后完成替换并拉起新版本。
    /// </summary>
    public static async Task<UpdateInstallResult> InstallAsync(
        UpdateInfo info,
        string downloadedFile,
        CancellationToken cancellationToken = default)
    {
        if (!info.HasAsset)
        {
            throw new UpdateException(UpdateErrorKind.NoAsset, "该发布没有可用的下载源");
        }

        if (!File.Exists(downloadedFile))
        {
            throw new UpdateException(UpdateErrorKind.InvalidFile, $"更新包不存在：{downloadedFile}");
        }

        if (!await ValidateFileAsync(downloadedFile, info, cancellationToken).ConfigureAwait(false))
        {
            throw new UpdateException(UpdateErrorKind.InvalidFile, "更新包未通过校验");
        }

        var current = CurrentExecutablePath;
        if (string.IsNullOrWhiteSpace(current) || !File.Exists(current))
        {
            throw new UpdateException(UpdateErrorKind.UnsupportedHost, "取不到当前 exe 路径");
        }

        if (!string.Equals(Path.GetExtension(current), ".exe", StringComparison.OrdinalIgnoreCase))
        {
            throw new UpdateException(UpdateErrorKind.UnsupportedHost, $"当前进程不是 exe：{current}");
        }

        // 开发/调试时通常由 dotnet.exe 宿主启动，不能把宿主替换掉。
        if (string.Equals(Path.GetFileNameWithoutExtension(current), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            throw new UpdateException(UpdateErrorKind.UnsupportedHost, "当前由 dotnet 宿主启动，不支持自动替换");
        }

        var exeFolder = Path.GetDirectoryName(current)!;
        var staging = Path.Combine(exeFolder, ".update");
        try
        {
            Directory.CreateDirectory(staging);
            ProbeWritable(staging);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new UpdateException(
                UpdateErrorKind.NotWritable,
                $"程序目录不可写：{exeFolder}（请放到用户可写目录，或手动替换 exe）",
                ex);
        }

        var staged = Path.Combine(staging, $"new-{Guid.NewGuid():N}.exe");
        var backup = Path.Combine(staging, $"old-{SanitizeFileName(info.Version)}-{DateTime.Now:yyyyMMddHHmmss}.exe");

        try
        {
            File.Copy(downloadedFile, staged, overwrite: true);
            if (new FileInfo(staged).Length != new FileInfo(downloadedFile).Length)
            {
                throw new IOException("复制到程序目录后大小不一致");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryDelete(staged);
            throw new UpdateException(UpdateErrorKind.NotWritable, $"无法写入程序目录：{ex.Message}", ex);
        }

        // 正在运行的 exe 允许改名、不允许覆盖。先改名备份，再把新文件放到原路径。
        try
        {
            File.Move(current, backup);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Core.Log.Info($"[更新] 就地改名失败，改用辅助脚本：{ex.Message}");
            return new UpdateInstallResult
            {
                Mode = UpdateRelaunchMode.ByHelper,
                BackupPath = CreateReplaceScript(current, staged, backup, info.Version),
            };
        }

        try
        {
            File.Move(staged, current);
        }
        catch (Exception ex)
        {
            // 换不上就把旧 exe 放回去，保证用户至少还能启动。
            try
            {
                File.Move(backup, current);
            }
            catch (Exception rollback)
            {
                Core.Log.Error($"[更新] 回滚失败：{rollback}");
                try
                {
                    File.Copy(backup, current, overwrite: true);
                }
                catch (Exception copy)
                {
                    Core.Log.Error($"[更新] 回滚复制也失败，旧 exe 仍在 {backup}：{copy.Message}");
                }
            }

            TryDelete(staged);
            throw new UpdateException(UpdateErrorKind.ReplaceFailed, $"替换 exe 失败：{ex.Message}", ex);
        }

        Core.Log.Info($"[更新] 已替换 {current}（旧文件备份：{backup}），准备重启");
        return new UpdateInstallResult { Mode = UpdateRelaunchMode.ByApp, BackupPath = backup };
    }

    /// <summary>
    /// 兜底：写一个等待本进程退出的脚本（改名/换文件/拉起新进程）。
    /// 返回备份路径（仅用于日志）。
    /// </summary>
    private static string CreateReplaceScript(string current, string staged, string backup, string version)
    {
        var folder = Path.GetDirectoryName(staged)!;
        var script = Path.Combine(folder, "apply-update.cmd");
        var pid = Environment.ProcessId;
        var batchCurrent = current.Replace("%", "%%");
        var batchStaged = staged.Replace("%", "%%");
        var batchBackup = backup.Replace("%", "%%");

        var content = new StringBuilder()
            .AppendLine("@echo off")
            .AppendLine("setlocal")
            .AppendLine(":wait")
            .AppendLine($"tasklist /FI \"PID eq {pid}\" 2>nul | find \"{pid}\" >nul")
            .AppendLine("if not errorlevel 1 (")
            .AppendLine("  timeout /t 1 /nobreak >nul")
            .AppendLine("  goto wait")
            .AppendLine(")")
            .AppendLine("set tries=0")
            .AppendLine(":replace")
            .AppendLine($"move /Y \"{batchCurrent}\" \"{batchBackup}\" >nul 2>&1")
            .AppendLine($"move /Y \"{batchStaged}\" \"{batchCurrent}\" >nul 2>&1")
            .AppendLine($"if exist \"{batchCurrent}\" goto start")
            .AppendLine("set /a tries+=1")
            .AppendLine("if %tries% GEQ 15 goto rollback")
            .AppendLine("timeout /t 1 /nobreak >nul")
            .AppendLine("goto replace")
            .AppendLine(":rollback")
            .AppendLine($"if not exist \"{batchCurrent}\" if exist \"{batchBackup}\" move /Y \"{batchBackup}\" \"{batchCurrent}\" >nul 2>&1")
            .AppendLine(":start")
            .AppendLine($"if exist \"{batchCurrent}\" start \"\" \"{batchCurrent}\"")
            .AppendLine(":end")
            .AppendLine($"rem update to {version}")
            .AppendLine("del /F /Q \"%~f0\" >nul 2>&1")
            .AppendLine("endlocal")
            .ToString();

        File.WriteAllText(script, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{script}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden,
            });
        }
        catch (Exception ex)
        {
            throw new UpdateException(UpdateErrorKind.ReplaceFailed, $"无法启动更新脚本：{ex.Message}", ex);
        }

        return backup;
    }

    private static void ProbeWritable(string folder)
    {
        var probe = Path.Combine(folder, $".probe-{Guid.NewGuid():N}");
        using (File.Create(probe, 1, FileOptions.DeleteOnClose))
        {
        }
    }

    // ==================================================================
    //  清理
    // ==================================================================

    /// <summary>
    /// 启动时清理：上次更新留下的旧 exe、失败的分片、过期的更新包。
    /// 必须在本进程可以正常读写 exe 目录之后调用（<c>App.OnStartup</c> 里 CoreInitializer 之后）。
    /// </summary>
    public static void CleanupOnStartup()
    {
        try
        {
            var current = CurrentExecutablePath;
            if (!string.IsNullOrWhiteSpace(current))
            {
                var folder = Path.GetDirectoryName(current);
                if (!string.IsNullOrWhiteSpace(folder))
                {
                    var staging = Path.Combine(folder, ".update");
                    if (Directory.Exists(staging))
                    {
                        // old-*.exe 是刚更新完留下的旧版本（体积≈一整个 exe）：新进程启动时旧进程已退出，
                        // 文件锁已释放，直接删；删不掉（安全软件/仍被占用）就留给下次启动。
                        // new-*.exe / apply-update.cmd 可能是尚未跑完的兜底脚本，留 10 分钟再清。
                        CleanupStagingFiles(staging, "old-*.exe", TimeSpan.Zero);
                        CleanupStagingFiles(staging, "new-*.exe", TimeSpan.FromMinutes(10));
                        CleanupStagingFiles(staging, "apply-update.cmd", TimeSpan.FromMinutes(10));

                        if (!Directory.EnumerateFileSystemEntries(staging).Any())
                        {
                            Directory.Delete(staging);
                        }
                    }
                }
            }

            var updates = Path.Combine(SettingsService.DataPath, "Updates");
            if (Directory.Exists(updates))
            {
                foreach (var part in Directory.EnumerateFiles(updates, "*.part", SearchOption.AllDirectories))
                {
                    if (DateTime.UtcNow - File.GetLastWriteTimeUtc(part) > TimeSpan.FromDays(7))
                    {
                        TryDelete(part);
                    }
                }

                foreach (var assembling in Directory.EnumerateFiles(updates, "*.assembling", SearchOption.AllDirectories))
                {
                    if (DateTime.UtcNow - File.GetLastWriteTimeUtc(assembling) > TimeSpan.FromDays(1))
                    {
                        TryDelete(assembling);
                    }
                }

                foreach (var versionFolder in Directory.EnumerateDirectories(updates))
                {
                    if (DateTime.UtcNow - Directory.GetLastWriteTimeUtc(versionFolder) > TimeSpan.FromDays(30))
                    {
                        try
                        {
                            Directory.Delete(versionFolder, recursive: true);
                        }
                        catch
                        {
                            // 用户可能正在手动查看，忽略。
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Core.Log.Warn($"[更新] 启动清理失败：{ex.Message}");
        }
    }

    // ==================================================================
    //  辅助
    // ==================================================================

    /// <summary>把发布说明（Markdown）转成适合只读文本框显示的纯文本：保留层级与条目，去掉标记噪音。</summary>
    public static string FormatNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return string.Empty;
        }

        var text = notes.Replace("\r\n", "\n").Replace('\r', '\n');
        text = MarkdownLink.Replace(text, m => m.Groups[1].Value);
        text = MarkdownBold.Replace(text, "$1");
        text = MarkdownInlineCode.Replace(text, "$1");

        var lines = text.Split('\n');
        var builder = new StringBuilder(text.Length);
        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            var trimmed = line.TrimStart();

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                continue;
            }

            if (trimmed.StartsWith('#'))
            {
                line = trimmed.TrimStart('#').Trim();
            }
            else if (trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal))
            {
                line = "• " + trimmed[2..].TrimStart();
            }
            else if (trimmed.StartsWith("> ", StringComparison.Ordinal))
            {
                line = trimmed[2..];
            }

            builder.AppendLine(line);
        }

        return builder.ToString().Trim();
    }

    /// <summary>把字节数格式化成 <c>134.0 MB</c> 这类短文本（与语言无关）。</summary>
    public static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
        {
            return "-";
        }

        string[] units = ["B", "KB", "MB", "GB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return string.Create(CultureInfo.InvariantCulture, $"{value:0.#} {units[unit]}");
    }

    /// <summary>把速度格式化成 <c>12.3 MB/s</c>。</summary>
    public static string FormatSpeed(double bytesPerSecond)
        => bytesPerSecond <= 0 ? "-" : FormatBytes((long)bytesPerSecond) + "/s";

    private static HttpClient CreateClient(bool followRedirects)
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = followRedirects,
            AutomaticDecompression = DecompressionMethods.All,
        };

        var client = new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("TheGuideToTheNewEden-Updater/1.0 (+https://github.com/qedsd/TheGuideToTheNewEden)");
        return client;
    }

    private static CancellationTokenSource CreateTimeout(CancellationToken cancellationToken, TimeSpan timeout)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);
        return cts;
    }

    private static UpdateErrorKind ClassifyNetworkError(Exception ex)
    {
        var message = ex.ToString();
        if (message.Contains("403", StringComparison.Ordinal) || message.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
        {
            return UpdateErrorKind.RateLimited;
        }

        return UpdateErrorKind.Network;
    }

    private static string NormalizeTag(string tag)
    {
        var value = tag.Trim();
        var slash = value.LastIndexOf('/');
        if (slash >= 0)
        {
            value = value[(slash + 1)..];
        }

        return value.TrimStart('v', 'V');
    }

    private static string? NormalizeSha(string? sha)
    {
        if (string.IsNullOrWhiteSpace(sha))
        {
            return null;
        }

        var value = sha.Trim();
        if (value.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["sha256:".Length..];
        }

        value = value.Trim().ToLowerInvariant();
        return value.Length == 64 && value.All(Uri.IsHexDigit) ? value : null;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            builder.Append(invalid.Contains(c) ? '_' : c);
        }

        var value = builder.ToString().Trim();
        return string.IsNullOrWhiteSpace(value) ? "TheGuideToTheNewEden.exe" : value;
    }

    private static string? GetString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }

    private static List<string> GetStringArray(JsonElement element, params string[] names)
    {
        var values = new List<string>();
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    values.Add(text!);
                }
            }
            else if (value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in value.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                    {
                        values.Add(item.GetString()!);
                    }
                }
            }
        }

        return values;
    }

    private static long? GetInt64(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
            {
                return number;
            }

            if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out number))
            {
                return number;
            }
        }

        return null;
    }

    private static DateTimeOffset? GetDateTimeOffset(JsonElement element, params string[] names)
    {
        var text = GetString(element, names);
        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value)
            ? value
            : null;
    }

    private static void EnsureFreeSpace(string folder, long expectedSize)
    {
        if (expectedSize <= 0)
        {
            return;
        }

        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(folder));
            if (string.IsNullOrWhiteSpace(root))
            {
                return;
            }

            var available = new DriveInfo(root).AvailableFreeSpace;
            const long reserve = 64L * 1024 * 1024;
            if (available < expectedSize + reserve)
            {
                throw new UpdateException(
                    UpdateErrorKind.DownloadFailed,
                    $"磁盘空间不足：需要 {FormatBytes(expectedSize)}，可用 {FormatBytes(available)}");
            }
        }
        catch (UpdateException)
        {
            throw;
        }
        catch
        {
            // 取不到磁盘信息就不拦。
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // 清理失败不影响主流程。
        }
    }

    private static void CleanupStagingFiles(string folder, string pattern, TimeSpan minimumAge)
    {
        foreach (var file in Directory.EnumerateFiles(folder, pattern))
        {
            try
            {
                if (DateTime.UtcNow - File.GetLastWriteTimeUtc(file) >= minimumAge)
                {
                    File.Delete(file);
                }
            }
            catch
            {
                // 文件仍被占用（例如正在等待退出的辅助脚本），下次启动再清。
            }
        }
    }

    private static void LogResult(UpdateCheckResult result)
    {
        switch (result.Status)
        {
            case UpdateCheckStatus.UpdateAvailable:
                Core.Log.Info($"[更新] 发现新版本 {result.Info}（当前 {result.CurrentVersion}，来源 {result.Info?.Source}）");
                break;
            case UpdateCheckStatus.NoAsset:
                Core.Log.Info($"[更新] 发现新版本 {result.Info?.Version}，但没有可用的下载源");
                break;
            case UpdateCheckStatus.UpToDate:
                Core.Log.Info($"[更新] 已是最新版本（{result.CurrentVersion}）");
                break;
            case UpdateCheckStatus.Failed:
                Core.Log.Info($"[更新] 检查失败（{result.Error?.Kind}）：{result.Error?.Detail}");
                break;
        }
    }
}
