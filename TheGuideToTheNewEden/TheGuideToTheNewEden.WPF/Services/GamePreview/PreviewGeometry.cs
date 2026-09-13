using TheGuideToTheNewEden.WPF.Helpers.Interop;

namespace TheGuideToTheNewEden.WPF.Services.GamePreview;

/// <summary>预览画面的几何计算（物理像素）。</summary>
internal static class PreviewGeometry
{
    /// <summary>
    /// 在容器矩形内按源画面的纵横比<b>居中</b>摆放，多余部分留边（letterbox / pillarbox），
    /// 避免把游戏画面拉伸变形。源尺寸无效时回退为铺满容器。
    /// <para>
    /// 比例已经一致时（差一个像素以内）直接铺满：窗口尺寸已按游戏客户区比例锁定，
    /// 这时再按比例重算会在边缘留下 1px 缝（缩略图背后是黑底，看起来就是"细黑边"）。
    /// </para>
    /// </summary>
    /// <param name="container">可用区域（物理像素，目标窗口客户区坐标）。</param>
    /// <param name="sourceWidth">源画面宽（源窗口客户区）。</param>
    /// <param name="sourceHeight">源画面高。</param>
    internal static NativeMethods.RECT FitAspect(
        NativeMethods.RECT container,
        int sourceWidth,
        int sourceHeight)
    {
        var availableWidth = container.Width;
        var availableHeight = container.Height;
        if (sourceWidth <= 0 || sourceHeight <= 0 || availableWidth <= 0 || availableHeight <= 0)
        {
            return container;
        }

        var scale = Math.Min(
            (double)availableWidth / sourceWidth,
            (double)availableHeight / sourceHeight);
        var width = Math.Max(1, (int)Math.Round(sourceWidth * scale));
        var height = Math.Max(1, (int)Math.Round(sourceHeight * scale));

        // 比例一致（取整误差级）→ 铺满，别留缝
        if (availableWidth - width <= 1 && availableHeight - height <= 1)
        {
            return container;
        }

        var left = container.Left + ((availableWidth - width) / 2);
        var top = container.Top + ((availableHeight - height) / 2);

        return new NativeMethods.RECT
        {
            Left = left,
            Top = top,
            Right = left + width,
            Bottom = top + height,
        };
    }

    /// <summary>源窗口客户区尺寸（DWM 只显示客户区，故用它作为纵横比依据）。</summary>
    internal static bool TryGetSourceSize(IntPtr sourceWindow, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (sourceWindow == IntPtr.Zero || !NativeMethods.IsWindow(sourceWindow))
        {
            return false;
        }

        if (NativeMethods.GetClientRect(sourceWindow, out var client) && client.Width > 0 && client.Height > 0)
        {
            width = client.Width;
            height = client.Height;
            return true;
        }

        if (NativeMethods.GetWindowRect(sourceWindow, out var window) && window.Width > 0 && window.Height > 0)
        {
            width = window.Width;
            height = window.Height;
            return true;
        }

        return false;
    }

    /// <summary>按源画面纵横比计算缩放后的窗口尺寸（滚轮缩放用），超出上限时以高度为准收缩。</summary>
    internal static (int Width, int Height) ScalePreservingAspect(
        int currentWidth,
        int currentHeight,
        int sourceWidth,
        int sourceHeight,
        double factor,
        int minWidth,
        int minHeight,
        int maxWidth,
        int maxHeight)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            return (
                Math.Clamp((int)Math.Round(currentWidth * factor), minWidth, Math.Max(minWidth, maxWidth)),
                Math.Clamp((int)Math.Round(currentHeight * factor), minHeight, Math.Max(minHeight, maxHeight)));
        }

        var aspect = (double)sourceWidth / sourceHeight;
        var width = Math.Max(minWidth, (int)Math.Round(currentWidth * factor));
        var height = Math.Max(minHeight, (int)Math.Round(width / aspect));

        if (height > maxHeight)
        {
            height = maxHeight;
            width = Math.Max(minWidth, (int)Math.Round(height * aspect));
        }

        if (width > maxWidth)
        {
            width = maxWidth;
            height = Math.Max(minHeight, (int)Math.Round(width / aspect));

            // 以宽度为准收缩后高度可能仍超出上限（例如"又宽又高"的请求同时越过两个上限），
            // 这时必须以高度为准再收一次，否则返回的尺寸会大于 maxHeight。
            if (height > maxHeight)
            {
                height = maxHeight;
                width = Math.Max(minWidth, (int)Math.Round(height * aspect));
            }
        }

        return (width, height);
    }
}
