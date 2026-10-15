# -*- coding: utf-8 -*-
"""由 banner_{dark,light}.svg 生成 WPF 品牌条背景资源字典（**全自动，不手写**）。

产出：
    TheGuideToTheNewEden.WPF/Resources/BannerBackground.Dark.xaml
    TheGuideToTheNewEden.WPF/Resources/BannerBackground.Light.xaml

生成的键（两套字典键名完全一致，只是色值不同）：
    Banner.Bg.Base            底色（线性渐变）
    Banner.Bg.Nebula1/2/3     三色星云（径向渐变）
    Banner.Stars              星野圆点 + 闪星（四角星）

## 为什么不手写
这条链路上手写已经出过三次静默走形（导航图标尺寸 / 星野形状 / logo 配色）——
手写不会报错，只会悄悄画错，肉眼逐条核对几乎不可能。见 REFACTORING.md 阶段 112。
本脚本与 _svg_to_wpf_icons.py 同思路：**设计稿是唯一真源，工程资产一律转换产出**。

## 坐标换算
SVG 用 userSpaceOnUse（1200×200 画布），而 WPF 的渐变画刷默认按**相对比例**（0~1）定位，
故统一除以画布尺寸。这样画刷会随 Banner 实际尺寸自动伸缩，是响应式的正确做法。
"""
import io
import math
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
WPF = (r"D:\QEDSD\Code\TheGuideToTheNewEden\TheGuideToTheNewEden"
       r"\TheGuideToTheNewEden.WPF")
RES = os.path.join(WPF, "Resources")
HP = os.path.join(WPF, "Views", "Pages", "HomePage.xaml")

VIEW_W, VIEW_H = 1200.0, 200.0
VIEWBOX = "0 0 1200 200"

# 工程 banner 的**实际高度**（HomePage.xaml 里 HeroBanner 的 Height="160"）。
# 设计稿是 1200×200，故等比缩放 = 160/200 = 0.8。
# 纵向定位、半径、描边**统一用这一个比例**，才与设计稿等比 ——
# 原先手写版横纵各拍一个数，导致环心偏移、星比环大 1.31 倍、星尖被裁。
BANNER_H = 160.0
SCALE = BANNER_H / VIEW_H

# HomePage.xaml 里「右侧星门环 + 罗盘星」区块的自动生成标记
SYM_BEGIN = "BEGIN BannerSymbol"
SYM_END = "END BannerSymbol"

ATTR_RE = re.compile(r'([\w-]+)\s*=\s*"([^"]*)"')


def fmt(v):
    s = f"{float(v):.4f}".rstrip("0").rstrip(".")
    return s if s not in ("", "-") else "0"


def to_hex(color, opacity):
    """CSS 颜色 + opacity → #AARRGGBB。"""
    c = color.strip()
    if not c.startswith("#"):
        raise ValueError(f"不支持的色值: {color}")
    hexp = c[1:]
    if len(hexp) == 3:
        hexp = "".join(ch * 2 for ch in hexp)
    if len(hexp) != 6:
        raise ValueError(f"不支持的色值: {color}")
    a = 255 if opacity in (None, "") else max(0, min(255, round(float(opacity) * 255)))
    return f"#{a:02X}{hexp.upper()}"


# ---------------------------------------------------------------- 渐变
def parse_gradients(svg):
    """返回 {id: dict(kind, attrs, stops)}；stops = [(offset, #AARRGGBB)]。"""
    out = {}
    pat = re.compile(r'<(linear|radial)Gradient\s+([^>]*?)>(.*?)</\1Gradient>', re.S)
    for m in pat.finditer(svg):
        kind, attrs, body = m.group(1), dict(ATTR_RE.findall(m.group(2))), m.group(3)
        gid = attrs.get("id")
        if not gid:
            continue
        stops = []
        for sm in re.finditer(r"<stop\s+([^/>]+?)/?>", body):
            a = dict(ATTR_RE.findall(sm.group(1)))
            stops.append((float(a.get("offset", 0)),
                          to_hex(a.get("stop-color", "#000000"), a.get("stop-opacity"))))
        stops.sort(key=lambda t: t[0])
        out[gid] = {"kind": kind, "attrs": attrs, "stops": stops}
    return out


