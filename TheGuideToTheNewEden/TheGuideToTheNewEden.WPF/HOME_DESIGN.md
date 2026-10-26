# WPF 主页（HomePage）设计方案

> 状态：**P0 / P1 / P2 / P3 已全部实现**（阶段 92 三轮，见 `REFACTORING.md` 阶段 92 及其三条追加）。
> 本文保留完整的分析、规格与阶段划分；**实现中的偏差与踩坑**都记在阶段 92 里，下表也标了"实现差异"。
> 前置阅读：`REFACTORING.md`（§8 第 61 条：主页曾是 26 个导航目标里唯一剩下的占位页，阶段 92 已销账）。
> 设计图目录：[`Docs/HomeDesign/`](Docs/HomeDesign/)
>
> | 图 | 文件 | 内容 |
> |---|---|---|
> | 1 | [`01-home-dark.png`](Docs/HomeDesign/01-home-dark.png) / [`02-home-light.png`](Docs/HomeDesign/02-home-light.png) | 主页默认态（深色 / 浅色），推荐布局 |
> | 2 | [`03-add-feature.png`](Docs/HomeDesign/03-add-feature.png) | 「添加功能」窗口 |
> | 3 | [`04-edit-spec.png`](Docs/HomeDesign/04-edit-spec.png) | 快捷方式编辑态 + 磁贴组件规格 |
> | 4 | [`05-layout-options.png`](Docs/HomeDesign/05-layout-options.png) | 三个信息架构方案对比 + 三个添加入口 |

---

## 1. 现状盘点

### 1.1 WPF 侧

`Views/Pages/HomePage.cs` 目前只有 10 行，继承 `PlaceholderPage`（图标 + 标题 + 「功能开发中」）。

```csharp
public sealed class HomePage : PlaceholderPage
{
    public HomePage() : base("Nav.Home", SymbolRegular.Home24) { }
}
```

除它以外，26 个导航目标**全部已是真实页面**（阶段 79/80/89 依次销账），所以主页是最后一块拼图。

### 1.2 可以直接复用的基础设施（不用新造）

| 能力 | 现成资产 | 主页用途 |
|---|---|---|
| 卡片外壳 | `Controls/CardControl.cs` + `CardControlStyles.xaml` | 快捷方式卡 / 钱包卡 / 行情卡 |
| 细占比条 | `Controls/RatioBar.cs` | 钱包收支构成（**禁止 ProgressBar**，§9-17 会栈溢出） |
| 转圈指示 | `Controls/SpinnerIcon.xaml`（旋转 `SymbolIcon`） | 行情卡 / 钱包卡加载中 |
| 异步图片 | `Controls/AsyncImage.cs`（附加属性：`Source` / `IdName` / `TypeId`） | 物品图标、角色头像（**不要用 `BitmapImage.UriSource`**，§8-27） |
| 全屏等待 + 右下角通知 | `PageNotifyService` / `WaitingOverlay` / `MessageHost` | 刷新、失败提示 |
| 工具窗口 | `Services/Tools/ToolWindowHost` + `Views/Windows/ToolWindow` | 小工具（快捷输入 / 延迟 / 键盘 / 分屏）的入口 |
| 页面跳转 | `Services/Navigation`（`Navigate` / `Activate` / `NavigateToMarket`） | 磁贴点击 |
| 设置子页跳转 | `SettingsPage.RequestCategory(pageType)`（已存在，翻译页在用） | （P2）把设置子页固定成磁贴 |
| 主题语义色 | `TextFillColor*` / `SystemFillColorSuccess|Caution|Critical` / `SystemAccentColor*` | 全部配色 |
| 应用版本 | `Helpers/AppVersion.cs` | 品牌条版本号 |
| 服务器状态 | `Core.Services.ESIService` 的 `Status.GetStatusAsync()` | 在线人数 |
| 行情取数 | `Business/MarketOrderService`、`MarketStarService`（收藏） | 我的行情 |
| 钱包取数 | `Characters/CharacterWalletService`（`GetBalanceAsync` / `GetCharacterJournalAsync`，带 2 分钟缓存） | 我的钱包 |
| 更新检查 | Core `Helpers/GithubHelper.GetLastReleaseInfoAsync()`（`UpdateSettingPage` 在用） | 版本徽标 |

### 1.3 WinUI 版主页（对照基准）

`TheGuideToTheNewEden.WinUI/Views/HomePage.xaml`（261 行）+ `HomeViewModel`（197 行），实机截图见 [`../../Img/zh/Home_Dark.png`](../../Img/zh/Home_Dark.png)。逐块拆解：

| 区块 | WinUI 实现 | 说明 |
|---|---|---|
| A. Hero 大图 | `home.jpg` 铺满 + 32px 应用名 + 右下角 QQ / GitHub 圆形按钮 | 竖向上吃掉约 1/3 首屏 |
| B. 状态小卡 ×6 | `GridView` 自适应：在线人数（含红/绿图标）、EVE 时间（1 秒定时器）、版本号（点击去更新页）、关于、日志、手册 | 固定集合，`140×42` 按钮 |
| C. 我的钱包 | `Views/Home/WalletStatistic`：**遍历所有角色 → 全部分页**拉钱包流水 → 按 ref_type 归入 收入 6 类 / 支出 5 类 → 自绘 `ProgressBar` | 代价最高的区块；下拉式刷新按钮 |
| D. 我的行情 | `MarketStarService.GetIds()` 收藏列表（空则回落 PLEX / 大型技能注入器 / 三钛合金），每项 `PriceCardControl`（物品图标 + 价格 + 涨跌 + 销量 + 涨跌，点开跳市场页） | 无条数上限、无缓存说明 |
| E. 导航 | 平铺（死亡远征 / 任务攻略 为顶层），WPF 版已改为「攻略」分组 | 以 WPF 版为准 |

