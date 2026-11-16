using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;

namespace TheGuideToTheNewEden.WPF.Views.Windows;

/// <summary>
/// 阶段 347：显示区域选择框的绘制与拖拽层（透明、置顶、覆盖在 DWM 缩略图之上）。
/// <para>
/// 只负责“画框 + 拖拽”，不关心游戏画面：矩形以<b>本窗口 DIP</b> 表示，
/// 由 <see cref="PreviewCropWindow"/> 负责换算成源窗口客户区像素并应用到预览窗。<br/>
/// 交互：拖框内 = 平移；拖 8 个手柄 = 改尺寸；始终夹在本窗口范围内。
/// </para>
/// </summary>
public sealed partial class PreviewCropOverlayWindow : Window
{
    /// <summary>选择框最小边长（DIP），避免拖成一条线后无法再抓住。</summary>
    private const double MinSelectionSize = 16;

    private readonly Rectangle[] _handles;
    private Rect _selection;
    private bool _dragging;
    private string? _dragMode;
    private Point _dragStartPoint;
    private Rect _dragStartSelection;

    public PreviewCropOverlayWindow()
    {
        InitializeComponent();
        _handles = new[]
        {
            HandleTopLeft, HandleTop, HandleTopRight, HandleRight,
            HandleBottomRight, HandleBottom, HandleBottomLeft, HandleLeft,
        };

        foreach (var handle in _handles)
        {
            handle.MouseLeftButtonDown += OnHandleDown;
        }

        Selection.MouseLeftButtonDown += OnSelectionDown;
        Root.MouseLeftButtonDown += OnRootDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseUp;
    }

    /// <summary>选择框变化（单位：本窗口 DIP）。</summary>
    public event Action<Rect>? SelectionChanged;

    /// <summary>设置/读取选择框（DIP）。</summary>
    public Rect SelectionRect
    {
        get => _selection;
        set
        {
            _selection = value;
            LayoutSelection();
        }
    }