def brush_xaml(gid, grad, key, comment):
    kind, a, stops = grad["kind"], grad["attrs"], grad["stops"]
    lines = [f"    <!--  {comment}  -->"]
    if kind == "linear":
        x1, y1 = float(a.get("x1", 0)), float(a.get("y1", 0))
        x2, y2 = float(a.get("x2", 1)), float(a.get("y2", 0))
        if a.get("gradientUnits") == "userSpaceOnUse":
            x1, y1, x2, y2 = x1 / VIEW_W, y1 / VIEW_H, x2 / VIEW_W, y2 / VIEW_H
        lines.append(f'    <LinearGradientBrush x:Key="{key}" '
                     f'StartPoint="{fmt(x1)},{fmt(y1)}" EndPoint="{fmt(x2)},{fmt(y2)}">')
        close = "    </LinearGradientBrush>"
    else:
        cx, cy = float(a.get("cx", 0.5)), float(a.get("cy", 0.5))
        r = float(a.get("r", 1))
        if a.get("gradientUnits") == "userSpaceOnUse":
            cx, cy, rx, ry = cx / VIEW_W, cy / VIEW_H, r / VIEW_W, r / VIEW_H
        else:
            rx = ry = r
        lines.append(f'    <RadialGradientBrush x:Key="{key}" '
                     f'Center="{fmt(cx)},{fmt(cy)}" GradientOrigin="{fmt(cx)},{fmt(cy)}" '
                     f'RadiusX="{fmt(rx)}" RadiusY="{fmt(ry)}">')
        close = "    </RadialGradientBrush>"
    for off, col in stops:
        lines.append(f'        <GradientStop Offset="{fmt(off)}" Color="{col}" />')
    lines.append(close)
    return lines


# ---------------------------------------------------------------- 星野/闪星
def parse_stars(svg):
    """返回 [(color #AARRGGBB, kind, payload)]；kind ∈ {ellipse, path}。"""
    items = []
    for label in ("星野", "闪星"):
        m = re.search(r"<!--\s*" + label + r"[^>]*-->\s*<g\s+([^>]*)>(.*?)</g>", svg, re.S)
        if not m:
            continue
        ga = dict(ATTR_RE.findall(m.group(1)))
        fill = ga.get("fill", "#000000")
        gop = ga.get("opacity")
        base = to_hex(fill, gop)

        for cm in re.finditer(
                r'<circle\s+cx="([\d.\-]+)"\s+cy="([\d.\-]+)"\s+r="([\d.\-]+)"'
                r'(?:\s+opacity="([\d.]+)")?\s*/>', m.group(2)):
            # 元素 opacity 与组 opacity 相乘
            eo = float(cm.group(4)) if cm.group(4) else 1.0
            go = float(gop) if gop else 1.0
            alpha = max(0, min(255, round(int(base[1:3], 16) / 255 * eo * go * 255)))
            col = f"#{alpha:02X}{fill.lstrip('#').upper()}"
            items.append((col, "ellipse",
                          (fmt(cm.group(1)), fmt(cm.group(2)), fmt(cm.group(3)))))

        for pm in re.finditer(r'<path\s+d="([^"]+)"\s*/>', m.group(2)):
            items.append((base, "path", pm.group(1)))
    return items


def stars_xaml(items):
    groups = {}
    for color, kind, payload in items:
        groups.setdefault(color, []).append((kind, payload))
    n_dot = sum(1 for _, k, _ in items if k == "ellipse")
    n_star = sum(1 for _, k, _ in items if k == "path")

    lines = [
        f"    <!--  星野 {n_dot} 个圆点 + 闪星 {n_star} 个四角星"
        f"（忠实转自 SVG；Viewbox 让坐标随 Banner 尺寸自动缩放）  -->",
        f'    <DrawingBrush x:Key="Banner.Stars" Stretch="Fill" '
        f'Viewbox="{VIEWBOX}" ViewboxUnits="Absolute">',
        "        <DrawingBrush.Drawing>",
        "            <DrawingGroup>",
    ]
    for color in sorted(groups):
        lines.append(f'            <GeometryDrawing Brush="{color}">')
        lines.append("                <GeometryDrawing.Geometry>")
        lines.append("                    <GeometryGroup>")
        for k, p in groups[color]:
            if k == "ellipse":
                lines.append(f'                        <EllipseGeometry '
                             f'Center="{p[0]},{p[1]}" RadiusX="{p[2]}" RadiusY="{p[2]}" />')
            else:
                lines.append(f'                        <PathGeometry Figures="{p}" />')
        lines.append("                    </GeometryGroup>")
        lines.append("                </GeometryDrawing.Geometry>")
        lines.append("            </GeometryDrawing>")
    lines += [
        "            </DrawingGroup>",
        "        </DrawingBrush.Drawing>",
        "    </DrawingBrush>",
    ]
    return lines


