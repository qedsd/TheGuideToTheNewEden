using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Windows.Media.Imaging;

namespace TheGuideToTheNewEden.WPF.Controls;

/// <summary>
/// 进程内图片缓存 + 非阻塞下载器：按 URL 缓存已 <c>Freeze()</c> 的位图，同一地址并发请求合并为一次下载。
///
/// <para>
/// <b>为什么不用 <c>BitmapImage.UriSource</c></b>：那是 WIC 的按需下载路径，首次 <c>EndInit()</c> 会在
/// 调用线程（图像绑定都在 UI 线程求值）**同步下载**——列表每行一张图时整页会卡住一会儿
/// （击杀者多的 KB 详情页、击杀列表页最明显）。<c>CacheOption=OnLoad</c> 配 <c>UriSource</c>
/// 同样是同步下载，只是把时机提前。这里先在线程池抓字节、再同线程解码并冻结。
/// </para>
/// </summary>
public static class AsyncImageCache
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    /// <summary>URL → 已冻结图片（null = 该地址取不到，不再重试）。</summary>
    private static readonly ConcurrentDictionary<string, BitmapSource?> Cache = new();

    /// <summary>同一 URL 的并发请求合并为一次下载。</summary>
    private static readonly ConcurrentDictionary<string, Task<BitmapSource?>> Pending = new();

    /// <summary>
    /// 取图片并交付：命中缓存同步回调；否则启动（或复用）下载任务，完成后回调。
    /// <paramref name="onLoaded"/> 在调用方线程（通常是 UI 线程）执行。
    /// </summary>
    public static void Load(string? url, Action<BitmapSource?> onLoaded)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            onLoaded(null);
            return;
        }

        if (Cache.TryGetValue(url, out var cached))
        {
            onLoaded(cached);
            return;
        }

        Pending.GetOrAdd(url, DownloadAsync).ContinueWith(
            t =>
            {
                Pending.TryRemove(url, out _);
                var bitmap = t.Status == TaskStatus.RanToCompletion ? t.Result : null;
                Cache[url] = bitmap;
                onLoaded(bitmap);
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            SynchronizationContext.Current is null ? TaskScheduler.Default : TaskScheduler.FromCurrentSynchronizationContext());
    }

    /// <summary>
    /// 同步取：仅命中缓存才有值；未命中返回 null 并后台预热（供无法 await / 无法刷新的转换器使用）。
    /// </summary>
    public static BitmapSource? TryGet(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        if (Cache.TryGetValue(url, out var cached))
        {
            return cached;
        }

        _ = Pending.GetOrAdd(url, DownloadAsync).ContinueWith(
            t =>
            {
                Pending.TryRemove(url, out _);
                Cache[url] = t.Status == TaskStatus.RanToCompletion ? t.Result : null;
            },
            TaskScheduler.Default);
        return null;
    }

    private static async Task<BitmapSource?> DownloadAsync(string url)
    {
        try
        {
            var bytes = await Http.GetByteArrayAsync(url).ConfigureAwait(false);
            if (bytes.Length == 0)
            {
                return null;
            }

            // 解码与冻结必须同一线程（BitmapSource 冻结前有线程亲缘性）；此处为线程池线程
            using var stream = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex)
        {
            Core.Log.Warn($"[AsyncImage] 加载失败（{url}）：{ex.Message}");
            return null;
        }
    }
}
