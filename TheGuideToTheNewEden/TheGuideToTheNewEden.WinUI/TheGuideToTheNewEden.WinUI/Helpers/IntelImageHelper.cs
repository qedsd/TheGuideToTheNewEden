using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheGuideToTheNewEden.WinUI.Helpers
{
    internal static class IntelImageHelper
    {
        public static Mat GetEdge(System.Drawing.Image img)
        {
            using (var memory = new MemoryStream())
            {
                img.Save(memory, ImageFormat.Png);
                memory.Position = 0;
                Mat graySource = Mat.FromStream(memory, ImreadModes.Grayscale);
                Mat afterBlur = new Mat();
                Cv2.GaussianBlur(graySource, afterBlur, new Size(3,3), 0);
                Mat afterCanny = new Mat();
                Cv2.Canny(afterBlur, afterCanny, 200, 200);
                return afterCanny;
            }
        }

        public static Mat CalStandingRects(Mat input,out Point[][] points)
        {
            Mat output = new Mat(input.Rows, input.Cols, input.Type());
            points = null;
            int lineThreshold = (int)(input.Cols * 0.05);//每一行颜色超过此阈值才判断为有颜色
            int[] lineFill = new int[input.Rows];//表示每一行是否有颜色
            for (int i = 0; i < input.Rows; i++)
            {
                int count = 0;
                for (int j = 0; j < input.Cols; j++)
                {
                    var p = input.At<byte>(i, j);
                    if(p != 0)
                    {
                        count++;
                        if(count >= lineThreshold)
                        {
                            lineFill[i] = 1;
                            break;
                        }
                    }
                }
            }
            bool foundStartLine = false;//是否已找到当前矩形开始行
            int startRow = -1;//当前矩形最上方所在行数
            int lastFillRow = -1;//最后一次找到有颜色一行的行数
            int fillLineSpanThreshold = 3;//连续空白多少行才结束一个矩形
            int emptyLines = 0;
            List<Point> rects = new List<Point>();//使用xy来分别表示矩形最上面和最下面行数
            List<int> rectHeights = new List<int>();
            for (int i = 0; i < lineFill.Length; i++)
            {
                if (lineFill[i] == 1)
                {
                    for (int k = 0; k < input.Cols; k++)
                    {
                        output.At<byte>(i, k) = 255;
                    }
                    if(!foundStartLine)
                    {
                        startRow = i;
                        foundStartLine = true;
                    }
                    lastFillRow = i;
                    emptyLines = 0;
                }
                else
                {
                    emptyLines++;
                    if (foundStartLine && startRow != lastFillRow && emptyLines > fillLineSpanThreshold)
                    {
                        //结束当前矩形
                        rects.Add(new Point(startRow, lastFillRow));
                        rectHeights.Add(lastFillRow - startRow);
                        foundStartLine = false;
                        startRow = -1;
                        lastFillRow = -1;
                    }
                }
            }
            int rectHeight = 0;
            if(rectHeights.Any())
            {
                if(rectHeights.Count == 1)
                {
                    //只有一个则以这个为参考
                    rectHeight = rectHeights[0];
                }
                else if(rects.Count == 2)
                {
                    
                }
                else if(rects.Count > 3)
                {
                    var group = rectHeights.GroupBy(p=>p);
                }
            }
            return output;
        }
    }
}
