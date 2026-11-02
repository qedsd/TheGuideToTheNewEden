using System;
using System.Collections.Generic;
using System.Linq;

namespace TheGuideToTheNewEden.WPF.Services.Update;

/// <summary>更新检查的最终状态。</summary>
public enum UpdateCheckStatus
{
    /// <summary>已是最新版本。</summary>
    UpToDate,

    /// <summary>发现更新，且找到了可直接替换的下载源。</summary>
    UpdateAvailable,

    /// <summary>发现新版本，但发布里没有可用的单文件 exe（只能去发布页手动下载）。</summary>
    NoAsset,

    /// <summary>检查失败（网络 / 限流 / 解析失败）。</summary>
    Failed,
}

/// <summary>失败原因分类（界面按它取本地化文案）。</summary>
public enum UpdateErrorKind
{
    None,
    Network,
    RateLimited,
    NoAsset,
    BadManifest,
    DownloadFailed,
    HashMismatch,
    SizeMismatch,
    InvalidFile,
    NotWritable,
    ReplaceFailed,
    UnsupportedHost,
    Cancelled,
}

/// <summary>更新失败的描述：<see cref="Kind"/> 决定界面上的一句人话，<see cref="Detail"/> 供日志/展开查看。</summary>
public sealed class UpdateError
{
    public UpdateError(UpdateErrorKind kind, string? detail = null)
    {
        Kind = kind;
        Detail = detail;
    }

    public UpdateErrorKind Kind { get; }

    public string? Detail { get; }

    public override string ToString() => string.IsNullOrWhiteSpace(Detail) ? Kind.ToString() : $"{Kind}: {Detail}";
}

/// <summary>
/// 一个可下载的文件分片。单文件源只有 1 个分片；Gitee 源因单附件 100 MB 限制，
/// 可能由多个顺序分片组成（下载后按顺序拼接成完整 exe）。
/// </summary>
public sealed class UpdateFilePart
{
    public required string Name { get; init; }

    public required string Url { get; init; }

    /// <summary>字节数；0 表示未知。</summary>
    public long Size { get; init; }

    /// <summary>该分片的小写 SHA-256；null 表示发布方没有提供（最终仍会校验整包哈希）。</summary>
    public string? Sha256 { get; init; }
}

/// <summary>
/// 一条完整的下载路径：要么是单个 exe，要么是若干按顺序拼接的分片。
/// 多个 <see cref="UpdateDownloadSource"/> 互为备份，按顺序尝试（Gitee 优先、GitHub 兜底）。
/// </summary>
public sealed class UpdateDownloadSource
{
    public required string Name { get; init; }

    public required IReadOnlyList<UpdateFilePart> Parts { get; init; }

    /// <summary>已知的分片总字节数；任一分片大小未知时为 0。</summary>
    public long KnownSize => Parts.All(p => p.Size > 0) ? Parts.Sum(p => p.Size) : 0;
}

/// <summary>一个可用的新版本。</summary>
public sealed class UpdateInfo
{
    /// <summary>显示/比较用的版本号（已去掉 tag 前缀，可能带 <c>-alpha.2</c> 后缀）。</summary>
    public required string Version { get; init; }

    public string? Tag { get; init; }

    /// <summary>最终保存/替换的文件名，例如 <c>TheGuideToTheNewEden.exe</c>。</summary>
    public required string FileName { get; init; }

    /// <summary>完整 exe 的字节数；0 表示未知。</summary>
    public long Size { get; init; }

    /// <summary>完整 exe 的小写 SHA-256；null 表示发布方没有提供。</summary>
    public string? Sha256 { get; init; }

