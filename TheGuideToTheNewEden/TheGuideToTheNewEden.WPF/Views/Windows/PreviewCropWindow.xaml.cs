using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using TheGuideToTheNewEden.Core.Models.GamePreviews;
using TheGuideToTheNewEden.WPF.Helpers.Interop;
using TheGuideToTheNewEden.WPF.Services.GamePreview;
using Wpf.Ui.Controls;

namespace TheGuideToTheNewEden.WPF.Views.Windows;

/// <summary>
/// 阶段 347：多开预览的“自定义显示区域”选择弹窗。
/// <para>
/// 画面由 <see cref="DwmThumbnail"/> 直接合成到本窗口的 <c>StageHost</c> 区域（不抓图、零额外开销），
/// 可拖拽的选择框由独立的透明窗口 <see cref="PreviewCropOverlayWindow"/> 盖在画面之上绘制
/// （DWM 缩略图会盖住同一窗口里的 WPF 元素，参见 <see cref="PreviewNameOverlayWindow"/> 的说明）。
/// </para>
/// <para>
/// 拖动时**即时**写回 <see cref="PreviewItem"/> 并通过回调让预览窗重新取画面，预览窗背后就能看到效果；
/// 点“取消”恢复进入前的区域，点“确定”保留并保存。
/// </para>
/// </summary>
public sealed partial class PreviewCropWindow : FluentWindow
{
    private const int TimerIntervalMs = 100;

    private readonly PreviewItem _setting;
    private readonly IntPtr _source;
    private readonly Action _apply;
    private readonly DwmThumbnail _thumbnail = new();
    private readonly DispatcherTimer _timer;

    // 进入时的原始值：取消时恢复
    private readonly bool _originalEnabled;
    private readonly int _originalX;
    private readonly int _originalY;
    private readonly int _originalW;
    private readonly int _originalH;

    private IntPtr _hwnd;
    private PreviewCropOverlayWindow? _overlay;
    private int _fullWidth;
    private int _fullHeight;
    private bool _ready;
    private bool _closing;

    internal PreviewCropWindow(PreviewItem setting, ProcessInfo process, Action apply)
    {
        InitializeComponent();

        _setting = setting;
        _apply = apply;
        _source = process.MainWindowHandle;

        _originalEnabled = setting.CropEnabled;
        _originalX = setting.CropX;
        _originalY = setting.CropY;
        _originalW = setting.CropW;
        _originalH = setting.CropH;

        Title = FindString("PreviewCrop_Title");
        TitleBar.Title = Title;
        HintText.Text = FindString("PreviewCrop_Hint");

        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(TimerIntervalMs),
        };
        _timer.Tick += (_, _) => UpdateThumbnail();

        Loaded += OnLoaded;
        LocationChanged += (_, _) => SyncOverlayBounds();
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        if (_hwnd == IntPtr.Zero)
        {
            Close();
            return;
        }

        if (!PreviewGeometry.TryGetSourceSize(_source, out _fullWidth, out _fullHeight)
            || _fullWidth <= 0
            || _fullHeight <= 0)
        {
            // 源窗口已退出/取不到尺寸：没有可选的画面
            Close();
            return;
        }

