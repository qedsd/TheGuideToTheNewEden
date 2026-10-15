# -*- coding: utf-8 -*-
"""品牌资产一致性自检：确认「设计稿 → 工程资产」没走形。

跑法：
    python design/branding/_verify_branding.py

## 为什么需要它
这条链路上已经出过三次「静默走形」——都不报错、不崩，只是悄悄画错：
  1) 导航图标：DrawingImage 自然尺寸不一致 → 显示忽大忽小（见 NavIcons.xaml 头注释）
  2) 品牌条星野：**手工搬运**导致闪星变圆点、少 13 颗星、6 颗被裁（REFACTORING 阶段 112）
  3) Logo 位图：渐变插值公式写反 → 青靛互换、中间色变灰紫（同阶段）
这类偏差肉眼很难逐条核对，所以固化成检查项，改完资产跑一次。

## 检查项
  A. logo 位图（app.ico 各帧 + logo_*.png）的颜色是否与 logo.svg 的渐变定义一致
  B. 导航图标数：icons/nav_*.svg 数量 == NavIcons.xaml 里的 DrawingImage 数量
  C. 导航图标自然尺寸是否统一（防"忽大忽小"回归）
  D. 品牌条星野：BannerBackground.*.xaml 的圆点/星数是否与 banner_*.svg 一致

退出码：0 = 全部通过；1 = 有检查项失败。
"""
import glob
import io
import os
import re
import struct
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
WPF = os.path.join(ROOT, "TheGuideToTheNewEden", "TheGuideToTheNewEden.WPF")

FAILURES = []
WARNINGS = []


def warn(msg):
    WARNINGS.append(msg)
    print(f"  [!] {msg}")


def expected_sizes():
    """从 _gen_logo_bitmap.py 读当前会生成哪些尺寸（避免校验被遗留文件干扰）。"""
    src = io.open(os.path.join(HERE, "_gen_logo_bitmap.py"), encoding="utf-8").read()
    def grab(name):
        m = re.search(name + r"\s*=\s*\[([\d,\s]+)\]", src)
        return [int(x) for x in m.group(1).split(",")] if m else []
    return grab("png_sizes"), grab("ico_sizes")


def fail(msg):
    FAILURES.append(msg)
    print(f"  [X] {msg}")


def ok(msg):
    print(f"  [v] {msg}")


# ---------------------------------------------------------------- A. Logo 颜色
def svg_gradient(svg_path):
    """读 linearGradient 的 G_FROM / G_TO / STOPS（只支持本项目的三停等距写法）。"""
    s = io.open(svg_path, encoding="utf-8").read()
    g = re.search(r'<linearGradient[^>]*x1="([\d.]+)"[^>]*y1="([\d.]+)"[^>]*'
                  r'x2="([\d.]+)"[^>]*y2="([\d.]+)"', s)
    if not g:
        return None
    frm = np.array([float(g.group(1)), float(g.group(2))])
    to = np.array([float(g.group(3)), float(g.group(4))])
    stops = [(float(o), tuple(int(c[i:i + 2], 16) for i in (1, 3, 5)))
             for o, c in re.findall(r'<stop\s+offset="([\d.]+)"\s+stop-color="(#[0-9A-Fa-f]{6})"', s)]
    return frm, to, stops


def expect_color(t, stops):
    out = np.zeros(3) + np.array(stops[0][1], dtype=float)
    for i in range(len(stops) - 1):
        x0, x1 = stops[i][0], stops[i + 1][0]
        seg = np.clip((t - x0) / (x1 - x0), 0, 1)
        out += seg * (np.array(stops[i + 1][1], dtype=float) - np.array(stops[i][1], dtype=float))
    return out


def check_logo():
    print("\n[A] Logo 位图颜色 vs logo.svg")
    grad = svg_gradient(os.path.join(HERE, "logo.svg"))
    if not grad:
        fail("logo.svg 里找不到 linearGradient")
        return
    frm, to, stops = grad
    ax, dn = to - frm, float((to - frm) @ (to - frm))

    targets = []
    png_sizes, ico_sizes = expected_sizes()
    present = {int(re.search(r"logo_(\d+)", f).group(1))
               for f in glob.glob(os.path.join(HERE, "assets", "logo_*.png"))}

    for n in png_sizes:
        f = os.path.join(HERE, "assets", f"logo_{n}.png")
        if os.path.exists(f):
            targets.append((f"logo_{n}.png", Image.open(f).convert("RGBA")))
        else:
            fail(f"logo_{n}.png 缺失（跑 _gen_logo_bitmap.py 生成）")

    # 孤儿文件：存在但当前脚本不再生成 —— 多半是历史遗留（曾用过错误配色），
    # 不自动删除，只提示由人决定。
    orphans = sorted(present - set(png_sizes))
    if orphans:
        warn(f"assets/ 下有 {len(orphans)} 个孤儿文件（当前脚本不再生成，无任何引用）："
             + ", ".join(f"logo_{n}.png" for n in orphans))

    # app.ico 的全部帧
    ico = os.path.join(WPF, "Assets", "app.ico")
    if os.path.exists(ico):
        im = Image.open(ico)
        for sz in sorted(im.info.get("sizes", [im.size])):
            try:
                im.size = sz
                targets.append((f"app.ico@{sz[0]}px", im.convert("RGBA")))
            except Exception:
                continue

    for name, fr in targets:
        n = fr.size[0]
        a = np.array(fr).astype(int)
        ys, xs = np.mgrid[0:n, 0:n]
        t = np.clip(((np.stack([xs, ys], -1) * (128.0 / n) - frm) @ ax) / dn, 0, 1)
        m = a[..., 3] > 250
        if m.sum() == 0:
            fail(f"{name}: 没有不透明像素")
            continue
        errs = np.array([np.abs(rgb - expect_color(tt, stops)).max()
                         for tt, rgb in zip(t[m], a[..., :3][m])])
        if errs.max() > 3:
            fail(f"{name}: 与 logo.svg 配色偏差过大（最大 {errs.max():.0f}）"
                 f" —— 多半是渐变公式出错/方向反转")
        else:
            ok(f"{name}: 配色一致（偏差 ≤{errs.max():.0f}）")


