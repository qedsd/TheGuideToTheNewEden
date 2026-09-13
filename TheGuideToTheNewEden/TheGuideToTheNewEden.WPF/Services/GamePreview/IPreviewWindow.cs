using System.Windows;
using TheGuideToTheNewEden.Core.Models.GamePreviews;

namespace TheGuideToTheNewEden.WPF.Services.GamePreview;

/// <summary>
/// 单个客户端的预览载体：<see cref="Views.Windows.GamePreviewWindow"/> 是带 DWM 缩略图的浮窗；
/// <c>ShowPreviewWindow == false</c> 时用无窗口实现（只保留热键激活能力）。
/// <para>矩形一律是"物理像素的屏幕坐标"，与 Win32/DWM 的坐标空间一致。</para>
/// </summary>
public interface IPreviewWindow : IDisposable
{
    ProcessInfo Process { get; }

    PreviewItem Setting { get; }

    bool IsShowing { get; }

    /// <summary>创建窗口并把源窗口画面接上。</summary>
    void Start();

    /// <summary>结束预览并销毁窗口。</summary>
    void Stop();

    void ShowWindow();

    void HideWindow();

    void SetHighlight(bool highlight);

    /// <summary>
    /// 告知"该客户端当前是否在前台"。
    /// <para>
    /// 与 <see cref="SetHighlight"/> 分开是因为两者语义不同：后者是"边框高亮"（受用户的 Highlight 开关控制），
    /// 而角色名底色的高亮**不受那个开关影响**，必须拿到未过滤的真实前台状态。
    /// </para>
    /// </summary>
    void SetForegroundState(bool isForeground);

    /// <summary>
    /// 该预览画面应保持的宽高比（宽/高）；**源窗口最小化或取不到时为 0**，调用方应按"未知"处理。
    /// <para>
    /// 之所以要暴露它：最小化时源窗口的客户区是"任务栏缩略图"那种又宽又扁的小矩形，
    /// 谁拿它算比例都会把窗口压扁（曾导致"最小化状态下启动预览，高度明显变小"）。
    /// </para>
    /// </summary>
    double SourceAspect { get; }

    void SetPosition(int x, int y);

    void SetSize(int width, int height);

    Int32Rect GetRect();

    /// <summary>把该客户端切到前台。</summary>
    void ActivateSource();

    /// <summary>设置被修改后重新套用（名称、颜色、不透明度、高亮边距、显示样式）。</summary>
    void ApplySettings();

    /// <summary>角色切换/进程重启：换绑进程与设置，并重新绑定缩略图源。</summary>
    void Rebind(ProcessInfo process, PreviewItem setting);

    /// <summary>源窗口最小化/恢复后恢复缩略图。</summary>
    void Recover();

    /// <summary>窗口位置/尺寸变化（用于持久化）。</summary>
    event Action<PreviewItem>? SettingChanged;

    /// <summary>用户主动关闭预览（点关闭键 / Esc）。</summary>
    event Action<PreviewItem>? StopRequested;
}
