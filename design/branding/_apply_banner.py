# -*- coding: utf-8 -*-
"""把 design/branding/BrandBanner.xaml 的完整品牌条替换进 HomePage.xaml，并删除旧 Hero* 资源。"""
import io, re

WPF = r"D:\QEDSD\Code\TheGuideToTheNewEden\TheGuideToTheNewEden\TheGuideToTheNewEden.WPF"
HP = WPF + r"\Views\Pages\HomePage.xaml"
BB = r"D:\QEDSD\Code\TheGuideToTheNewEden\design\branding\BrandBanner.xaml"

bb = io.open(BB, encoding="utf-8").read()
# 定位真实标签（注释头里出现过同名引用，靠 Height 属性区分）
start = bb.index('<Border x:Name="HeroBanner" Height')
block = bb[start:].rstrip() + "\n"

lines = io.open(HP, encoding="utf-8").read().split("\n")

# 1) 品牌条块：找标记注释行 与 之后的 </Border> 块结束
bi = next(i for i, l in enumerate(lines) if "品牌 / 状态条" in l and "===" in l)
# 块从注释行起（注释保留），Border 从下一行起；块结束于与 Border 配对的 </Border>
depth = 0
bj = None
for i in range(bi + 1, len(lines)):
    s = lines[i].strip()
    if bj is None:
        if s.startswith("<Border") and not s.startswith("<Border.") and "/>" not in s:
            depth += 1
        if s == "</Border>":
            depth -= 1
            if depth == 0:
                bj = i
                break
assert bj is not None, "hero border block end not found"

new_head = lines[:bi]
new_tail = lines[bj + 1:]
out = new_head + ["            " + block] + new_tail

text = "\n".join(out)

# 2) 删除 Page.Resources 里的旧 Hero* 画刷（整段注释+7 行）
text = re.sub(
    r"\n        <!--\n            品牌条的配色[\s\S]*?不参与深浅主题切换（若写成主题资源，浅色主题下白底白字反而不可读）。\n        -->\n"
    r"(        <SolidColorBrush x:Key=\"Hero[^\"]+\" Color=\"#[0-9A-Fa-f]+\" />\n)+",
    "\n",
    text,
)

# 3) 删除 HeroIconButton / HeroPill 样式（含注释行）
text = re.sub(
    r"\n        <!--  品牌条右侧的图标按钮  -->\n        <Style x:Key=\"HeroIconButton\"[\s\S]*?</Style>",
    "",
    text,
)
text = re.sub(
    r"\n        <!--  品牌条上的状态胶囊  -->\n        <Style x:Key=\"HeroPill\"[\s\S]*?</Style>",
    "",
    text,
)

# 4) 顶部注释更新（提到矢量主题感知）
text = text.replace(
    "        · 品牌 / 状态条（104）——原来是 WinUI 版占掉 1/3 首屏的大图 Hero，这里压成一条；",
    "        · 品牌 / 状态条（104 → 品牌重设计）：纯矢量主题感知版（DynamicResource 随深浅主题切换），替代 home.jpg 固定深色图；",
)

io.open(HP, "w", encoding="utf-8", newline="\n").write(text)
print("HomePage.xaml updated, lines:", text.count("\n") + 1)
print("Hero leftovers:", len(re.findall(r"Hero(?:Text|Pill|Button|Image|Update|Secondary)", text)))