### 1.4 与 WPF 版的可移植性判断

| WinUI 区块 | 能否直搬 | 结论 |
|---|---|---|
| A Hero | 部分 | 保留品牌感，但**降到 104 的状态/品牌条** |
| B 状态卡 | 部分 | 拆成「打包进品牌条的胶囊」+「品牌条右侧的图标按钮（日志/关于/手册/社区）」 |
| C 钱包 | 需重写 | 复用 WPF 的 `CharacterWalletService` + `RatioBar`；**必须加区间、缓存与降级**（原文案"月收入/月支出"其实统计的是 JSON 全量流水，口径要在 UI 上说清） |
| D 行情 | 可搬 | 复用 `MarketStarService` + `MarketOrderService`，加 TTL 缓存、条数上限、单项失败降级 |
| E 缺失项 | 需新做 | WPF **没有 About 页、没有 Manual PDF、没有日志查看器**（`Resources/Manuals` 未链接进项目）→ 用「关于窗口 + 打开日志文件/配置目录 + 在线手册链接」替代 |

---

## 2. 设计目标

1. **主页要"能干活"**：常用功能一屏一点即达，而不是装饰性欢迎页。
2. **个性化可编辑**：用户能把**任意功能**固定成磁贴，自己排序、改尺寸、随时移除。
3. **不重复造轮子**：卡片、提示条、等待遮罩、工具窗口、跳转全部复用既有设施。
4. **遵守项目既有 UI 约定**：顶层页不写页面标题（§10「UI 约定」）、不用 `ProgressBar`、颜色只用 `DynamicResource` 主题资源、文字不靠继承取 `Foreground`、图标名先核验（§9-18 / §9-17 / §9-30）。
5. **失败可降级**：没有角色、没有授权、ESI 抽风、收藏为空，都要有正常形态而不是空白或 0。

---

## 3. 信息架构：三个方案与选型

见 [`05-layout-options.png`](Docs/HomeDesign/05-layout-options.png)。

| | 方案 A（照搬 + 追加） | **方案 B（推荐）** | 方案 C（左快捷栏） |
|---|---|---|---|
| 结构 | Hero 260 → 状态卡 3×2 → 快捷区（1 行）→ 钱包/行情 | **品牌条 104 → 快捷方式（2 行 ×8）→ 钱包 + 行情** | 快捷栏常驻左侧 + 右侧品牌/钱包/行情 |
| 优点 | 迁移成本最低，与 WinUI 观感一致 | 快捷方式落在首屏可点区；品牌与社区入口仍在；一屏 16 个磁贴 | 快捷方式永远可见 |
| 缺点 | Hero 挤掉快捷区，只能看到 1 行，个性化收益浪费 | 状态信息要压缩成胶囊 + 图标按钮 | 与左侧 `NavigationView` 形成两条竖向列表，主次易混 |
| 结论 | ✗ | **✔ 采用** | 备选（磁贴很多时才考虑） |

**推荐布局（1280×860 实测排布，见设计图 1）**

```
┌──────────────┬───────────────────────────────────────────────────────────────┐
│ 标题栏 36    │  logo  新伊甸漫游指南 4.0.0          ─  □  ✕                 │
├──────────────┼───────────────────────────────────────────────────────────────┤
│ 主页 ◀(选中) │ ┌───────────────────────────────────────────────────────────┐ │
│ 角色         │ │ 品牌/状态条 104                                           │ │
│ 商业 ⌄       │ │  新伊甸漫游指南 [4.0.0]                   帮助与社区       │ │
│ 多开         │ │  ●欧服在线 28,806 人  🕒世界时 13:39:53  ⬇有新版本 4.1.0  │ │
│ 频道 ⌄       │ └───────────────────────────────────────────────────────────┘ │
│ 翻译 ⌄       │ ┌───────────────────────────────────────────────────────────┐ │
│ 攻略 ⌄       │ │ 快捷方式   共 12 个 · 上限 24   （阶段 101 起无按钮）     │ │
│ 虫洞         │ │  ┌────┐┌────┐┌────┐┌────┐┌────┐┌────┐┌────┐┌────┐        │ │
│ 快速链接     │ │  │角色││市场││订单││倒货││估价││多开││预警││监控│        │ │
│ 星图         │ │  └────┘└────┘└────┘└────┘└────┘└────┘└────┘└────┘        │ │
│ Zkillboard   │ │  ┌────┐┌────┐┌────┐┌────┐                                │ │
│ 数据库       │ │  │查价││统计││日志││星图│                                │ │
│ 小工具 ⌄     │ │  └────┘└────┘└────┘└────┘                                │ │
│              │ └───────────────────────────────────────────────────────────┘ │
│              │ ┌──────────────────────────────┐┌──────────────────────────┐ │
│              │ │ 我的钱包            [刷新]   ││ 我的行情      全部收藏 › │ │
│              │ │ 540,590,315,795.67           ││ [伊甸币] [技能注入器]    │ │
│ 设置         │ │ 近30天收入 / 支出 + 收支构成 ││ [三钛]   [类晶体胶矿]    │ │
│              │ └──────────────────────────────┘└──────────────────────────┘ │
└──────────────┴───────────────────────────────────────────────────────────────┘
```

**尺寸与自适应**

