# 品牌资产维护说明（design/branding）

> 本目录是**所有品牌资产的设计源**。运行时真正使用的文件都在 WPF 工程里，
> 本目录的 `.py` 脚本负责「设计源 → 工程资产」的转换。
>
> 一并阅读：`TheGuideToTheNewEden.WPF/REFACTORING.md` 的**阶段 112**（品牌重设计全记录，
> 含踩坑过程与设计决策）。

---

## 1. 资产总览

| 设计源（本目录） | 转换脚本 | 产物（工程内） | 何时需要重跑 |
|---|---|---|---|
| `logo.svg` | `_gen_logo_bitmap.py` | `WPF/Assets/app.ico`<br>`WPF/Assets/logo_32.png`<br>`WPF/Assets/logo_128.png` | 改主 Logo 后 |
| `icons/nav_*.svg`（35 个） | `_svg_to_wpf_icons.py` | `WPF/Resources/NavIcons.xaml` | 改功能图标后 |
| `banner_dark.svg` / `banner_light.svg` | `_svg_to_wpf_banner.py` | `WPF/Resources/BannerBackground.Dark.xaml`<br>`WPF/Resources/BannerBackground.Light.xaml` | 改横幅后 |
| `BrandBanner.xaml` | 无（工程内已有一份，此为留档） | — | 改主页品牌条时手工同步 |

> **注意**：工程内的品牌条背景**整体**来自本目录，由 `_svg_to_wpf_banner.py`
> 生成到 `TheGuideToTheNewEden.WPF/Resources/BannerBackground.{Dark,Light}.xaml`，
> 再由 `ThemeService.ApplyBannerDictionary()` 随主题整体换入。改 `banner_*.svg` 后
> **跑一次脚本即可**（底色 + 三层星云 + 品牌符号 + 星野全部自动转换）。
>
> 生成的键：
> | 键 | 内容 | 用在哪 |
> |---|---|---|
> | `Banner.Bg.Base` | 底色 | 横幅整块底 |
> | `Banner.Bg.Nebula1..3` | 三层星云 | 横幅背景 |
> | `Banner.Symbol` | 品牌符号渐变 | **两处**：左侧迷你 Logo + 右侧出血大环（`ring`/`mark` 色值相同，故共用） |
> | `Banner.Stars` | 星野 + 闪星 | 横幅星点 |
>
> **颜色与几何均已 100% 脚本化** —— `HomePage.xaml` 里再无手写色值或手抄坐标。
> 除生成上述键外，`_svg_to_wpf_banner.py` 还会把「右侧星门环 + 罗盘星」的**几何**
> 写回 `HomePage.xaml`（`BEGIN/END BannerSymbol` 标记区块），等比与设计稿严格同构。
> **改 `banner_*.svg` 后跑一次脚本，颜色和几何一起更新。**
>
> 界面上那些应用名 / 版本号 / 在线人数 / 游戏时间 / 各类按钮是**活内容**，
> 写在 `Views/Pages/HomePage.xaml` 里，**不在**本目录的 SVG 内 ——
> 与 logo 同款处理：**SVG 只负责静态背景与装饰图形**。

### 字体：设计稿与工程要一起看

SVG 里声明的字栈是 `'Segoe UI','Microsoft YaHei','PingFang SC',sans-serif`。
工程侧对应的资源是 `BrandFontFamily`（定义在 `Controls/GlobalControlStyles.xaml`），
值写成 `Segoe UI, Microsoft YaHei, PingFang SC, Segoe UI Variable Text, Arial`
—— **比设计稿多两个兜底项**，因为 WPF 的 FontFamily 支持逗号分隔的回退序列，
逐项尝试、缺失即跳到下一个，全部失效才落到内置的「全局用户界面」复合字体。

改设计稿的 `font-family` 时，请**同步改 `BrandFontFamily`**，两边保持一致。