# ---------------------------------------------------------------- 组装
HEADER = """<!--
    ============================================================================
    主页品牌条 · 背景画刷（{theme}主题版）· 自动生成，勿手改
    ============================================================================
    来源：design/branding/banner_{name}.svg
    重新生成：python design/branding/_svg_to_wpf_banner.py
    配套：BannerBackground.{other}.xaml（键名完全相同，只有色值不同）

    ## 为什么转成 XAML 而不是直接用 .svg
    WPF 原生不能渲染 SVG（Image.Source 只认位图，要读须引第三方库）。
    而 Banner 背景只是渐变 + 星点，转成 XAML 矢量后：零第三方依赖、零图片文件、
    任意分辨率清晰、可随主题整体替换。正文（应用名/版本号/在线人数/按钮）是
    "活内容"，仍写在 HomePage.xaml —— 与 logo 同款处理：**SVG 只负责静态背景**。

    ## 为什么不手写
    手写不会报错，只会"悄悄走形"（曾漏图层、形状走样、配色反转），
    肉眼逐条核对几乎不可能。故与图标一样：**设计稿是唯一真源，资产一律转换产出**。

    ## 主题切换
    由 ThemeService.ApplyBannerDictionary() 在切换主题时整体换入/移除
    （同 LanguageService 换语言字典的做法），键名两套一致。
    ============================================================================
-->
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

"""

# 图层顺序 = 绘制顺序（底 → 星云 → 星野）
GRAD_SPEC = [
    (["bg", "bgL"], "Banner.Bg.Base", "底色"),
    (["neb1", "neb1L"], "Banner.Bg.Nebula1", "星云 1（左下）"),
    (["neb2", "neb2L"], "Banner.Bg.Nebula2", "星云 2（右上）"),
    (["neb3", "neb3L"], "Banner.Bg.Nebula3", "星云 3（右侧）"),
]

# 品牌符号渐变（星门环 + 罗盘星）：供 HomePage 的**两处**共用 ——
#   ① 左侧迷你「星门罗盘」Logo（内容层，26px 环）
#   ② 右侧出血大星门环（装饰层，264px 环）
# SVG 里对应两个 id（ring / mark），但同一主题内**色值相同**，故工程合并为一支画刷。
#
# 方向固定用 0,0 → 1,1（相对的左上→右下），**不按 SVG 的绝对坐标换算**：
# SVG 的 ring 从 (960,-40) 到 (1280,240)、mark 从 (52,66) 到 (112,126)，
# 二者跨度差异极大，若各自按画布归一化，套到不同尺寸的图形上会退化成近纯色。
# 用相对对角则每处图形都是"左上青 → 右下靛"，与设计稿的视觉意图一致且适配任意尺寸。
# 色值取 mark（其 offset 为标准的 0/0.5/1；ring 是 0/0.55/1，色值相同）。
SYMBOL_SPEC = (["mark", "markL"], "Banner.Symbol", "品牌符号（星门环 + 罗盘星）")


def symbol_xaml(grad, key, comment):
    """品牌符号画刷：只取色值，方向固定为相对对角（见 SYMBOL_SPEC 说明）。"""
    lines = [
        f"    <!--  {comment}  -->",
        f'    <LinearGradientBrush x:Key="{key}" StartPoint="0,0" EndPoint="1,1">',
    ]
    for off, col in grad["stops"]:
        lines.append(f'        <GradientStop Offset="{fmt(off)}" Color="{col}" />')
    lines.append("    </LinearGradientBrush>")
    return lines


# ---------------------------------------------------------------- 品牌符号图形
def _find_symbol_shapes(svg):
    """取出「星门环」(circle) 与「罗盘星」(path)：两者都引用 url(#ring)。"""
    ring = star = None
    for m in re.finditer(r"<circle\b([^>]*?)/>", svg):
        a = dict(ATTR_RE.findall(m.group(1)))
        if "ring" in a.get("stroke", ""):
            ring = a
            break
    for m in re.finditer(r"<path\b([^>]*?)/>", svg):
        a = dict(ATTR_RE.findall(m.group(1)))
        if "ring" in a.get("fill", ""):
            star = a
            break
    if not ring or not star:
        raise SystemExit("找不到「星门环 + 罗盘星」（应引用 url(#ring)）")
    return ring, star