| 项 | 值 |
|---|---|
| 主窗口 | 默认 1200×800，最小 960×600（`MainWindow.xaml` 现状） |
| 内容左右边距 | 16（页面根 `Grid`），卡片内边距 20 |
| 品牌条 | 高 104，圆角 8，`home.jpg` 背景 + 左侧暗化渐变 + 稀疏星点 |
| 快捷方式卡 | 高 302（2 行磁贴 + 表头 46），磁贴随内容自适应高度 |
| 数据卡 | 高 348；窗口 < 1100 时按 `WrapPanel` 自动改为上下堆叠 |
| 磁贴列数 | 内容宽 1028 时 8 个/行；最小窗口（内容 ≈ 700）时 5 个/行（自动换行，无需断点代码） |

---

## 4. 快捷方式（核心功能）

### 4.1 两个入口，一份数据（阶段 101 起）

见 [`05-layout-options.png`](Docs/HomeDesign/05-layout-options.png) 底部。

| 入口 | 位置 | 适合场景 |
|---|---|---|
| ① 「固定到主页」 | **左侧导航项右键菜单** | 最短路径：看到哪个功能固定哪个，不必离开当前页 |
| ② **设置 → 主页** | 设置页（阶段 100 加入）：拖动排序列表 + ✕ 移除 + ⤢ 宽窄切换 + 「添加功能」 + 恢复默认 | **全局唯一的编辑入口**（阶段 101）：集中管理、拖动排序 |

（演变过程：最初的「③ 设置 → 主页」子页是上下移按钮的列表，与主页编辑的拖动重复——阶段 94 删除；
阶段 99 重建为卡片显示开关；阶段 100 把快捷方式编辑放回设置页、保留拖动，与主页编辑模式互为主次；
**阶段 101 应用户要求删除主页编辑模式与「⋯」菜单，编辑只保留设置页一处**，导航右键的「管理快捷方式…」
也改为直接跳 设置 → 主页。主页磁贴自此只有"点开"一种交互。）

两者读写同一份 `Configs/HomeShortcuts.json`，通过 `HomeShortcutService.Changed` 事件实时同步
（主页磁贴、导航右键勾选态、设置页列表与开关初值）。

### 4.1.1 显示策略的分工（阶段 101 起）

| 控制 | 归属 | 理由 |
|---|---|---|
| 钱包卡 / 行情卡 / 「最近使用」显示 | **设置 → 主页** | 属于主页卡片显示，与快捷方式无关，不该混在快捷方式菜单里 |
| 快捷方式的增删 / 排序 / 宽窄 / 恢复默认 | **设置 → 主页**（阶段 101 起唯一入口） | 用户要求全局只保留设置页可编辑 |
| ~~显示分组（磁贴按分组分段）~~ | —— | 功能随阶段 101 移除（连同「⋯」按钮与 `ShowGrouped` 配置字段） |

### 4.2 磁贴规格与状态

见 [`04-edit-spec.png`](Docs/HomeDesign/04-edit-spec.png)。

| 项 | 小磁贴（默认） | 宽磁贴 |
|---|---|---|
| 尺寸 | 112 × 112 | 232 × 112（= 2 个小磁贴宽） |
| 图标 | 28，居中，上边距 26 | 26，左对齐，左边距 22 |
| 文字 | 名称 13 / 行高 18 / 超长省略（最多 2 行） | 名称 14 `SemiBold` + 副标题 11.5（所属分组名） |
| 圆角 / 描边 | 8 / 1px `CardStrokeColorDefaultBrush` | 同左 |
| 背景 | `CardBackgroundFillColorDefaultBrush`（悬停 `…SecondaryBrush`） | 同左 |
| 间距 | 磁贴间 12，卡内边距 20，卡片间距 12 | — |

**磁贴标签一律取导航键（`Nav.*`）原文**——零新增翻译；副标题取所属分组名（`Nav.Business` 等）。所有图标名已用反射核验存在于 `SymbolRegular`（见 §7 清单）。

### 4.3 ~~编辑模式~~（阶段 101 移除）

原设计：卡片右上角「编辑」进入编辑态（磁贴 ✕、末尾「＋ 添加功能」槽位、拖动排序、`Alt + ←/→`、
底部提示条 + 恢复默认）。**阶段 101 应用户要求整体移除**——编辑统一收进 设置 → 主页（§4.1），
拖动排序的交互在那里原样保留；"恢复默认"与宽窄切换也移到设置页行内。

### 4.4 右键菜单（阶段 101 移除）

磁贴右键菜单（`打开` / `移到最前` / `切换宽窄` / `从主页移除`）随编辑入口的收拢一并删除；
宽窄切换移到 设置 → 主页 行内（⤢ 按钮），其余编辑动作同理。

### 4.5 「添加功能」窗口

见 [`03-add-feature.png`](Docs/HomeDesign/03-add-feature.png)。`ui:FluentWindow`，默认 760×690，居中到主窗口（**不设 `Owner`**，与翻译弹窗同一策略，§46）。

- 左侧：分组列表（全部功能 / 角色 / 商业 / 多开 / 频道 / 翻译 / 攻略 / 虫洞 / 星图 / Zkillboard / 数据库 / 小工具 / 设置）。
- 右上：搜索框（匹配名称 / 描述 / 分组，支持拼音首字母可后续加）。
- 行：图标 + 名称 + 描述 + 右侧 `添加`（强调色）/ `已添加 ✓`（次要色，点击即移除）。
- 底部：`已添加 N 个功能` + 提示文案 + `完成`。
- 单实例（重复点击只激活），关闭后主页磁贴即时刷新。

