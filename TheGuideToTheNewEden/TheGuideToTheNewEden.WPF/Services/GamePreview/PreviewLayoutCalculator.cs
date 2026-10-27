using TheGuideToTheNewEden.WPF.Helpers.Interop;

namespace TheGuideToTheNewEden.WPF.Services.GamePreview;

/// <summary>
/// 依据"锚点窗口"把其余预览窗口排成一列/一行。纯计算，不碰窗口。
/// <para>
/// 坐标语义（工作区与窗口矩形均为物理像素）：
/// <list type="bullet">
///   <item><c>mode</c>：0 向左排（锚点左侧）/ 1 向右排（锚点右侧）/ 2 向下排 / 3 向上排；</item>
///   <item><c>align</c>：交叉轴相对锚点的 0 起始 / 1 居中 / 2 末尾对齐；</item>
///   <item><c>span</c>：窗口之间、行/列之间的间隔；</item>
///   <item><c>maxPerLine</c>：每行/列最多放几个**非锚点**窗口，&lt;=0 表示不限。</item>
/// </list>
/// </para>
/// <para>
/// 关键修正（阶段 194）：
/// <list type="number">
///   <item>向左/向上的推进方向在负方向：起始边是锚点边缘，窗口**左上角要再减去自身宽/高**，
///         否则窗口会压住锚点（旧实现就是从这里开始重叠的）；</item>
///   <item>先按"主方向能否放进工作区"切行/列，再在交叉轴上一行行铺开，最后把每行整体夹进工作区；</item>
///   <item>横向排列时的 X 夹取用错成工作区底边（旧实现），会算出屏幕外的位置/重叠；</item>
///   <item>mode 0/1 与本地化文案统一为"向左 = 锚点左侧、向右 = 锚点右侧"，不再出现文字与行为相反。</item>
/// </list>
/// </para>
/// </summary>
internal static class PreviewLayoutCalculator
{
    internal readonly record struct Size(int Width, int Height);

