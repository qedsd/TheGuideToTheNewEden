using System;
using System.Threading;
using System.Windows;
using TheGuideToTheNewEden.WPF.Services;

namespace TheGuideToTheNewEden.WPF.Helpers;

/// <summary>
/// 剪贴板读写（阶段 288）：Windows 的剪贴板是全局独占资源，`Clipboard.SetText` 在别的程序
/// 正开着剪贴板（微信/QQ/输入法/剪贴板管理器/远程桌面剪贴板重定向/云剪贴板/杀软等）时会抛
/// <c>CLIPBRD_E_CANT_OPEN (0x800401D0)</c>。这种占用通常是毫秒级的，微软建议重试，
/// 这里统一做"短重试 + OLE 兜底"，并且**不向外抛异常**，由调用方决定怎么提示。
/// </summary>
public static class ClipboardHelper
{
    private const int Attempts = 6;
    private const int BaseDelayMs = 25;

    /// <summary>把文本写进剪贴板；成功返回 true，剪贴板持续被占用则返回 false（不抛异常）。</summary>
    public static bool TrySetText(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            try
            {
                Clipboard.SetText(text);
                return true;
            }
            catch (Exception)
            {
                // 另一个进程短暂持有剪贴板：等一会儿再试
                Thread.Sleep(BaseDelayMs * (attempt + 1));
            }
        }

        // 兜底：部分剪贴板管理器只放行 OLE 路径（SetDataObject 带 copy=true 会把数据留在剪贴板）
        try
        {
            Clipboard.SetDataObject(text, true);
            return true;
        }
        catch (Exception ex)
        {
            Core.Log.Warn($"[Clipboard] 复制失败：{ex.Message}");
            return false;
        }
    }

    /// <summary>复制并保证"成功才继续"：失败时弹右下角通知并返回 false，调用方应直接 return 不再提示成功。</summary>
    public static bool CopyOrNotify(string? text)
    {
        if (TrySetText(text))
        {
            return true;
        }

        var message = Application.Current?.TryFindResource("General_ClipboardBusy") as string
                      ?? "剪贴板被其它程序占用，请稍后重试";
        PageNotifyService.Error(message);
        return false;
    }
    /// <summary>读取剪贴板文本；读不到（被占用/没有文本）返回 null，不抛异常。</summary>
    public static string? TryGetText()
    {
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            try
            {
                return Clipboard.ContainsText() ? Clipboard.GetText() : null;
            }
            catch (Exception)
            {
                Thread.Sleep(BaseDelayMs * (attempt + 1));
            }
        }

        return null;
    }
}