### 4.6 数据模型与持久化

`%LocalAppData%\TheGuideToTheNewEden\Configs\HomeShortcuts.json`（**WPF 专有文件**，WinUI 版不读）。
不塞进共用的 `settings.json` 的原因：它是扁平的「键 → 字符串值」字典（`SettingsService.SetValue`），有序列表只能序列化成一个字符串塞进某个键，既不便手改、又要与 WinUI 版争用同一个键名；单文件更适合"有顺序、有上限、可手改"的数据。

```jsonc
{
  "version": 1,
  "items": [
    { "id": "characters",    "size": "small" },
    { "id": "market",        "size": "wide"  },
    { "id": "order"          },
    { "id": "scalper"        },
    { "id": "appraisal"      },
    { "id": "game-preview"   },
    { "id": "channel-intel" },
    { "id": "channel-monitor" },
    { "id": "channel-market" },
    { "id": "channel-scan"   },
    { "id": "game-log-monitor" },
    { "id": "map"            }
  ],
  "maxItems": 24,
  "showWalletCard": true,
  "showMarketCard": true,
  "walletRangeDays": 30
}
```

规则：

- **顺序即数组顺序**；`size` 缺省 = `small`。
- `id` 用**稳定的 kebab-case 字符串**（不用页面类型名），以便：跨版本稳定、用户可手改、将来支持带参数磁贴。
- 加载时**丢弃目录里不存在的 id**（功能下线/改名）并记一条 `Log.Warn`，同时把清理结果写回一次；文件损坏 → 备份为 `HomeShortcuts.json.bak` 后用推荐种子重建。
- 首次运行（无文件）写入**推荐种子 12 项**：角色 / 市场 / 订单 / 倒货 / 估价 / 多开 / 频道预警 / 频道监控 / 频道查价 / 频道统计 / 日志监控 / 星图。
- 上限 24：超出时禁用「＋」并提示 `最多固定 24 个`（防止主页变成滚动列表）。
- **「主页」自身不参与固定**（无意义）；「设置」可以固定。

### 4.7 功能目录（`HomeFeatureCatalog`）——"每一个功能"的边界

| 层级 | 范围 | 数量 | 阶段 |
|---|---|---|---|
| L1 导航叶子 | 除「主页」外的**全部导航叶子**（含「设置」，含 4 个小工具） | 26 | **P0** |
| L2 轻量动作 | 打开日志 / 打开配置目录 / 检查更新 / 关于 / 使用手册 | 5 | P2 |
| L3 设置子页 | 10 个设置分类 + 新增「主页」分类 | 11 | P2 |
| L4 带参数磁贴 | `{ id: "market", param: "645" }`（某物品）/ 指定角色工作区 / 指定星系星图 | — | P3（数据结构已预留 `param` 字段） |

L1 完整清单（图标名**已逐个核验存在**，见 §10）：

| id | 图标 | 标题键（复用） | 分组 | 动作 |
|---|---|---|---|---|
| `characters` | `Person24` | `Nav.Characters` | 角色 | Navigate `CharactersShellPage` |
| `market` | `ArrowTrending24` | `Nav.Market` | 商业 | Navigate `MarketPage` |
| `order` | `Receipt24` | `Nav.Order` | 商业 | Navigate `OrderPage` |
| `scalper` | `ShoppingBag24` | `Nav.Scalper` | 商业 | Navigate `ScalperPage` |
| `appraisal` | `Calculator24` | `Nav.Appraisal` | 商业 | Navigate `AppraisalPage` |
| `game-preview` | `SquareMultiple24` | `Nav.GamePreview` | 多开 | Navigate `GamePreviewPage` |
| `channel-intel` | `Alert24` | `Nav.ChannelIntel` | 频道 | Navigate `ChannelIntelPage` |
| `channel-monitor` | `Comment24` | `Nav.ChannelMonitor` | 频道 | Navigate `ChannelMonitorPage` |
| `channel-scan` | `People24` | `Nav.ChannelScan` | 频道 | Navigate `ChannelScanPage` |
| `channel-market` | `Money24` | `Nav.ChannelMarket` | 频道 | Navigate `ChannelMarketPage` |
| `game-log-monitor` | `DocumentText24` | `Nav.GameLogMonitor` | 频道 | Navigate `GameLogMonitorPage` |
| `translation-ai` | `Translate24` | `Nav.TranslationAi` | 翻译 | Navigate `AiTranslationPage` |
| `translation-channel` | `CommentMultiple24` | `Nav.ChannelTranslation` | 翻译 | Navigate `ChannelTranslationPage` |
| `translation-local` | `LocalLanguage24` | `Nav.TranslationLocal` | 翻译 | Navigate `LocalTranslationPage` |
| `ded` | `DataSunburst24` | `Nav.DED` | 攻略 | Navigate `DEDPage` |
| `mission` | `DocumentOnePage24` | `Nav.Mission` | 攻略 | Navigate `MissionPage` |
| `wormhole` | `Target24` | `Nav.Wormhole` | 虫洞 | Navigate `WormholePage` |
| `links` | `Link24` | `Nav.Links` | 链接 | Navigate `LinksPage` |
| `map` | `Map24` | `Nav.Map` | 星图 | Navigate `MapPage` |
| `zkb` | `Scan24` | `Nav.ZKB` | Zkillboard | Navigate `ZKBPage` |
| `database` | `Database24` | `Nav.Database` | 数据库 | Navigate `DatabasePage` |
| `tool-ime` | `Keyboard24` | `Nav.Tools_IME` | 小工具 | `ToolWindowHost.Show(IMEPage)` |
| `tool-server-ping` | `Gauge24` | `Nav.Tools_ServerPing` | 小工具 | `ToolWindowHost.Show(ServerPingPage)` |
| `tool-keyboard-span` | `Wrench24` | `Nav.Tools_KeyboardSpan` | 小工具 | `ToolWindowHost.Show(KeyboardSpanPage)` |
| `tool-taskbar` | `Window24` | `Nav.Tools_Taskbar` | 小工具 | `ToolWindowHost.Show(TaskbarPage)` |
| `settings` | `Settings24` | `Nav.Settings` | 设置 | Navigate `SettingsPage` |