    /// <param name="anchor">锚点窗口矩形（物理像素），不会被移动。</param>
    /// <param name="items">待摆放窗口的尺寸，按期望顺序。</param>
    /// <param name="workArea">目标显示器工作区（物理像素）。</param>
    /// <param name="mode">0 向左 / 1 向右 / 2 向下 / 3 向上。</param>
    /// <param name="align">交叉轴对齐：0 起始 / 1 居中 / 2 末尾（相对锚点）。</param>
    /// <param name="span">间隔。</param>
    /// <param name="maxPerLine">每行/列最多几个非锚点窗口；&lt;=0 表示不限。</param>
    internal static List<(int X, int Y)> Compute(
        NativeMethods.RECT anchor,
        IReadOnlyList<Size> items,
        NativeMethods.RECT workArea,
        int mode,
        int align,
        int span,
        int maxPerLine)
    {
        var result = new List<(int X, int Y)>(items.Count);
        if (items.Count == 0)
        {
            return result;
        }

        span = Math.Max(0, span);
        mode = Math.Clamp(mode, 0, 3);
        maxPerLine = Math.Max(0, maxPerLine);

        // 主方向：0 向左、1 向右、2 向下、3 向上。只有 1/2 是正方向。
        var vertical = mode is 2 or 3;
        var positivePrimary = mode is 1 or 2;
        var primarySign = positivePrimary ? 1 : -1;

        int PrimarySize(Size size) => Math.Max(1, vertical ? size.Height : size.Width);

        int CrossSize(Size size) => Math.Max(1, vertical ? size.Width : size.Height);

        // 向左/向上时，primaryStart 是第一个窗口靠近锚点的那条边（右边缘/下边缘），
        // 摆放时还要减去窗口自身的宽/高；向右/向下时就是左上角。
        var primaryStart = mode switch
        {
            0 => anchor.Left - span,
            1 => anchor.Right + span,
            2 => anchor.Bottom + span,
            _ => anchor.Top - span,
        };
        var primaryLimit = mode switch
        {
            0 => workArea.Left,
            1 => workArea.Right,
            2 => workArea.Bottom,
            _ => workArea.Top,
        };

        // 锚点贴着工作区边缘时，选中的方向可能连一个窗口都放不下（例如锚点在最左边却选"向左排"）。
        // 这种情况下翻到反方向，避免把窗口硬夹回屏幕边缘、压在锚点上。
        if (items.Count > 0)
        {
            bool FitsFirst(int start, int sign, int limit, int size)
            {
                var farEdge = start + (sign * size);
                return sign > 0 ? farEdge <= limit : farEdge >= limit;
            }

            var firstSize = PrimarySize(items[0]);
            if (!FitsFirst(primaryStart, primarySign, primaryLimit, firstSize))
            {
                var flipSign = -primarySign;
                var flipStart = mode switch
                {
                    0 => anchor.Right + span,
                    1 => anchor.Left - span,
                    2 => anchor.Top - span,
                    _ => anchor.Bottom + span,
                };
                var flipLimit = mode switch
                {
                    0 => workArea.Right,
                    1 => workArea.Left,
                    2 => workArea.Top,
                    _ => workArea.Bottom,
                };

                if (FitsFirst(flipStart, flipSign, flipLimit, firstSize))
                {
                    primarySign = flipSign;
                    positivePrimary = flipSign > 0;
                    primaryStart = flipStart;
                    primaryLimit = flipLimit;
                }
            }
        }

        // 交叉轴：向左/右排时是 Y，向下/上排时是 X。
        var crossMin = vertical ? workArea.Left : workArea.Top;
        var crossMax = vertical ? workArea.Right : workArea.Bottom;
        var anchorCrossStart = vertical ? anchor.Left : anchor.Top;
        var anchorCrossSize = vertical ? anchor.Width : anchor.Height;

        // ---- 1. 切行/列：只按主方向是否放得下、以及每行上限 ----
        var lines = new List<List<int>>();
        var line = new List<int>();
        var used = 0;
        for (var i = 0; i < items.Count; i++)
        {
            var size = PrimarySize(items[i]);
            var nextUsed = line.Count == 0 ? size : used + span + size;
            var farEdge = primaryStart + (primarySign * nextUsed);
            var fits = positivePrimary ? farEdge <= primaryLimit : farEdge >= primaryLimit;
            var full = maxPerLine > 0 && line.Count >= maxPerLine;

            if (line.Count > 0 && (!fits || full))
            {
                lines.Add(line);
                line = [];
                nextUsed = size;
            }

            line.Add(i);
            used = nextUsed;
        }

        if (line.Count > 0)
        {
            lines.Add(line);
        }

        // ---- 2. 每行/列的交叉轴尺寸与位置 ----
        var crossSizes = new int[lines.Count];
        for (var li = 0; li < lines.Count; li++)
        {
            crossSizes[li] = lines[li].Max(index => CrossSize(items[index]));
        }

        int ClampCross(int position, int size)
        {
            var max = Math.Max(crossMin, crossMax - size);
            return Math.Clamp(position, crossMin, max);
        }

        var crossPositions = new int[lines.Count];
        var firstCross = align switch
        {
            1 => anchorCrossStart + ((anchorCrossSize - crossSizes[0]) / 2),
            2 => anchorCrossStart + anchorCrossSize - crossSizes[0],
            _ => anchorCrossStart,
        };
        crossPositions[0] = ClampCross(firstCross, crossSizes[0]);

        var forwardEdge = crossPositions[0] + crossSizes[0];
        var backwardEdge = crossPositions[0];
        for (var li = 1; li < lines.Count; li++)
        {
            var crossSize = crossSizes[li];

            // 先往"正向"（下 / 右）铺；放不下再往反方向（上 / 左）铺。
            var forward = forwardEdge + span;
            if (forward + crossSize <= crossMax)
            {
                crossPositions[li] = forward;
                forwardEdge = forward + crossSize;
                continue;
            }

            var backward = backwardEdge - span - crossSize;
            if (backward >= crossMin)
            {
                crossPositions[li] = backward;
                backwardEdge = backward;
                continue;
            }

            // 两个方向都放不下（窗口太多/工作区太小）：夹进工作区，保持全部窗口可见。
            // 此时重叠不可避免，但这是容量问题，不是方向算错。
            crossPositions[li] = ClampCross(forward, crossSize);
            forwardEdge = crossPositions[li] + crossSize;
        }

        // ---- 3. 行内按主方向逐窗摆放 ----
        var positions = new (int X, int Y)[items.Count];
        for (var li = 0; li < lines.Count; li++)
        {
            var primary = primaryStart;
            var cross = crossPositions[li];

            foreach (var index in lines[li])
            {
                var size = items[index];
                var width = Math.Max(1, size.Width);
                var height = Math.Max(1, size.Height);
                int x;
                int y;

                if (vertical)
                {
                    x = cross;
                    y = positivePrimary ? primary : primary - height;
                    primary += primarySign * (height + span);
                }
                else
                {
                    x = positivePrimary ? primary : primary - width;
                    y = cross;
                    primary += primarySign * (width + span);
                }

                positions[index] = (x, y);
            }
        }

        // ---- 4. 每行整体夹进工作区（保持行内相对间距，避免逐个夹取把窗口叠在一起） ----
        foreach (var indices in lines)
        {
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;
            foreach (var index in indices)
            {
                var (x, y) = positions[index];
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x + Math.Max(1, items[index].Width));
                maxY = Math.Max(maxY, y + Math.Max(1, items[index].Height));
            }

            var shiftX = 0;
            var shiftY = 0;
            if (maxX > workArea.Right)
            {
                shiftX = workArea.Right - maxX;
            }

            if (minX + shiftX < workArea.Left)
            {
                shiftX += workArea.Left - (minX + shiftX);
            }

            if (maxY > workArea.Bottom)
            {
                shiftY = workArea.Bottom - maxY;
            }

            if (minY + shiftY < workArea.Top)
            {
                shiftY += workArea.Top - (minY + shiftY);
            }

            if (shiftX == 0 && shiftY == 0)
            {
                continue;
            }

            foreach (var index in indices)
            {
                var (x, y) = positions[index];
                positions[index] = (x + shiftX, y + shiftY);
            }
        }

        // ---- 5. 最终兜底：窗口本身仍可能比工作区还大，逐个夹进可见范围 ----
        for (var i = 0; i < items.Count; i++)
        {
            var width = Math.Max(1, items[i].Width);
            var height = Math.Max(1, items[i].Height);
            var maxX = Math.Max(workArea.Left, workArea.Right - width);
            var maxY = Math.Max(workArea.Top, workArea.Bottom - height);
            var (x, y) = positions[i];
            result.Add((
                Math.Clamp(x, workArea.Left, maxX),
                Math.Clamp(y, workArea.Top, maxY)));
        }

        return result;
    }
}