| 设计稿字体 | 用途 | 缺失时 |
|---|---|---|
| `Segoe UI` | 拉丁首选 | 跳到下一项 |
| `Microsoft YaHei` | 中文首选 | 跳到下一项 |
| `PingFang SC` | macOS 用，Windows 上恒缺 | 跳过（留着是为与设计稿字栈一致） |
| （工程新增）`Segoe UI Variable Text` | Win11 新字体 | 跳到下一项 |
| （工程新增）`Arial` | 最终兜底 | 必定存在 |
>
> 而界面上的应用名 / 版本号 / 在线人数 / 游戏时间 / 各类按钮是**活内容**，
> 写在 `Views/Pages/HomePage.xaml` 里，**不在**本目录的 SVG 内 ——
> 这与 logo 的处理方式一致：**SVG 只负责静态背景**。

其它：`preview.html`（Logo 与横幅的交互预览）、`icons/preview.html`（35 个图标的预览）。

---

## 0. 改完资产先跑自检

```bash
# 快速自检（4 项，退出码 0=通过 / 1=有失败）
python design/branding/_verify_branding.py

# 细粒度审计（更全，逐项列出比对结果，用于排查）
python design/branding/_audit_branding.py
```

| 检查 | `_verify_branding.py` | `_audit_branding.py` |
|---|---|---|
| logo 位图配色 vs `logo.svg` | ✓ | ✓ |
| 导航图标数量（svg ↔ xaml） | ✓ | ✓ |
| 导航图标尺寸统一性 | ✓ | ✓ |
| 导航图标颜色 / opacity / 线帽 | — | ✓ |
| banner 星野数量 | ✓ | ✓ |
| banner 图层数 / 渐变几何 | — | ✓ |
| banner ring 配色 | — | ✓ |

> **为什么要这两个脚本**：这条链路已出现**四次静默走形** —— 都不报错、不崩，只是悄悄画错：
> 导航图标显示忽大忽小 / 品牌条闪星变圆点 / Logo 渐变青靛反转 / 图标 `opacity` 被丢、
> banner 漏了一整层星云。这类偏差肉眼逐条核对几乎不可能，见 `REFACTORING.md` 阶段 112。

---

## 2. 改一个功能图标（最常见）

**只需改 1 个文件 + 跑 1 个脚本。**

```bash
# ① 编辑图形（35 个独立文件，文件名即功能名）
design/branding/icons/nav_market.svg

# ② 重新生成资源字典
python design/branding/_svg_to_wpf_icons.py

# ③ 构建（XAML 资源变化需重新编译）
dotnet build TheGuideToTheNewEden/TheGuideToTheNewEden.WPF/TheGuideToTheNewEden.WPF.csproj -c Debug
```

**四个引用位置一处都不用动**（左侧导航、主页磁贴、添加功能窗口、设置→主页），
它们全部指向 `NavIcons.xaml` 里的同一个 `NavIcon.<id>` 资源：

```
icons/nav_<id>.svg            ← 唯一真源
      │  _svg_to_wpf_icons.py
      ▼
Resources/NavIcons.xaml       ← 唯一图标资源（NavIcon.<id>）
      │  StaticResource / Binding
      ▼
导航栏 / 主页磁贴 / 添加功能窗口 / 设置→主页   ← 都只是「指针」
```

不必再跑 `_apply_nav_icons.py` —— 那是**一次性**的接入脚本（当初把 35 个
`ui:SymbolIcon` 批量换成 `ImageIcon`），已完成使命，日常改图标用不到。

### 图标资源键的三种引用方式

```xml
<!-- ① 导航栏：直接静态引用 -->
<ui:ImageIcon Width="20" Height="20" Source="{StaticResource NavIcon.home}" />

<!-- ② 主页磁贴：走 ViewModel 的 TileIcon（已查好资源对象） -->
<Image Width="28" Height="28" Source="{Binding TileIcon}" />

<!-- ③ 添加功能窗口左侧分组：走 AddShortcutGroup.IconSource -->
<Image Width="16" Height="16" Source="{Binding IconSource}" />
```

---

## 3. 改主 Logo

