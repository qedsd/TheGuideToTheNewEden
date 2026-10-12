using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace TheGuideToTheNewEden.WPF.Helpers;

/// <summary>视觉树上的小查询（目前只服务于"嵌套按钮"的点击归属判定）。</summary>
public static class VisualTreeSearch
{
    /// <summary>
    /// 从 <paramref name="source"/> 向上走，途中是否先遇到某个按钮（即"点在子按钮上"）。
    /// <para>
    /// 用途：卡片本身是 <see cref="Button"/>，里面还有一个 ✕ / 添加 子按钮。WPF 里子按钮会
    /// 把鼠标事件标记为已处理，正常情况下父按钮的 <c>Click</c> 不会跟着触发；但主页的磁贴拖动
    /// 走的是 <c>PreviewMouseLeftButtonDown</c>（隧道事件，先于子按钮拿到），必须显式判断
    /// "按下的位置是不是子按钮"，否则按 ✕ 也会被当成开始拖动。
    /// </para>
    /// </summary>
    public static bool IsInsideChildButton(DependencyObject? source, Button hostButton)
    {
        var node = source;
        while (node is not null && !ReferenceEquals(node, hostButton))
        {
            if (node is Button)
            {
                return true;
            }

            node = node switch
            {
                Visual or Visual3D => VisualTreeHelper.GetParent(node),
                _ => LogicalTreeHelper.GetParent(node),
            };
        }

        return false;
    }
}