L2 动作项：

| id | 图标 | 新增标题键 | 动作 |
|---|---|---|---|
| `action-open-log` | `DocumentBulletList24` | `Home.Action.OpenLog` | 打开当日日志文件（`ShellHelper.OpenFile`） |
| `action-open-config` | `Folder24` | `Home.Action.OpenConfigFolder` | 打开配置目录（`ShellHelper.OpenFolder`） |
| `action-check-update` | `CloudArrowDown24` | `Home.Action.CheckUpdate` | 跳设置 → 软件更新（`RequestCategory`） |
| `action-about` | `Info24` | `Home.Action.About` | 打开关于窗口（P2 新建） |
| `action-manual` | `BookQuestionMark24` | `Home.Action.Manual` | 打开在线手册 / GitHub 文档 |

**目录与导航的一致性保障**：`MainWindow` 启动后遍历 `NavigationView.MenuItems`（递归到叶子，读 `TargetPageType`），与目录做双向差集；缺项只 `Core.Log.Warn`（开发期告警，不弹窗、不影响使用）。这样将来新增导航页不会"忘了给主页做磁贴"。

### 4.8 导航右键「固定到主页」

在 `MainWindow` 构造时给每个 `NavigationViewItem`（分组**父项除外**）挂 `ContextMenu`：

- `打开`（等价点击）
- `固定到主页` / `从主页取消固定`（按当前状态只显示一个，带 `IsCheckable` 视觉）
- 分隔线 + `管理快捷方式…`（阶段 101 起跳转 设置 → 主页，不再打开添加功能窗口）

要点：

- 分组父项（商业 / 频道 / 翻译 / 攻略 / 小工具）不给菜单，避免"固定一个空壳"。
- 工具类导航项（`ToolPages` 字典里的 4 项）固定后，磁贴点击走 `ToolWindowHost.Show`，**不要走 `Navigate`**（否则会先实例化页面再被 `Navigating` 拦截）。
- 语言切换后菜单文本要刷新（`LanguageService.LanguageChanged` 时重设 `Header`）。

### 4.9 ~~角标~~（阶段 188 移除）

磁贴右上角的黄色状态圆点（预警 / 监控 / 多开 / 击杀流 / 收藏的运行态）已按用户要求**整体移除**：
界面模板去掉圆点、视图模型去掉角标属性与刷新方法、`Services/Home/HomeFeatureStatusService.cs` 删除、
`HomeShortcutItem.Badge` 配置字段与 `Home.Quick.BadgeCount` / `Home.Quick.BadgeRunning` 语言键一并删除。
旧 `HomeShortcuts.json` 里的 `badge` 字段反序列化时被忽略，下次保存自然消失。

### 4.10 「最近使用」（可选，P2）

品牌条右侧或快捷方式表头放一个 `最近使用` 折叠条（最多 6 个，按点击次数/时间排序，开关默认**关**）。理由：它替代不了用户显式固定的语义，但能兜住"偶尔用的功能"；关掉时零成本（不记录）。

---

## 5. 数据卡片

### 5.1 我的钱包（左，620 宽）

- **总余额**：逐角色 `CharacterWalletService.GetBalanceAsync`（走 `CharacterOverviewService`，2 分钟缓存）求和。
- **收支构成**：默认只取**每个角色 journal 的第 1 页（最近 50 条）**，成本 = 角色数 × 2 次 ESI；`walletRangeDays` 可选 `近 30 天`（翻页，成本高）与 `全部`（更高），切换时用 `PageNotifyService.ShowWaiting` + 明确提示耗时。**这是与 WinUI 版最大的有意差异**（WinUI 是静默全量拉取，几百页）。
- 分类口径沿用 WinUI（`ref_type` 含 `market/store` → 贸易；`agent/mission` → 任务；`bounty` → 赏金；`contract` → 合同；`tax/fee/player_trading` → 税；`player_donation` → 转账；其余 → 其他），收入/支出各自归一化成百分比，用 `RatioBar`（绿 = `SystemFillColorSuccessBrush`，红 = `SystemFillColorCriticalBrush`）。
- **降级**：无角色 → 引导卡（图标 + 「去角色页添加」按钮）；部分角色取数失败 → 底部提示 `N 个角色取数失败`（不阻断其余角色）；刷新按钮手动强制刷新。
- 文案必须写清口径（`口径：ESI 钱包流水（近 7 天 / 近 30 天 / 全部 可切换）`），避免 WinUI 版"月收入"实际是全量流水这种歧义。

### 5.2 我的行情（右，392 宽）

