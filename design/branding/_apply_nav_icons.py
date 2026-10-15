# -*- coding: utf-8 -*-
"""把 MainWindow.xaml 里 NavigationViewItem 的单色 SymbolIcon 换成彩色矢量 ImageIcon。

两遍式（比"逐行扫描插入"稳，多行属性标签不会被截断）：
  Pass 1 —— 删掉所有 <ui:NavigationViewItem.Icon>…</ui:NavigationViewItem.Icon> 块；
  Pass 2 —— 正则整体匹配每个导航项的**开标签**（含多行属性），按声明顺序插入 Icon 块；
            自闭合标签（子项）需同时补出配对的 </ui:NavigationViewItem>。
缩进按开标签所在行的缩进 + 4 空格递进，保持文件风格。
"""
import io
import re

MW = (r"D:\QEDSD\Code\TheGuideToTheNewEden\TheGuideToTheNewEden"
      r"\TheGuideToTheNewEden.WPF\Views\MainWindow.xaml")

SEQ = [
    "home", "characters", "business",
    "market", "order", "scalper", "compareMarket", "appraisal",
    "gamePreview", "assistant", "channel",
    "channelIntel", "channelMonitor", "channelScan", "channelMarket", "gameLogMonitor",
    "translation", "translationAi", "channelTranslation", "translationLocal",
    "guides", "ded", "mission",
    "wormhole", "links", "map", "zkb", "database", "eveSettingsSync", "tools",
    "toolsIME", "serverPing", "keyboardSpan", "taskbar",
    "settings",
]

# 导航图标显示尺寸。WPF-UI 的导航项图标槽标准是 20×20；
# 不显式限尺寸时 ImageIcon 会按 DrawingImage 的自然尺寸铺满，实测到 45px（行高仅 49px）。
ICON_SIZE = 20

text = io.open(MW, encoding="utf-8").read()

# ---------- Pass 1：清除旧 Icon 块 ----------
text, n_removed = re.subn(
    r"[ \t]*<ui:NavigationViewItem\.Icon>.*?</ui:NavigationViewItem\.Icon>[ \t]*\n",
    "", text, flags=re.S)
print("removed old icon blocks:", n_removed)

# ---------- Pass 2：按顺序插入 ----------
OPEN = re.compile(r"(?P<indent>[ \t]*)<ui:NavigationViewItem(?![.\w])[^>]*?(?P<self>/?)>", re.S)

idx = 0
out, last = [], 0
for m in OPEN.finditer(text):
    nav_id = SEQ[idx]
    idx += 1
    indent = m.group("indent")
    child = indent + "    "
    icon = (f"\n{child}<ui:NavigationViewItem.Icon>"
            f"\n{child}    <ui:ImageIcon Width=\"{ICON_SIZE}\" Height=\"{ICON_SIZE}\" "
            f"Source=\"{{StaticResource NavIcon.{nav_id}}}\" />"
            f"\n{child}</ui:NavigationViewItem.Icon>")

    out.append(text[last:m.start()])
    if m.group("self"):
        # 自闭合 "… />" → 开标签 "…>"（去掉末尾的 "/>"，共两个字符）+ Icon + 配对闭合标签
        open_tag = text[m.start():m.end()].rstrip()
        assert open_tag.endswith("/>"), open_tag[-20:]
        out.append(open_tag[:-2].rstrip() + ">")
        out.append(icon)
        out.append(f"\n{indent}</ui:NavigationViewItem>")
    else:
        out.append(text[m.start():m.end()])
        out.append(icon)
    last = m.end()

out.append(text[last:])
result = "".join(out)

assert idx == len(SEQ), f"导航项数量不符：文件 {idx} vs 预期 {len(SEQ)}"
io.open(MW, "w", encoding="utf-8", newline="\n").write(result)
print("inserted:", idx, "| remaining SymbolIcon:", result.count("<ui:SymbolIcon"))
