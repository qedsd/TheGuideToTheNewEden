# -*- coding: utf-8 -*-
"""全面审计：所有「设计稿 → 工程资产」转换点的一致性。

覆盖：
  A. logo.svg → _gen_logo_bitmap.py → PNG/ICO         （颜色）
  B. icons/nav_*.svg → _svg_to_wpf_icons.py → xaml    （颜色 / opacity / 线帽）
  C. banner_*.svg → BannerBackground.*.xaml           （星野数量 / 星色 / 渐变几何）
  D. banner_*.svg 的 ring / mark → HomePage.xaml      （BannerRingBrush 配色）
  E. banner 底色/星云（手写）与 SVG 定义的数值差异
"""
import glob
import io
import os
import re
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
WPF = os.path.join(ROOT, "TheGuideToTheNewEden", "TheGuideToTheNewEden.WPF")
HOME_XAML = os.path.join(WPF, "Views", "Pages", "HomePage.xaml")


def hr(t):
    print("\n" + "=" * 62)
    print(t)
    print("=" * 62)


# ============================================================ A
def audit_logo():
    hr("A. logo.svg → 位图（颜色一致性）")
    s = io.open(os.path.join(HERE, "logo.svg"), encoding="utf-8").read()
    g = re.search(r'<linearGradient[^>]*x1="([\d.]+)"[^>]*y1="([\d.]+)"[^>]*'
                  r'x2="([\d.]+)"[^>]*y2="([\d.]+)"', s)
    frm = np.array([float(g.group(1)), float(g.group(2))])
    to = np.array([float(g.group(3)), float(g.group(4))])
    stops = [(float(o), tuple(int(c[i:i + 2], 16) for i in (1, 3, 5)))
             for o, c in re.findall(r'<stop\s+offset="([\d.]+)"\s+stop-color="(#[0-9A-Fa-f]{6})"', s)]
    ax, dn = to - frm, float((to - frm) @ (to - frm))
    print(f"  SVG 渐变: {['#%02X%02X%02X' % c for _, c in stops]}")

    def expect(t):
        out = np.zeros(3) + np.array(stops[0][1], float)
        for i in range(len(stops) - 1):
            x0, x1 = stops[i][0], stops[i + 1][0]
            seg = np.clip((t - x0) / (x1 - x0), 0, 1)
            out += seg * (np.array(stops[i + 1][1], float) - np.array(stops[i][1], float))
        return out

    worst = 0
    for f in sorted(glob.glob(os.path.join(WPF, "Assets", "logo_*.png"))):
        a = np.array(Image.open(f).convert("RGBA")).astype(int)
        n = a.shape[0]
        ys, xs = np.mgrid[0:n, 0:n]
        t = np.clip(((np.stack([xs, ys], -1) * (128.0 / n) - frm) @ ax) / dn, 0, 1)
        m = a[..., 3] > 250
        e = max(np.abs(rgb - expect(tt)).max() for tt, rgb in zip(t[m], a[..., :3][m]))
        worst = max(worst, e)
        print(f"    {os.path.basename(f):<18} 最大偏差 {e:.0f}")
    ico = os.path.join(WPF, "Assets", "app.ico")
    if os.path.exists(ico):
        im = Image.open(ico)
        e_worst = 0
        for sz in sorted(im.info.get("sizes", [im.size])):
            im.size = sz
            fr = im.convert("RGBA")
            n = fr.size[0]
            a = np.array(fr).astype(int)
            ys, xs = np.mgrid[0:n, 0:n]
            t = np.clip(((np.stack([xs, ys], -1) * (128.0 / n) - frm) @ ax) / dn, 0, 1)
            m = a[..., 3] > 250
            e_worst = max(e_worst, max(np.abs(rgb - expect(tt)).max()
                                       for tt, rgb in zip(t[m], a[..., :3][m])))
        worst = max(worst, e_worst)
        print(f"    {'app.ico (11帧)':<18} 最大偏差 {e_worst:.0f}")
    print(f"  => {'[OK] 一致' if worst <= 3 else '[X] 不一致'}")


