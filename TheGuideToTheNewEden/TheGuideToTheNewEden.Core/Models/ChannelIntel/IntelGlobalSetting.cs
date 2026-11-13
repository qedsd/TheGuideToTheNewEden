using CommunityToolkit.Mvvm.ComponentModel;

namespace TheGuideToTheNewEden.Core.Models.ChannelIntel
{
    /// <summary>
    /// 频道预警小窗的**全局**外观设置（阶段 307）：全部小窗共用。
    /// </summary>
    public class IntelGlobalSetting : ObservableObject
    {

    private double labelWidthThreshold = 500;
    /// <summary>星图名称标签宽度阈值：画布宽度低于该值（px）不显示星系名；0 = 总是显示。</summary>
    public double LabelWidthThreshold
    {
        get => labelWidthThreshold;
        set => SetProperty(ref labelWidthThreshold, value);
    }

    private int uniformWidth = 360;
    /// <summary>统一窗口宽度（DIP）。</summary>
    public int UniformWidth
    {
        get => uniformWidth;
        set => SetProperty(ref uniformWidth, value);
    }

    private int uniformHeight = 360;
    /// <summary>统一窗口高度（DIP）。</summary>
    public int UniformHeight
    {
        get => uniformHeight;
        set => SetProperty(ref uniformHeight, value);
    }

    private bool snapEnabled = true;
    /// <summary>拖动吸附（与其它预警小窗对齐，不吸屏幕边缘）。</summary>
    public bool SnapEnabled
    {
        get => snapEnabled;
        set => SetProperty(ref snapEnabled, value);
    }

    private int snapDistance = 12;
    /// <summary>吸附触发距离（px）。</summary>
    public int SnapDistance
    {
        get => snapDistance;
        set => SetProperty(ref snapDistance, value);
    }

    private int layoutMode = 2;
    /// <summary>自动排列方向：0 向左 / 1 向右 / 2 向下 / 3 向上（相对锚点窗）。</summary>
    public int LayoutMode
    {
        get => layoutMode;
        set => SetProperty(ref layoutMode, value);
    }

    private int layoutAlign = 1;
    /// <summary>自动排列交叉轴对齐：0 起始 / 1 居中 / 2 末尾。</summary>
    public int LayoutAlign
    {
        get => layoutAlign;
        set => SetProperty(ref layoutAlign, value);
    }

    private int layoutSpan = 8;
    /// <summary>自动排列间隔（px）。</summary>
    public int LayoutSpan
    {
        get => layoutSpan;
        set => SetProperty(ref layoutSpan, value);
    }

    private int layoutMaxPerLine;
    /// <summary>每行/列最多放几个非锚点窗；0 = 不限。</summary>
    public int LayoutMaxPerLine
    {
        get => layoutMaxPerLine;
        set => SetProperty(ref layoutMaxPerLine, value);
    }
}
}