# 本设计稿的图形只用绝对坐标命令；相对命令（小写）不支持，遇到即报错。
PATH_ARGS = {"M": 2, "L": 2, "Q": 4, "C": 6, "Z": 0}


def transform_path(d, sx, sy, dx, dy):
    """把 SVG 路径（绝对坐标）等比缩放 + 平移，得到目标画布坐标。"""
    toks = re.findall(r"[A-Za-z]|-?\d*\.?\d+", d)
    out, i = [], 0
    while i < len(toks):
        cmd = toks[i]
        i += 1
        if not cmd.isalpha():
            raise SystemExit(f"路径解析异常，期望命令字母，得到 {cmd}")
        if not cmd.isupper():
            raise SystemExit(f"仅支持绝对坐标命令，遇到相对命令 {cmd}")
        out.append(cmd)
        n = PATH_ARGS.get(cmd)
        if n is None:
            raise SystemExit(f"不支持的路径命令 {cmd}（本设计稿只应出现 M/L/Q/C/Z）")
        if n == 0:
            continue
        vals = [float(toks[i + k]) for k in range(n)]
        i += n
        for k in range(0, n, 2):
            vals[k] = vals[k] * sx + dx
            vals[k + 1] = vals[k + 1] * sy + dy
        out.extend(fmt(v) for v in vals)
    return " ".join(out)


def symbol_figure_xaml(svg):
    """生成「星门环 + 罗盘星」的 XAML 片段，几何与 SVG **严格同构**。

    定位约定（照搬设计稿）：
      · 纵向：环心按等比缩放落到 banner 内（设计稿 cy=100/200 即垂直居中）；
      · 横向：以**距右边缘的距离**锚定（SVG 环心距右缘 1200-1120=80 → 缩放后 64px），
        环身自然向左/上/下出血，由 banner 的圆角裁切；
      · 罗盘星与环**同心**（设计稿两者中心都是 (1120,100)）。

    原先的手写版有两个错：把环心放在右缘上（应距右 64px）、把星心另写一个值
    （导致星与环不同心），且星的尖半径放大了 1.31 倍 —— 星尖戳出边界被裁，
    用户看到的"星星显示不全"就是这个。
    """
    ring, star = _find_symbol_shapes(svg)
    cx, cy = float(ring["cx"]), float(ring["cy"])
    r, sw = float(ring["r"]), float(ring["stroke-width"])

    r_out = (r + sw / 2) * SCALE                  # 环外缘半径
    offset = (VIEW_W - cx) * SCALE                # 环心距右边缘
    canvas_w = int(math.ceil(offset + r_out + 4))
    ox, oy = canvas_w - offset, cy * SCALE        # 画布内环心（画布右缘 = banner 右缘）

    nums = [float(v) for v in re.findall(r"-?\d*\.?\d+", star["d"])]
    xs, ys = nums[0::2], nums[1::2]
    tip = max(max(xs) - cx, cx - min(xs), max(ys) - cy, cy - min(ys)) * SCALE

    d = transform_path(star["d"], SCALE, SCALE, ox - cx * SCALE, oy - cy * SCALE)

    L = [
        f"                    <!-- {SYM_BEGIN}: 由 design/branding/_svg_to_wpf_banner.py 生成，勿手改 -->",
        "                    <!--",
        "                        装饰层：右侧出血星门环 + 罗盘星。",
        f"                        几何与 design/branding/banner_*.svg 严格同构（等比 {SCALE:g}）：",
        f"                          · 环心距右边缘 {fmt(offset)}px、纵向 y={fmt(oy)}，",
        f"                            半径 {fmt(r * SCALE)}、描边 {fmt(sw * SCALE)}；环身出血由圆角裁切；",
        f"                          · 罗盘星与环**同心**，尖半径 {fmt(tip)}px（故完整可见）。",
        "                        颜色用 Banner.Symbol（两主题各一份）。改设计稿后重跑脚本即可。",
        "                    -->",
        "                    <Canvas",
        f'                        Width="{canvas_w}"',
        f'                        Height="{fmt(BANNER_H)}"',
        '                        HorizontalAlignment="Right"',
        '                        IsHitTestVisible="False">',
        '                        <!--  大环：环心在画布右缘内侧，环身出血（同设计稿）  -->',
        "                        <Ellipse",
        f'                            Canvas.Left="{fmt(ox - r_out)}"',
        f'                            Canvas.Top="{fmt(oy - r_out)}"',
        f'                            Width="{fmt(r_out * 2)}"',
        f'                            Height="{fmt(r_out * 2)}"',
        '                            Stroke="{DynamicResource Banner.Symbol}"',
        f'                            StrokeThickness="{fmt(sw * SCALE)}" />',
        '                        <!--  罗盘星：与环同心，不会戳出边界  -->',
        "                        <Path",
        f'                            Data="{d}"',
        '                            Fill="{DynamicResource Banner.Symbol}" />',
        "                    </Canvas>",
        f"                    <!-- {SYM_END} -->",
    ]
    return "\n".join(L), dict(offset=offset, oy=oy, r=r * SCALE, sw=sw * SCALE,
                              tip=tip, canvas_w=canvas_w)