# ============================================================ B
def audit_nav_icons():
    hr("B. icons/nav_*.svg → NavIcons.xaml")
    files = sorted(glob.glob(os.path.join(HERE, "icons", "nav_*.svg")))
    xaml = io.open(os.path.join(WPF, "Resources", "NavIcons.xaml"), encoding="utf-8").read()

    # B1 颜色：SVG 里所有 #RRGGBB 的 **RGB 部分**是否都出现在 xaml 中
    #    （xaml 侧颜色可能带 alpha 前缀 #AARRGGBB，故只比 RGB 六位）
    xaml_rgb = {c.upper() for c in re.findall(r'#[0-9A-Fa-f]{8}', xaml)}
    xaml_rgb |= {("#FF" + c[1:].upper()) for c in re.findall(r'#[0-9A-Fa-f]{6}\b', xaml)}
    xaml_rgb_6 = {c[3:] for c in xaml_rgb} | {c[1:] for c in re.findall(r'#[0-9A-Fa-f]{6}\b', xaml)}

    svg_colors, missing = set(), []
    for f in files:
        c = set(re.findall(r'(?:stroke|fill)="(#[0-9A-Fa-f]{6})"',
                           io.open(f, encoding="utf-8").read()))
        svg_colors |= c
        for col in c:
            if col[1:].upper() not in xaml_rgb_6:
                missing.append((os.path.basename(f), col))
    print(f"  B1 颜色: SVG 用到 {len(svg_colors)} 种颜色")
    if missing:
        print(f"     [X] 有 {len(missing)} 处颜色未出现在 xaml：{missing[:6]}")
    else:
        print("     [OK] 全部颜色都出现在 xaml 中（RGB 一致，alpha 另计）")

    # B2 opacity：脚本已支持折算进 alpha
    op_files = []
    for f in files:
        s = io.open(f, encoding="utf-8").read()
        for m in re.finditer(r'opacity="([\d.]+)"', s):
            op_files.append((os.path.basename(f), m.group(1)))
    print(f"  B2 opacity: SVG 用了 {len(op_files)} 处 {op_files}")
    if op_files:
        bad = []
        for fname, op in op_files:
            icon_key = "NavIcon." + fname.replace("nav_", "").replace(".svg", "")
            mm = re.search(r'x:Key="' + re.escape(icon_key) + r'">(.*?)</DrawingImage>',
                           xaml, re.S)
            if not mm:
                bad.append(fname + "(图标缺失)")
                continue
            # 该图标内应存在带非 FF alpha 的颜色
            cols = re.findall(r'Brush="(#[0-9A-Fa-f]{8})"', mm.group(1))
            if not any(c[1:3].upper() not in ("FF",) for c in cols):
                bad.append(f"{fname}(opacity={op} 未折算)")
        print(f"     {'[OK] 均已折算进 alpha' if not bad else '[X] ' + str(bad)}")
    else:
        print("     [OK] 无 opacity 用法")

    # B3 线帽/连接
    caps, joins = set(), set()
    for f in files:
        s = io.open(f, encoding="utf-8").read()
        caps |= set(re.findall(r'stroke-linecap="(\w+)"', s))
        joins |= set(re.findall(r'stroke-linejoin="(\w+)"', s))
    print(f"  B3 linecap={caps or '未指定'} linejoin={joins or '未指定'} "
          f"（脚本硬编码 Round）")
    bad = (caps - {"round", ""}) | (joins - {"round", ""})
    print(f"     {'[OK] 与硬编码一致' if not bad else '[X] 有差异: ' + str(bad)}")

    # B4 数量
    n = xaml.count("<DrawingImage x:Key=")
    print(f"  B4 数量: SVG {len(files)} 个 / xaml {n} 个 "
          f"{'[OK]' if n == len(files) else '[X]'}")