- 数据源 = `MarketStarService.GetIds()`（与市场页共用 `StaredMarketInvType.json`）；为空时**不回落硬编码物品**，改为引导卡 + 「去市场页收藏」按钮（比 WinUI 的隐式回落到 PLEX/技能注入器/三钛更可预期）。
- 每张卡：物品图标（`ctl:AsyncImage.TypeId="{Binding TypeId}"`，控件内部自动走 `GameImageHelper` 取址 + 后台下载 + 解码 + `Freeze`）+ 名称 + 最低卖价 + 涨跌% + 销量 + 涨跌%（涨跌来自近 N 天历史，复用 `MarketOrderService.GetHistoryInternalAsync`）。
- **上限 6 张**（2 列 × 3 行）+ 「全部收藏 ›」跳市场页；超出部分只显示计数。
- 缓存：订单/历史按 `MarketOrderSettingService` 的 TTL 走磁盘缓存；卡片级降级（单项失败 → 该卡显示 `—`，不影响其它）。点击卡片 → `Navigation.NavigateToMarket(typeId)`（已存在，会前置主窗口）。
- 市场取"伏尔戈"（`MarketOrderService.DefaultMarketRegion`）并在卡底注明，避免与用户在市场页选的星域混淆（P3 可跟随市场页最近选择）。

---

## 6. 文件与改动清单

### 新增（P0）

| 文件 | 预估 | 职责 | 状态 |
|---|---|---|---|
| `Services/Home/HomeShortcutItem.cs` | 40 行 | 数据模型（`Id` / `Size` / `Param` / `Label`） | ✅ 已实现 |
| `Services/Home/HomeShortcutService.cs` | 180 行 | 文件读写、增删改排序、种子、恢复默认、`Changed` 事件、坏文件备份 | ✅ 已实现 |
| `Services/Home/HomeFeatureCatalog.cs` | 220 行 | 功能目录（L1 26 项 + L2 3 项）+ 按 id/分组/页面类型查询 + `NavigationExclusions` | ✅ 已实现 |
| `Services/Home/HomeFeatureLauncher.cs` | 70 行 | 统一的功能打开入口（设计稿原计划写在 VM 里，实现时抽成独立类供主窗口复用） | ✅ 已实现 |
| `ViewModels/Home/HomeShortcutTileViewModel.cs` | 80 行 | 磁贴视图模型（阶段 101 去掉 `IsAddSlot` 哨兵项与编辑态，主页磁贴与设置页行共用） | ✅ 已实现 |
| `ViewModels/Home/HomePageViewModel.cs` | 260 行 | 磁贴集合（阶段 101 起只读 + 点开）、品牌条、语言刷新（编辑相关方法已移除） | ✅ 已实现 |
| `Views/Pages/HomePage.xaml` + `.cs` | 360 + 120 行 | **替换占位页**（类名 / 命名空间 / 导航注册保持不变） | ✅ 已实现 |
| `Helpers/VisualTreeSearch.cs` | 40 行 | `IsInsideChildButton`（嵌套按钮 + 拖动共存；阶段 101 起主页不再使用，设置页行拖动用页内防御） | ✅ 已实现 |
| `Views/Windows/AddShortcutWindow.xaml(.cs)` | 180 + 140 行 | 添加功能窗口（原计划 P1，实为 P0 必需入口） | ✅ 已实现 |
| `ViewModels/Home/AddShortcutViewModel.cs` | 190 行 | 搜索 / 分组 / 已添加状态 | ✅ 已实现 |

### 新增（P1 / P2）

| 文件 | 阶段 | 职责 | 状态 |
|---|---|---|---|
| `Services/Home/HomeWalletSummaryService.cs` | P1 | 钱包汇总（余额 + 收支分类 + 区间 + 缓存） | ✅ 已实现 |
| `ViewModels/Home/HomeWalletCardViewModel.cs` | P1 | 钱包卡（四态 + 区间切换） | ✅ 已实现 |
| `ViewModels/Home/HomeMarketCardViewModel.cs` | P1 | 行情卡（异步图标 + 均价/销量 + 失败降级） | ✅ 已实现 |
| ~~`Services/Home/HomeFeatureStatusService.cs`~~ | P2→188 | 角标的运行态来源——阶段 188 随角标功能一并删除 | ❌ 已删除 |
| ~~`ViewModels/Home/HomeShortcutGroupViewModel.cs`~~ | P3→101 | 分段显示的一段——「磁贴按分组分段显示」随阶段 101 移除 | ❌ 已删除 |
| `Views/Windows/AboutWindow.xaml(.cs)` | P2 | 关于（版本 / 运行时 / 链接 / 开源致谢） | ✅ 已实现 |
| ~~`Views/Pages/Settings/HomeSettingPage.xaml(.cs)`~~ | P2→94 | 设置 → 主页（显示策略 / 列表管理）——阶段 94 删除 | ♻️ 阶段 99 重建（卡片显示开关）、阶段 100 加入快捷方式编辑、**阶段 101 起为唯一编辑入口** |

### 改动

| 文件 | 改动 |
|---|---|
| `Views/MainWindow.xaml.cs` | 导航项右键菜单（固定/取消固定/管理）、语言切换刷新菜单；+40 行左右 |
| `Views/Pages/SettingsPage.xaml.cs` | ~~`BuildCategories` 增加「主页」分类（P2）~~ → 阶段 94 已随"设置 → 主页"删除一并移除 |
| `Resources/Languages/zh-CN.xaml` / `en-US.xaml` | 新增约 45 个键（见 §8） |
| `TheGuideToTheNewEden.WPF.csproj` | 不需要新增引用（全部复用现有包） |
| `REFACTORING.md` | 实现后在 §8-61 销账，并新增阶段记录 |

---

## 7. 实施阶段与验收标准