def apply_symbol_figure():
    """把生成的图形片段写回 HomePage.xaml 的标记区块。"""
    svg = io.open(os.path.join(HERE, "banner_dark.svg"), encoding="utf-8").read()
    figure, info = symbol_figure_xaml(svg)

    text = io.open(HP, encoding="utf-8").read()
    i, j = text.find(SYM_BEGIN), text.find(SYM_END)
    if i < 0 or j < 0:
        raise SystemExit(f"HomePage.xaml 里找不到生成标记（{SYM_BEGIN} / {SYM_END}）")
    ls = text.rfind("\n", 0, i) + 1
    le = text.find("\n", j)
    le = len(text) if le < 0 else le
    io.open(HP, "w", encoding="utf-8", newline="\n").write(text[:ls] + figure + text[le:])

    print(f"  HomePage.xaml 星门环：环心距右 {info['offset']:.0f}px / y={info['oy']:.0f}"
          f" / r={info['r']:.0f} / 描边={info['sw']:.0f} / 星尖半径={info['tip']:.0f}"
          f"（星尖距右缘 {info['offset'] - info['tip']:.0f}px，在界内）")


def build(svg_name, theme_label, other_label):
    svg = io.open(os.path.join(HERE, f"banner_{svg_name}.svg"), encoding="utf-8").read()
    grads = parse_gradients(svg)

    lines = [HEADER.format(theme=theme_label, name=svg_name, other=other_label)]
    for cands, key, comment in GRAD_SPEC:
        gid = next((c for c in cands if c in grads), None)
        if gid is None:
            raise SystemExit(f"{svg_name}: 找不到渐变 {cands}")
        lines += brush_xaml(gid, grads[gid], key, comment)
        lines.append("")

    lines.append("    <!-- ============ 品牌符号（两处共用：迷你 Logo + 右侧大环）============ -->")
    cands, key, comment = SYMBOL_SPEC
    gid = next((c for c in cands if c in grads), None)
    if gid is None:
        raise SystemExit(f"{svg_name}: 找不到品牌符号渐变 {cands}")
    lines += symbol_xaml(grads[gid], key, comment)
    lines.append("")

    lines.append("    <!-- ============ 星野 + 闪星 ============ -->")
    lines += stars_xaml(parse_stars(svg))
    lines.append("")
    lines.append("</ResourceDictionary>")
    return "\n".join(lines) + "\n"


def main():
    os.makedirs(RES, exist_ok=True)
    for name, label, other, out in (
            ("dark", "深色", "Light", "BannerBackground.Dark.xaml"),
            ("light", "浅色", "Dark", "BannerBackground.Light.xaml")):
        text = build(name, label, other)
        path = os.path.join(RES, out)
        io.open(path, "w", encoding="utf-8", newline="\n").write(text)
        n_neb = text.count("<RadialGradientBrush")
        n_dot = text.count("<EllipseGeometry")
        n_star = text.count("<PathGeometry")
        n_sym = text.count('x:Key="Banner.Symbol"')
        print(f"  {name}: 底1 + 星云{n_neb} + 符号{n_sym} + 星野({n_dot}圆/{n_star}星)  ->  {out}")


if __name__ == "__main__":
    main()
    apply_symbol_figure()
