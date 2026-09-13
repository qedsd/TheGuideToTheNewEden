using System.Windows;
using TheGuideToTheNewEden.Core.Models.GamePreviews;

namespace TheGuideToTheNewEden.WPF.Services.GamePreview;

/// <summary>
/// 不显示预览窗口（<c>ShowPreviewWindow == false</c>）时的空实现：
/// 仍然算"正在预览"，仍可被全局快捷键切换激活，只是没有可视窗口。
/// </summary>
public sealed class HeadlessPreviewWindow : IPreviewWindow
{
    private readonly PreviewSetting _globalSetting;
    private bool _stopped;

    public ProcessInfo Process { get; private set; }

    public PreviewItem Setting { get; private set; }

    public bool IsShowing => false;

    public event Action<PreviewItem>? SettingChanged;

    public event Action<PreviewItem>? StopRequested;

    public HeadlessPreviewWindow(ProcessInfo process, PreviewItem setting, PreviewSetting globalSetting)
    {
        Process = process;
        Setting = setting;
        _globalSetting = globalSetting;
    }

    public void Start()
    {
    }

    public void Stop()
    {
        if (_stopped)
        {
            return;
        }

        _stopped = true;
        SettingChanged = null;
        StopRequested = null;
    }

    public void ShowWindow()
    {
    }

    public void HideWindow()
    {
    }

    public void SetHighlight(bool highlight)
    {
    }

    /// <summary>无窗口预览没有可显示的叠加层，前台状态无需处理（保留接口语义）。</summary>
    public void SetForegroundState(bool isForeground)
    {
    }

    /// <summary>无窗口预览不涉及尺寸/比例，恒为 0（调用方按"未知"处理）。</summary>
    public double SourceAspect => 0;

    public void SetPosition(int x, int y)
    {
    }

    public void SetSize(int width, int height)
    {
    }

    public Int32Rect GetRect() => new(0, 0, 0, 0);

    public void ActivateSource() => GameClientService.Activate(Process.MainWindowHandle, _globalSetting.SetForegroundWindowMode);

    public void ApplySettings()
    {
    }

    public void Rebind(ProcessInfo process, PreviewItem setting)
    {
        Process = process;
        Setting = setting;
    }

    public void Recover()
    {
    }

    public void Dispose() => Stop();
}
