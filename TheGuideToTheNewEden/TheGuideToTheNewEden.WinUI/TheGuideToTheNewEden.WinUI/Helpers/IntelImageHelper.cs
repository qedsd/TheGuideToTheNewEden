using OpenCvSharp;
using Syncfusion.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinRT;

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
                //Cv2.Sobel(afterBlur, afterCanny, graySource.Type(), 0, 1);
                //Cv2.Laplacian(graySource, afterCanny, graySource.Type(), 1, 1);
                return afterCanny;
            }
        }

        public static Mat CalStandingRects(Mat input,out Point[][] points)
        {
            Mat output = new Mat(input.Rows, input.Cols, MatType.CV_8UC3);
            points = null;
            int lineThreshold = (int)(input.Cols * 0.1);//每一行颜色超过此阈值才判断为有颜色
            int[] rowFill = new int[input.Rows];//表示每一行是否有颜色
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
                            rowFill[i] = 1;
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
            for (int i = 0; i < rowFill.Length; i++)
            {
                if (rowFill[i] == 1)
                {
                    for (int k = 0; k < input.Cols; k++)
                    {
                        //output.At<byte>(i, k) = 255;
                        //output.At<Vec3b>(i, k) = new Vec3b(255,255,255);
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
                else if(rectHeights.Count == 2)
                {
                    //有两个则看是否相差太大，太大就取大的，相差不大则取平均
                    if ((double)Math.Abs(rectHeights[1] - rectHeights[0]) /  Math.Max(rectHeights[0], rectHeights[1]) > 0.3)
                    {
                        rectHeight = Math.Max(rectHeights[0], rectHeights[1]);
                    }
                    else
                    {
                        rectHeight = rectHeights.Sum() / rectHeights.Count;
                    }
                }
                else if(rects.Count > 3)
                {
                    //有三个以上去最大最小值求平均
                    rectHeight = (rectHeights.Sum() - rectHeights.Max() - rectHeights.Min()) / (rectHeights.Count - 2);
                }

                //重新定位每个矩形上下边
                foreach(var rect in rects)
                {
                    var centerY = (rect.Y + rect.X) / 2;
                    var topY = centerY - rectHeight / 2;
                    topY = topY < 0 ? 0 : topY;
                    var bottomY = centerY + rectHeight / 2;
                    for (int k = 0; k < input.Cols; k++)
                    {
                        output.At<Vec3b>(topY, k) = new Vec3b(255, 0, 0);
                        output.At<Vec3b>(bottomY, k) = new Vec3b(255, 0, 0);
                    }
                }

                int[] colFillCount = new int[input.Cols];
                for (int i = 0; i < colFillCount.Length; i++)
                {
                    for(int j = 0; j < input.Rows; j++)
                    {
                        var p = input.At<byte>(j, i);
                        if (p != 0)
                        {
                            colFillCount[i]++;
                        }
                    }
                }
                //将从左到右开始的首个有颜色的像素占比大于30%的列判断为起始列
                //最后一个有颜色的像素占比大于30%的列判断为结束列
                int startCol = colFillCount.IndexOf(colFillCount.FirstOrDefault(p => p > input.Rows * 0.1));
                int endCol = colFillCount.IndexOf(colFillCount.LastOrDefault(p => p > input.Rows * 0.1));
                //绘制起始结束列
                for (int k = 0; k < input.Rows; k++)
                {
                    output.At<Vec3b>(k, startCol) = new Vec3b(0, 255, 0);
                    output.At<Vec3b>(k, endCol) = new Vec3b(0, 255, 0);
                }
            }
            return output;
        }
    }
}
