# -*- coding: utf-8 -*-
"""把 design/branding/icons/nav_*.svg 转成 WPF ResourceDictionary（DrawingImage 图标资源）。

产出：TheGuideToTheNewEden.WPF/Resources/NavIcons.xaml
每个图标一枚 DrawingImage，x:Key="NavIcon.<id>"，供 NavigationViewItem.Icon 引用。

处理要点（都是踩过的坑）：
  · fill="none" / stroke="none" 表示"不画"，不能当画刷输出；
  · 同一形状同时有 stroke 与 fill 时，合并进**一个** GeometryDrawing（Pen + Brush），
    否则会画两遍；
  · 旋转 transform（本套图标用了 rotate，如虫洞同心椭圆、设置齿轮八瓣）要转成
    WPF 的 RotateTransform 挂到独立 DrawingGroup 上，不能丢；
  · <g> 上的属性（stroke / stroke-width / transform）要被子元素继承。
"""
import os
import re

ICON_DIR = r"D:\QEDSD\Code\TheGuideToTheNewEden\design\branding\icons"
OUT = (r"D:\QEDSD\Code\TheGuideToTheNewEden\TheGuideToTheNewEden"
       r"\TheGuideToTheNewEden.WPF\Resources\NavIcons.xaml")

ORDER = [
    ("home", "主页"), ("characters", "角色"), ("business", "商业"), ("gamePreview", "多开"),
    ("assistant", "AI 助手"), ("channel", "频道"), ("translation", "翻译"), ("guides", "攻略"),
    ("wormhole", "虫洞"), ("links", "快速链接"), ("map", "星图"), ("zkb", "Zkillboard"),
    ("database", "数据库"), ("eveSettingsSync", "配置同步"), ("tools", "小工具"),
    ("market", "市场"), ("order", "订单"), ("scalper", "倒货"),
    ("compareMarket", "比价"), ("appraisal", "估价"),
    ("channelIntel", "频道预警"), ("channelMonitor", "频道监控"), ("channelScan", "频道统计"),
    ("channelMarket", "频道查价"), ("channelTranslation", "频道翻译"), ("gameLogMonitor", "日志监控"),
    ("translationAi", "AI 翻译"), ("translationLocal", "本地词库"),
    ("ded", "死亡远征"), ("mission", "任务攻略"),
    ("toolsIME", "快捷输入"), ("serverPing", "游戏延迟"),
    ("keyboardSpan", "键盘"), ("taskbar", "分屏"),
    ("settings", "设置"),
]

TAG_RE = re.compile(r"<(path|circle|ellipse|rect|line|polygon)\b([^>]*?)/?>", re.S)
ATTR_RE = re.compile(r'([\w-]+)\s*=\s*"([^"]*)"')
ROT_RE = re.compile(r"rotate\(\s*([-\d.]+)\s+([-\d.]+)\s+([-\d.]+)\s*\)")

# 导航栏图标显示尺寸。Fluent 导航项的标准图标是 20×20（WPF-UI 的 NavigationViewItem 图标槽同款），
# 设计稿是 128 坐标空间，故：
#   · DrawingImage **显式** Width/Height = 20 —— 靠 ScaleTransform 不足以约束固有尺寸，
#     ImageIcon 会按绘制 bounds 渲染（实测会到 45px，几乎撑满 49px 的行高）；
#   · 描边 ×1.25 加粗（128 空间的 8–9px 描边缩到 20px 只剩 ~1.3px，会糊），
#     与 app 图标"小尺寸要加粗"的结论一致。
NAV_TARGET = 20.0
NAV_SCALE = NAV_TARGET / 128.0          # 0.15625
STROKE_SCALE = 1.25


def parse_rotate(transform: str):
    """把 SVG 的 rotate(a cx cy) 解析成 (angle, cx, cy)；无旋转返回 None。"""
    if not transform:
        return None
    m = ROT_RE.search(transform)
    return (float(m.group(1)), float(m.group(2)), float(m.group(3))) if m else None


