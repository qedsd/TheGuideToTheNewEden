using System.IO;
using TheGuideToTheNewEden.WPF.Models.EveSettings;

namespace TheGuideToTheNewEden.WPF.Services.EveSettings;

/// <summary>
/// 同步引擎：把一个设置文件（源）<b>整体覆盖</b>到同类型的其他设置文件（目标）。
/// <para>
/// 语义与 <c>mintnick/eve-settings-manager</c> 的 <c>settings:copy</c> 一致——
/// 没有任何"合并"，就是逐个 <c>File.Copy(overwrite)</c>；边界也一样：
/// <b>只允许同一配置文件目录内、同一类型的文件之间同步</b>（不跨配置文件、不跨服务器、不跨类型）。
/// </para>
/// <para>
/// 与参考实现的差异：逐个目标独立 try/catch + 重试，最后给出"成功 N / 失败 M"的结果，
/// 而不是一个 <c>Promise.all</c> 抛出去让整批失败。
/// </para>
/// </summary>
public static class EveSettingsSyncService
{
    /// <summary>
    /// 执行同步。<paramref name="progress"/> 报告的是"已完成 / 总数"（供等待遮罩显示进度）。
    /// 取消时返回 <see cref="EveSettingsSyncResult.Cancelled"/> = true 的部分结果。
    /// </summary>
    public static EveSettingsSyncResult Sync(
        string sourcePath,
        IReadOnlyList<string> targetPaths,
        IProgress<(int Done, int Total)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = new EveSettingsSyncResult();

        if (!File.Exists(sourcePath))
        {
            result.Error = "source-not-found";
            return result;
        }

        if (targetPaths.Count == 0)
        {
            result.Error = "no-target";
            return result;
        }

        var sourceFolder = Path.GetDirectoryName(sourcePath) ?? string.Empty;
        var done = 0;

        foreach (var target in targetPaths)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                result.Cancelled = true;
                return result;
            }

            done++;

            if (string.Equals(target, sourcePath, StringComparison.OrdinalIgnoreCase))
            {
                continue; // 源与目标是同一个文件（UI 已排除，这里兜底）
            }

            // 兜底边界：目标必须与源在同一个配置文件目录里。
            if (!string.Equals(Path.GetDirectoryName(target), sourceFolder, StringComparison.OrdinalIgnoreCase))
            {
                result.Failed++;
                result.Failures.Add((Path.GetFileName(target), "target-outside-profile"));
                progress?.Report((done, targetPaths.Count));
                continue;
            }

            if (EveSettingsFileCopy.CopyWithRetry(sourcePath, target, out var error))
            {
                result.Succeeded++;
            }
            else
            {
                result.Failed++;
                result.Failures.Add((Path.GetFileName(target), error));
            }

            progress?.Report((done, targetPaths.Count));
        }

        return result;
    }
}
