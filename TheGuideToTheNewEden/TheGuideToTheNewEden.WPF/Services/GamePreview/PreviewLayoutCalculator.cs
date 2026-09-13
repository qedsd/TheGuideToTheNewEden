using TheGuideToTheNewEden.WPF.Helpers.Interop;

namespace TheGuideToTheNewEden.WPF.Services.GamePreview;

/// <summary>
/// 依据"锚点窗口"把其余预览窗口排成一列/一行。纯计算，不碰窗口。
/// <para>
/// 与 WinUI 版相比的行为修正（原实现见 REFACTORING 记录）：
/// ① 先按"能否放进显示器工作区"和"每行上限"切好行，再逐行定位，不再边放边试；
/// ② 换行后从锚点同侧起排（原实现换行后从锚点自身边缘起排，导致第二行压住锚点）；
/// ③ 纵向/横向的换行推进统一按"该行最大交叉尺寸"计算（原实现有两处把宽高写反）；
/// ④ 所有结果夹进工作区，窗口不会被放到屏幕外。
/// </para>
/// </summary>
internal static class PreviewLayoutCalculator
{
    internal readonly record struct Size(int Width, int Height);

    /// <param name="anchor">锚点窗口矩形（物理像素），不会被移动。</param>
    /// <param name="items">待摆放窗口的尺寸，按期望顺序。</param>
    /// <param name="workArea">目标显示器工作区（物理像素）。</param>
    /// <param name="mode">0 向左排（锚点右侧）/ 1 向右排 / 2 向下排 / 3 向上排。</param>
    /// <param name="align">交叉轴对齐：0 起始 / 1 居中 / 2 末尾（相对锚点）。</param>
    /// <param name="span">间隔。</param>
    /// <param name="maxPerLine">每行最多几个；&lt;=0 表示不限。</param>
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
        var vertical = mode is 2 or 3;
        var primarySign = mode is 0 or 2 ? 1 : -1;

        var primaryStart = mode switch
        {
            0 => anchor.Right + span,
            1 => anchor.Left - span,
            2 => anchor.Bottom + span,
            _ => anchor.Top - span,
        };
        var crossStart = vertical ? anchor.Left : anchor.Top;
        var anchorCrossSize = vertical ? anchor.Width : anchor.Height;
        var primaryLimit = vertical
            ? (primarySign > 0 ? workArea.Bottom : workArea.Top)
            : (primarySign > 0 ? workArea.Right : workArea.Left);
        var crossMin = vertical ? workArea.Left : workArea.Top;
        var crossMax = vertical ? workArea.Right : workArea.Bottom;

        int PrimarySize(Size s) => vertical ? s.Height : s.Width;

        int CrossSize(Size s) => vertical ? s.Width : s.Height;

        // ---- 1. 切行 ----
        var lines = new List<List<int>>();
        var line = new List<int>();
        var lineExtent = 0;
        for (var i = 0; i < items.Count; i++)
        {
            var size = PrimarySize(items[i]);
            var projected = line.Count == 0 ? size : lineExtent + span + size;
            var farEdge = primaryStart + (primarySign * projected);
            var fits = primarySign > 0 ? farEdge <= primaryLimit : farEdge >= primaryLimit;

            if (line.Count > 0 && (!fits || (maxPerLine > 0 && line.Count >= maxPerLine)))
            {
                lines.Add(line);
                line = [];
                projected = size;
            }

            line.Add(i);
            lineExtent = projected;
        }

        if (line.Count > 0)
        {
            lines.Add(line);
        }

        // ---- 2. 逐行定位 ----
        var crossForward = true;
        var forwardEdge = 0;
        var backwardEdge = 0;
        var positions = new (int X, int Y)[items.Count];

        for (var li = 0; li < lines.Count; li++)
        {
            var currentLine = lines[li];
            var lineCross = 0;
            foreach (var index in currentLine)
            {
                lineCross = Math.Max(lineCross, CrossSize(items[index]));
            }

            int crossPos;
            if (li == 0)
            {
                crossPos = align switch
                {
                    1 => crossStart + ((anchorCrossSize - lineCross) / 2),
                    2 => crossStart + anchorCrossSize - lineCross,
                    _ => crossStart,
                };
                forwardEdge = crossPos + lineCross;
                backwardEdge = crossPos;
            }
            else if (crossForward)
            {
                var candidate = forwardEdge + span;
                if (candidate + lineCross <= crossMax)
                {
                    crossPos = candidate;
                    forwardEdge = crossPos + lineCross;
                }
                else
                {
                    // 正向放不下，改为往反方向排
                    crossForward = false;
                    crossPos = backwardEdge - span - lineCross;
                    backwardEdge = crossPos;
                }
            }
            else
            {
                var candidate = backwardEdge - span - lineCross;
                if (candidate >= crossMin)
                {
                    crossPos = candidate;
                    backwardEdge = crossPos;
                }
                else
                {
                    crossForward = true;
                    crossPos = forwardEdge + span;
                    forwardEdge = crossPos + lineCross;
                }
            }

            var primary = primaryStart;
            foreach (var index in currentLine)
            {
                var size = items[index];
                var x = vertical ? crossPos : primary;
                var y = vertical ? primary : crossPos;
                positions[index] = (x, y);
                primary += primarySign * (PrimarySize(size) + span);
            }
        }

        // ---- 3. 夹进工作区 ----
        for (var i = 0; i < items.Count; i++)
        {
            var maxX = Math.Max(workArea.Left, crossMax - items[i].Width);
            var maxY = Math.Max(workArea.Top, workArea.Bottom - items[i].Height);
            var (x, y) = positions[i];
            if (vertical)
            {
                result.Add((Math.Clamp(x, crossMin, Math.Max(crossMin, crossMax - items[i].Width)), Math.Clamp(y, workArea.Top, maxY)));
            }
            else
            {
                result.Add((Math.Clamp(x, workArea.Left, maxX), Math.Clamp(y, crossMin, Math.Max(crossMin, maxY))));
            }
        }

        return result;
    }
}