# ------------------------------------------------------- B/C. 导航图标
def check_nav_icons():
    print("\n[B] 导航图标数量")
    svgs = [f for f in glob.glob(os.path.join(HERE, "icons", "nav_*.svg"))]
    xaml = os.path.join(WPF, "Resources", "NavIcons.xaml")
    if not os.path.exists(xaml):
        fail("NavIcons.xaml 不存在（先跑 _svg_to_wpf_icons.py）")
        return
    n_xaml = io.open(xaml, encoding="utf-8").read().count("<DrawingImage x:Key=")
    if len(svgs) == n_xaml:
        ok(f"svg {len(svgs)} 个 == xaml 资源 {n_xaml} 个")
    else:
        fail(f"数量不一致：icons/nav_*.svg {len(svgs)} 个，"
             f"NavIcons.xaml {n_xaml} 个（跑 _svg_to_wpf_icons.py 重新生成）")

    print("\n[C] 导航图标尺寸统一性（防'忽大忽小'回归）")
    text = io.open(xaml, encoding="utf-8").read()
    missing = text.count("<DrawingImage x:Key=") - text.count("透明占位")
    if missing <= 0:
        ok(f"全部 {text.count('<DrawingImage x:Key=')} 枚图标都带透明占位矩形")
    else:
        fail(f"{missing} 枚图标缺透明占位矩形 —— 会导致自然尺寸不一致、显示忽大忽小")


# ------------------------------------------------------- D. 品牌条星野
def check_stars():
    print("\n[D] 品牌条星野数量 vs banner SVG")
    for theme, svg_name in (("Dark", "banner_dark.svg"), ("Light", "banner_light.svg")):
        svg_path = os.path.join(HERE, svg_name)
        xaml_path = os.path.join(WPF, "Resources", f"BannerBackground.{theme}.xaml")
        if not (os.path.exists(svg_path) and os.path.exists(xaml_path)):
            fail(f"缺少文件：{svg_name} 或 BannerBackground.{theme}.xaml")
            continue

        svg = io.open(svg_path, encoding="utf-8").read()
        m = re.search(r"<!--\s*星野[^>]*-->\s*<g\s+[^>]*>(.*?)</g>", svg, re.S)
        s2 = re.search(r"<!--\s*闪星[^>]*-->\s*<g\s+[^>]*>(.*?)</g>", svg, re.S)
        svg_dots = len(re.findall(r"<circle\s", m.group(1))) if m else 0
        svg_stars = len(re.findall(r"<path\s", s2.group(1))) if s2 else 0

        xaml = io.open(xaml_path, encoding="utf-8").read()
        x_dots = xaml.count("<EllipseGeometry")
        x_stars = xaml.count("<PathGeometry")

        if (svg_dots, svg_stars) == (x_dots, x_stars):
            ok(f"{theme}: 圆点 {x_dots}、四角星 {x_stars}，与 {svg_name} 一致")
        else:
            fail(f"{theme}: SVG 有 {svg_dots} 圆点 + {svg_stars} 星，"
                 f"xaml 里是 {x_dots} + {x_stars}（跑 _svg_to_wpf_stars.py 重新生成）")


def main():
    print("品牌资产一致性自检")
    print("=" * 56)
    check_logo()
    check_nav_icons()
    check_stars()
    print("\n" + "=" * 56)
    if WARNINGS:
        print(f"提示 {len(WARNINGS)} 条（不影响通过）：")
        for w in WARNINGS:
            print(f"  - {w}")
    if FAILURES:
        print(f"结果：{len(FAILURES)} 项未通过")
        for f in FAILURES:
            print(f"  - {f}")
        sys.exit(1)
    print("结果：全部通过")


if __name__ == "__main__":
    main()