    /// <summary>
    /// 用**物理像素**摆放本窗口（与 PreviewNameOverlayWindow 同一套做法，绕开 WPF 的 DIP 换算）。
    /// </summary>
    public void SetBounds(int x, int y, int width, int height)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        Helpers.Interop.NativeMethods.SetWindowPos(
            hwnd,
            IntPtr.Zero,
            x,
            y,
            Math.Max(1, width),
            Math.Max(1, height),
            0x0004 | 0x0010); // SWP_NOZORDER | SWP_NOACTIVATE
    }

    /// <summary>把选择框设为整块可用区域（等价于“显示全部”）。</summary>
    public void SelectAll()
    {
        SelectionRect = new Rect(0, 0, Math.Max(MinSelectionSize, ActualWidth), Math.Max(MinSelectionSize, ActualHeight));
    }

    private void OnRootDown(object sender, MouseButtonEventArgs e)
    {
        // 点在选框外的暗区：把框整体挪到以点击处为中心的位置
        var point = e.GetPosition(Root);
        var width = _selection.Width;
        var height = _selection.Height;
        _selection = ClampToBounds(new Rect(
            point.X - (width / 2),
            point.Y - (height / 2),
            width,
            height));
        LayoutSelection();
        RaiseSelectionChanged();
        BeginDrag("Move", e);
    }

    private void OnSelectionDown(object sender, MouseButtonEventArgs e)
    {
        BeginDrag("Move", e);
        e.Handled = true;
    }

    private void OnHandleDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string mode })
        {
            BeginDrag(mode, e);
            e.Handled = true;
        }
    }

    private void BeginDrag(string mode, MouseButtonEventArgs e)
    {
        _dragging = true;
        _dragMode = mode;
        _dragStartPoint = e.GetPosition(Root);
        _dragStartSelection = _selection;
        CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging || _dragMode is null)
        {
            return;
        }

        var current = e.GetPosition(Root);
        var dx = current.X - _dragStartPoint.X;
        var dy = current.Y - _dragStartPoint.Y;
        var start = _dragStartSelection;

        var left = start.Left;
        var top = start.Top;
        var right = start.Right;
        var bottom = start.Bottom;

        switch (_dragMode)
        {
            case "Move":
                left += dx;
                right += dx;
                top += dy;
                bottom += dy;
                break;
            case "Left":
                left += dx;
                break;
            case "Right":
                right += dx;
                break;
            case "Top":
                top += dy;
                break;
            case "Bottom":
                bottom += dy;
                break;
            case "TopLeft":
                left += dx;
                top += dy;
                break;
            case "TopRight":
                right += dx;
                top += dy;
                break;
            case "BottomLeft":
                left += dx;
                bottom += dy;
                break;
            case "BottomRight":
                right += dx;
                bottom += dy;
                break;
        }

        var box = Normalize(left, top, right, bottom);
        if (_dragMode == "Move")
        {
            _selection = ClampToBounds(box);
        }
        else
        {
            // 改尺寸：最小边限制 + 不越界
            var maxWidth = Math.Max(MinSelectionSize, ActualWidth);
            var maxHeight = Math.Max(MinSelectionSize, ActualHeight);
            box.Width = Math.Clamp(box.Width, MinSelectionSize, maxWidth);
            box.Height = Math.Clamp(box.Height, MinSelectionSize, maxHeight);
            box.X = Math.Clamp(box.X, 0, Math.Max(0, ActualWidth - box.Width));
            box.Y = Math.Clamp(box.Y, 0, Math.Max(0, ActualHeight - box.Height));
            _selection = box;
        }

        LayoutSelection();
        RaiseSelectionChanged();
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        _dragMode = null;
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }
    }

    private static Rect Normalize(double left, double top, double right, double bottom)
        => new(
            Math.Min(left, right),
            Math.Min(top, bottom),
            Math.Abs(right - left),
            Math.Abs(bottom - top));

    private Rect ClampToBounds(Rect box)
    {
        var width = Math.Min(box.Width, Math.Max(MinSelectionSize, ActualWidth));
        var height = Math.Min(box.Height, Math.Max(MinSelectionSize, ActualHeight));
        var x = Math.Clamp(box.X, 0, Math.Max(0, ActualWidth - width));
        var y = Math.Clamp(box.Y, 0, Math.Max(0, ActualHeight - height));
        return new Rect(x, y, width, height);
    }

    private void RaiseSelectionChanged() => SelectionChanged?.Invoke(_selection);

    /// <summary>把 <see cref="_selection"/> 反映到视觉元素上（4 块遮罩 + 边框 + 8 个手柄）。</summary>
    private void LayoutSelection()
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var box = _selection;
        Canvas.SetLeft(Selection, box.X);
        Canvas.SetTop(Selection, box.Y);
        Selection.Width = Math.Max(0, box.Width);
        Selection.Height = Math.Max(0, box.Height);

        SetRect(DimTop, 0, 0, width, Math.Max(0, box.Top));
        SetRect(DimBottom, 0, box.Bottom, width, Math.Max(0, height - box.Bottom));
        SetRect(DimLeft, 0, box.Top, Math.Max(0, box.Left), Math.Max(0, box.Height));
        SetRect(DimRight, box.Right, box.Top, Math.Max(0, width - box.Right), Math.Max(0, box.Height));

        var half = _handles[0].Width / 2;
        PositionHandle(HandleTopLeft, box.Left, box.Top, half);
        PositionHandle(HandleTop, box.Left + (box.Width / 2), box.Top, half);
        PositionHandle(HandleTopRight, box.Right, box.Top, half);
        PositionHandle(HandleRight, box.Right, box.Top + (box.Height / 2), half);
        PositionHandle(HandleBottomRight, box.Right, box.Bottom, half);
        PositionHandle(HandleBottom, box.Left + (box.Width / 2), box.Bottom, half);
        PositionHandle(HandleBottomLeft, box.Left, box.Bottom, half);
        PositionHandle(HandleLeft, box.Left, box.Top + (box.Height / 2), half);
    }

    private static void SetRect(FrameworkElement element, double left, double top, double width, double height)
    {
        Canvas.SetLeft(element, left);
        Canvas.SetTop(element, top);
        element.Width = width;
        element.Height = height;
    }

    private static void PositionHandle(FrameworkElement handle, double centerX, double centerY, double half)
    {
        Canvas.SetLeft(handle, centerX - half);
        Canvas.SetTop(handle, centerY - half);
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);

        // 宿主尺寸变了（移动/DPI 变化）：把选择框按比例缩回可用范围
        if (sizeInfo.NewSize.Width <= 0 || sizeInfo.NewSize.Height <= 0)
        {
            return;
        }

        _selection = ClampToBounds(_selection);
        LayoutSelection();
    }
}
