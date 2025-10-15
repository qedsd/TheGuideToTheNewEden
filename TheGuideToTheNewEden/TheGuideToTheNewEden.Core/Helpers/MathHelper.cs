using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TheGuideToTheNewEden.Core.Helpers
{
    public static class MathHelper
    {
        /// <summary>
        /// 计算线性回归
        /// </summary>
        public static  (double slope, double intercept, double rSquared) CalculateLinearRegression(double[] x, double[] y)
        {
            if (x.Length != y.Length)
                throw new ArgumentException("x和y数组长度必须相同");

            var n = x.Length;
            var sumX = x.Sum();
            var sumY = y.Sum();
            var sumXY = x.Zip(y, (a, b) => a * b).Sum();
            var sumX2 = x.Select(val => val * val).Sum();
            var sumY2 = y.Select(val => val * val).Sum();

            // 计算斜率 (m) 和截距 (b)
            var denominator = n * sumX2 - sumX * sumX;
            if (Math.Abs(denominator) < 1e-10)
                return (0, y.Average(), 0); // 避免除零，返回水平线

            var slope = (n * sumXY - sumX * sumY) / denominator;
            var intercept = (sumY - slope * sumX) / n;

            // 计算R平方
            var yMean = sumY / n;
            var totalSumSquares = y.Select(yi => (yi - yMean) * (yi - yMean)).Sum();
            var residualSumSquares = y.Zip(x, (yi, xi) =>
            {
                var predicted = slope * xi + intercept;
                return (yi - predicted) * (yi - predicted);
            }).Sum();

            var rSquared = 1.0 - (residualSumSquares / totalSumSquares);

            return (slope, intercept, rSquared);
        }
    }
}