def parse_shape(tag, attrs):
    """统一转成 Path mini-language 的 Data 片段。"""
    a = dict(ATTR_RE.findall(attrs))
    seg = ""
    if tag == "path":
        seg = a.get("d", "")
    elif tag == "circle":
        cx, cy, r = float(a["cx"]), float(a["cy"]), float(a["r"])
        seg = (f"M{cx - r},{cy} A{r},{r} 0 1 0 {cx + r},{cy} "
               f"A{r},{r} 0 1 0 {cx - r},{cy} Z")
    elif tag == "ellipse":
        cx, cy, rx, ry = float(a["cx"]), float(a["cy"]), float(a["rx"]), float(a["ry"])
        seg = (f"M{cx - rx},{cy} A{rx},{ry} 0 1 0 {cx + rx},{cy} "
               f"A{rx},{ry} 0 1 0 {cx - rx},{cy} Z")
    elif tag == "rect":
        x, y = float(a.get("x", 0)), float(a.get("y", 0))
        w, h = float(a["width"]), float(a["height"])
        r = float(a.get("rx", 0))
        if r > 0:
            seg = (f"M{x + r},{y} H{x + w - r} A{r},{r} 0 0 1 {x + w},{y + r} "
                   f"V{y + h - r} A{r},{r} 0 0 1 {x + w - r},{y + h} "
                   f"H{x + r} A{r},{r} 0 0 1 {x},{y + h - r} "
                   f"V{y + r} A{r},{r} 0 0 1 {x + r},{y} Z")
        else:
            seg = f"M{x},{y} H{x + w} V{y + h} H{x} Z"
    elif tag == "line":
        seg = f"M{a['x1']},{a['y1']} L{a['x2']},{a['y2']}"
    return seg, a


def apply_opacity(color, opacity):
    """把 opacity（0~1）折算成颜色的 alpha 通道，返回 #AARRGGBB。

    为什么必须折算而不能丢：SVG 的 opacity 是**普通的半透明**，
    丢掉会让淡色元素变实心（如 nav_zkb 的 50% 红环会变成刺眼实心红环）。
    WPF 的 Brush 若用 #RRGGBB 字符串就不带透明度，故这里统一折进 alpha。
    """
    if not color or color.lower() == "none" or not color.startswith("#"):
        return color

    hexp = color[1:]
    if len(hexp) == 3:
        hexp = "".join(c * 2 for c in hexp)
    if len(hexp) != 6:
        return color          # 本套图标只用 6 位色；异常值原样返回

    alpha = 255
    if opacity is not None and opacity < 1.0:
        alpha = max(0, min(255, round(float(opacity) * 255)))
    return f"#{alpha:02X}{hexp.upper()}"


def collect(svg_text):
    """返回 [(seg, stroke, fill, stroke_width, rotate)]，已展开 <g> 的继承属性。

    stroke / fill 已按 opacity（元素级 × 组级）折算进 alpha 通道。
    """
    items = []

    def emit(tag, attrs, gattrs):
        seg, a = parse_shape(tag, attrs)
        if not seg:
            return
        def pick(key):
            v = a.get(key)
            return v if v is not None else gattrs.get(key)

        # opacity：元素自己的 opacity 优先，否则继承组的
        eff = a.get("opacity", gattrs.get("opacity"))

        def with_op(key, op_key):
            """把 fill-opacity / stroke-opacity 也算进去。"""
            base = pick(key)
            if not base:
                return base
            o = a.get(op_key, gattrs.get(op_key))
            if o is not None:
                # 与通用 opacity 相乘
                e = float(eff) if eff is not None else 1.0
                base = apply_opacity(base, e * float(o))
                return base
            return apply_opacity(base, float(eff) if eff is not None else None)

        items.append((
            seg,
            with_op("stroke", "stroke-opacity"),
            with_op("fill", "fill-opacity"),
            pick("stroke-width"),
            parse_rotate(a.get("transform") or gattrs.get("transform") or ""),
        ))

    # <g> 包裹的图形
    for g in re.finditer(r"<g\b([^>]*)>(.*?)</g>", svg_text, re.S):
        ga = dict(ATTR_RE.findall(g.group(1)))
        for tm in TAG_RE.finditer(g.group(2)):
            emit(tm.group(1), tm.group(2), ga)

    # 顶层图形
    stripped = re.sub(r"<g\b[^>]*>.*?</g>", "", svg_text, flags=re.S)
    for tm in TAG_RE.finditer(stripped):
        emit(tm.group(1), tm.group(2), {})
    return items