| 阶段 | 内容 | 验收标准 |
|---|---|---|
| **P0 ✅**（阶段 92） | 目录 + 数据层 + 主页骨架（品牌条 + 快捷方式网格 + 编辑模式 + 导航右键固定 + 种子）+「添加功能」窗口 | ① 26 项导航功能全部可固定/移除；② 顺序、尺寸、上限落盘，重启保留；③ 编辑态拖动排序可用；④ 磁贴点击后左侧导航高亮跟随；⑤ 深浅色切换配色正确；⑥ 中英切换磁贴文字即时更新 |
| **P1 ✅**（追加一） | 钱包卡 + 行情卡 | ① 无角色时钱包卡显示引导；② 收藏为空时行情卡显示引导；③ 单项取数失败不影响其它卡；④ 刷新期间有加载指示、失败有右下角通知 |
| **P2 ✅**（追加二） | 角标 + 最近使用 + 设置→主页 + 关于窗口 + 日志/配置目录/手册入口 + 设置子页磁贴 | ① 预警/多开/ZKB/监控/收藏的运行态出现角标（**角标已于阶段 188 移除**）；② 设置子页磁贴能直达对应分类；③ 关于窗口版本号与标题栏一致；④ 手册按语言打开、缺失时明确提示 |
| **P3 ✅**（追加三） | 带参数磁贴（`param` + `label`）：指定物品市场 / 指定角色工作区 / 指定星系星图；磁贴分段显示 | ① 三个参数在点击后都被正确消费（探针断言）；② 老文件（无 `param`）向后兼容；③ 分段显示与平铺共用同一份磁贴模板 |

**实现差异（与设计稿不同、且是有意为之）**

| 设计稿 | 实现 | 原因 |
|---|---|---|
| 行情卡显示"最低卖价" | 显示**最近一日历史均价** | 实时订单是每物品一次无缓存 ESI 请求；主页只做速览，实时数据交给点击后的市场页（卡底已写明口径） |
| 收藏为空时回落 PLEX / 技能注入器 / 三钛 | 显示引导卡 + 「去市场页」 | 硬编码回落对用户不可预期 |
| ~~角标"不轮询、只订阅事件"~~ | ~~进入主页 + **可见期内 3 秒定时器**~~ | 角标整体于阶段 188 移除（用户不再需要），定时器与运行态数据源一并删除 |
| 数据卡"窗口 &lt; 1100 时上下堆叠" | 钱包卡内部用 `WrapPanel`（收入支出两行、两个构成列各自换行）；两张卡本身右栏固定 392 | 不加断点代码也能自适应，窄窗口下钱包卡仍可读 |
| 添加功能窗口"不设 Owner" | **设了 Owner** | 该规则针对 ToolWindow 的置顶按钮语义；本窗口没有图钉，设 Owner 才不会被主窗口盖住 |
| 磁贴分组"分段 / 文件夹" | 曾实现**分段**（按功能分组显示小标题） | "文件夹"语义复杂；**分段本身也随阶段 101 移除**（用户不再需要该功能，连同「⋯」按钮与 `ShowGrouped` 字段） |

---

## 8. 本地化键

**复用（零新增）**：26 个磁贴标题用 `Nav.*`；`Nav.Home`；`General_Refresh` / `General_Close` / `General_Cancel`；`SettingPage_*` 作为设置子页磁贴标题。

**新增（按用途分组，中英同步，注意 §8-33：改语言文件后必须查重键）**：

- 快捷方式：`Home.Quick.Title` `Home.Quick.Count` `Home.Quick.RestoreDefaults` `Home.Quick.RestoreConfirm` `Home.Quick.Empty` `Home.Quick.LimitReached` `Home.Quick.Pin` `Home.Quick.Unpin` `Home.Quick.Remove` `Home.Quick.ToWide` `Home.Quick.ToSmall` `Home.Quick.Manage`
  （阶段 101 删除：`Home.Quick.Add` `AddTip` `Edit` `Done` `EditHint` `MoveFirst`、`Home.Setting.Grouped`——编辑统一在设置页；
  阶段 188 删除：`Home.Quick.BadgeCount` `Home.Quick.BadgeRunning`——角标功能移除）
- 添加功能：`Home.Add.Title` `Home.Add.Search` `Home.Add.Add` `Home.Add.Added` `Home.Add.Summary` `Home.Add.Hint` `Home.Add.GroupAll`
- 品牌与状态：`Home.Status.Online` `Home.Status.Players` `Home.Status.GameTime` `Home.Status.NewVersion` `Home.Status.Community`
- 钱包：`Home.Wallet.Title` `Home.Wallet.Total` `Home.Wallet.Income` `Home.Wallet.Outcome` `Home.Wallet.IncomeBreakdown` `Home.Wallet.OutcomeBreakdown` `Home.Wallet.NoCharacter` `Home.Wallet.PartialFailed` `Home.Wallet.Range` `Home.Wallet.Range30` `Home.Wallet.RangeAll` `Home.Wallet.Note`
- 行情：`Home.Market.Title` `Home.Market.AllFavorites` `Home.Market.Price` `Home.Market.Volume` `Home.Market.NoFavorite` `Home.Market.Note`
- 动作 & 设置页：`Home.Action.OpenLog` `Home.Action.OpenConfigFolder` `Home.Action.CheckUpdate` `Home.Action.About` `Home.Action.Manual`（`SettingPage_Home` / `SettingPage_Home_Desc` 随"设置 → 主页"删除已移除，阶段 94）

功能描述文案（添加功能窗口里的副标题）建议**多数组件复用** `SettingPage_*_Desc`，确实没有的再补 `Home.Feature.<id>Desc`（避免为同一功能写两套描述）。

---

## 9. 风险与待验证项

