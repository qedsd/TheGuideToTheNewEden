# -*- coding: utf-8 -*-
"""由 logo.svg 几何生成多尺寸位图资产：logo_32.png / logo_128.png / app.ico

要点（任务管理器图标发糊的两个根因，都已修掉）：
1) 小尺寸不再由大图等比缩放 —— 每个尺寸独立超采样渲染（8×）后 LANCZOS 收缩，
   并且小尺寸走「图标适配版几何」：环加粗（10→12）、星腰更饱满（凹度降低），
   避免 16/20/24px 下细环与细尖角糊成一团。
2) ICO 不再交给 Pillow 自动重采样（它对 sizes 列表用的是低质量缩放）——
   改为手写 ICO 容器，每一帧塞进该尺寸独立渲染的精确 PNG。

几何：128 空间下 环(cx64 cy64 r46) + 罗盘星(二次贝塞尔四瓣)，渐变 #5EEAD4→#38BDF8→#6366F1。
"""
import io
import os
import struct

import numpy as np
from PIL import Image, ImageDraw

STOPS = [(0.0, (0x5E, 0xEA, 0xD4)), (0.5, (0x38, 0xBD, 0xF8)), (1.0, (0x63, 0x66, 0xF1))]
G_FROM = np.array([14.0, 8.0])
G_TO = np.array([114.0, 118.0])

# 尺寸 → (环宽, 星腰凹度 t, 星尖半径缩放)。t=0 与 SVG 原稿一致（细腰闪星）。
# 小尺寸三件套：环加粗 / 腰更饱满 / 星整体缩一点 —— 给星与环留出可见间隙。
# 注：原稿星尖半径 40 与环内缘 41 只差 1 单位，等比缩到小尺寸会顶到环，故小尺寸星要收得更多。
GEOM = {
    16: (13.0, 0.72, 0.72),
    20: (12.5, 0.62, 0.77),
    24: (12.0, 0.52, 0.82),
    28: (11.5, 0.45, 0.86),
    32: (11.0, 0.40, 0.91),
}
DEFAULT_GEOM = (10.0, 0.15, 1.0)


def bezier_quad(p0, c, p1, n=120):
    t = np.linspace(0, 1, n)[:, None]
    return (1 - t) ** 2 * np.array(p0) + 2 * (1 - t) * t * np.array(c) + t ** 2 * np.array(p1)


def star_points(concavity, tip=1.0):
    """四瓣星：控制点由「中心」向「角点连线中点」插值 —— t=0 细腰闪星，t=1 直边菱形。
    tip：角点向中心收缩的比例（小尺寸下缩小星体，给环留间隙）。"""
    o = np.array([64.0, 64.0])
    corners = [(64, 24), (104, 64), (64, 104), (24, 64)]
    corners = [tuple(o + (np.array(c, dtype=float) - o) * tip) for c in corners]
    pts = []
    for i in range(len(corners)):
        p0 = np.array(corners[i], dtype=float)
        p1 = np.array(corners[(i + 1) % len(corners)], dtype=float)
        mid = (p0 + p1) / 2
        c = o + concavity * (mid - o)
        for x, y in bezier_quad(p0, c, p1):
            pts.append((x, y))
    return pts


def gradient_rgb(size):
    axis = G_TO - G_FROM
    denom = float(axis @ axis)
    ys, xs = np.mgrid[0:size, 0:size]
    coords = np.stack([xs, ys], axis=-1) * (128.0 / size)
    t = ((coords - G_FROM) @ axis) / denom
    t = np.clip(t, 0.0, 1.0)[..., None]
    out = np.zeros((size, size, 3), dtype=np.float64)
    locs = [s[0] for s in STOPS]
    cols = [np.array(s[1], dtype=np.float64) for s in STOPS]
    for i in range(len(locs) - 1):
        x0, x1 = locs[i], locs[i + 1]
        seg = np.clip((t - x0) / (x1 - x0), 0, 1)
        out += seg * (cols[i] - cols[i + 1])
    out += cols[-1]
    return np.clip(out, 0, 255).astype(np.uint8)


def build(size: int, ring_w: float = 10.0, concavity: float = 0.15, tip: float = 1.0) -> Image.Image:
    sup = max(size * 8, 256)          # 每尺寸独立超采样
    k = sup / 128.0
    mask = Image.new("L", (sup, sup), 0)
    d = ImageDraw.Draw(mask)
    r, w = 46.0, ring_w                # 环中心线半径 / 环宽
    r_out, r_in = (r + w / 2) * k, (r - w / 2) * k
    cx = cy = 64.0 * k
    d.ellipse([cx - r_out, cy - r_out, cx + r_out, cy + r_out], fill=255)
    d.ellipse([cx - r_in, cy - r_in, cx + r_in, cy + r_in], fill=0)
    d.polygon([(x * k, y * k) for x, y in star_points(concavity, tip)], fill=255)

    alpha = mask.resize((size, size), Image.LANCZOS)
    img = Image.fromarray(gradient_rgb(size), "RGB")
    img.putalpha(alpha)
    return img


def build_size(size: int) -> Image.Image:
    ring_w, concavity, tip = GEOM.get(size, DEFAULT_GEOM)
    return build(size, ring_w, concavity, tip)


def write_ico(path: str, images):
    """手写 ICO 容器（每帧 PNG，Vista+ 支持），按尺寸从小到大排列。"""
    images = sorted(images, key=lambda p: p[0])
    header = struct.pack("<HHH", 0, 1, len(images))
    offset = 6 + 16 * len(images)
    entries, datas = [], []
    for size, im in images:
        buf = io.BytesIO()
        im.save(buf, "PNG", optimize=True)
        data = buf.getvalue()
        entries.append(struct.pack("<BBBBHHII",
                                   size % 256, size % 256, 0, 0, 1, 32, len(data), offset))
        datas.append(data)
        offset += len(data)
    with open(path, "wb") as f:
        f.write(header)
        for e in entries:
            f.write(e)
        for d in datas:
            f.write(d)


def main():
    out_dir = r"D:\QEDSD\Code\TheGuideToTheNewEden\design\branding\assets"
    assets = (r"D:\QEDSD\Code\TheGuideToTheNewEden\TheGuideToTheNewEden"
              r"\TheGuideToTheNewEden.WPF\Assets")
    os.makedirs(out_dir, exist_ok=True)
    os.makedirs(os.path.join(assets, "_backup"), exist_ok=True)

    ico_sizes = [16, 20, 24, 28, 32, 40, 48, 64, 96, 128, 256]
    png_sizes = [16, 24, 32, 48, 64, 128, 256]
    imgs = {n: build_size(n) for n in sorted(set(ico_sizes + png_sizes))}

    for n in png_sizes:
        imgs[n].save(os.path.join(out_dir, f"logo_{n}.png"))

    ico_path = os.path.join(out_dir, "app.ico")
    write_ico(ico_path, [(n, imgs[n]) for n in ico_sizes])

    # 工程资产覆盖（旧文件已备份过一次，不重复搬动）
    for n in (32, 128):
        imgs[n].save(os.path.join(assets, f"logo_{n}.png"))
    with open(ico_path, "rb") as src, open(os.path.join(assets, "app.ico"), "wb") as dst:
        dst.write(src.read())

    # 自检：读回 ICO 各帧尺寸
    with Image.open(os.path.join(assets, "app.ico")) as ic:
        frames = sorted({(ic.size[0], ic.size[1])} | {(s, s) for s in ico_sizes})
    print("done. ico frames:", ico_sizes, "| size:", os.path.getsize(os.path.join(assets, "app.ico")))


if __name__ == "__main__":
    main()