# ============================================================ C
def audit_banner():
    hr("C. banner_*.svg → BannerBackground.*.xaml（星野与渐变）")
    for theme, svg_name in (("Dark", "banner_dark.svg"), ("Light", "banner_light.svg")):
        svg = io.open(os.path.join(HERE, svg_name), encoding="utf-8").read()
        xp = os.path.join(WPF, "Resources", f"BannerBackground.{theme}.xaml")
        xaml = io.open(xp, encoding="utf-8").read()
        print(f"\n  [{theme}] {svg_name}")

        # C1 星点
        m = re.search(r"<!--\s*星野[^>]*-->\s*<g\s+([^>]*)>(.*?)</g>", svg, re.S)
        s2 = re.search(r"<!--\s*闪星[^>]*-->\s*<g\s+([^>]*)>(.*?)</g>", svg, re.S)
        dots = len(re.findall(r"<circle\s", m.group(2))) if m else 0
        stars = len(re.findall(r"<path\s", s2.group(2))) if s2 else 0
        x_dots = xaml.count("<EllipseGeometry")
        x_stars = xaml.count("<PathGeometry")
        print(f"     C1 星点: SVG {dots}圆+{stars}星 / xaml {x_dots}圆+{x_stars}星 "
              f"{'[OK]' if (dots, stars) == (x_dots, x_stars) else '[X]'}")

        # C2 星色：SVG 的 fill 与 group opacity 组合 vs xaml 的 #AARRGGBB
        def parse_group(mm):
            if not mm:
                return None, None
            a = mm.group(1)
            fill = re.search(r'fill="(#[0-9A-Fa-f]{6})"', a)
            gop = re.search(r'opacity="([\d.]+)"', a)
            return (fill.group(1).upper() if fill else None,
                    float(gop.group(1)) if gop else 1.0)
        fc, go = parse_group(m)
        sc, so = parse_group(s2)
        xs_colors = sorted(set(re.findall(r'Brush="(#[0-9A-Fa-f]{8})"', xaml)))
        print(f"     C2 星色: SVG 星野 {fc}×{go}  闪星 {sc}×{so}")
        print(f"              xaml 里: {xs_colors}")

        # C4 图层数：SVG 的底 + 各星云，工程是否一一实现
        svg_nebs = re.findall(r'<radialGradient[^>]*id="(neb\d+L?)"', svg)
        has_bg = bool(re.search(r'<linearGradient[^>]*id="bgL?"', svg))
        x_nebs = len(re.findall(r'<RadialGradientBrush', xaml))
        x_has_base = bool(re.search(r'x:Key="Banner\.Bg\.Base"', xaml))
        print(f"     C4 图层: SVG 底{1 if has_bg else 0}+星云{len(svg_nebs)}{svg_nebs} "
              f"/ xaml 底{1 if x_has_base else 0}+星云{x_nebs} "
              f"{'[OK]' if (has_bg == x_has_base and len(svg_nebs) == x_nebs) else '[X] 图层数不符，有图层未实现'}")

        print(f"     C3 渐变几何对比（SVG 用 1200×200 空间；换算后应自动一致）")
        for gid, key in (("bg", "Banner.Bg.Base"), ("neb1", "Banner.Bg.Nebula1"),
                         ("neb2", "Banner.Bg.Nebula2"), ("neb3", "Banner.Bg.Nebula3")):
            mm = re.search(r'<radialGradient[^>]*id="' + gid + r'L?"[^>]*cx="([\d.\-]+)"[^>]*'
                           r'cy="([\d.\-]+)"[^>]*r="([\d.]+)"', svg)
            if not mm:
                # 线性渐变：比对首尾点
                lm = re.search(r'<linearGradient[^>]*id="' + gid + r'L?"[^>]*'
                               r'x1="([\d.\-]+)"[^>]*y1="([\d.\-]+)"[^>]*'
                               r'x2="([\d.\-]+)"[^>]*y2="([\d.\-]+)"', svg)
                xm = re.search(r'<LinearGradientBrush\s+x:Key="' + re.escape(key) + r'"[^>]*'
                               r'StartPoint="([\d.,\-]+)"[^>]*EndPoint="([\d.,\-]+)"', xaml, re.S)
                if lm and xm:
                    svg_pts = ((float(lm.group(1)) / 1200, float(lm.group(2)) / 200),
                               (float(lm.group(3)) / 1200, float(lm.group(4)) / 200))
                    x_pts = tuple(tuple(float(v) for v in g.split(","))
                                  for g in (xm.group(1), xm.group(2)))
                    same = all(abs(a - b) < 0.02
                               for p, q in zip(svg_pts, x_pts) for a, b in zip(p, q))
                    print(f"        {key:<24} 线性 {svg_pts[0]}→{svg_pts[1]}  "
                          f"{'[OK]' if same else '[X] 与SVG不符'}")
                else:
                    print(f"        {key:<24} 线性（未能比对）")
                continue
            cx, cy, r = float(mm.group(1)), float(mm.group(2)), float(mm.group(3))
            svg_c = (cx / 1200.0, cy / 200.0)
            svg_r = (r / 1200.0, r / 200.0)
            xmlb = re.search(r'<RadialGradientBrush\s+x:Key="' + re.escape(key) + r'"[^>]*'
                             r'Center="([\d.,\-]+)"[^>]*RadiusX="([\d.]+)" RadiusY="([\d.]+)"',
                             xaml, re.S)
            if xmlb:
                xc = tuple(float(v) for v in xmlb.group(1).split(","))
                xr = (float(xmlb.group(2)), float(xmlb.group(3)))
                same = (all(abs(a - b) < 0.02 for a, b in zip(svg_c, xc))
                        and all(abs(a - b) < 0.02 for a, b in zip(svg_r, xr)))
                print(f"        {key:<24} 中心({xc[0]:.3f},{xc[1]:.2f}) 半径({xr[0]:.3f},{xr[1]:.2f})  "
                      f"{'[OK] 与SVG一致' if same else '[X] 与SVG不符'}")
            else:
                print(f"        {key:<24} [X] xaml 里找不到该画刷")


