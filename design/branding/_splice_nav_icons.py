# -*- coding: utf-8 -*-
"""把生成器新产出的 NavIcon.anomaly / NavIcon.ded 两枚图标拼回**原格式**的 NavIcons.xaml。

为什么不让生成器整体覆盖：`_svg_to_wpf_icons.py` 重排了全部 40 枚图标的 XML 缩进与属性顺序，
diff 会变成 600+ 行纯格式噪音。这里只动这两枚，其余字节保持原样。
"""
import io
import re

ORIG = (r"D:\QEDSD\Code\TheGuideToTheNewEden\TheGuideToTheNewEden"
        r"\TheGuideToTheNewEden.WPF\Resources\NavIcons.xaml")
GEN = r"C:\Users\QEDSD\AppData\Local\Temp\NavIcons.generated.xaml"


def read(path):
    return io.open(path, encoding="utf-8").read()


def block(text, key):
    """取 `<!--  名字  -->` + `<DrawingImage x:Key="NavIcon.<key>"> … </DrawingImage>` 整段。"""
    m = re.search(r'<DrawingImage x:Key="NavIcon\.%s">' % re.escape(key), text)
    if not m:
        raise SystemExit("not found: " + key)
    start = m.start()
    # 往上吃掉紧邻的注释行
    before = text.rfind("\n", 0, start)
    prev_line_start = text.rfind("\n", 0, before) + 1
    candidate = text[prev_line_start:before + 1]
    if "<!--" in candidate:
        start = prev_line_start
    end = text.index("</DrawingImage>", start) + len("</DrawingImage>")
    return text[start:end]


orig = read(ORIG)
gen = read(GEN)

anomaly = block(gen, "anomaly")
ded = block(gen, "ded")

old_ded = block(orig, "ded")
if old_ded not in orig:
    raise SystemExit("old ded block mismatch")

merged = orig.replace(old_ded, anomaly.rstrip("\n") + "\n\n" + ded.rstrip("\n"), 1)

io.open(ORIG, "w", encoding="utf-8", newline="").write(merged)
print("spliced anomaly + new ded")