    /// <summary>发布说明（Markdown 原文，界面会做轻量纯文本化）。</summary>
    public string? Notes { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    /// <summary>下载源（按优先级排序：国内镜像在前，GitHub 在后）。</summary>
    public required IReadOnlyList<UpdateDownloadSource> Sources { get; init; }

    /// <summary>发布页地址（无资产时让用户手动下载）。</summary>
    public string? ReleasePageUrl { get; init; }

    /// <summary>信息来自哪个渠道（Gitee update.json / GitHub API / latest 重定向），仅用于日志。</summary>
    public string? Source { get; init; }

    public bool HasAsset => Sources.Count > 0;

    public override string ToString()
        => HasAsset
            ? $"{Version} ({Sources.Count} 个下载源, {Sources[0].Parts.Count} 个分片, {Size} bytes)"
            : $"{Version} (无下载源)";
}

/// <summary>一次检查的结果。</summary>
public sealed class UpdateCheckResult
{
    private UpdateCheckResult(UpdateCheckStatus status)
    {
        Status = status;
        CheckedAt = DateTimeOffset.Now;
    }

    public UpdateCheckStatus Status { get; }

    public UpdateInfo? Info { get; private init; }

    public UpdateError? Error { get; private init; }

    public string CurrentVersion { get; private init; } = string.Empty;

    public DateTimeOffset CheckedAt { get; }

    public bool HasUpdate => Status is UpdateCheckStatus.UpdateAvailable or UpdateCheckStatus.NoAsset;

    public static UpdateCheckResult UpToDate(string current)
        => new(UpdateCheckStatus.UpToDate) { CurrentVersion = current };

    public static UpdateCheckResult Available(UpdateInfo info, string current)
        => new(UpdateCheckStatus.UpdateAvailable) { Info = info, CurrentVersion = current };

    public static UpdateCheckResult NoAsset(UpdateInfo info, string current)
        => new(UpdateCheckStatus.NoAsset) { Info = info, CurrentVersion = current };

    public static UpdateCheckResult Failed(string current, UpdateError error)
        => new(UpdateCheckStatus.Failed) { CurrentVersion = current, Error = error };
}

/// <summary>下载进度（线程池线程产生，界面侧用 <see cref="Progress{T}"/> 归位）。</summary>
public readonly record struct UpdateDownloadProgress(
    long Received,
    long Total,
    double BytesPerSecond,
    string? SourceName = null)
{
    /// <summary>0–100；总大小未知时返回 -1。</summary>
    public int Percent => Total > 0 ? (int)Math.Clamp(Received * 100L / Total, 0, 100) : -1;
}

/// <summary>安装（替换当前 exe）之后由谁拉起新进程。</summary>
public enum UpdateRelaunchMode
{
    /// <summary>文件已就地替换，调用方走 <c>App.RequestRestartAfterUpdate()</c>（等本进程退出后拉起）。</summary>
    ByApp,

    /// <summary>文件被占用等情况下交给一个等待本进程退出的 cmd 脚本完成替换并拉起。</summary>
    ByHelper,
}

public sealed class UpdateInstallResult
{
    public required UpdateRelaunchMode Mode { get; init; }

    /// <summary>旧 exe 的备份路径（成功时）。</summary>
    public string? BackupPath { get; init; }
}

/// <summary>更新流程中可预期的失败（带 <see cref="UpdateErrorKind"/>，界面据此出文案）。</summary>
public sealed class UpdateException : Exception
{
    public UpdateException(UpdateErrorKind kind, string message, Exception? inner = null)
        : base(message, inner)
    {
        Kind = kind;
    }

    public UpdateErrorKind Kind { get; }
}

/// <summary>某个下载源失败（界面据此弹右下角通知，再自动尝试下一个源）。</summary>
public sealed class UpdateSourceFailedEventArgs : EventArgs
{
    public required string SourceName { get; init; }

    public required int SourceIndex { get; init; }

    public required int SourceCount { get; init; }

    public required UpdateErrorKind Kind { get; init; }

    public required string Message { get; init; }

    /// <summary>后面还有没有源可试。</summary>
    public bool HasNextSource => SourceIndex + 1 < SourceCount;

    /// <summary>是否属于"文件内容不对"（SHA-256 / 大小），而不是网络失败。</summary>
    public bool IsChecksumFailure => Kind is UpdateErrorKind.HashMismatch or UpdateErrorKind.SizeMismatch;
}