# ============================================================ D
def audit_banner_ring():
    hr("D. 品牌符号渐变（星门环 + 罗盘星）→ Banner.Symbol")
    xaml = io.open(HOME_XAML, encoding="utf-8").read()
    refs = xaml.count("{DynamicResource Banner.Symbol}")

    # 已改为随主题切换：两主题各一份，均应与各自 SVG 的 mark/ring 色值一致
    print(f"  HomePage.xaml 引用 Banner.Symbol: {refs} 处 "
          f"（应为 4：左侧迷你 Logo 的环+星、右侧大环的环+星）")
    ok_all = refs == 4
    for theme, svg_name, gids in (("Dark", "banner_dark.svg", ("ring", "mark")),
                                 ("Light", "banner_light.svg", ("ringL", "markL"))):
        svg = io.open(os.path.join(HERE, svg_name), encoding="utf-8").read()
        svg_stops = None
        for gid in gids:
            g = re.search(r'<linearGradient[^>]*id="' + gid + r'"[^>]*>(.*?)</linearGradient>',
                          svg, re.S)
            if g:
                svg_stops = [("#FF" + c[1:].upper())
                             for c in re.findall(r'stop-color="(#[0-9A-Fa-f]{6})"', g.group(1))]
                break
        px = os.path.join(WPF, "Resources", f"BannerBackground.{theme}.xaml")
        pxaml = io.open(px, encoding="utf-8").read()
        xm = re.search(r'x:Key="Banner\.Symbol"[^>]*>(.*?)</LinearGradientBrush>', pxaml, re.S)
        x_stops = [c.upper() for c in
                   re.findall(r'Color="(#[0-9A-Fa-f]{8})"', xm.group(1))] if xm else []
        # SVG 的 offset 是 0/0.5/1（mark）或 0/0.55/1（ring）；工程取 mark 的等距写法，
        # 只比色值序列，不比 offset。
        same = (svg_stops or []) == x_stops
        ok_all = ok_all and same
        print(f"  {theme}: SVG {svg_stops}  /  xaml {x_stops}  "
              f"{'[OK] 一致' if same else '[X] 不一致'}")
    print(f"  说明：两主题各一份（浅色版用更深的青蓝补偿浅底对比度）。"
          f"两处引用共用同一支渐变（与设计稿 ring/mark 色值相同的事实一致）。")
    print(f"  => {'[OK]' if ok_all else '[X]'}")


# ============================================================ E
def audit_handwritten():
    hr("E. banner 的结构部分（非脚本转换）")
    xaml = io.open(HOME_XAML, encoding="utf-8").read()
    # 用带 ===== 的精确标记切段（注释正文里也会出现"内容层"三字，不能用裸词匹配）
    a = xaml.find("品牌 / 状态条")
    b = xaml.find("===== 内容层")
    seg = xaml[a:b] if (a >= 0 and b > a) else ""
    print(f"  品牌条的结构元素（颜色已全部来自 SVG，这里只是几何/布局）：")
    for tag in ("Ellipse", "Path", "Canvas"):
        print(f"    <{tag}: {seg.count('<'+tag)}")
    print(f"  说明：右侧大环与左侧迷你 Logo 的**环/星几何**写在 XAML 里（属布局），")
    print(f"        但两者的**颜色**来自 Banner.Symbol（脚本生成），不再手写。")
    # 迷你 logo
    m = re.search(r'迷你「星门罗盘」.*?</Canvas>', xaml, re.S)
    if m:
        ring = re.search(r'Width="(\d+)"\s+Height="(\d+)"', m.group(0))
        if ring:
            print(f"\n  迷你 Logo: {ring.group(1)}×{ring.group(2)}px 画布（几何对应 SVG 的 mark）")


if __name__ == "__main__":
    audit_logo()
    audit_nav_icons()
    audit_banner()
    audit_banner_ring()
    audit_handwritten()
