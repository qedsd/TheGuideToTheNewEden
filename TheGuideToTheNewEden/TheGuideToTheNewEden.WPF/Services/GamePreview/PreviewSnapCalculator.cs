using TheGuideToTheNewEden.WPF.Helpers.Interop;

namespace TheGuideToTheNewEden.WPF.Services.GamePreview;

/// <summary>
/// 预览窗口拖动吸附：只与**其它可见预览窗**对齐，不吸屏幕边缘。
/// <para>
/// 每个轴独立判断，命中阈值内的最近一条边后只改该轴的坐标：
/// 因此顶边吸附之后仍可左右拖动（沿对齐边滑动），两个轴同时命中就是角对齐。
/// </para>
/// <para>
/// 候选边（X 轴，Y 轴同理）：
/// <list type="bullet">
///   <item>同边对齐：左对左 / 右对右；</item>
///   <item>相邻贴合：本窗左边贴目标右边、本窗右边贴目标左边；</item>
///   <item>Y 轴：上对上 / 下对下 / 上贴下 / 下贴上。</item>
/// </list>
/// 为避免"隔着半屏也吸附"，X 轴候选要求两窗在垂直方向有交叠（或相距不超过阈值），
/// Y 轴候选要求水平方向有交叠（或相距不超过阈值）。
/// </para>
/// </summary>
internal static class PreviewSnapCalculator
{
    internal readonly record struct SnapResult(int X, int Y, bool SnappedX, bool SnappedY);

    /// <summary>
    /// 把 <paramref name="moving"/> 的位置吸附到 <paramref name="targets"/> 的边/角。
    /// 宽高不变；没有命中时原样返回。
    /// </summary>
    internal static SnapResult Snap(
        NativeMethods.RECT moving,
        IReadOnlyList<NativeMethods.RECT> targets,
        int threshold)
    {
        var x = moving.Left;
        var y = moving.Top;
        if (threshold <= 0 || targets.Count == 0)
        {
            return new SnapResult(x, y, false, false);
        }

        var bestX = int.MaxValue;
        var bestXDelta = 0;
        var bestY = int.MaxValue;
        var bestYDelta = 0;

        foreach (var target in targets)
        {
            if (target.Width <= 0 || target.Height <= 0)
            {
                continue;
            }

            // X 轴：两窗在垂直方向要"够近/有交叠"，否则隔着很远的窗口也会被横向拽过去
            if (Near(moving.Top, moving.Bottom, target.Top, target.Bottom, threshold))
            {
                Consider(target.Left - moving.Left);
                Consider(target.Right - moving.Right);
                Consider(target.Right - moving.Left);
                Consider(target.Left - moving.Right);
            }

            // Y 轴：两窗在水平方向要"够近/有交叠"
            if (Near(moving.Left, moving.Right, target.Left, target.Right, threshold))
            {
                ConsiderY(target.Top - moving.Top);
                ConsiderY(target.Bottom - moving.Bottom);
                ConsiderY(target.Bottom - moving.Top);
                ConsiderY(target.Top - moving.Bottom);
            }
        }

        var snappedX = bestXDelta != 0;
        var snappedY = bestYDelta != 0;
        return new SnapResult(
            x + bestXDelta,
            y + bestYDelta,
            snappedX,
            snappedY);

        void Consider(int delta)
        {
            var distance = Math.Abs(delta);
            if (distance <= threshold && distance < bestX)
            {
                bestX = distance;
                bestXDelta = delta;
            }
        }

        void ConsiderY(int delta)
        {
            var distance = Math.Abs(delta);
            if (distance <= threshold && distance < bestY)
            {
                bestY = distance;
                bestYDelta = delta;
            }
        }
    }

    /// <summary>一维区间 [aMin,aMax] 与 [bMin,bMax] 是否交叠，或最近距离不超过 margin。</summary>
    private static bool Near(int aMin, int aMax, int bMin, int bMax, int margin)
        => aMin <= bMax + margin && aMax >= bMin - margin;
}

/// <summary>
/// 一次拖动过程中的吸附状态。
/// <para>
/// 为什么要有状态：仅按"当前鼠标位置"每帧重算，窗口沿对齐边滑出目标窗口的横向/纵向范围后，
/// 吸附会立刻解除（因为交叉轴不再接近）。而用户要的是"贴住一条边后可以沿这条边继续移动"，
/// 所以某个轴一旦吸附成功，就在拖动期间**保持**该轴的坐标，直到鼠标在垂直方向离开吸附距离
/// 或结束拖动。
/// </para>
/// </summary>
internal sealed class PreviewSnapSession
{
    private bool _xActive;
    private bool _yActive;
    private int _snapX;
    private int _snapY;

    public void Reset()
    {
        _xActive = false;
        _yActive = false;
    }

    public PreviewSnapCalculator.SnapResult Snap(
        NativeMethods.RECT moving,
        IReadOnlyList<NativeMethods.RECT> targets,
        int threshold)
    {
        if (threshold <= 0 || targets.Count == 0)
        {
            Reset();
            return new PreviewSnapCalculator.SnapResult(moving.Left, moving.Top, false, false);
        }

        var x = moving.Left;
        var y = moving.Top;
        var snappedX = false;
        var snappedY = false;

        // 已吸附的轴：只要原始拖动位置还在吸附距离内，就继续钉在原来的边上
        if (_xActive)
        {
            if (Math.Abs(moving.Left - _snapX) <= threshold)
            {
                x = _snapX;
                snappedX = true;
            }
            else
            {
                _xActive = false;
            }
        }

        if (_yActive)
        {
            if (Math.Abs(moving.Top - _snapY) <= threshold)
            {
                y = _snapY;
                snappedY = true;
            }
            else
            {
                _yActive = false;
            }
        }

        // 未吸附的轴再做一次"就近找边/角"
        if (!snappedX || !snappedY)
        {
            var proposal = new NativeMethods.RECT
            {
                Left = x,
                Top = y,
                Right = x + moving.Width,
                Bottom = y + moving.Height,
            };
            var fresh = PreviewSnapCalculator.Snap(proposal, targets, threshold);

            if (!snappedX && fresh.SnappedX)
            {
                x = fresh.X;
                snappedX = true;
                _snapX = x;
                _xActive = true;
            }

            if (!snappedY && fresh.SnappedY)
            {
                y = fresh.Y;
                snappedY = true;
                _snapY = y;
                _yActive = true;
            }
        }

        return new PreviewSnapCalculator.SnapResult(x, y, snappedX, snappedY);
    }
}