def main():
    lines = [
        '<!--',
        '    导航图标资源（自动生成，勿手改）：由 design/branding/icons/nav_*.svg 转出。',
        '    重新生成：python design/branding/_svg_to_wpf_icons.py',
        '',
        '    每个图标一枚 DrawingImage，键名 NavIcon.<id>。',
        f'    · 外层 ScaleTransform 把 128 坐标空间缩到 {int(NAV_TARGET)}×{int(NAV_TARGET)}；',
        '      注意：仅靠它不足以保证显示尺寸，ImageIcon 必须显式写 Width/Height（见 MainWindow.xaml），',
        '      否则会按绘制 bounds 铺满（实测 45px，几乎撑满导航行高）；',
        f'    · 描边同步 ×{STROKE_SCALE} 加粗，避免缩到 {int(NAV_TARGET)}px 后细到看不见。',
        '    用法：<ui:ImageIcon Source="{StaticResource NavIcon.home}" />',
        '-->',
        '<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"',
        '                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">',
        '',
    ]

    for iid, name in ORDER:
        path = os.path.join(ICON_DIR, f"nav_{iid}.svg")
        if not os.path.exists(path):
            print("MISSING", path)
            continue
        text = open(path, encoding="utf-8").read()
        text = re.sub(r"<!--.*?-->", "", text, flags=re.S)
        text = re.sub(r"</?svg[^>]*>", "", text)

        lines.append(f'    <!--  {name}  -->')
        lines.append(f'    <DrawingImage x:Key="NavIcon.{iid}">')
        lines.append('        <DrawingImage.Drawing>')
        lines.append('            <DrawingGroup>')
        lines.append('                <DrawingGroup.Transform>')
        lines.append(f'                    <ScaleTransform ScaleX="{NAV_SCALE}" ScaleY="{NAV_SCALE}" />')
        lines.append('                </DrawingGroup.Transform>')
        # 透明全画布矩形：把绘制包围盒**统一**成 128×128。
        # 否则每个图标的自然尺寸都不一样（实测 15.5×13.7 / 13.3×9.6 / 11.9×11.9…），
        # ImageIcon 会把各自拉伸到同一显示框 → 比例失真 + 原点偏移 → 忽大忽小、边缘被裁。
        lines.append('                <!--  透明占位：统一固有尺寸，勿删  -->')
        lines.append('                <GeometryDrawing Brush="Transparent" Geometry="M0 0 H128 V128 H0 Z" />')

        for seg, stroke, fill, sw, rot in collect(text):
            seg = " ".join(seg.split())
            has_stroke = bool(stroke) and stroke.lower() != "none"
            has_fill = bool(fill) and fill.lower() != "none"
            if not has_stroke and not has_fill:
                continue

            # 描边加粗（补偿缩放后过细）
            try:
                thickness = float(sw) * STROKE_SCALE if sw else 8 * STROKE_SCALE
            except ValueError:
                thickness = 10.0
            thickness = round(thickness, 1)
            thickness = int(thickness) if thickness == int(thickness) else thickness

            indent = '                '
            if rot:
                lines.append(f'{indent}<DrawingGroup>')
                lines.append(f'{indent}    <DrawingGroup.Transform>')
                lines.append(f'{indent}        <RotateTransform Angle="{rot[0]}" CenterX="{rot[1]}" CenterY="{rot[2]}" />')
                lines.append(f'{indent}    </DrawingGroup.Transform>')
                indent += '    '
            lines.append(f'{indent}<GeometryDrawing Geometry="{seg}"' +
                         (f' Brush="{fill}"' if has_fill else '') + '>')
            if has_stroke:
                lines.append(f'{indent}    <GeometryDrawing.Pen>')
                lines.append(f'{indent}        <Pen Brush="{stroke}" Thickness="{thickness}" '
                             'StartLineCap="Round" EndLineCap="Round" LineJoin="Round" />')
                lines.append(f'{indent}    </GeometryDrawing.Pen>')
            lines.append(f'{indent}</GeometryDrawing>')
            if rot:
                lines.append(f'{indent[:-4]}</DrawingGroup>')

        lines.append('            </DrawingGroup>')
        lines.append('        </DrawingImage.Drawing>')
        lines.append('    </DrawingImage>')
        lines.append('')

    lines.append('</ResourceDictionary>')
    lines.append('')

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    open(OUT, "w", encoding="utf-8", newline="\n").write("\n".join(lines))
    print("written:", OUT, "| icons:", len(ORDER))


if __name__ == "__main__":
    main()
