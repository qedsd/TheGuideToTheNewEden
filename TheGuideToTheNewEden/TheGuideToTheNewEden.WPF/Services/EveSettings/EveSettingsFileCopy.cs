using System.IO;

namespace TheGuideToTheNewEden.WPF.Services.EveSettings;

/// <summary>
/// 覆盖式文件复制的统一实现（同步与备份共用）。
/// <para>
/// 失败重试 3 次、每次间隔 200ms：写游戏设置目录时最容易碰到的是防病毒/网盘同步/
/// 客户端残留句柄造成的<b>瞬时</b>占用，重试一次往往就过了；真失败才如实报错。
/// </para>
/// </summary>
public static class EveSettingsFileCopy
{
    /// <summary>把 <paramref name="source"/> 覆盖复制到 <paramref name="dest"/>；成功返回 true，失败带出原因。</summary>
    public static bool CopyWithRetry(string source, string dest, out string error)
    {
        error = string.Empty;

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var folder = Path.GetDirectoryName(dest);
                if (!string.IsNullOrEmpty(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                File.Copy(source, dest, overwrite: true);

                // 保留原文件的修改时间，便于用户在两份设置之间对照时间。
                try
                {
                    File.SetLastWriteTime(dest, File.GetLastWriteTime(source));
                }
                catch (Exception ex)
                {
                    Core.Log.Warn($"[配置同步] 保留修改时间失败：{dest} —— {ex.Message}");
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                if (attempt < 3)
                {
                    Thread.Sleep(200);
                }
                else
                {
                    Core.Log.Error(ex);
                }
            }
        }

        return false;
    }
}