| # | 项 | 说明与预案 |
|---|---|---|
| 1 | `NavigationView.Navigate(Type)` 对**分组子项**是否自动展开父级并高亮 | 项目已在用（`Navigation.NavigateToMarket` → 商业/市场）。P0 第一步用探针/实机确认；若不展开，在 `Navigation` 里补"按 `TargetPageType` 递归查 `MenuItems` → 设 `SelectedItem`"。 |
| 2 | 拖拽排序在 WPF 里要自实现 | 用 `DragDrop.DoDragDrop` + 插入指示器 + `Drop` 时改集合；**拖动过程中不要重建容器**（否则拖动视觉中断）。若一轮做不完，先只提供右键「移到最前」+ 添加功能窗口里的排序（功能不缺失，只是弱一点）。 |
| 3 | 工具类磁贴不要走 `Navigate` | `MainWindow.OnNavigationViewNavigating` 会拦截并开窗（可行但绕）；磁贴直接 `ToolWindowHost.Show(pageType, titleKey)`，语义清晰。 |
| 4 | 磁贴标题/描述必须在语言切换后刷新 | 照 `SettingsCategory.Refresh()` 的做法在 VM 里发 `PropertyChanged`（§9-18 末句：VM 里缓存本地化字符串会切语言不更新）。 |
| 5 | 无角色 / 未授权 / ESI 失败的形态 | 每个卡片独立降级 + 引导文案；不要把 0 当作正常值展示。 |
| 6 | 钱包"近 30 天 / 全部"的 ESI 成本 | 默认只取首页 50 条；高成本区间必须显式切换 + 等待遮罩 + 提示，默认不选。 |
| 7 | 首页自身的可见性 | 「主页」不参与固定；快捷方式为空时显示推荐种子（引导文案 + ＋ 磁贴），不留纯空白页。 |
| 8 | 主页页面缓存 | `NavigationCacheMode="Required"`，编辑态要在 `Unloaded` 时退出，避免切回时仍在编辑。 |
| 9 | 添加功能窗口与主页的双向同步 | 窗口不关闭时也能看到主页变化 → 统一走 `HomeShortcutService.Changed`。 |
| 10 | 图标名核验 | 本方案用到的图标已核验（见 §10）；**`Factory24` / `Slider24` / `Store24` / `Bank24` 在 `SymbolRegular` 里不存在**，不要使用。 |

---

## 10. 附录：图标核验方法与结论

`ui:SymbolIcon` 的 `SymbolRegular` 共 **9235** 个成员，字体（`fluentsystemicons-regular.ttf`）**内嵌在 `Wpf.Ui.dll` 的 `Wpf.Ui.g.resources` 里**，因此图标名只需在枚举中存在即可显示（不依赖系统装没装 Segoe Fluent Icons）。

核验方式（可复用）：读 `%USERPROFILE%\.nuget\packages\wpf-ui\4.3.0\lib\net462\Wpf.Ui.dll` → 反射 `Wpf.Ui.Controls.SymbolRegular` → 得到 `名称 → 码位`；如需确认字形是否存在，再从 `Wpf.Ui.g.resources` 里导出 `resources/fonts/fluentsystemicons-regular.ttf` 做一次渲染比对。

本方案用到的图标（**全部存在且有字形**）：
`Person24` `ArrowTrending24` `Receipt24` `ShoppingBag24` `Calculator24` `SquareMultiple24` `Alert24` `Comment24` `CommentMultiple24` `Money24` `People24` `DocumentText24` `DocumentOnePage24` `DocumentBulletList24` `Translate24` `LocalLanguage24` `DataSunburst24` `Target24` `Link24` `Map24` `Scan24` `Database24` `Keyboard24` `Gauge24` `Wrench24` `Window24` `Toolbox24` `Settings24` `Home24` `Add24` `Delete24` `Edit24` `Pin24` `PinOff24` `Star24` `StarEmphasis24` `Clock24` `Pulse24` `ArrowSync24` `Search24` `MoreHorizontal24` `ChevronDown24` `ChevronRight24` `Dismiss24` `Checkmark24` `Info24` `QuestionCircle24` `Warning24` `ErrorCircle24` `CloudArrowDown24` `ArrowUpload24` `ArrowDownload24` `Folder24` `Open24` `History24` `Wallet24` `Mail24` `Building24` `Shield24` `Rocket24` `Globe24` `Trophy24` `Flash24` `Timer24` `Calendar24` `Tag24` `BookOpen24` `BookQuestionMark24` `Bug24` `Apps24` `GridDots24` `CoinMultiple24` `Beaker24` `Cube24` `Layer24` `PuzzlePiece24` `Resize24` `PanelLeft24` `Play24` `RecordStop24` `Copy24` `ArrowReset24` `SpeakerMute24` `AlertOff24` `VehicleShip24`

---

## 11. 与 REFACTORING.md 衔接

实现后需要在该文档里：

1. §8 第 61 条「剩余 1 个：HomePage」→ **销账**。
2. 新增阶段记录（按既有格式：目标 / 新增代码 / 与 WinUI 的有意差异 / 实机核验 / 未做后续），并在 §8 补一条口径说明：**主页钱包统计默认只取每个角色最近 50 条流水**（WinUI 的"全量分页拉取"不作为 WPF 基准）。
3. §10「主要文件清单」补 `### 主页模块（阶段 NN）` 一节，列出上表文件。
4. §9 踩坑备忘补两条（若实测命中）：导航分组子项的程序化高亮行为、`ItemsControl` 拖拽排序与容器重建的配合。