```bash
# ① 编辑 design/branding/logo.svg（主版）与 logo_white.svg（单色白版）
# ② 生成全部尺寸位图，并自动覆盖工程资产（旧文件自动备份到 WPF/Assets/_backup/）
python design/branding/_gen_logo_bitmap.py

# ③ 必须重新构建 —— 图标是编译期链进 exe 的 Win32 资源，换文件本身不生效
dotnet build TheGuideToTheNewEden/TheGuideToTheNewEden.WPF/TheGuideToTheNewEden.WPF.csproj -c Debug
```

产物：`app.ico`（11 档尺寸：16/20/24/28/32/40/48/64/96/128/256）+ `logo_32.png` + `logo_128.png`。

> **任务管理器缓存**：改完图标后任务管理器可能仍显示旧图标。彻底退出程序再看；
> 必要时注销一次资源管理器。

---

## 4. 可调参数速查

### `_svg_to_wpf_icons.py`（功能图标）

| 参数 | 默认 | 说明 |
|---|---|---|
| `NAV_TARGET` | `20.0` | 图标显示尺寸（px）。改这里会同步改缩放比 |
| `STROKE_SCALE` | `1.25` | 描边加粗倍数。嫌图标太细就调大（如 `1.4`） |
| `ICON_DIR` / `OUT` | 绝对路径 | 源目录 / 产物路径 |

### `_gen_logo_bitmap.py`（主 Logo）

| 参数 | 说明 |
|---|---|
| `GEOM` | `{尺寸: (环宽, 星腰凹度, 星体缩放)}`，**小尺寸单独调参**表 |
| `DEFAULT_GEOM` | ≥40px 用的默认几何（`(10.0, 0.15, 1.0)` = 与 SVG 原稿一致） |
| `STOPS` | 渐变三个色停（青 `#5EEAD4` → 蓝 `#38BDF8` → 靛 `#6366F1`） |
| `ico_sizes` / `png_sizes` | 导出的尺寸清单 |

---

## 5. 必须记住的三个坑

这三条都是实际踩出来的，改之前先看一眼能省很多时间。

### ① `Image.Source` 不能绑资源键**字符串**

```xml
<!-- ✗ 错误：ImageSourceConverter 会把它当文件路径解析，图标不显示 -->
<Image Source="NavIcon.home" />
<Image Source="{Binding SomeStringKey}" />

<!-- ✓ 正确：绑「已解析的对象」 -->
<Image Source="{StaticResource NavIcon.home}" />
<Image Source="{Binding IconSource}" />   <!-- IconSource 返回 TryFindResource 的结果 -->
```

视图模型里必须返回**对象**，例如：

```csharp
public object? IconSource => Application.Current?.TryFindResource("NavIcon." + IconKey);
```

### ② `Image`/`ImageIcon` 的尺寸必须显式写死

`DrawingImage` 的自然尺寸来自**绘制内容的包围盒**，而每个图标图形占比不同，
不写尺寸时会被拉伸到各自包围盒 → **忽大忽小、比例失真、边缘裁切**。

所以 `NavIcons.xaml` 里每个图标都垫了一枚**透明全画布矩形**（`M0 0 H128 V128 H0 Z`）
把包围盒统一成 128×128（缩放后恒为 20×20），并且引用处一律写 `Width`/`Height`。

> **不要删那条透明矩形** —— 删了图标立刻失真。生成器里有注释标注。

### ③ 改 `app.ico` 后必须重新构建

图标是**编译期**链接进 exe 的 Win32 资源，替换文件不会自动生效。

---

## 6. 一次性脚本（可忽略）

| 脚本 | 用途 | 现状 |
|---|---|---|
| `_apply_nav_icons.py` | 把 `MainWindow.xaml` 的 35 个 `SymbolIcon` 批量换成 `ImageIcon` | 已完成，日常不用 |
| `_apply_banner.py` | 把新品牌条写入 `HomePage.xaml` 并清理旧的 `Hero*` 资源 | 已完成，日常不用 |