        ApplyStageSize();
        _thumbnail.TryRegister(_hwnd, _source);
        _ready = true;
        CreateOverlay();
        _timer.Start();
        UpdateThumbnail();
    }

    /// <summary>按源画面比例算出台阶区尺寸，并把窗口收紧到“舞台 + 边距”。</summary>
    private void ApplyStageSize()
    {
        var workArea = SystemParameters.WorkArea;
        var maxStageWidth = Math.Max(320, (workArea.Width * 0.72) - 32);
        var maxStageHeight = Math.Max(200, (workArea.Height * 0.72) - 140);

        var scale = Math.Min(maxStageWidth / _fullWidth, maxStageHeight / _fullHeight);
        scale = Math.Min(scale, 1.5); // 小画面不放大太多，避免糊
        var stageWidth = Math.Max(240, Math.Round(_fullWidth * scale));
        var stageHeight = Math.Max(140, Math.Round(_fullHeight * scale));

        StageHost.Width = stageWidth;
        StageHost.Height = stageHeight;

        // 舞台之外还有：标题栏 32 + 提示 26 + 选区信息 24 + 按钮 40 + 边距 48
        Width = stageWidth + 32;
        Height = stageHeight + 170;
        Left = Math.Max(workArea.Left, (workArea.Width - Width) / 2 + workArea.Left);
        Top = Math.Max(workArea.Top, (workArea.Height - Height) / 2 + workArea.Top);
        UpdateLayout(); // 先把舞台尺寸落到布局上：选区换算是按 ActualWidth/Height 做的
    }

    private void CreateOverlay()
    {
        if (_overlay is not null)
        {
            return;
        }

        _overlay = new PreviewCropOverlayWindow { Owner = this };
        _overlay.SelectionChanged += OnOverlaySelectionChanged;
        _overlay.Show();
        SyncOverlayBounds();

        // 选区要等叠加窗完成布局后再设：它的 ActualWidth 才是 DIP 坐标空间
        Dispatcher.BeginInvoke(
            () =>
            {
                if (_overlay is not { } overlay)
                {
                    return;
                }

                if (_setting.HasCrop)
                {
                    overlay.SelectionRect = ToOverlayRect(_setting.CropX, _setting.CropY, _setting.CropW, _setting.CropH);
                }
                else
                {
                    overlay.SelectAll();
                }

                UpdateSelectionText();
            },
            DispatcherPriority.Loaded);
    }


    /// <summary>把选择框窗口摆到画面上（物理像素，跟随弹窗移动）。</summary>
    private void SyncOverlayBounds()
    {
        if (_overlay is null || _hwnd == IntPtr.Zero || !_ready)
        {
            return;
        }

        var origin = StageHost.TransformToAncestor(this).Transform(new Point(0, 0));
        var dpi = VisualTreeHelper.GetDpi(this);
        var point = new NativeMethods.POINT
        {
            X = (int)Math.Round(origin.X * dpi.DpiScaleX),
            Y = (int)Math.Round(origin.Y * dpi.DpiScaleY),
        };
        if (!NativeMethods.ClientToScreen(_hwnd, ref point))
        {
            return;
        }

        var width = (int)Math.Round(StageHost.ActualWidth * dpi.DpiScaleX);
        var height = (int)Math.Round(StageHost.ActualHeight * dpi.DpiScaleY);
        _overlay.SetBounds(point.X, point.Y, width, height);
    }

    /// <summary>把完整画面合成到舞台区（始终显示全部内容，选区只是叠加的框）。</summary>
    private void UpdateThumbnail()
    {
        if (!_ready || _hwnd == IntPtr.Zero || _closing)
        {
            return;
        }

        SyncOverlayBounds();

        if (!NativeMethods.IsWindow(_source) || NativeMethods.IsIconic(_source))
        {
            _thumbnail.Hide();
            return;
        }

        var origin = StageHost.TransformToAncestor(this).Transform(new Point(0, 0));
        var dpi = VisualTreeHelper.GetDpi(this);
        var left = (int)Math.Round(origin.X * dpi.DpiScaleX);
        var top = (int)Math.Round(origin.Y * dpi.DpiScaleY);
        var width = (int)Math.Round(StageHost.ActualWidth * dpi.DpiScaleX);
        var height = (int)Math.Round(StageHost.ActualHeight * dpi.DpiScaleY);
        if (width <= 1 || height <= 1)
        {
            _thumbnail.Hide();
            return;
        }

        var destination = new NativeMethods.RECT
        {
            Left = left,
            Top = top,
            Right = left + width,
            Bottom = top + height,
        };
        _thumbnail.TryUpdate(destination, 255, true);
    }

    private void OnOverlaySelectionChanged(Rect rect)
    {
        if (!_ready)
        {
            return;
        }

        var sourceRect = ToSourceRegion(rect);
        if (sourceRect is null)
        {
            return;
        }

        var (x, y, w, h) = sourceRect.Value;
        var isFull = x <= 1 && y <= 1 && w >= _fullWidth - 1 && h >= _fullHeight - 1;
        if (isFull)
        {
            _setting.ClearCrop();
        }
        else
        {
            _setting.CropEnabled = true;
            _setting.CropX = x;
            _setting.CropY = y;
            _setting.CropW = w;
            _setting.CropH = h;
        }

        _apply();
        UpdateSelectionText();
    }

    /// <summary>选择框（舞台 DIP）→ 源窗口客户区像素。</summary>
    private (int X, int Y, int W, int H)? ToSourceRegion(Rect rect)
    {
        var stageWidth = StageHost.ActualWidth;
        var stageHeight = StageHost.ActualHeight;
        if (stageWidth <= 1 || stageHeight <= 1 || _fullWidth <= 0 || _fullHeight <= 0)
        {
            return null;
        }

        var scaleX = _fullWidth / stageWidth;
        var scaleY = _fullHeight / stageHeight;
        var x = (int)Math.Round(rect.X * scaleX);
        var y = (int)Math.Round(rect.Y * scaleY);
        var w = (int)Math.Round(rect.Width * scaleX);
        var h = (int)Math.Round(rect.Height * scaleY);

        x = Math.Clamp(x, 0, Math.Max(0, _fullWidth - 1));
        y = Math.Clamp(y, 0, Math.Max(0, _fullHeight - 1));
        w = Math.Clamp(w, 1, _fullWidth - x);
        h = Math.Clamp(h, 1, _fullHeight - y);
        return (x, y, w, h);
    }

    /// <summary>源像素区域 → 选择框（舞台 DIP）。</summary>
    private Rect ToOverlayRect(int x, int y, int w, int h)
    {
        var stageWidth = Math.Max(1, StageHost.ActualWidth);
        var stageHeight = Math.Max(1, StageHost.ActualHeight);
        var scaleX = stageWidth / Math.Max(1, _fullWidth);
        var scaleY = stageHeight / Math.Max(1, _fullHeight);
        return new Rect(x * scaleX, y * scaleY, Math.Max(1, w * scaleX), Math.Max(1, h * scaleY));
    }

    private void UpdateSelectionText()
    {
        if (!_setting.HasCrop)
        {
            SelectionText.Text = FindString("PreviewCrop_Full");
            return;
        }

        SelectionText.Text = string.Format(
            FindString("PreviewCrop_RegionFormat"),
            _setting.CropW,
            _setting.CropH,
            _setting.CropX,
            _setting.CropY);
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        _setting.ClearCrop();
        _overlay?.SelectAll();
        _apply();
        UpdateSelectionText();
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        _apply();
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        _setting.CropEnabled = _originalEnabled;
        _setting.CropX = _originalX;
        _setting.CropY = _originalY;
        _setting.CropW = _originalW;
        _setting.CropH = _originalH;
        _apply();
        Close();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _closing = true;
        _timer.Stop();
        _thumbnail.Dispose();

        if (_overlay is { } overlay)
        {
            _overlay = null;
            overlay.SelectionChanged -= OnOverlaySelectionChanged;
            overlay.Close();
        }

        _ready = false;
    }

    private static string FindString(string key)
        => Application.Current?.TryFindResource(key) as string ?? key;
}
