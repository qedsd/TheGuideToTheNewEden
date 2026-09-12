# TheGuideToTheNewEden.WPF 重构与更新记录

> 本文档记录 WinUI 3 → WPF 迁移过程中的**全部重构决策与每一次更新**，包含背景、方案、变更明细、问题根因、验证结果与已知限制。
> 起点：对 WinUI 3 项目做"重构为 WPF"的可行性评估；终点：WPF 版已具备导航框架、完整设置模块、角色功能（授权 + 数据层 + 7 个子页）。

---

## 目录

1. [背景与目标](#1-背景与目标)
2. [总体架构决策](#2-总体架构决策)
3. [依赖与技术栈](#3-依赖与技术栈)
4. [更新日志（按阶段）](#4-更新日志按阶段)
5. [角色功能分层设计](#5-角色功能分层设计)
6. [问题根因与修复清单](#6-问题根因与修复清单)
7. [验证记录](#7-验证记录)
8. [已知限制与待办](#8-已知限制与待办)
9. [WPF / WPF-UI 踩坑备忘](#9-wpf--wpf-ui-踩坑备忘)
10. [主要文件清单](#10-主要文件清单)

---

## 1. 背景与目标

- 原项目 `TheGuideToTheNewEden.WinUI`：WinUI 3（net9.0-windows10.0.19041.0），依赖 `TheGuideToTheNewEden.Core`(netstandard2.1)、`PreviewIPC`、`ZKB.NET`，界面重度使用 Syncfusion（WinUI 版）与 DevWinUI。
- 目标：新建 `TheGuideToTheNewEden.WPF`，**不复用**已有 WPF 项目、**不引入 Syncfusion**，用 **WPF-UI** 承载默认样式；先搭导航框架，再逐步迁移设置与角色功能。
- 约束与取向：
  - 页面状态必须保留（工具页在后台运行，切走再切回不受影响）。
  - 保持"左侧菜单 + 右侧内容"的整体形态。
  - 设置页参考 Windows 11 系统设置的观感。

---

## 2. 总体架构决策

| 决策点 | 结论 | 理由 |
|---|---|---|
| UI 框架 | .NET 10 + WPF + **WPF-UI 4.3.0** | 默认样式/主题/导航控件开箱即用，避免自写控件模板 |
| 表格控件 | **WPF-UI `DataGrid`**（不用 Syncfusion） | 用户明确不再使用 Syncfusion；行为可控 |
| 图表控件 | **LiveCharts2**（`LiveChartsCore.SkiaSharpView.WPF`，不用 Syncfusion 图表） | 市场历史价格/销量图；开源、SkiaSharp 渲染、支持日期轴与缩放 |
| 工具窗口 | 统一外壳 **`Views/Windows/ToolWindow`**（`ui:FluentWindow` + `ui:TitleBar`） | 标题栏样式统一（左上角 logo + 窗口名称）；标题按钮组合、置顶按钮、是否显示在任务栏均可配置；内容支持 Page/UserControl |
| 页面承载 | 主内容用 `NavigationView` 内置 Frame；**壳页内的子页统一用 `Frame` 承载** | WPF 规定 `Page` 只能由 `Window`/`Frame` 承载（见 §9） |
| 页面状态保留 | 页面实例常驻（缓存字典 / Tab 标签常驻） | 满足"后台运行、切回不受影响" |
| 依赖项目 | WPF 直接引用 **Core**（netstandard2.1，UI 无关） | 复用 ESI/DB/模型，不重复实现 |
| 运行时资源 | `Resources/Configs`、`Resources/Database` 以**链接方式**引入，不复制进仓库 | 资源约 100MB，避免仓库重复存储 |
| 设置存储 | 与 WinUI **共用** `Configs/settings.json` | 现有配置直接延续 |
| 窗口位置 | Win32 `Get/SetWindowPlacement`，**物理像素** | 免疫跨不同 DPI 显示器的坐标换算错误 |
| 托盘与通知 | **H.NotifyIcon.Wpf**（不再用 WPF-UI.Tray / Microsoft.Toolkit.Uwp.Notifications） | 与 WinUI 版一致（WinUI 用 H.NotifyIcon.WinUI）、维护活跃、无需 AUMID 注册 |
| 邮件正文 | **HtmlRenderer.WPF**（`HtmlPanel`），不使用 WebView2 | 邮件是简单 HTML；零浏览器运行时依赖 |

---

## 3. 依赖与技术栈

### 最终 csproj 要点（`TheGuideToTheNewEden.WPF/TheGuideToTheNewEden.WPF.csproj`）

```
TargetFramework      net10.0-windows10.0.19041   （阶段 25 起带平台版本：LiveCharts 的 SkiaSharp 资产要求 TPV ≥ 10.0.19041）
UseWPF               true
ApplicationIcon      Assets\app.ico
ApplicationManifest  app.manifest (PerMonitorV2 DPI)
EnableWindowsTargeting true
```

包引用：

| 包 | 版本 | 用途 |
|---|---|---|
| WPF-UI | 4.3.0 | 默认样式、主题、NavigationView、DataGrid、NumberBox、Snackbar 等 |
| H.NotifyIcon.Wpf | 2.4.1 | 托盘图标 + 系统通知（net10 目标） |
| Newtonsoft.Json | 13.0.4 | 设置/令牌/缓存序列化 |
| HtmlRenderer.WPF | 1.6.1 | 邮件正文 HTML 渲染（替代 WebView2） |
| LiveChartsCore.SkiaSharpView.WPF | 2.0.5 | 市场历史图表（替代 Syncfusion 图表）；传递带入 SkiaSharp 3.119.0 |

项目引用：`TheGuideToTheNewEden.Core`（其再传递引用 `ZKB.NET`）。

链接的运行时资源（`Link` + `CopyToOutputDirectory`）：
`Resources/Configs/**`、`Resources/Database/**`、`Resources/default.mp3`，并把 `log4net.config` 单独放到输出根（`Core.Log.Init()` 从根目录读取）。

### 环境变更

- 安装 **.NET 10 SDK 10.0.401**（winget `Microsoft.DotNet.SDK.10`），运行时 `Microsoft.WindowsDesktop.App 10.0.12`；实测无需重启。
- TFM 演进：`net9.0-windows` → `net9.0-windows10.0.19041.0`（为 Toast 临时启用）→ 最终 **`net10.0-windows`**（去掉 Windows SDK 后缀）。

---

## 4. 更新日志（按阶段）

### 阶段 0：可行性评估
- 盘点 WinUI 设置与角色功能规模、依赖、可移植性；结论：**整体重写 UI 层、保留业务层**可行。
- 关键发现：Core 为 netstandard2.1 **UI 无关**可直接复用；WinUI 的**设置持久化/主题/语言/背景**与 `BaseViewModel` 是 WinUI 专有；角色功能**没有数据服务层**（ESI 调用散落在各页代码后置）。

### 阶段 1：WPF 项目骨架与导航框架
- 新建项目 `TheGuideToTheNewEden.WPF`，加入解决方案。
- **导航框架**：`FluentWindow` + `ui:NavigationView`（左菜单）+ 页面实例池；目标页用 `TargetPageType` + `NavigationCacheMode="Required"` 实现**页面常驻缓存**。
- 25 个功能页以 `PlaceholderPage` 基类占位。
- **设置页首版**：主题色（8 色块）、深色模式、语言、关闭到托盘。
- 三个服务：`ThemeService`（浅/深两态，后加"保留用户主题色"修复）、`LanguageService`（运行时替换语言字典）、`SettingsService`（持久化）。

### 阶段 2：标题栏与窗口行为
- 标题栏左上角显示应用图标：`ui:TitleBar.Icon` + `ui:ImageIcon`；窗口 `Icon` 用于任务栏/Alt-Tab。（仓库无 PNG，由 `app.ico` 生成 `logo_32/128.png`。）
- **汉堡按钮移到 logo 左侧**：改为 WPF-UI 官方布局——`NavigationView` 铺满整窗、`TitleBar` 叠加其上并设置 `TitleBar` 绑定，让 `NavigationView` 自带的汉堡按钮落在标题栏左侧预留区。
- **去除导航菜单首末两条分隔线**：`IsTopSeparatorVisible=False`、`IsFooterSeparatorVisible=False`（WPF-UI 默认开启）。
- **窗口位置记忆**（见 §6-5，含多轮修正）。
- **`FrameMargin` 被覆盖问题**：WPF-UI 在绑定 `TitleBar` 后会把 `FrameMargin` 覆盖为 `(0,50,0,0)`，XAML 写死无效——改为在 `Loaded` 与 `TitleBar` 属性变更回调中重新应用 `NavigationTopInset`。

### 阶段 3：设置界面改为 Win11 风格
- 新增设置行控件 **`SettingCard`**（图标 + 标题 + 描述 + 右侧操作控件）与样式字典 `SettingCardStyles.xaml`：分组卡片、分隔线、小节标题、整行可点样式、色板弹窗样式。
- 设置页改为**分组卡片**布局；主题色改为 **色板按钮 + Flyout 色板**（选中项带对勾）。
- 用 `ToggleButton.IsChecked` 与 `Popup.IsOpen` **双向绑定**替代手写开关逻辑，消除"打开即被同一事件关闭"的竞态。

### 阶段 4：设置模块全量迁移
- **Core 引用**与运行时资源链接（见 §3）。
- **设置存储统一**：移植 WinUI 的键值字典存储，共用 `Configs/settings.json`；旧 `settings.wpf.json` 首次运行自动导入；WPF 专有键（主题色、托盘、窗口位置）并入同一文件。
- **`CoreInitializer`**：灌入 DB 路径、`NeedLocalization`、游戏服务器、玩家 API、**ESI 凭据**（`ESILicense.txt`）与 `Config.Scopes`，并 `Log.Init()` + `Config.InitDb()`。
- **移植 10 个纯 .NET 设置服务**：`GameServer` / `LocalDb` / `DBLocalization` / `PlayerStatus` / `AutoUpdate` / `GameLogs` / `GameLogInfo` / `MarketOrder` / `ESIScope` / `ZKB`。
- **设置壳页 + 9 个子页**：一般、游戏日志、市场、玩家建筑、权限选择、Zkillboard、按键列表、测试、软件更新。
- **本地化键合并**：从 WinUI 语言字典抽取设置相关键（`Setting*`、`SettingPage_*`、各设置页前缀、`General_*`）并转换 `x:String`→`sys:String` 合并；跳过重复键。

### 阶段 5：托盘与通知的依赖替换（方案 C）
- 移出 `WPF-UI.Tray` 与 `Microsoft.Toolkit.Uwp.Notifications`（后者停更、需 AUMID/开始菜单注册）。
- 引入 **`H.NotifyIcon.Wpf`**，`tray:NotifyIcon` → `tb:TaskbarIcon`；新增 `NotificationService`（托盘实例持有 + `ShowNotification`）。
- **踩坑记录（重要）**：`H.NotifyIcon.Wpf 2.4.1` 仅提供 `net462` 与 `net10.0` 目标，在 .NET 9 下会回退到 .NET Framework（`NU1701`）→ 当时固定 **2.3.1**；升级到 .NET 10 后再升回 **2.4.1**。

### 阶段 6：升级 .NET 10
- 安装 .NET 10 SDK；WPF 项目 TFM → `net10.0-windows`；`H.NotifyIcon.Wpf` → 2.4.1（其 `net10.0-windows7.0` 目标正好匹配）。
- 验证：运行时报告 ` .NET 10.0.12`；托盘图标创建成功、通知调用无异常；输出资源照常拷贝。

### 阶段 7：角色功能迁移
分四步推进（授权优先、首期三页、加缓存）：

- **阶段 A：授权基础设施**
  - `CoreInitializer` 灌入 ESI 凭据（`Configs/ESILicense.txt` → ClientId/Callback/Secret）+ 权限范围。
  - `Helpers/AuthHelper`：统一协议名（修正 WinUI 的 `neweden2/neweden3` 混用）、等待浏览器回调、**用查询串稳健解析 `code`**（替换 WinUI 的 `Split('=','&')[1]`）。
  - `Helpers/SerenityAuthHelper`：国服授权地址（独立 client_id、过滤不支持权限）。
  - `Services/Characters/CharacterStore`：令牌按服务器落盘（`Auth.json` / `Auth_Serenity.json`），增删/排序（**修正 WinUI 的 `SetOrder` 不持久化**）、集中令牌刷新 `EnsureTokenValidAsync`。
  - `Services/Characters/CharacterAuthService`：打开授权页 → 等回调 → 换码 → 落盘；国服粘贴 code。
  - App 接入 Core `SingleInstanceHelper`（授权回调把 URL 交回主实例）。
- **阶段 B：数据服务层 + 缓存**（WinUI 原本没有这层，是本次重构核心）
  - `CharacterContext`（类型安全上下文，替代 `object[] { api, characterData }`）。
  - `PagedResult<T>`（统一分页）。
  - `CharacterCache`：内存 + 磁盘（`Configs/CharacterCache/<角色ID>/<键>.json`），带 TTL；刷新可绕过。
  - `CharacterOverviewService` / `CharacterSkillService` / `CharacterWalletService`：服务不泄漏 ESI 类型，统一映射 DTO；每个接口独立容错。
- **阶段 C：壳页 + 角色卡片页**
  - `CharactersShellPage`：`TabControl`；首标签固定"全部角色"，每个角色一个**可关闭**标签；标签内容用 `Frame` 托管。
  - `CharacterCardsPage`：汇总卡（角色数/总 SP/总资产/忠诚点）+ 自适应卡片网格 + 无数据时的引导（登录主按钮）+ 添加/移除。
  - `CharactersViewModel` / `CharacterCardViewModel`：卡片数据、头像（失败回落首字母）、ISK/剩余时间格式化。
- **阶段 D：工作区 + 首期三子页**
  - `CharacterWorkspacePage`：左侧信息栏（身份/资产/技能队列）+ 右侧子页标签。
  - `OverviewPage`（军团/联盟、舰船/星系、在线/最近登录、技能队列）、`SkillPage`（技能组折叠 + 搜索）、`WalletPage`（4 页签 + `DataGrid` + 分页）。
  - `PagerControl`：通用分页控件（页码 + 上一页/下一页）。

### 阶段 8：角色功能补全（克隆/邮件/合同/工业 + 邮件详情窗口）
- 引入 **`HtmlRenderer.WPF`**（方案 A），邮件正文用 `HtmlPanel` 渲染；代码中剥离 `<img>` 以离线/隐私友好。
- 新增服务：`CharacterCloneService`、`CharacterIndustryService`、`CharacterMailService`、`CharacterContractService`。
- 新增页面：`ClonePage`、`MailPage`、`MailDetailWindow`、`ContractPage`、`IndustryPage`；工作区 7 个子页全部为真实页面。
- 本地化键增至 **472**（新增 126，无重复）。

### 阶段 9：未核验页面的实机核验与缺陷修复
- 对 **克隆 / 邮件（含详情窗）/ 合同 / 工业** 做逐页实机截图核验（共 5 张：4 个页面 + 独立邮件详情窗口）。
- 核验中发现并修复 **4 个缺陷**（3 个在 Core、1 个在 WPF 页面），逐条见 §6 的 #16~#19。
- 修复前最严重的问题：**切换到“克隆”标签后进程无任何日志地消失**——定位为 Core 中的无限递归导致栈溢出（退出码 `0xC00000FD`）。
- 修复后 4 个页面均正常渲染真实数据，名称（地点/发件人）解析正常。
- 核验方式：实机点击逐页截图目检（截图与抓图脚本属一次性产物，核对后已清理，仓库不再保留）。

### 阶段 10：结构 ID 解析约定落地（文档 + 代码）
- 明确并落实约定：**structure id 请走 `StructureService`**（`IDNameService` 的 ID 是 `int`，结构 ID 约 1e12 会被静默截断）。
- `Core/Services/IDNameService.cs` 补 XML 注释（类级 + `GetByIds(List<long>)` / `GetByIdsAsync(List<long>)` 两个重载），说明截断风险与正确去向。
- 新增 `Services/Characters/LocationNameResolver.cs`：按 `int.MaxValue` 分流 —— 范围内走 `IDNameService`，超出（结构）走 `StructureService`；查不到的名称不写入结果，由调用方回退显示原始 ID。
- 把 **合同 `Start/EndLocationId`**、**工业 `FacilityId`**、**克隆位置**、**钱包 `ClientId`** 四处可能拿到结构/大 ID 的解析改走该分流器（此前会被截断成**错误但看起来正常**的名称）。
- 见 §6 的 #20 与文末「结构（structure）ID 解析约定」。

### 阶段 11：角色卡片头像的线程亲缘性修复
- 现象：角色卡片头像**始终不显示**（占位首字母）；调试时抛 `InvalidOperationException`「调用线程无法访问此对象」于 `BitmapImage.IsDownloading`。
- 根因：`BitmapImage` 是 `Freezable`，**冻结前有线程亲缘性**。原代码在 UI 线程 `new BitmapImage()` + `EndInit()`，却把 `Freeze()` 丢进 `Task.Run` → 工作线程访问 UI 线程拥有的 WIC 状态 → 抛异常 → 被 `catch` 吞掉后头像恒为空。
- 修复：改为 `HttpClient.GetByteArrayAsync(...).ConfigureAwait(false)` 先取字节，再**在同一线程池线程**上用 `MemoryStream` 解码并 `Freeze()`（创建与冻结同线程即合法），冻结后跨线程交给绑定。这样也彻底绕开了 WIC 的 URI 下载路径。副作用：`HttpClient` 不缓存，每次刷新会重新下载头像（128px PNG 约 6 KB）；若日后觉得浪费，可把字节落到 `Configs/CharacterCache/<charId>/avatar.png`。
- 验证：用等价的独立探针程序验证「线程池线程上解码 + 冻结」模式，输出 `frozen=True px=128x128`（下载线程 = 解码线程），不再抛异常。

### 阶段 12：角色功能 UI 全量照搬 WinUI3
- 目标：此前 WPF 角色页只做了"能跑通"的精简版，本次把**卡片页 / 工作区 / 7 个子页全部按 WinUI3 版的信息项与布局重做**（Syncfusion 表格按约定换成标准 `DataGrid`）。
- **卡片页**：`200×305` 竖版卡片（对齐 WinUI）——圆形头像、加粗角色名、`SP` 行、`ISK` 行，底部技能队列块（`技能队列` 标签 + 剩余百分比 + 细进度条 + 剩余时间 + `已暂停` 红字 + `未完成/总数`）；左上角在线/离线指示（离线显示"4mo 19.7d"这类时长），右上角 `···` 菜单（移除）。数据侧新增 `LastLogout` 与队列剩余百分比计算。
- **工作区左栏**：身份/资产卡（出生、SP(未分配)、LP、安全等级、个人钱包、公司钱包、势力=军团名+`[联盟代号]`）+ 技能队列卡 + **ZKB 战绩卡**（危险系数/抱团概率双色条、击杀/损失舰船与 ISK、ZKB 按钮）+ 左栏刷新与子页刷新**两个**刷新按钮；子页刷新走新的 `ICharacterSubPage` 接口，不重建页面实例。
- **子页**（逐页对齐 WinUI，表格列名/列序/数值格式/红绿语义一致）：
  - 总览：军团卡、联盟卡、舰船+地点卡（舰船图 + `空间站 (安全等级 星系)` + `舰船名 (船型)`）、在线卡（最近上线时长 + 上线次数）、技能队列卡（四态图标 + 技能名/等级 + 剩余时间 + 起止时间）。
  - 技能：技能点汇总（总/未分配）+ 技能队列 + 带搜索框的按组 `Expander` 技能列表。
  - 克隆：基地/上次变更/克隆数量/上次远克 + 当前激活克隆（含脑插名称与描述）+ 跳跃克隆（`Expander` + 脑插）。
  - 钱包：四个页签（个人-日志/个人-交易/军团-日志/军团-交易）+ 与 WinUI 同序的列 + 金额红绿语义 + `PagerControl` 分页 + 军团 division 切换。
  - 邮件：左侧标签列表（含未读数徽标与"全部邮件"）+ 右侧邮件列表（未读点/主题/发件人/本地日期）+ 详情窗口（头像、发件人、日期、收件人、标签、`HtmlPanel` 正文），打开即标记已读并回写列表。
  - 合同：个人/军团两页签 + 16 列（与 WinUI 同序）+ 分页。
  - 工业：10 列（蓝图/产品/状态/流程/成功率/开始时间/项目周期/完成时间/项目费用/位置）。
- 新增本地化键：`EntityStatistPage_DangerRatio`、`EntityStatistPage_GangRatio`、`StatistMonthPage_ItemDestroyed/IskDestroyed/ItemLost/IskLost`、`SkillPage_SkillIdsCount`、`SkillPage_TrainedSkillLevel`、`SkillPage_SkillpointsInSkill`，并补上此前设置页遗漏的 `GeneralSettingPage_NeedLocalization`（取值一律沿用 WinUI 语言文件的原文）。
- **实机核验**（`dotnet build` 0 错误 + 实机逐页截图）：卡片页、工作区左栏（含 ZKB）、总览、技能、克隆、钱包、邮件、邮件详情窗口、合同、工业 **全部通过**，无崩溃。
- 本轮踩到的两个坑（详见 §9）：**并发构建导致 `obj` 里的 `.baml` 陈旧**（界面一直跑旧 XAML，排查了较久），以及 **WPF-UI 隐式样式下的 `ProgressBar` 触发进程级栈溢出**（已改用自绘 `RatioBar`）。

### 阶段 13：角色 UI 的主题跟随修复（深浅色切换后颜色不更新）
- 现象：切换深/浅色后，角色界面**部分文字与颜色不跟着变**（网格文字尤其明显）。
- 根因共四类（逐条见 §6 #24）：
  1. **钱包 / 合同 / 工业用的是标准 `DataGrid`**（WPF-UI 只给 `Wpf.Ui.Controls.DataGrid` 提供主题样式）→ 取系统主题色，与应用主题无关；
  2. `SkillPageViewModel` 用 `Application.Current.TryFindResource("TextFillColorPrimaryBrush")` **在加载时抓了一个 Brush 实例**缓存进条目 → 主题切换后仍是旧色；
  3. `WalletPageViewModel` 暴露的是 `static readonly Brushes.SeaGreen/OrangeRed` → 恒定色；
  4. 约 20 处**字面量颜色**（`#4CD1AC`、`OrangeRed`、`MediumSeaGreen`、`White`、`#CC3CB371`）分布在卡片页/工作区/技能/总览/邮件。
- 修复：
  - 三个表格页统一改为 `ui:DataGrid`（与设置页既有的 `KeyboardListPage` 写法一致），主题样式自动接管表头/单元格文字与背景；
  - 所有语义色改为 WPF-UI 主题资源：绿 → `SystemFillColorSuccessBrush`，红 → `SystemFillColorCriticalBrush`，橙/暂停 → `SystemFillColorCautionBrush`，徽标文字 → `TextOnAccentFillColorPrimaryBrush`；
  - 技能队列状态图标改为**样式 + `DataTrigger`**（`IsFinished`/`IsRunning`/`IsPause`）直接设 `DynamicResource`，删除 VM 里缓存的 `StateBrush`（连带删掉 `ResolveBrush`/`Freeze` 与三个静态 Brush）；
  - 钱包金额改为 VM 暴露 `IsAmountNegative` / `IsBuy` 布尔 + `DataTrigger` 设主题色，删除 `WalletColors`、`AmountBrush`、`TotalPriceBrush`；
  - `RatioBar` 的 `BarBrush` 默认值由 `Brushes.MediumSeaGreen` 改为 `null`（所有调用点都显式给主题色，避免留一个不跟随主题的兜底色）。
- 实机核验：分别在 **深色** 与 **浅色** 下重启程序，逐页确认（卡片页、工作区含 ZKB、技能、钱包表格）配色均正确、文字可读；角色 UI 内已无字面量颜色（只剩 `Background="Transparent"`）。
- 说明：颜色一律走 `DynamicResource` / `DataTrigger`，因此运行时切换主题会即时重解析；本次核验方式是"改设置后重启"，因为测试环境下驱动设置页点击不稳定（点击落点漂移）。

### 阶段 14：国服（Serenity）添加角色打通
- 现象：**"添加角色"实际上无法完成国服授权**。原因不止一处：
  1. 国服分支只弹一个"请粘贴 code"的输入框，**从不打开授权页**（用户根本拿不到 code），也没有登出步骤与失败提示；
  2. **运行时切换服务器不生效**：`ESIService.Current` 是单例，SSO/客户端在构造时就把 `Config.ClientId/ClientSecret/ESICallback` 与 `DefaultGameServer` 固化了，而设置页切服只改了 `Config.DefaultGameServer` 并提示"需要重启" → 切到国服后仍用国际服客户端 ID 去换码，必然失败；
  3. 粘贴**整条回调网址**（WinUI 的提示就是这么写的）时解析不出 code（原实现把整串直接丢给换码接口）。
- 修复：
  - Core 新增 `ESIService.Reset()`（丢弃单例，下次访问按当前 Config 重建）；
  - `CoreInitializer` 新增 `SwitchGameServer(server)`：写设置 → 重灌 ESI 凭据 → `ESIService.Reset()` → `CharacterStore.Init()`（换成该国服的 `Auth.json`/`Auth_Serenity.json`）→ 触发 `GameServerChanged` 事件；设置页改为**立即生效**并提示"已切换"（不再要求重启）；
  - `CharactersShellPage` 监听 `GameServerChanged`：关闭另一服务器的角色标签并重载卡片页；
  - `CharacterAuthService` 新增 `OpenSerenityLogoffPage()`（步骤 0）与 `ExtractAuthorizationCode()`（**整条网址或纯 code 都能识别**，纯 code 场景 WinUI 的 `Split('=','&')[1]` 会取错）；
  - 新增 `Views/Windows/SerenityAuthWindow.xaml(.cs)`：按 WinUI `AddSerenityAuthDialog` 做成四步向导（登出 → 打开 ali-esi 授权页 → 粘贴网址/code → 校验），带"正在校验…/成功/失败+服务端错误详情"，卡片页的"添加角色"与空状态"登录 EVE 账号"改走该向导。
- 实机核验（应用设为国服启动）：
  - 卡片页读到**国服**的角色文件（无角色 → 空状态），证明切服后凭据与角色列表都换了；
  - 点"登录 EVE 账号"→ 弹出**国服授权向导**（四个步骤、按钮与提示齐全）；
  - 粘贴一条完整回调网址后点"校验授权"→ 请求确实打到国服 SSO 并返回服务端错误（`An error occurred trying to get keys for access token verification.`），失败态与错误详情正常显示；若把网址解析当纯 code 处理，这里只会显示本地的"请粘贴…"提示，因此该结果同时验证了 URL→code 的解析。
- 未覆盖：真实的国服账号完整授权（需要真实 NetEase 授权码）；设置页 ComboBox 触发的那次"运行时切服"未在本环境点到（点击落点不稳定），改以"以国服启动"覆盖了同一套凭据/角色文件切换逻辑。

### 阶段 15：卡片内文字在深色下变黑（继承色被自定义样式截断）
- 现象：深色主题下，角色卡片里**"技能队列"、`0%`、`2/2` 等小字发黑**（浅色主题下正常）。
- 根因：**显式 `Style` 会整体替换隐式样式，连同其 `Foreground` 一起丢掉**。卡片用的是
  `Style="{StaticResource SettingsRowButton}"`（一个带自定义 `ControlTemplate` 的 Button 样式），
  它没有设置 `Foreground`，于是 `Button.Foreground` 退回控件默认值（`SystemColors.ControlTextBrush`，系统色）；
  卡片内**未显式写 Foreground 的 TextBlock 会继承这个系统色** → 深色主题下就是黑字。
  （WinUI 版没有这个问题：那里的文字都带资源色。）
- 修复：
  1. `SettingsRowButton` 补 `Foreground="{DynamicResource TextFillColorPrimaryBrush}"`（治本：所有用该样式的行——角色卡片、设置分类行——内含文字都会正常继承主题色）；
  2. 卡片页与工作区的"技能队列"信息块逐项补显式主题色：标签/图标用 `TextFillColorSecondaryBrush`，"0%"、剩余时间、`2/2` 等数据用 `TextFillColorPrimaryBrush`，"已暂停"仍用 `SystemFillColorCautionBrush`。
- 实机核验：深色下启动、进角色页放大卡片确认——"技能队列"与 `0%`、`2/2`、时钟/队列字形均为主题浅色，不再发黑。
- 复用结论（已记入 §9 第 19 条）：**给控件套自带 `Template` 的自定义样式时，必须显式补齐 `Foreground`**，否则其内部未指定颜色的文字会退回系统色。

### 阶段 16：表格样式被打回 WPF 原生（自定义样式顶掉了 Fluent 模板）
- 现象：钱包 / 合同 / 工业三页的表格**又变回 WPF 原生外观**（灰底表头、系统边框、不跟主题），与设置页里同为 `ui:DataGrid` 的按键列表不一致。
- 根因：这三页各自定义了一个本地的 `WalletGridStyle / ContractGridStyle / IndustryGridStyle`（`TargetType="DataGrid"`），并通过 `Style="{StaticResource …}"` 应用。**显式样式会整体替换 WPF-UI 的隐式 DataGrid 样式**，于是 Fluent 的列头/行选中模板全被丢掉，只剩原生模板——元素名是 `ui:DataGrid` 也没用（同名于阶段 15 的 `SettingsRowButton` 问题：显式样式会顶掉隐式样式）。
- 修复：三个本地样式改为 `TargetType="{x:Type ui:DataGrid}"` 且 `BasedOn="{StaticResource {x:Type ui:DataGrid}}"`，保留原本的"透明底、只留横向网格线、交替行色"等设置；同时给三个 `*CellStyle` 补 `Foreground="{DynamicResource TextFillColorPrimaryBrush}"`，单元格文字不再依赖模板内部继承。
  - 注：`BasedOn` 的 `TargetType` 必须与基样式一致（`ui:DataGrid`），写 `DataGrid` 会因为"基类型不是派生类型"而报错。
- 实机核验（深色）：钱包页表头变成主题化的扁平表头、行文字为主题色、无原生 3D 边框，与设置页表格观感一致；应用无异常。

### 阶段 17：总览页内容占满内容区宽度
- 现象：总览页内容只靠左、右侧大片空白。
- 根因：外层 `StackPanel` 写了 `MaxWidth="820"` + `HorizontalAlignment="Left"`（照搬 WinUI 时的遗留限制）。
- 修复：去掉宽度上限与左对齐，卡片随内容区拉伸铺满整行（实机确认）。

### 阶段 18：技能 / 克隆页首次显示时文字发黑（继承色首帧未解析）
- 现象：技能、克隆页**刚点开时**部分文字（技能名、技能组名、脑插名等）是黑色，切走再切回该 TabItem 后才恢复主题白色。
- 根因：这些文字**没有显式 Foreground**，靠继承链取色；而继承链顶端（WPF-UI 的 `TabItem`）的 `Foreground` 是 `DynamicResource`。子项首次布局时取到的是祖先**尚未解析完的默认值**（黑），切 Tab 触发内容重建后才拿到已解析的主题色——"继承取色"在懒加载 `Frame` + `TabControl` 组合下的时序问题；显式写在元素自己的 `Foreground="{DynamicResource …}"` 首次布局即解析，无此问题。
- 修复：把技能页（队列技能名、技能组名、组内技能名）与克隆页（激活克隆脑插名、跳跃克隆脑插名）共 5 处"靠继承"的文字补上显式 `Foreground="{DynamicResource TextFillColorPrimaryBrush}"`；顺带把钱包页军团 division 标签与卡片页空状态标题也补上（同类隐患）。
- 实机核验（深色）：技能页、克隆页**首次点开即为主题白色**，无需来回切 Tab。
- 结论（并入 §9 第 18 条排查清单）：Frame + TabControl 懒加载场景下，**文字不要依赖继承取 Foreground**，一律显式写主题资源。

### 阶段 19：邮件"全部邮件"切走再切回后列表为空
- 现象：邮件页首次进入"全部邮件"有邮件；切到 Inbox/Sent 再切回"全部邮件"，列表**空白**。
- 根因："全部邮件"是 `LabelId = 0` 的伪标签。首次加载走 `SelectedLabelId`（其语义是 `LabelId > 0 ? LabelId : null`，0 被归一化为"不过滤"）；而标签的 `SelectionChanged` 处理器直接把 `label.LabelId`（=0）传给了 `LoadHeadersAsync` → `labelId.HasValue` 为 true → ESI 请求按**"标签 0"**过滤，标签 ID 从正数开始，结果恒为空。
- 修复（双保险）：`SelectionChanged` 改传 `_viewModel.SelectedLabelId`；`MailPageViewModel.LoadHeadersAsync` 内部再把 `<= 0` 归一化为 `null`。
- 实机核验（深色、真实账号）：全部邮件 → Inbox（列表变为收件箱内容）→ 全部邮件（列表恢复为全量，含已发送邮件），不再为空。

### 阶段 20：TabItem 全局默认样式（选中样式与间距）
- 现象：角色管理页顶部 TabControl 的**选中标签不明显**，且标签之间/与边缘没有间距。
- 修复：新增 `Controls/GlobalControlStyles.xaml`（专用放"整个应用统一生效"的隐式控件样式），在 `App.xaml` 里合并于 `ui:ControlsDictionary` **之后**（覆盖库默认），**排在 `SettingCardStyles.xaml` 之前**（该文件只放设置卡片相关样式）；其中定义**隐式 `TabItem` 样式**——应用内所有 TabControl（角色标签、工作区子页、钱包/合同的页签）统一生效；自带 `ControlTemplate`，颜色全部显式给主题资源，避免阶段 15/16 的"隐式样式被顶掉"问题：
  - 选中态 = 圆角底色（`SubtleFillColorSecondaryBrush`）+ 主题色下划线（`SystemAccentColorPrimaryBrush`）+ 字重 SemiBold，一眼可辨；
  - 悬停（未选中）= `SubtleFillColorTertiaryBrush`；
  - 标签间距 = Border 右侧 `Margin="0,0,2,0"`、内边距 `Padding="14,8"`（数值以当前代码为准，可随时微调）；
  - `CharactersShellPage` 里散落的 `new TabItem { … }` 收敛到 `CreateTabItem(header, content)` 一处，且不再显式指定 `Style`（隐式样式自动生效）。
- 踩坑记录（并入 §9）：自定义 `TabItem` 模板里的 `ContentPresenter` **必须写 `ContentSource="Header"`**，否则不显示标签文字（默认指向 `Content`，标题区看起来是空的）。
- 追加修复：选中态的 `FontWeight=SemiBold` 之前直接设在 TabItem 上——**`FontWeight` 是可继承属性**，会把标签内容（整个工作区页面，经 Frame 也照样继承）全部变粗。改为在模板里对 `HeaderPresenter` 设 `TextElement.FontWeight=SemiBold`，加粗只作用于标签文字。
- 核验：构建通过；样式只存在于 `GlobalControlStyles.xaml` 一处（已从 `SettingCardStyles.xaml` 移除）。

### 阶段 21：总览页每次进入都停在底部
- 现象：进入角色总览页时，页面**默认滚动到底部**。
- 根因：页面实例常驻（Frame 承载），`ScrollViewer` 的滚动位置会跨"进入"保留；加上异步数据填充与焦点/布局处理可能触发 `BringIntoView`，把视图带到下方内容（技能队列在页面末尾，最明显）。
- 修复：`OverviewPage` 的 `Loaded` 里，数据加载完成后以 `DispatcherPriority.ContextIdle`（等布局与焦点队列出空）执行 `ScrollHost.ScrollToTop()`，保证每次进入都从顶部开始。
- 备注：若其他自带 ScrollViewer 的页面出现同样现象，可用同一处理（`Loaded` 末尾回顶）。

### 阶段 22：钱包 / 合同 / 工业表格的横向滚动（最终方案）
- 现象：表格列宽超出可视区时**没有水平滚动条**，右侧列（完成时间/项目费用/位置等）看不到。
- 排查结论（多轮实测）：
  1. WPF-UI 为 DataGrid（`ui:DataGrid`，其资源中也引用了标准 `DataGrid` 类型）提供了自己的模板，**该模板的横向滚动条在本项目里始终不出现**；换标准 `DataGrid`、加 `CanContentScroll=True`、把滚动条可见性设成 Auto/Visible 均无效。
  2. 实测证据：钱包表格的列被"压缩到刚好填满可视区"（时间 61.6、合计 31.2、余额 52、描述 160、类型 51.2、原因 120 DIP，合计正好等于可视宽度）——这是表格**认为自己不能横向滚动**时"把列挤进可视区"的行为，所以数据被截断且没有滚动条。
  3. 中途踩过的坑：把表格塞进外层横向 ScrollViewer 时，星号列在无限宽度测量下会按内容撑爆（表头被推出可视区）；外层纵向禁用又会让表格无限增高、行被裁掉。
- 最终方案（实测通过）：
  1. 每个表格外层包 `ScrollViewer HorizontalScrollBarVisibility="Auto" VerticalScrollBarVisibility="Auto"`——**横向滚动由外层标准 ScrollViewer 提供**（滚动条必然出现、初始位置在最左），纵向也交给外层（表格按内容排布，外层出垂直滚动条）；
  2. 表格设 `MinWidth="{Binding ViewportWidth, RelativeSource={RelativeSource AncestorType=ScrollViewer}}"`：列不宽时铺满可视区、列宽时自然溢出；
  3. **所有星号列改为固定宽度**（描述 260 / 原因 180 / 交易物品 220 / 交易位置 220 / 合同标题 240 / 合同起止位置 220；工业页保持 `Auto`），避免无限宽度测量下星号列塌陷或撑爆；
  4. 表头/单元格/行外观继续用 `FluentColumnHeaderStyle` / `FluentDataGridCellStyle` / `FluentDataGridRowStyle`；**滚动条保持 WPF-UI 默认样式**（曾一度改成 10px 可见样式，因过粗已回退）。 `FluentDataGridRowStyle` 另设 `MinHeight="36"`（默认行高只有约 18px，太挤）。
- 实测结果：钱包表格内容宽 1337px（可视约 595），列宽恢复为 170/140/160/260/160/180（DIP），底部出现横向滚动条，列不再被压缩截断。

---

### 阶段 23：邮件列表选中效果退回系统默认蓝
- 现象：邮件页两个列表（标签列表、邮件列表）的选中高亮是 **WPF 系统默认的蓝色块**，与其他页面的 Fluent 观感不一致。
- 根因：同阶段 15/16 的"显式样式顶掉隐式样式"——两个列表用 `ItemContainerStyle="{StaticResource StretchListItem}"`，而该样式只设了铺满/内边距、**没有基于 WPF-UI 的隐式 `ListBoxItem` 样式**，于是选中/悬停的视觉退回主题默认（系统蓝）。
- 修复：给 `StretchListItem` 加上自带 `ControlTemplate`（不 `BasedOn` 未验证的隐式样式，避免页面构造时抛 `XamlParseException`）：圆角 4 的底框，**选中 = `SubtleFillColorSecondaryBrush`、悬停 = `SubtleFillColorTertiaryBrush`**、文字用 `TextFillColorPrimaryBrush`，与 TabItem / 表格行的选中观感一致。
- 核验：编译通过（当时应用在运行、主输出目录被占用，改用重定向输出路径校验）。

---

### 阶段 24：邮件详情弹窗白色背景、不跟随主题
- 现象：邮件详情弹窗背景是白色的，与主窗口（深色/主题色）不一致。
- 根因：`MailDetailWindow` 是**普通 `Window`**——普通窗口用的是系统原生标题栏与背景，不跟随应用主题（Windows 不会为该进程自动套用深色标题栏）；主窗口用的是 `ui:FluentWindow` + Mica 背景 + 自绘 `ui:TitleBar`，两者观感自然不一致。
- 修复：把两个弹窗（`MailDetailWindow` 与 `SerenityAuthWindow`）都改成 `ui:FluentWindow`：`ExtendsContentIntoTitleBar="True"` + `WindowBackdropType="Mica"` + 自绘 `ui:TitleBar`（与 MainWindow 完全同一套），内容顶部留 48px 避开标题栏；代码后置的基类同步改为 `Wpf.Ui.Controls.FluentWindow`。另外把正文 `HtmlPanel` 的 `Background` 显式设为 `Transparent`，避免 HTML 渲染器自身涂白底。
- 核验：编译通过（应用在运行，主输出目录被占用，改用重定向输出路径校验）；全项目已无普通 `Window`（仅剩 `ui:FluentWindow`）。

### 阶段 25：商业-市场迁移（WPF + LiveCharts）
- 目标：把 WinUI 的市场页迁到 WPF，历史图表由 **Syncfusion 换成 LiveCharts2**。**本期只做星域市场**——建筑（结构）市场依赖尚未移植的 `StructureService` ESI 结构解析与角色授权，按既有设置 `MarketSkipStructure`（默认 true）跳过。
- 依赖与工程：
  - 新增 `LiveChartsCore.SkiaSharpView.WPF 2.0.5`（SkiaSharp/HarfBuzz 由它传递）。
  - **`TargetFramework` 由 `net10.0-windows` 改为 `net10.0-windows10.0.19041`**：LiveCharts 传递依赖的 `SkiaSharp.Views.WPF 3.119.0` 只提供 `net462` / `net8.0-windows10.0.19041` 资产，不写平台版本（隐含 TPV=7.0）会触发 NU1701/NU1202；改后 restore/build 干净，`runtimes/win-{x64,x86,arm64}/native/libSkiaSharp.dll` 正常随包复制。
- 新增代码：
  - `Services/Business/MarketOrderService.cs`：星域订单分页（匿名 ESI，无需授权）+ 历史统计（缓存 `Configs/HistoryOrders/{region}/{type}.json`，TTL 取设置）+ 订单富化（物品类型 / 空间站名 / 星系与所属星域，均来自本地 SDE）；PLEX（44992）自动切全球星域（19000001）；公开方法统一 `try/catch` + `Log.Error` 后返回 null（WPF 服务层约定）。**不拉取建筑订单**，结构位置名只从本地结构列表兜底。
  - `Services/Business/MarketStarService.cs`：物品收藏，读写与 WinUI **共用**的 `Configs/StaredMarketInvType.json`。
  - `ViewModels/Business/MarketPageViewModel.cs`：市场/物品选择、卖单（价升序）与买单（价降序）、5%/均价/数量统计（沿用 WinUI 公式）、计算器（逐档吃单、税后总收入、立即卖出）、历史时间范围（1/3/6/12 月/全部，默认 3 月）、**LiveCharts 系列**（价格 Highest/Average/Lowest 三条线 + 销量一条线；`DateTimeAxis` 按天、`ZoomMode=X`、tooltip 开启）、可绑定的加载/错误状态。
  - `Views/Pages/MarketPage.xaml(.cs)`：替换原占位页（类名/命名空间不变，`MainWindow.xaml` 的导航注册无需改动）。左侧 = 市场下拉（星域搜索）+ **"列表 / 收藏"两个 TabItem**（列表内含物品搜索框与分组树，收藏独立成页）；右侧 = 物品头 + 卖单/买单/历史三页签；表格用 `ui:DataGrid` + 阶段 22 的"外层 ScrollViewer + 固定列宽 + `MinWidth` 绑 `ViewportWidth`"方案。
  - 物品头布局（按使用反馈细化）：**第一行 = 图标 | 物品名称 | 买单/卖单统计（5% / 平均 / 数量），三者同排**；**第二行 = 操作按钮（收藏、简介、买入、刷新）右对齐**。这样统计与名称同行（对齐 WinUI），同时给名称留出宽度——实测：名称列曾因四个文字按钮占位被压到 21px 逐字换行，改两行后为 267px 单行。
  - **新增可复用的工具窗口外壳 `Views/Windows/ToolWindow.xaml(.cs)`**（对齐 WinUI 版 `Wins/ToolWindow`），并据此把 `简介` / `买入` 改为它的实例（不再是各自一个窗口类）：
    - **统一标题栏样式**：`ui:TitleBar` 提供——左上角 logo（`ui:ImageIcon` → `Assets/logo_32.png`，与主窗口同一资源），logo 右侧是窗口名称（`DisplayTitle`）；系统标题（任务栏/Alt+Tab 文本）用 `SystemTitle`，未设置时跟随 `DisplayTitle`。
    - **内容可传入**：构造函数 / `SetContent(object)`，Page、UserControl 或任意 UIElement 均可——内部用 **`Frame`** 承载（`Page` 只能由 Window/Frame 承载，放 `ContentControl` 会抛异常）。
    - **标题按钮可配置**：`ShowMinimizeButton` / `ShowMaximizeButton` / `ShowCloseButton` / `ShowTitleBar` 四个依赖属性，外加 `TitleStyle` 枚举（对齐 WinUI 的 `WindowTitleStyle`：Default / OnlyClose / OnlyMini / OnlyMax / NoButton / Empty / MiniAndClose）；`ShowMaximizeButton=false` 同时置 `TitleBar.CanMaximize=false`，双击标题栏也不会最大化。
    - **置顶**：`ShowTopmostButton` 在标题栏右侧显示图钉按钮（`Pin24`/`PinOff24` 切换、ToolTip 与 AutomationName 同步为「置顶显示 / 取消置顶」）；也可直接用继承自 `Window` 的 `Topmost` 或 `SetAlwaysOnTop()`。
    - **任务栏**：直接用继承自 `Window` 的 `ShowInTaskbar`（构造参数 `showInTaskbar`）。
    - 置顶按钮的提示/无障碍名称用新增语言键 `ToolWindow.Topmost` / `ToolWindow.TopmostOff`（中英同步）。
    - 另有 `SetCloseToHide()`（关窗改为隐藏，配合 `AllowClose()`）与 `Owner`/`WindowStartupLocation=CenterOwner` 的常规用法。
    - `简介` / `买入` 的内容相应改为 UserControl：`Views/UserControls/MarketTypeInfoView.xaml(.cs)`、`MarketCalculatorView.xaml(.cs)`，`DataContext` 复用市场页 VM（与选中物品实时联动）；`MarketPage` 里按单实例打开（重复点击只激活）。
  - `Services/ThemeService.cs` 新增 `ThemeChanged` 事件：LiveCharts 的画笔是 SkiaSharp 对象、**无法用 `DynamicResource` 跟随主题**，因此图表的 Series/坐标轴 Paint 监听该事件重新着色（`SolidColorPaint` 由主题 Brush 转换而来）。
  - 语言文件补 **52 个 `MarketPage_*` 键**（46 个沿用 WinUI 原文 + 星域搜索 / 搜索物品 / 体积 / 刷新 / 简介 / 计算明细 6 个新增），中英同步。
- 布局细化的实机核验（`dotnet build` 0 错误后逐项确认）：
  - 左侧"列表 / 收藏"确为两个 TabItem（列表页内含搜索框与分组树）；
  - 市场按钮宽度 = 左栏整宽（390px）；
  - 物品头第一行：物品名 `因卡萨斯级` 宽 267px、与统计（`5%` / `平均` / `数量`）**同为 y=291 一行**，操作按钮在第二行 y≈372 右对齐；
  - `简介` / `买入` 均为**独立窗口**（枚举该进程顶层窗口可见：标题「简介」440×520、标题「买入」400×620，各自有标题栏，可 Alt+Tab 切换）；重复点击按钮不产生第二个窗口；
  - 两个窗口都由 `ToolWindow` 承载：标题栏文字分别为「简介」「买入」，内容用 UserControl 传入；
  - **置顶按钮实测**：用 UIA 调用「买入」窗口标题栏上的图钉按钮 → 该窗口扩展样式由 `[APPWINDOW]` 变为 `[TOPMOST,APPWINDOW]`、按钮名自动变为「取消置顶」；再调用一次 → 恢复 `[APPWINDOW]`、按钮名回到「置顶显示」；期间主窗口与「简介」窗口均不受影响；
  - `简介` 窗口内容：`因卡萨斯级` / 体积 `29,500.00 m3` / 完整中文简介（与主界面同一物品数据）；
  - `买入` 窗口内容："计算明细"逐档行 `1 × 170,100.00 = 170,100.00`、总花费 `170,100.00`、总收入 `162,275.40`（3.6% 销售税 + 1% 中介税，算式核对无误）。
- 实机核验（`dotnet build` 0 错误）：
  - 进入"商业 → 市场"：市场按钮显示"伏尔戈"；19 个市场根分组由本地 DB 建树（制造和研究/舰船/技能…）；搜索"级"返回巨神兵级/裂谷级等；收藏区、计算器、刷新按钮齐全。
  - 选中"巨神兵级"后：物品头显示完整中文描述与体积 `24,398.00 m³`；卖单表加载真实订单（价格升序 1,300,000 → 1,290,000 → 1,276,000 …，位置解析为"吉他 VI - 顶峰集团 运营中心"、星系"吉他"、安全等级 0.9、过期时间 `00.14:43:33`）；汇总统计 卖单 5%=299,350.00 / 均价 521,497.96 / 数量 1,414，买单 5%=212,800.00 / 均价 186,100.06 / 数量 100,573；历史缓存落地 `HistoryOrders/10000002/591.json`（46 KB）。
  - 切到"历史"页签（价格/销量两图 + 时间范围下拉）无异常；全程进程存活、应用日志无错误。
  - 收藏按钮写入 `StaredMarketInvType.json` = `[591]`。
- 未做/后续：建筑市场（结构解析与授权）、外部页面"跳市场看物品"（WPF 侧暂无调用方）、表格安全等级按值着色、TreeView 选中态仍是 WPF 默认样式（ListBox 未显式设 `ItemContainerStyle`，走 WPF-UI 隐式样式）。

### 阶段 26：商业-订单迁移（个人 / 军团未结订单 + 市场参考价差）
- 目标：把 WinUI「商业 → 订单」页迁到 WPF。WinUI 版该页**没有 VM**（逻辑全在代码后置）、表格是 Syncfusion `SfDataGrid`、角色靠 WinUI 专有的 `SelecteCharacterControl`；WPF 侧按本项目既有分层重写（服务 + VM + Page），表格用 `DataGrid`，角色下拉直接绑 `CharacterStore.Characters`。
- 新增代码：
  - `Services/Business/CharacterOrderService.cs`：
    - `GetCharacterOrdersAsync(ctx)` → `Market.ListOpenOrdersFromCharacterAsync(auth)`（单次、无分页、无缓存）；
    - `GetCorpOrdersAsync(ctx)` → `ListOpenOrdersFromCorporationAsync(auth, corpId, page)` 分页。**修正 WinUI 的 off-by-one**：WinUI 写成 `ListOpenOrders...(page++)` 后又拿自增后的 `page` 与 `MaxPages` 比较，会多请求一页（可能触发 "Requested page does not exist!"），这里改为 `if (resp.MaxPages <= page) break;`；
    - `BuildStatusAsync(orders)` → 逐单算与市场参考价的差；**参考价按 `(TypeId, RegionId)` 分组只拉一次星域订单**（WinUI 是逐单拉取，订单多时非常慢）；规则与 WinUI 一致——只对空间站订单取参考（结构订单在 WPF 侧解析不到星系，恒「未知」），买单比**最高买价**、卖单比**最低卖价**，`Difference`/`Normal` 交给 Core 的 `StatusOrder` 计算；
    - `OpenMarketDetailsAsync(ctx, typeId)` → `UserInterface.OpenMarketDetailsAsync`（在游戏内打开市场详情，需 `esi-ui.open_window.v1`）。
  - `MarketOrderService`：新增公开的 `EnrichOrdersAsync`——原富化链（物品类型 / 位置名 / 星系与星域）是 `private`，而个人/军团订单同样需要补这些字段。
  - `ViewModels/Business/OrderPageViewModel.cs`：角色集合（直接暴露 `CharacterStore.Characters`，角色被移除时清空选中）、订单类型（卖/买）与来源（个人/军团）两个过滤器、`ObservableCollection<StatusOrder> Orders`、加载/错误状态、`BuildGameOrderText`（生成剪贴板文本）、`EnsureDefaultCharacter`（首次进入自动选中第一个角色）。
  - `Views/Pages/OrderPage.xaml(.cs)`：替换占位页（类名不变，`MainWindow.xaml` 的注册无需改动）。工具栏 = 角色 / 订单类型 / 来源 / 刷新（**后两者与 WinUI 一样是 ComboBox 过滤器**，没有改成 Tab）；表格 10 列（物品 / 数量 / 价格 / 订单状态 / 与市场价差 / 位置 / 星系 / 范围 / 过期时间 / LocationId，与 WinUI 同序）；**订单状态列用模板列 + `DataTrigger`**（未知=次要色、正常=成功色、被压单=危险色，文字与颜色都随语言/主题变化——若直接给 `DataGridTextColumn` 的 ElementStyle 设 `Text`，会被列自身的 Binding 以"本地值优先于样式"覆盖，所以必须用模板列）；右键菜单保留 **复制为游戏批量购买订单**（只复制被压单/未知的行，每行 `物品名 1`）与 **在游戏中查看**。
  - 语言键：新增 **`BusinessPage_Type` + 22 个 `OrderPage_*`**（中英同步；英文沿用 WinUI 原文，仅修正上游两个键值里的拼写 `Succcess`/`Falied` → `Success`/`Failed`）；复用 `StructuresSettingPage_Character`、`General_Refresh`、`MarketPage_Amount/Price/Location/Range/Duration`、`General_SolarSystemName`、`MarketPage_GettingOrder`、`General_CharacterUnselected`。
- 与 WinUI 的有意差异：参考价按 `(TypeId, RegionId)` 去重取数；军团订单失败时给出**明确提示**（WinUI 用空 `catch` 静默清空表格）；首次进入自动选中第一个角色（WinUI 要求手选）；右键菜单**去掉**「添加到倒货排除列表」「从购物车减去数量」两项（依赖 WinUI 的 `BusinessService` 与倒货页，WPF 侧尚未迁移）；过期时间直接显示 `Order.RemainTime`（`dd.hh:mm:ss`），未做 WinUI 那种本地化"天/小时/分钟/秒"拼接。
- 实机核验（`dotnet build` 0 错误）：
  - 进入「商业 → 订单」：工具栏三个下拉与刷新按钮齐全；10 列表头与 WinUI 同序；自动选中第一个角色并取数；
  - 个人来源：ESI 调用成功、返回空表 → 显示「该角色没有未结订单」；
  - 切「来源 = 军团」再刷新：同样调用成功并返回空表（说明军团订单的授权与分页路径正常，走的不是权限失败分支）；应用日志无错误；
  - ⚠️ **该账号当前没有任何个人/军团未结订单**，因此「订单状态」「与市场价差」两列**没有真实数据可核验**（算法本身是 Core 的 `StatusOrder`，与 WinUI 共用、未改动）。
- 未做/后续：倒货相关的两个右键动作（依赖倒货模块）；`SfDataGrid` 的分组/列过滤/分组合计（WPF 无对应实现，与合同/钱包页一致）。

### 阶段 27：结构（建筑）服务补齐 + 市场支持建筑
- 目标（即文档原 §8「计划内未做」项）：把 WinUI 的 `StructureService.QueryStructureAsync`（结构名称的 ESI 解析）移植到 WPF，并让市场页支持选择"建筑"市场。
- **结构服务**：
  - 新增 `QueryStructureAsync(long id)`（用**默认角色**解析）与 `QueryStructureAsync(long id, long characterId)`（指定角色；`characterId <= 0` = 只查本地缓存、不发 ESI，与 WinUI `-1` 的语义一致）：命中 `Structures.json` / `MarketStructures.json` 直接返回；否则用该角色的 `AuthDTO` 调 `Universe.GetStructureInfoAsync`，再用本地 SDE 补 `SolarSystemName` / `RegionId` / `RegionName`，最后回写 `Configs/Structures.json`（新增 `SaveAutoStructures`）。需要 `esi-universe.read_structures.v1`。
  - `Add(Structure)` 重载：把**已解析的完整结构**（含星系/星域）加入市场建筑列表；原 `Add(long, string?)` 保留。
  - **修复既有缺陷**：`StructureService.Init()` 在 WPF 里**从未被任何代码调用** → `GetMarketStrutures()` 返回的是临时空集合，用户已有的 `MarketStructures.json` 根本不会加载，且任何一次保存都会把该文件覆盖成空。已在 `CoreInitializer.Init()`（`CharacterStore.Init()` 之后）补上调用。
  - 设置 → 玩家建筑：按结构 ID 添加改为**先解析再入库**（`QueryStructureAsync`），解析失败给明确提示（新增键 `Settings.Structures.ResolveFailed`）；因此市场建筑列表里显示的是名称/星系，而不是原始 ID。
- **市场页支持建筑**：
- `MarketOrderService.GetStructureTypeOrdersAsync(structureId, invTypeId)`（阶段 29 前的名字是 `GetStructureOrdersAsync(structureId, invTypeId)`）：`Market.ListOrdersInStructureAsync` 分页（`MaxPages <= page` 停），鉴权**优先用登记该建筑的角色、否则用默认角色**；建筑订单不含星系，按 WinUI 做法统一 `order.SystemId = structure.SolarSystemId` 再走富化链（从而映射到星域）；结果按 `Configs/StructureOrders/{id}.json` 缓存（TTL 同订单设置，按文件内 `UpdateTime` 判定）。
- `MarketPageViewModel`：新增 `SelectedMarketTypeIndex`（0 星域 / 1 建筑）、`SelectedStructure`（与 `SelectedRegion` **互斥**）、`Structures` / `FilteredStructures` / `StructureFilterText` / `HasNoStructure`；取数分支——选了建筑走 `GetStructureTypeOrdersAsync`，历史仍用**该建筑所在星域**（与 WinUI 一致：建筑"历史"实为星域历史，共用 `HistoryOrders/{regionId}/`）；建筑订单失败时给明确提示（新增键 `MarketPage_StructureOrdersFailed`）。
  - `MarketPage.xaml`：市场选择器弹层改成 `TabControl`（**星域 / 建筑** 两个页签），建筑页签含搜索框 + 建筑列表（列表为空时显示 `StructuresSettingPage_EmptyTip` 引导）。
  - **顺带修复一个静默的取数副作用**：市场页的几个 `ListBox` 未设 `IsSynchronizedWithCurrentItem`，WPF 会把列表选中项同步到 CollectionView 的 CurrentItem——**搜索框一输入、结果列表重建就会自动选中首项并触发取数**（实测证据：仅输入搜索词，就有两条"我没点过"的物品的历史缓存被写入）。已给星域 / 建筑 / 搜索结果 / 收藏四个列表显式设 `IsSynchronizedWithCurrentItem="False"`。
- 实机核验（`dotnet build` 0 错误）：
  - 设置 → 玩家建筑，按 ID `1044752365771` 添加 → `MarketStructures.json` 写入**解析后的完整结构**（名称 `Perimeter - 0.0% Neutral States Market HQ`、星系 皮尔米特、星域 伏尔戈、RegionId 10000002），设置页表格显示 名称/ID/星系；
  - 市场页 → 市场选择器出现 **星域 / 建筑** 两个页签 → 建筑页签列出该建筑 → 选中后市场名变为建筑名、弹层自动收起；
  - 选中物品"因卡萨斯级"：**建筑订单取数成功**（生成 `Configs/StructureOrders/1044752365771.json` ≈ 7 MB，该建筑订单量很大），页面统计 卖单 5%=90,080.00 / 均价 89,430.00 / 数量 161（买入侧 0，即该建筑内无买单）；历史统计走该建筑所在星域（命中已缓存的 `HistoryOrders/10000002/594.json`）；
  - 自动加载修复验证：仅输入搜索词"多米尼克斯" → 历史缓存文件数不变（12，无自动加载）；手动点选"多米尼克斯级" → 新增 `HistoryOrders/10000002/645.json`（13，手动路径正常）；应用日志无错误。
- 未做/后续：`LocationNameResolver`（无角色上下文）与订单富化里的**结构位置名仍只查本地列表**，解析不到回退原始 ID；建筑订单是**整建筑全量**拉取（7 MB 级，按 TTL 缓存，首次较慢）；结构详情窗口 / "按角色搜索建筑"未做。

---

### 阶段 28：商业-倒货迁移（分析 / 购物车 / 记录）
- 目标（§8「计划内未做」项）：把 WinUI 的倒货（Scalper）整套迁移到 WPF：源/目的市场选择、目标物品多选树、取订单、分析推荐、物品详情窗、购物车、购物记录。
- **服务层扩展**（`Services/Business/`，全部为新增文件）：
  - `MarketOrderService`：
- 新增 `GetStructureOrdersAsync(long structureId, ct)`（**整建筑全量**）并把原按物品的 `GetStructureOrdersAsync(structureId, invTypeId)` 改为"拉全量再按 TypeId 过滤"（阶段 29 改名为 `GetStructureTypeOrdersAsync`）。
- 新增星域级接口 `GetAllRegionOrdersAsync(long regionId, bool skipStructure, ct, Action<int,int>? pageCallback)`（阶段 29 前的名字是 `GetRegionOrdersAsync`）：先 `GetCachedRegionOrdersAsync`（`ListOrdersInRegionAsync(regionId, null, page)` 分页 + `Configs/RegionOrders/{regionId}.json` 缓存，按文件 `LastWriteTime` 判 TTL），`skipStructure=false` 时再合并该星域下**本地已知建筑**的订单（`GetStructureOrdersOfRegionAsync`），按 `OrderId` 去重（星域接口优先，因其刷新更快）。
    - 新增 `GetSolarSystemOrdersAsync(systemId, skipStructure, ct, cb)`（拉星域后按 `SystemId` 过滤）。
    - 新增 `GetHistoryBatchAsync(typeIds, regionId, ct, Action<int,int>? progress)`：`ThreadHelper.RunAsync(ids, MaxThread, …)` 多线程批量拉历史（`MaxThread` = `MarketOrderSettingService.ThreadValue`），返回 `Dictionary<int, List<Statistic>>`；单个失败仅记日志不影响整体。
    - 富化链 `SetOrderInfoAsync` 增加 `skipStructure` 开关：倒货整星域查询时跳过建筑名解析（建筑数量大、本地也多数解析不到）；建筑名仍只查本地列表（与阶段 27 的限制一致）。
  - `BusinessService`：倒货排除清单（`AddToFilter` / `RemoveFromFilter` / `GetFilterTypes` + `FilterChanged` 事件，供市场/订单页右键与倒货页联动）与物品数量变化通知（`NotifyTypeCountChanged` / `TypeCountChanged`）；另加**共享购物车** `ShoppingCart`（WPF 把三个子页收进同一个壳页，用服务层共享比 WinUI 的页面事件转发更直接）。
  - `ShoppingRecordService`：购物记录读写（`Configs/ShoppingRecords/yyyy.MM.dd_n.json`）。
  - `ScalperSettingService`：`Configs/ScalperSetting.json` 读写（与 WinUI 同文件，可互相读取），并补齐旧配置缺失的 `SolarSystemId`（跳数计算依赖）。
  - `ScalperCalculator`：倒货计算引擎，逐条对应 WinUI `ScalperViewModel.Cal*`（销量 / 买卖价 10 种口径 / 目标销量 / 净利润 / 回报率 / 本金 / 历史与当前价格波动 / 饱和度 / 热力值 / ISK-跳 / ISK-体积 / 推荐度排名加权），**保留原实现的取舍**（买单取价沿用目的市场历史与销量、历史极值按"去掉一个最值后求均值"、"实际数量价格"按日销量逐档吃单等）。
- **VM 层**（`ViewModels/Business/`）：`ScalperPageViewModel`（设置读写、非 UI 线程回调用 `Dispatcher` 归位、取消支持、`Message`+`IsMessageError` 统一提示）、`ScalperShoppingCartViewModel`（合计、复制为游戏批量购买订单文本、从剪贴板回填剩余数量、保存记录）、`ScalperShoppingRecordViewModel`（记录列表 / 载入 / 删除 / 加回购物车）。
- **控件与视图**：
  - `Views/UserControls/MarketSelecteTreeView`：三态物品多选树（分组三态 → 物品），`SelectedItems`（`List<int>`）与 `SelectedItemsCount` 两个 DP 与 VM **TwoWay** 绑定（与 WinUI 的 `MarketSelecteTreeControl` 同机制：控件就地增删列表 + 回写数量）；带搜索列表。
  - `Views/UserControls/MarketLocationSelectorView`：星域 / 星系 / 建筑三页签位置选择器（各带搜索；星系列表**输入才显示**以避免一次性铺 8000 条），`SelectedItem`（`MarketLocation`）DP + `SelectedItemChanged` 事件。
  - `Views/UserControls/ScalperAnalyseView`：左"基本设置 / 进阶设置 / 排除列表"三页签 + 右 18 列结果表（右键加入购物车、双击开详情窗）；市场用 `ToggleButton` + `Popup` 选择器。
  - `Views/UserControls/ScalperShoppingCartView`、`ScalperShoppingRecordView`：购物车（统计 + 明细 + 复制/粘贴/保存 + 编辑/删除）与记录（文件列表 + 明细 + 删除/加回购物车）。
  - `Views/Pages/ScalperPage`（替换原占位 `ScalperPage.cs`）：倒货 / 购物车 / 记录三个页签的壳页，三个子视图各挂自己的 VM。
  - `Views/Windows/ScalperItemDetailWindow`：物品详情窗（左指标、右源/目的市场的卖单/买单表 + 历史价格/销量图，LiveCharts，时间范围 1/3/6/12 月/全部）。
  - `Views/Windows/ScalperShoppingItemEditWindow`：购物车条目买价/卖价/数量编辑对话框（对应 WinUI 的 `AddToShoppingCartDialog`），实时回显回报率与净利润。
- 本地化：补齐 `BusinessPage_*` 共约 100 个键（`zh-CN.xaml` / `en-US.xaml`，取值沿用 WinUI 英文/中文原文），新增 `MarketSelecteTreeControl_SearchType`、`MarketPage_SearchSolarSystem`、`BusinessPage_ShoppingCartEmpty`。
- 构建：`dotnet build` **0 错误 0 新警告**（仅剩既有的 `NU1903`）。
- 实机 smoke 核验（应用日志无错误、未写 `ScalperSetting.json`）：
  - 商业 → 倒货：壳页三页签（倒货/购物车/记录）与三个子视图均正常渲染；物品树**根分组已建**、搜索框与"目标物品"数量（沿用存档 `ScalperSetting.json` 的 937）正确；源/目的市场名从存档读出（源=伏尔戈、目的=多美）；
  - 物品树勾选联动：勾选一个根分组 → "目标物品"由 **937 → 2,816**（三态传播 + `SelectedItems`/`SelectedItemsCount` TwoWay 回写均生效）；
  - 市场位置选择器：打开 `Popup`（窗口枚举到 `TOPMOST,TOOLWINDOW` 的 320x424 宿主，含 星域/星系/建筑 三页签与星域列表）→ 选中"伏尔戈" → 弹层自动关闭、按钮文本更新（`SelectedItem` TwoWay 生效）；
  - "分析推荐"在未取订单时给出红色提示"请先获取订单"（`Message` + `DataTrigger` 配色生效）。
- 未做/后续：`获取订单`+`分析推荐`的**完整数据链路未实机跑通**（源=伏尔戈整星域全量订单 + 全物品历史，属分钟级、大量 ESI 调用，未在本轮触发）；物品详情窗未实机打开（需先有分析结果）；订单页的两个倒货右键动作（"添加到倒货排除列表"、"从购物车减去数量"）本轮未接线（`BusinessService` 已具备接口）。

---

### 阶段 29：`MarketOrderService` 整体优化（去重 / 命名 / 健壮性）
倒货接入后该服务从"星域 + 建筑"长成了"星域 / 整星域 / 星系 / 建筑 / 单个历史 / 批量历史"六个入口，累计的分页与缓存样板明显重复，且出现了一对易误用的重载。本轮只做内部整理与新名，**不改动对外行为语义**（除下面标注的两处修正）。

- **分页样板收敛为一个 helper**：原先 4 段几乎相同的 `while + MaxPages` 循环（星域指定物品 / 整星域 / 建筑 / 带缓存星域）合并为 `FetchOrderPagesAsync(Func<int, Task<(订单列表, 总页数)>>, ct, pageCallback)`；各来源只提供"取第 N 页"的投影（`ListRegionOrdersPageAsync` / `ListStructureOrdersPageAsync`）。停页条件统一为 `空页 || 总页数 <= 当前页`。
  - **修正（1）**：某页响应异常（`Model == null`）时统一返回 `null`（失败）而不是把已取到的前几页当作完整结果返回。旧实现里整星域/建筑路径会 `break` 并把**残缺结果写进缓存**，会让后续分析静默基于不完整数据。
  - **修正（2）**：建筑订单取到空结果时**不再写缓存文件**（旧实现总是写，会留下一个空缓存文件，虽然下次会因 `Count > 0` 判定而重取，但浪费且易误判）。连带地，建筑取数失败（响应异常）现在返回 `null` 而不是空列表 → 市场页会显示 `MarketPage_StructureOrdersFailed` 提示，而不是静默空表（这正是该提示文案的原本意图）。
- **缓存读写收敛为基础设施**：`IsFileRecent`（按文件 `LastWriteTime`）/ `IsRecent`（按缓存文件内 `UpdateTime`）/ `ReadJsonFileAsync<T>` / `WriteJsonFileAsync<T>`（自动建目录）/ `RefreshRemainTime`。星域与历史缓存先判新鲜度再读文件（整星域缓存可达百 MB，过期文件不必读进内存）；建筑缓存沿用文件内 `UpdateTime`（与 WinUI 写入的字段兼容）。
- **历史接口合并**：原 `GetHistoryAsync(typeId, regionId, forceRefresh)` 与私有的 `GetHistoryRawAsync` 是两份几乎相同的取数逻辑，合并为 `GetHistoryInternalAsync(typeId, regionId, forceRefresh, logErrors)`；公开版 `logErrors: true`，批量版 `logErrors: false`（避免倒货批量拉几千个物品时把"无历史"刷满日志）。原公开版里那句 `throw new Exception(...)` 实际上会被自己的 `catch` 立即吞掉并返回 null，属于死代码，一并删除。
- **批量历史的小改进**：改为在并发 Lambda 内按 `Interlocked.Increment` 报进度，并用**闭包里的 typeId 作为字典键**（原实现从 `list[0].InvTypeId` 反推键，依赖模型字段正确）；顺带过滤 `typeId <= 0` 与 `regionId <= 0`。
- **消除易误用的重载**：`GetRegionOrdersAsync(long regionId, bool skipStructure, …)` 与 `GetRegionOrdersAsync(long typeId, long regionId, …)` 形参类型只差 `bool`/`long`，写 `GetRegionOrdersAsync(regionId, 0, ct)` 会**静默绑定到按物品的重载**并取到错误数据。已改名为：
  - `GetAllRegionOrdersAsync(regionId, skipStructure, ct, pageCallback)`（倒货用）；
  - `GetStructureTypeOrdersAsync(structureId, invTypeId, ct)`（市场页按物品查建筑用），保留 `GetStructureOrdersAsync(structureId, ct)` 为"整建筑全量"。
  同步更新了 `ScalperPageViewModel` / `MarketPageViewModel` 两处调用。
- **小优化**：`OrderDuration`/`HistoryDuration`/`MaxThread` 取值统一 `Math.Max(1, …)`（设置被改成 0 时不再出现 `TotalMinutes < 0` 恒过期、并发数为 0 等退化）；`SetSystemInfoAsync` 在 `SystemId` 全为 0 时跳过 DB 查询；`SetTypeInfoAsync`/`SetLocationInfoAsync` 的重复 `Where` 提出为局部列表；类头文档重写（原文还停留在"本期只做星域市场、不拉建筑订单"的旧状态）。
- 构建：**0 错误 0 新警告**（仅剩既有 `NU1903`）。改后做了实机 smoke 核验（应用日志无新增错误）：市场页搜索"多米尼克斯"→ 选中"多米尼克斯级"，**卖单表出数据**（`TypeId 645`，价格 158.7M–197.8M，位置名/星系/安全等级均正确解析）→ 说明重写后的 `FetchOrderPagesAsync` 分页与富化链正常；切"历史"页签**价格三线图正常渲染**（坐标轴到 2026.09.02），说明合并后的 `GetHistoryInternalAsync` 缓存/取数路径正常。倒货页的整星域入口（`GetAllRegionOrdersAsync`）本轮仍未跑真实数据。

---

### 阶段 30：订单分页改为并发拉取（倒货整星域提速）
`MarketOrderService.FetchOrderPagesAsync` 原本逐页串行（一页一往返），而 ESI 的总页数（`X-Pages`）在第一页的响应里就有，其余页彼此独立，因此可以并发。

- **实现**：第 1 页先串行取（拿 `X-Pages`），其余页用 `ThreadHelper.RunAsync` 按线程数**并发**取，结果按页序写回 `pages[page-1]`，最后顺序展平——保持与串行版相同的顺序与内容。
- **并发度**：`PageConcurrency = Math.Clamp(ThreadValue, 1, 16)`（默认设置值是 4）。上限 16 是因为同一路由并发过高容易被 ESI/Cloudflare 限流，反而更慢；需要更激进可调大设置值，但不会超过 16（已在 XML 注释写明）。
- **失败与取消**：单页请求失败（异常）**重试一次**（间隔 500ms），仍失败则整体返回 `null`，继续保持"不把残缺结果写进缓存"；单页返回空响应（抓取期间 `X-Pages` 变小、该页已不存在）按空页跳过，不算失败；取消返回 `null`（并发的 `Func<T,Task>` 重载没有 token，改在每页开工前与收尾各查一次 `IsCancellationRequested`）。
- **进度回调语义变化**：`pageCallback` 从"当前第几页"变成"已完成页数/总页数"，因为并发下页号会乱序；倒货页显示形如 `获取源市场订单中:217/411`。

**实机核验**（真实账号，日志无异常）：
- **整星域（倒货路径，411 页）**：实测 `X-Pages=411`（`GET /markets/10000002/orders/?page=1`）。点击"获取订单"后进度在约 1 分钟内从 `49/411` 推进到 `217/411`（≈3.7–5 页/秒）；对照实测的单页串行延迟 **3.2–5.1 秒/页**（同机 `Invoke-WebRequest` 连续 6 页），串行最多约 0.25 页/秒——**实测速率是串行上限的约 15 倍**，并发生效无疑。随后点"取消"：状态显示"已取消"，`Configs/RegionOrders/` **没有产生任何文件**（不在取消时写残缺缓存）。
- **多页组装正确性（建筑路径，26 页）**：删掉建筑订单缓存强制重新拉取，选中"多米尼克斯级"后 **约 14 秒**完成整建筑抓取并落盘（7,037,074 字节）。用 `ConvertFrom-Json` 校验落盘结果：**25,443 条订单、25,443 个不同 `OrderId`、0 重复**，`LocationId` 全为该建筑、`SystemId` 全为该建筑所在星系（8,761 种物品，24,898 买单 / 545 卖单）——页序组装无重复、无错位、无缺失。
  - 过程小结：核验中我先用一个正则数出"某物品有 9 条订单"，与页面显示的 2 条不符，一度怀疑丢数据；改用 `ConvertFrom-Json` 逐条解析后确认该物品确实只有 2 条（那个正则的 `"TypeId":645` 把 `64500` 这类类型号也匹配了），**页面显示与缓存数据完全一致，无缺陷**。教训：核对 JSON 用解析器，别用前缀不锚定的正则。
- 附带确认：本次触发"获取订单"会先 `SaveSetting()`，核对 `ScalperSetting.json` 内容与改动前**语义一致**（源=伏尔戈 10000002、目的=多美 10000043、目标物品 937、两边 `SolarSystemId` 保留）。

---

### 阶段 31：倒货两处 UI 缺陷修复（Expander 掉样式 / 记录详情不加载）
1. **进阶设置的 `Expander` 变成 WPF 原生外观**：我在 `ScalperAnalyseView.xaml` 的 `UserControl.Resources` 里放了三个**不带 `x:Key` 的本地隐式样式**（`Expander` / `ComboBox` / `ui:NumberBox`，只为设 `Margin`、`HorizontalContentAlignment`），而**无 key 的本地隐式样式同样会整体替换 WPF-UI 的库样式**（不只是显式 `Style=` 会；§9 #18 已补上这条），于是这三个控件退回原生模板。修法照技能页 `Expander` 的既有做法：**删掉本地隐式样式，把布局属性直接写在元素上**（`HorizontalAlignment="Stretch" HorizontalContentAlignment="Stretch" Margin="0,4" Background="Transparent"`；`ComboBox`/`ui:NumberBox` 只加 `Margin`、`ComboBox` 另加 `HorizontalAlignment`）。全文件现在只剩带 `x:Key` 的样式。
2. **记录页选中已保存的记录，右侧"记录详细"始终为空**：`ScalperShoppingRecordView.xaml` 的记录 `ListBox` 只绑了 `ItemsSource`，**漏了 `SelectedItem`**，而加载明细的逻辑挂在 VM 的 `SelectedFile` setter 上（`SelectedFile` 变了才 `LoadSelected()`），所以选中项从未回写到 VM。补上 `SelectedItem="{Binding SelectedFile, Mode=TwoWay}"` 即可（`SelectionMode="Extended"` 不影响 `SelectedItem`）。

**实机核验**（构建 0 错误 0 新警告）：
- 进阶设置：九个折叠项均为 Fluent 外观（圆角标题栏 + 右侧 chevron），不再是原生 WPF `Expander` 的样子。
- 记录页：左列显示用户此前保存的 `2026.09.11_1`，选中后右侧明细表**加载出 16 行**（物品 / 回报率(%) / 净利润 / 源市场买入价格 / 目的市场卖出价格 / 数量 / 体积 均有值）。
- 期间还踩到一次构建失败：输出 `exe` 被正在运行的应用实例锁定（`MSB3021/MSB3027`），与 §8「构建前必须先退出应用」一致——先 `taskkill` 再构建即恢复。

---

### 阶段 32：全屏等待态 + 右下角通知（对齐 WinUI 的 ShowWaiting / InfoBar）
倒货"获取订单"此前只有页面底部一行小字进度与提示；WinUI 版是**全屏等待遮罩（可取消）+ 右下角堆叠通知**。WPF 侧此前只有托盘气泡（`NotificationService`），没有应用内通知，因此本轮补齐基础设施。

- **新增控件**：
  - `Controls/WaitingOverlay.xaml(.cs)`：半透明遮罩（`SmokeFillColorDefaultBrush`）铺满内容区并拦截点击，居中卡片 = 旋转指示 + 文案 + 可选"取消"按钮；`Show/UpdateText/Hide` 三个方法，取消按钮点击后先禁用避免重复触发。
    - 指示器用**旋转的 `ui:SymbolIcon`（`ArrowSync24`）+ `RotateTransform` 动画**，不用 `ProgressBar`/`ProgressRing`——延续 §9 #17 的结论（WPF-UI 的 `ProgressBar` 隐式样式曾导致栈溢出），旋转字形是纯 WPF 动画、无库模板风险。
  - `Controls/MessageHost.xaml(.cs)`：右下角通知栈，新消息在上（最多 5 条），淡入 + 右侧滑入（纯 XAML `EventTrigger` 动画），`DispatcherTimer` 到点自动移除，每张卡带 ✕ 手动关闭；`MessageItem` 携带文案/图标/图标色。
- **新增服务** `Services/PageNotifyService.cs`：`ShowWaiting / UpdateWaiting / HideWaiting / Success / Error / Info / Warning / ClearMessages`，由 `MainWindow` 在构造函数里 `Register(WaitingOverlay, MessageHost)`；内部统一 `Dispatcher` 归位，因此**线程池来的分页/历史进度回调可以直接调用**（VM 里原来的 `Post` 帮助方法随之删除）。
- **接入倒货**：`ScalperPageViewModel` 去掉 `StatusText`/`Message`/`IsMessageError` 与页内"取消"按钮，改为 `ShowWaiting(文案, Cancel)` → 进度回调 `UpdateWaiting("获取源市场订单中:217/411")` → 完成 `Success(汇总)` / 失败 `Error(...)` / 取消 `Info("已取消")`，`finally` 里 `HideWaiting()`；`ScalperAnalyseView` 相应只留两个动作按钮。
- **布局要点**：`MessageHost` 在 MainWindow 里显式 `HorizontalAlignment=Right VerticalAlignment=Bottom`（只占自身内容大小）——否则这个 `UserControl` 会铺满窗口、吃掉所有页面的点击；`WaitingOverlay` 留 `Margin="0,36,0,0"` 让标题栏（最小化/关闭）在等待期间仍可用。

**实机核验**（构建 0 错误 0 新警告；应用日志无异常）：
| 场景 | 结果 |
|---|---|
| 点"获取订单" | 全屏遮罩出现：左上标题栏仍可见，中央卡片含旋转指示、实时文案、取消按钮（截图确认；文案从"获取源市场订单中:x/411"推进到"获取目的市场订单历史中:377/930"） |
| 遮罩内点"取消" | 遮罩关闭、取数中止；右下角出现 **info 通知"已取消"**，且实测在 ~4s 后自动消失（`InfoDuration`） |
| 分析完成 | 右下角出现 **success 通知"分析完成: 528"**（528 条推荐结果） |
| 未取订单时点"分析推荐" | 右下角出现 **error 通知"请先获取订单"**（截图确认：图标 + 文案 + ✕，位于窗口右下角） |
| 通知栈 | 宿主的 UIA 子树可枚举到 `MessageItem` + 文案 + ✕，确认是独立于页面的全局层 |

**顺带完成**：本轮把"取订单 → 分析推荐"整条链路在真实账号上跑通了（源=伏尔戈 411 页并发抓取 + 全物品历史 + 计算引擎 → 528 条推荐，推荐度/回报率/净利润等列均有合理值），§8 原先"倒货完整数据链路未端到端跑通"的限制可以去掉。

---

### 阶段 33：市场选择器"星系"页签列表为空
- **现象**：倒货页打开源/目的市场选择器，"星系"页签里什么都没有。
- **根因（自己移植时改坏了行为）**：WinUI 的 `MapSystemSelectorControl` 是**直接把全部星系铺进虚拟化列表**（`QueryAllAsync().OrderBy(SolarSystemID)`，并按 `ShowSpecial=False` 排除虫洞/希拉等特殊星系），搜索框只做建议；我在 WPF 里为了"避免一次铺 8000 条"，把 `ApplySystemFilter` 写成**搜索词为空就返回空集**，于是不输入任何关键字时列表恒为空。这属于擅自"优化"了参照实现的行为，用户按 WinUI 的习惯点开自然以为是空的。
- **修法**：
  - 加载时用 `!IsSpecial()`（`SolarSystemID >= 31000000`）过滤后按 `SolarSystemID` 排序，**默认列出全部**；搜索词为空视为全部命中。
  - 三个列表（星域 / 星系 / 建筑）的过滤改用 **`ICollectionView`**（`new CollectionViewSource{Source=list}.View` + `Filter` + `Refresh()`）而不是"清空 `ObservableCollection` 再逐条 Add"——星系 8000+ 条时，每敲一个字符重建集合会发上万次 `CollectionChanged`。
  - 三个 `ListBox` 显式写 `ScrollViewer.CanContentScroll="True"` + `VirtualizingStackPanel.IsVirtualizing/VirtualizationMode=Recycling`（防御性写法；经查 `ScrollViewer.CanContentScroll` 的元数据 `Inherits=False`，页面根节点那句 `CanContentScroll="False"` 并不会传给内层列表，内层本来就默认虚拟化）。
- **实机核验**：星系页签默认列出 坦欧 / 拉什希亚 / 埃克葡温姆 / 加尔克 / … （UIA 只枚举到 8 个 ListItem = 可见行，说明 8000+ 条确实在虚拟化渲染）；搜索框输入"吉他"后列表收敛为 1 条"吉他"；选中后弹层关闭、市场按钮更新。
- 附带确认：本次只在界面上试选，未点"获取订单/分析推荐"，因此没有触发 `SaveSetting()`——`ScalperSetting.json` 仍是源=伏尔戈(10000002)、目的=多美(10000043)。

---

### 阶段 34：商业-估价迁移（ESI 直接取价 + 可配置估价方式）

- 目标（§8「计划内未做」最后一项商业子项）：把 WinUI 的「估价」页迁到 WPF。**与 WinUI 版的有意差异**：WinUI 版把粘贴文本整体 POST 给第三方 API（Janice，硬编码 API Key 与 `pricePercentage=100`，无任何估价设置）；WPF 版改为**本地解析输入 + ESI 公开市场接口直接取价**，并按用户要求把**估价方式做成可配置**（口径沿用倒货的 `ScalperSetting.PriceType`，另加买卖两侧的百分比）。
- **新增代码**（全部为新文件，页面占位 `AppraisalPage.cs` 已删除替换，类名/命名空间不变，`MainWindow.xaml` 的导航注册无需改动）：
  - `Services/Business/AppraisalTextParser.cs`：把游戏复制的多行文本解析成「物品名 + 数量」。按行支持 5 种格式（优先级递减）：① Tab 分隔（货柜/机库全选复制 `名称\t数量\t体积\t分组`，数量带千分位逗号）；② 合同物品列表 `名称 x数量`；③ `数量x 名称`；④ 行尾独立数字 `名称 数量`；⑤ 兜底整行为名称、数量 1。
  - `Services/Business/AppraisalService.cs`：估价服务 + 结果模型（`AppraisalResult`/`AppraisalItem`）。
    - 名称 → 类型：先主库精确匹配（英文 SDE 原名）→ 本地化库精确匹配（`Config.NeedLocalization` 时走 `LocalDbService.QueryInvTypes`，支持中文客户端复制的中文名）→ 模糊兜底（**唯一命中才采用**，多命中时只接受忽略大小写恰好相等者，避免张冠李戴）。同一类型的多行输入按 TypeID 聚合数量（与 Janice 同口径）。
    - 行情取数复用 `MarketOrderService`：订单口径（`Sell*`/`Buy*`）按唯一 TypeId 并发调 `GetRegionOrdersAsync`（匿名 ESI、`ThreadHelper` 按 `MarketOrderSettingService.ThreadValue` 并发、实时无缓存）；历史口径走 `GetHistoryBatchAsync`（带 TTL 磁盘缓存）。只要任一侧是订单口径就会一并拉历史（作为"市场无订单"时的兜底价）。
    - 单价口径计算（与 `ScalperCalculator.CalPrice*` 逐条同公式）：卖单升序/买单降序后取 **Top**（最低卖价/最高买价）、**Top5**（前 5% 均价，不足 2 条取首条）、**Available**（按估价数量逐档吃单的加权均价）；历史 **Highest/Average/Lowest/Median**（近 N 天、去极值=去掉一个最值后求均值，并补了倒货没有的"只剩 1 天不去极值"保护）。订单口径算不出（该星域无订单）回落最近历史均价，仍无则该物品按 0 计入并列入"无报价"提示。
    - **单价 = 口径价 × 百分比/100**（买、卖两侧独立配置）；中间价 =（总买价+总卖价）/2；体积按 `PackagedVolume`（缺省回落 `Volume`）。
  - `Services/Business/AppraisalSettingService.cs`：估价设置持久化 `Configs/AppraisalSetting.json`（WPF 版专有）。设置项：`RegionId`（默认 10000002 伏尔戈）、`SellPriceType`/`SellPercent`（默认 卖单最低价 × 100%）、`BuyPriceType`/`BuyPercent`（默认 买单最高价 × 100%）、`HistoryDay`（默认 7）、`RemoveExtremum`（默认 true）。
  - `ViewModels/Business/AppraisalPageViewModel.cs`：输入/设置/结果三块属性、`EstimateAsync`（`PageNotifyService` 全屏等待 + 实时进度 `估价中（done/total）` + 取消支持 + 成功/失败通知）、结果复制为文本（汇总 + 制表符分隔明细）、星域 `ICollectionView` 过滤。
  - `Views/Pages/AppraisalPage.xaml(.cs)`：左右两栏（对齐 WinUI 的布局与 WPF 市场页的控件风格）——左栏输入多行 `ui:TextBox` + 市场（星域）`ToggleButton`+`Popup` 选择器 + 卖/买估价方式 ComboBox（10 个口径项，复用 `BusinessPage_*` 本地化键）+ 买卖百分比与历史天数 `ui:NumberBox` + 去极值 CheckBox + 估价/复制按钮；右栏 5 项汇总条（市场/总体积/总买价/中间价/总卖价）+ 两条警告条（未识别物品 / 所选市场无报价，`SystemFillColorCautionBrush` 图标，不用 InfoBar 以免引入未验证控件）+ `ui:DataGrid` 明细（图标模板列 + 物品/数量/体积/买单价/买价/卖单价/卖价，外层 ScrollViewer + `MinWidth` 绑 `ViewportWidth` 的阶段 22 方案）。物品图标异步加载（`images.evetech.net`，同市场页模式）。
- 本地化：中英各补 24 个 `AppraisalPage_*` 键（键值对齐 WinUI 原有 11 键的语义 + 新增设置/提示项）；口径显示复用倒货既有 `BusinessPage_*` 键。
- 实现中自查修正的两处：①同一物品的中英文名混用时两个输入名会解析到同一 TypeID，聚合字典的 `ToDictionary` 会抛重复键 → 改为手动去重构建；②进度回调来自线程池线程，本地化文本改为**取数前在 UI 线程取好**再进回调。
- 构建状态：**0 错误 0 警告**（仅剩既有 `NU1903`）。按 §8 约定未做截图核验，界面效果由使用者自查。
- 与 WinUI 版的其他差异：输入解析由 Janice 的多格式解析改为本地解析器（覆盖合同/货柜/资产/击杀报告常见格式，未识别的行会明示而不是被服务端忽略）；数量千分位逗号兼容；结果多了"单价"列与复制功能；WinUI 版数值 `/100` 的疑似 Janice 单位修正不再需要。
- 未做/后续：估价不拉建筑订单（星域接口已含空间站与建筑卖单，与 WinUI 星域口径一致）；`Available` 口径的数量基准是**该物品的粘贴总量**（WinUI/倒货里该口径的基准是日销量，此处按估价语义取清单数量）。

---

### 阶段 35：市场位置选择器抽为独立控件（市场 / 估价支持 星域 / 星系 / 建筑）

- 目标：把倒货的市场选择器（`MarketLocationSelectorView`）做成**自包含独立控件**，市场、估价两页统一接入，并让两页的价格来源从"星域 / 建筑（市场页）"与"仅星域（估价页）"扩展为**星域 / 星系 / 建筑**三选一（与倒货一致）。
- **`MarketLocationSelectorView` 重写为独立控件**（对外只剩一个 TwoWay 绑定 + 一个可选事件）：
  - 原版只是"三页签内容"（星域 / 星系 / 建筑列表），ToggleButton + Popup 包装散在每个宿主页面里（倒货页两份、市场页一份自制的两页签版本）。现在**按钮 + 弹层 + 三页签全部收进控件**：宿主写 `<uc:MarketLocationSelectorView SelectedItem="{Binding Xxx, Mode=TwoWay}" />` 即可；选完自动收起弹层（原先靠各宿主代码后置收起，已删）。
  - 按钮：未选择时显示 `MarketPage_UnSelectedMarket` 占位文案（只读 DP `HasSelection` 切换），已选择显示位置名；弹层尺寸可调（`PopupWidth`/`PopupHeight` DP，默认 320×420）。
  - 外部 `SelectedItem`（含初始绑定回填）会把选中项在对应列表里高亮（先清三处搜索框避免过滤藏掉目标行；`_suppress` 期间不会反向回写）。
  - **建筑列表每次展开弹层都重新读取**（`StructureService.GetMarketStrutures()`）：设置页增删建筑后无需重建页面；建筑列表为空时在页签内显示 `StructuresSettingPage_EmptyTip` 引导（只读 DP `HasNoStructure`）。
  - 控件仍不自设 DataContext（§9 第 21 条）；内部按钮/占位/弹层尺寸全部用 `ElementName` 绑定自身 DP，外部绑定沿用页面上下文。
- **倒货页接入**：`ScalperAnalyseView.xaml` 的两处 `ToggleButton`+`Popup` 包装替换为两个控件实例，代码后置删掉 `OnSourceMarketSelected`/`OnDestinationMarketSelected`（自动收起已内置）；`Setting.SourceMarketLocation/DestinationMarketLocation` 绑定路径不变。
- **市场页接入 + 星系来源**：
  - `MarketPageViewModel` 删掉 `SelectedRegion`/`SelectedStructure`/`SelectedMarketTypeIndex` 与两组过滤集合（星域、建筑列表及其搜索框全在控件内），统一为 `SelectedMarketLocation`（`MarketLocation`，TwoWay）；变更且已选物品时自动重取订单与历史。默认市场仍为伏尔戈（`LoadAsync` 里以不抛异常的方式构造 `MarketLocation`——原 `MarketLocation(MapRegion)` 构造对无星系星域会抛异常）。
  - 取数三分支：星域 = `GetRegionOrdersAsync(typeId, regionId)`；**星系 = 星域订单按 `SystemId` 过滤**（每物品仍只拉该物品的星域页，不必整星域全量）；建筑 = `GetStructureTypeOrdersAsync`（整建筑全量 + TTL 缓存）。历史统计统一用 `location.RegionId`（星系/建筑的"历史"实为所在星域历史，与 WinUI 一致）。
  - `MarketPage.xaml` 选择器区块替换为控件实例；`MarketPage.xaml.cs` 删掉 `OnRegionSelectionChanged`/`OnStructureSelectionChanged`。
- **估价页接入 + 星系/建筑来源**：
  - `AppraisalSetting` 增加 `MarketLocation? Location`（原 `RegionId` 保留为旧字段，`Load()` 时把仅有星域的旧配置迁移为星域位置，新装默认也是伏尔戈）。
  - `AppraisalService` 取数拆出 `FetchOrdersAsync`，按来源三分支：星域 / 星系同市场页口径；**建筑整建筑全量拉一次**（并发按类型逐个调用会同时击穿缓存、重复拉取 7 MB 级数据）后按估价清单里的类型分组。取数前补齐旧缓存位置缺失的 `RegionId`（历史统计与星系过滤依赖它）。历史统计用 `location.RegionId`。
  - `AppraisalResult` 新增 `StructureOrdersFailed`：建筑来源取订单失败（角色无该建筑市场访问权）时与"该建筑没有这些物品的订单"区分开，估价页以右下角警告提示（复用 `MarketPage_StructureOrdersFailed` 文案）。
  - `AppraisalPageViewModel` 删掉星域列表/过滤/选中名一组属性，改为 `SelectedMarketLocation` TwoWay（**换来源不自动重新估价**，按"估价"按钮才取数）；未选来源时点估价给明确提示。
- 构建状态：**0 错误 0 警告**（仅剩既有 `NU1903`）。按 §8 约定未做截图核验。
- 未做/后续：估价换来源后不会自动重算（市场页会自动重取当前物品）；建筑来源首次估价仍受"整建筑全量（7 MB 级、首次较慢）"限制（见 §8 第 9 条）。

---

### 阶段 36：估价的价格参数改为"变更即持久化"

- 现象：估价的价格参数（买卖估价方式、买卖百分比、历史天数、去极值、价格来源）此前**只在点"估价"时才写盘**（`EstimateAsync` 开头的 `SaveSetting()`）。用户改了参数没点估价就切走（页面 `Loaded` 会重新 `Load()` 覆盖内存态）或重启程序，改动全部丢失。
- 修复：`AppraisalPageViewModel` 的全部参数 setter（`SellPriceTypeIndex`/`BuyPriceTypeIndex`/`SellPercent`/`BuyPercent`/`HistoryDay`/`RemoveExtremum`/`SelectedMarketLocation`）改为**值有变化才赋值并立即 `SaveSetting()`**（写 `Configs/AppraisalSetting.json`，每次仅几百字节）。`Init()` 的重载保留——因文件与内存态已一致，重载语义为空操作；`EstimateAsync` 里原有的保存保留（保证估价所用参数已落盘）。
- 持久化范围不含左侧粘贴的物品清单文本（属工作内容而非价格参数，避免设置文件膨胀）。
- 构建状态：**0 错误 0 新警告**。

---

### 阶段 37：估价输入解析不了"物品名\*＋Tab＋数量"格式

- 现象：形如下面的物品清单（部分扫描/导出格式，物品名末尾带 `*` 标记）整页识别不了，全部落入"未识别"提示：
  `导弹精确打击脚本*	2` / `鞭挞轻型导弹*\t100` / `核心扫描探针 I*	1` / `核心扫描探针 I*	9` / `缎光级增强薄荷 - 限量*\t2`
- 根因：Tab 分隔路径把 `*` 留在了物品名里（`导弹精确打击脚本*`），而 SDE 物品名本身不含 `*`——主库精确、本地化库精确、两级模糊全部匹配失败。
- 修复（`AppraisalTextParser`）：解析产物统一过 `CleanName`——去掉名称里的全部 `*`（EVE 物品名不含该字符，位置不限）再 trim；清完为空的行（如整行只有 `*`）直接跳过。用户样例 5 行全部解析正确（同名两行 `核心扫描探针 I` 的 1+9 由服务的按类型聚合合并为 10）。
- 顺带补强名称解析第 4 级兜底（`AppraisalService.ResolveTypes`）：中文输入查主库"英文名 Contains"恒为空，新增**本地化库模糊搜索**（`LocalDbService.SearchInvType`，仅 `NeedLocalization` 时；只接受唯一命中或忽略大小写恰好相等者，与主库模糊同级保护）。解析链现为：主库精确 → 本地化库精确 → 主库模糊 → 本地化库模糊。
- 验证：用独立控制台工程直引 `AppraisalTextParser.cs` 跑用户样例——5 行全部解析为 `名称 × 数量` 且 `*` 已剥离；tab/`x数量`/`数量x`/行尾数字/纯名称等既有格式回归无误；并确认 `zh.db` 里含全部 5 个物品的本地化名（去掉 `*` 后可被本地化库精确匹配）。`dotnet build` 0 错误 0 新警告。
- 未覆盖：`缎光级增强薄荷 - 限量` 这类国服限量物品若在 ESI 星域订单/历史里无报价，估价结果按 0 计并出现在"所选市场无报价"提示里（属数据可用性，非解析问题）。

---

## 5. 角色功能分层设计

```
授权层   CharacterAuthService ── CharacterStore ── AuthHelper / SerenityAuthHelper
              │                        │
              │                        └─ Auth.json / Auth_Serenity.json（按服务器分文件）
              └─ Core.Services.ESIService（授权 URL / 换码 / 刷新）
数据层   CharacterContext（令牌 + EVEStandardAPI）
         CharacterCache（内存 + 磁盘 + TTL）
         CharacterOverviewService / CharacterSkillService / CharacterWalletService
         CharacterCloneService / CharacterIndustryService / CharacterMailService / CharacterContractService
         LocationNameResolver（地点名分流解析：int → IDNameService，结构 → StructureService）
         ↑ 统一入口先 EnsureTokenValidAsync()，各接口独立 try/catch（缺 scope 不致整页失败）
VM 层    CharactersViewModel / CharacterCardViewModel；CharacterWorkspaceViewModel（左栏含 ZKB）；
         各子页 VM（OverviewPageViewModel / SkillPageViewModel / … / IndustryPageViewModel）
UI 层    CharactersShellPage(Tab) ─ CharacterCardsPage
         └ CharacterWorkspacePage（左信息栏 + 右子页 Tab，Frame 承载，子页实现 ICharacterSubPage）
             ├ OverviewPage ├ SkillPage ├ ClonePage ├ WalletPage
             ├ MailPage（→ MailDetailWindow） ├ ContractPage └ IndustryPage
```

缓存 TTL（当前取值）：总览 5 分钟、技能 30 分钟、钱包/邮件标签 2 分钟、克隆 30 分钟、工业 5 分钟。

---

## 6. 问题根因与修复清单

以下均为本次实际遇到并修复的问题（含根因分析）。

| # | 现象 | 根因 | 处理 |
|---|---|---|---|
| 1 | 主题色在切换深浅主题后被重置为系统色 | `ApplicationThemeManager.Apply` 默认 `updateAccent: true` 会用系统色覆盖 | 改为 `updateAccent: false`，切换后重新应用用户保存的强调色 |
| 2 | 托盘"退出"无效，或关闭窗口后进程变孤儿 | `ShutdownMode=OnExplicitShutdown` 下 `Closing` 取消关闭导致进程存活；退出路径被隐藏逻辑拦截 | 引入 `_isShuttingDown` 标志区分"收起到托盘"与"真正退出" |
| 3 | 标题栏图标左侧有空块 | WPF-UI 在 `TitleBar` 绑定后给标题栏加 **35px 左边距**（给汉堡按钮预留） | 见 #4：改为官方布局，让汉堡按钮占用该预留区 |
| 4 | 汉堡按钮不在 logo 左侧 | 标题栏与导航分成两行，预留区落空 | `NavigationView` 铺满整窗 + `TitleBar` 叠加（Grid 无 RowDefinitions，TitleBar 声明在后） |
| 5 | 窗口位置**总是回主屏居中** | ① 位置只在 `Closing` 保存，异常终止（停止调试/任务管理器）不落盘；② `CenterScreen` 按主显示器居中 | ① 移动/缩放/最大化后**防抖自动保存**；② 首次运行改为**居中到鼠标所在显示器**；③ 越界兜底同策略 |
| 6 | 跨不同 DPI 显示器位置错乱 | 用 WPF `Left/Top`（DIP）保存/恢复，DPI 基准不同导致算错 | 改用 Win32 `Get/SetWindowPlacement`（物理像素） |
| 7 | 点击设置子项报"Page 只能具有 window 或 frame 父级" | 把 `Page` 放进 `ContentControl` | 子页宿主改为 `Frame` |
| 8 | 角色标签页一打开就崩溃 | 同类问题：把 `Page` 放进 `TabItem.Content` | 标签内容统一用 `Frame` 托管 |
| 9 | 设置内容显示不全、无法滚动 | WPF-UI 依据 `ScrollViewer.GetCanContentScroll(页面)`（**默认 true**）把页面套进 `DynamicScrollViewer`，页面被无限高度测量 | 页面根元素显式声明 `ScrollViewer.CanContentScroll="False"` |
| 10 | `FrameMargin` 在 XAML 中设置无效（仅热重载生效） | 库在 `OnTitleBarChanged` 中直接赋值覆盖 | 移除 XAML 设置，改在 `Loaded` 与属性变更回调中重新应用 |
| 11 | 添加角色卡片上叠着空的角色信息 | 模板未按 `IsAdd` 门控信息区 | 用 `InverseBoolToVisibility` 分别门控头像与信息块 |
| 12 | `Appearance` 属性报错 | 该属性只存在于 WPF-UI 的 `ui:Button` | 相应按钮改用 `ui:Button` |
| 13 | `TabItem` 无 `Selected` 事件 | 事件在 `TabControl.SelectionChanged` 上 | 懒加载改挂 `TabControl.SelectionChanged` |
| 14 | 导航菜单首末各有一条线 | WPF-UI 默认开启顶/底分隔线 | 显式关闭两个分隔线属性 |
| 15 | 钱包"金额"显示 0.00（疑似映射错误） | **拉原始 ESI 数据核对**：`player_donation` 记录本身 `Amount=0` | 无需修改，映射正确 |
| 16 | 切到"克隆"标签后进程**无任何日志地消失**（事件日志/WER 均无记录） | `Core/Services/IDNameService.cs` 的 `GetByIds(List<long>)` 把 `List<long>` 又转成 `List<long>`（`(long)p`）**调回自己** → 无限递归 → **栈溢出**；栈溢出无法被 `catch` 或 `DispatcherUnhandledException` 捕获，进程立即以 `0xC00000FD` 退出 | `(long)p` 改为 `(int)p`，转发到真正的 `List<int>` 实现；用 `Start-Process -PassThru` 取得退出码 `-1073741571` 完成定性 |
| 17 | 克隆的"家空间站/位置"、邮件"发件人"一律显示原始数字 ID | ESI `/universe/names` 返回**小写**类别（`station`/`character`/`inventory_type`/`solar_system`），而 `Core/DBModels/IdName.cs` 用 `Enum.Parse<CategoryEnum>(category)`（区分大小写、且不认枚举名）解析 PascalCase 枚举名 → **每次调用都抛 `ArgumentException`**，名称解析整体失败、调用方回落为 ID | 按既有的 `[EnumMember(Value=...)]` 取值显式映射 ESI 字符串（并校正了空类别与未知类别的兜底） |
| 18 | 日志反复出现 `{"error":"too few items for 'ids', 'ids' is required"}` | `IDNameService.GetByIds` 在"本地库已全部命中、待查列表为空"时**仍调用 ESI 名称接口**，空 ids 被服务端拒绝 | 待查列表为空时跳过 ESI 调用 |
| 19 | 邮件列表每行只显示"主题 + 发件人"，**看不到日期**，且列表底部出现横向滚动条 | `MailList` 允许横向滚动，行模板在"无限宽度"下测量 → `*` 列不再收缩，右侧 `Auto` 的日期列被推出视口 | `MailList` 显式设置 `ScrollViewer.HorizontalScrollBarVisibility="Disabled"` |
| 20 | 合同起止地点 / 工业设施 / 克隆位置等显示成**不相干的名称**（不是原始 ID，也不是报错，最难发现） | 这些字段可能是玩家结构 ID（约 1e12），而 `IDNameService.GetByIds(List<long>)` 内部 `(int)` 截断后仍能查到**另一个** int ID 的名称 | 新增 `LocationNameResolver` 按 `int.MaxValue` 分流：范围内走 `IDNameService`，超出走 `StructureService`；钱包 `ClientId` 加范围判断。约定见文末同名小节 |
| 21 | 角色卡片头像**恒不显示**（只看到占位首字母） | `BitmapImage`（`Freezable`）冻结前有线程亲缘性：在 UI 线程创建、却在 `Task.Run` 里 `Freeze()` → 工作线程访问 UI 线程的 WIC 状态（`IsDownloading`）抛 `InvalidOperationException`，被 `catch` 吞掉后头像一直为空 | 先 `HttpClient` 取字节，再在**同一线程池线程**上解码 + `Freeze()`，冻结后跨线程绑定（见阶段 11） |
| 22 | 切到"角色"页进程**立刻以 `0xC00000FD` 退出**（故障模块 `dwrite.dll`，无日志） | 卡片模板里的 `ProgressBar`：WPF-UI 为它提供的隐式样式在本页组合下触发栈溢出（实测：带 ProgressBar → 点击角色页必崩；移除即恢复；本项目此前从未用过 ProgressBar，故是首次暴露的库问题） | 新增自绘控件 `Controls/RatioBar.cs`（`OnRender` 画轨道+进度两条圆角矩形），替换卡片页与工作区共 4 处进度条；见 §9 |
| 23 | 角色页改完仍显示**旧界面**（标签只有文字、没有值） | 上一轮多个子任务并发构建，`obj/**/OverviewPage.baml` 停留在旧时间戳（09:30）而 `.xaml` 已是 09:43，MSBuild 未重新编译该标记，程序集里嵌的是旧 BAML | 删除 `obj`/`bin` 全量重建，并用字符串探针核对程序集内新旧 BAML（`OverviewCard` 存在、`QueueEmptyText` 消失）；见 §9 |
| 24 | 切换深/浅色后角色界面**部分颜色/文字不跟随主题** | 四类：① 钱包/合同/工业用标准 `DataGrid`（WPF-UI 的主题样式只作用于 `ui:DataGrid`），取的是系统主题色；② `SkillPageViewModel` 加载时 `TryFindResource` 抓取 Brush 实例缓存进条目，切换后不更新；③ `WalletPageViewModel` 用 `static readonly Brushes.SeaGreen/OrangeRed`；④ 约 20 处字面量颜色（`#4CD1AC`/`OrangeRed`/`MediumSeaGreen`/`White`/`#CC3CB371`） | 表格改 `ui:DataGrid`；语义色改 `SystemFillColorSuccess/Caution/CriticalBrush` 与 `TextOnAccentFillColorPrimaryBrush`；技能状态图标改 `DataTrigger` + `DynamicResource` 并删掉 VM 缓存的 Brush；钱包金额改布尔标记 + `DataTrigger`；`RatioBar.BarBrush` 默认值改 `null`。见阶段 13 |
| 25 | **国服添加角色走不通**：只弹"粘贴 code"输入框、从不打开授权页，切到国服后换码仍用国际服凭据，粘贴整条网址也解析失败 | ① 国服分支缺"登出/打开授权页/失败提示"；② `ESIService.Current` 单例把 SSO 与客户端凭据在构造时固化，设置页切服只改 `Config.DefaultGameServer` 且提示"需要重启"；③ `CompleteSerenityAsync` 把整串文本当 code 传给换码接口（WinUI 的提示恰恰是让用户复制整条网址） | 见阶段 14：Core 加 `ESIService.Reset()`；`CoreInitializer.SwitchGameServer` 立即切服（重灌凭据 + 重建单例 + 换角色文件 + 触发事件）；新增 `SerenityAuthWindow` 四步向导；`ExtractAuthorizationCode` 兼容网址与纯 code |
| 26 | 深色主题下角色卡片里**"技能队列"、`0%`、`2/2` 等小字发黑** | 卡片外层是 `Style="{StaticResource SettingsRowButton}"`——**显式样式替换了 WPF-UI 的隐式 Button 样式，`Foreground` 随之退回控件默认值（系统色）**；卡片内未显式写 Foreground 的 TextBlock 继承了这个系统色 | `SettingsRowButton` 补 `Foreground="{DynamicResource TextFillColorPrimaryBrush}"`（治本），并把卡片/工作区技能队列块的标签与数值逐项补上主题色。见阶段 15 |
| 27 | 钱包 / 合同 / 工业的表格**变回 WPF 原生外观**（不跟主题） | 三个页面用本地 `*GridStyle`（`TargetType="DataGrid"`）通过 `Style=` 应用 —— **显式样式整体顶掉了 WPF-UI 的隐式 DataGrid 样式**，Fluent 模板（列头/选中/配色）全丢；元素虽已改成 `ui:DataGrid` 也不起作用 | 本地样式改为 `TargetType="{x:Type ui:DataGrid}"` + `BasedOn="{StaticResource {x:Type ui:DataGrid}}"`；`*CellStyle` 补主题色 Foreground。见阶段 16 |
| 28 | 邮件"全部邮件"切走再切回后**列表为空** | "全部邮件"是 `LabelId=0` 的伪标签：首次加载走 `SelectedLabelId`（0 → null=不过滤）所以正常；标签 `SelectionChanged` 却直接传 `label.LabelId`=0 → `HasValue=true` → ESI 按"标签 0"过滤 → 恒为空 | `SelectionChanged` 改传 `SelectedLabelId`；`LoadHeadersAsync` 内部再把 `<=0` 归一化为 `null`。见阶段 19 |
| 29 | 倒货页 XAML 编译失败：`MC3072 ... 命名空间中不存在属性"Header"` | WPF 的 `ComboBox` 与 WPF-UI 的 `ui:NumberBox` **都没有 `Header` 属性**（WinUI 版有），照搬 WinUI 标记即编译不过；且报错只指向第一个，逐条改才会依次暴露 | 每个控件前加 `TextBlock` 标签（沿用设置页的"标签 + 控件"风格） |
| 30 | `NumberBox` 由 `Header` 改前置标签后报 `MC3089 ... 已具有子级，无法添加...` | WPF `Expander` **只接受一个子元素**；原先是单个 `NumberBox`，加了标签就变成两个 | 标签与控件用 `StackPanel` 包起来 |
| 31 | `ScalperShoppingItemEditWindow` / `ScalperItemDetailWindow` 编译失败（`CS1501` / `CS0266` / `CS0246`） | ① WPF-UI `NumberBox.Value` 是 **`double?`**，而 `ScalperShoppingItem` 的价格/数量是 `double`，`ToString("N2")` 也不接受可空；② 详情窗文件缺 `using System.IO`（`MemoryStream`） | 取值处统一 `?? 0`；补 `using` |
| 32 | 倒货"进阶设置"里的 `Expander`（连带 `ComboBox`/`NumberBox`）变成 WPF 原生外观 | 页面 `UserControl.Resources` 里放了三个**不带 `x:Key` 的本地隐式样式**（只为设 `Margin` 等布局属性）——**无 key 的本地隐式样式也会整体替换 WPF-UI 的库样式**，控件连模板一起退回原生 | 删掉本地隐式样式，布局属性直接写元素上（照技能页 `Expander` 的做法）。见阶段 31 |
| 33 | 记录页选中已保存的记录后，右侧"记录详细"始终空白 | 记录 `ListBox` 只绑了 `ItemsSource`，**漏绑 `SelectedItem`**；加载逻辑挂在 VM `SelectedFile` 的 setter 上，选中项从未回写 VM | 补 `SelectedItem="{Binding SelectedFile, Mode=TwoWay}"`。见阶段 31 |
| 34 | 市场选择器"星系"页签列表为空 | 移植时擅自"优化"了参照实现：WinUI 直接列出全部星系，而 WPF 版写成**搜索词为空就返回空集**，不输入关键字时恒空 | 默认列出全部（`!IsSpecial()`、按 ID 排序），过滤改用 `ICollectionView.Filter + Refresh()`。见阶段 33 |

---

## 7. 验证记录

| 模块 | 验证方式 | 结果 |
|---|---|---|
| 设置：壳页 / 一般子页 / 按键列表 | **实机截图** | 通过（含滚动条出现、子页可导航） |
| 导航框架 / 占位页 | 实机截图 | 通过 |
| 窗口位置记忆 | 脚本：移动→关闭/强杀→重启 | 通过（普通与最大化、跨屏均正确恢复） |
| 托盘图标与通知 | 运行时自检 | `IsAvailable=True`、图标实例已创建、`ShowNotification` 无异常 |
| 设置子页构造 | 反射自检（10 页） | 全部 OK |
| 角色授权 | 真实账号 | 令牌过期后**自动刷新成功**；受授权保护的 ESI 调用返回真实数据（钱包 116,284,940.8 ISK；LP 558,672） |
| 角色数据服务与缓存 | 运行时自检 | 总览/技能（23,038,797 SP、22 技能组）/钱包流水均取到真实数据；缓存命中 0ms；磁盘缓存已生成 |
| 角色壳 + 卡片页 + 工作区 + 总览/技能/钱包 | **实机截图（4 屏）** | 通过 |
| 角色：克隆 | **实机截图**（含展开的"当前克隆"植入体列表） | 通过（修复 #16/#17 后）：家空间站与克隆位置显示**地名**，5 个植入体名称正确 |
| 角色：邮件列表 | **实机截图** | 通过（修复 #17/#19 后）：每行"日期 + 发件人名称 + 主题"三要素齐全，横向滚动条消失 |
| 角色：邮件详情窗口 | **窗口枚举 + 实机截图** | 通过：`HtmlPanel` 正确渲染中文正文、**无 HTML 标签泄漏**；发件人显示名称，收件人仍为 ID（见 §8） |
| 角色：合同 | **实机截图** | 通过：角色合同/军团合同 子页签、7 列表头、分页控件齐全；该账号无合同，列表为空态 |
| 角色：工业 | **实机截图** | 通过：7 列表头（蓝图/产品/状态/数量/花费/地点/结束）+ 刷新按钮齐全；该账号无工业任务，列表为空态 |
| 克隆页崩溃定性 | `Start-Process -PassThru` 取退出码 | `-1073741571` = `0xC00000FD`（栈溢出），修复后连续多轮切换页面进程存活 |

**阶段 12（角色 UI 全量照搬）实机核验**——构建 0 错误后启动真实账号，逐页点击并截窗口图：

| 页面 | 结果 |
|---|---|
| 卡片页 | 通过：`200×305` 卡片显示头像 / QEDSD / SP 23,038,797 / ISK 116.28M ISK / 离线时长 `4mo 19.7d` / 队列 `已暂停` `2/2` + `···` 移除菜单 + 添加卡 |
| 工作区左栏 | 通过：出生 2017.03.05 13:18、SP 23,038,797 (282,450)、LP 558,672、安全等级 5.0、个人钱包 116,284,940.80、公司钱包 425,434,350.04、势力 `I Know Nothing`；技能队列卡；**ZKB 卡**（危险系数/抱团概率条 + 击杀/损失 + ZKB 按钮） |
| 总览 | 通过：军团卡（徽标 + `I Know Nothing` + `I K N`）、舰船+地点卡（`吉他 IV - 卫星 4 - 加达里海军 组装车间 (0.95 吉他)` / `太空舱 - QEDSD (太空舱)`）、在线卡（`最近上线 4mo 19.7d`、上线次数 0）、技能队列 2 条（已暂停） |
| 技能 | 通过：技能点 23,038,797 / 未分配 282,450、队列 2 条、21 个技能组 `Expander` + 搜索框 |
| 克隆 | 通过：基地 `Jita IV - Moon 4 - Caldari Business Tribunal Bureau Offices`、克隆数量 2、上次远克 2023-08-18 23:32、当前激活克隆 5 个脑插、跳跃克隆展开项 |
| 钱包 | 通过：四页签 + 列（时间/合计/钱包余额/描述/类型/原因）+ 分页；真实流水行（`player_donation`，合计 0.00 绿、余额 116,284,940.80） |
| 邮件 | 通过：标签列表（全部邮件/Inbox/Sent/[Corp]/[Alliance]）+ 列表（主题/发件人/日期） |
| 邮件详情窗口 | 通过：独立窗口渲染发件人（头像+名称+日期）、收件人 `QEDSD`、标签 `Inbox`、`HtmlPanel` 中文正文 |
| 合同 | 通过：个人/军团页签 + 16 列头 + 分页（该账号无合同，空态） |
| 工业 | 通过：10 列头（蓝图/产品/状态/流程/成功率/开始时间/项目周期/完成时间/项目费用/位置）（该账号无任务，空态） |

核验全程进程未再出现 `0xC00000FD`；验证后已关闭应用进程。

**阶段 28（倒货）实机 smoke 核验**（应用目录 `Log/20260911.txt` 无新增内容 = 无异常；`ScalperSetting.json` 未被写 = 未污染用户配置）：

| 核验点 | 方式 | 结果 |
|---|---|---|
| 壳页三页签 + 三个子视图渲染 | 实机截图 | 通过：倒货 / 购物车 / 记录；购物车统计（回报率/净利润/本金/体积/ISK-跳/ISK-体积/数量）与记录页（所有记录 / 记录详细）均在 |
| 物品多选树构建 + 存档目标物品数量 | 实机截图 | 通过：根分组树已建（个性化 / 建筑改装件 / 舰船涂装 / … / 蓝图和反应），搜索框与目标物品数量 = 937（沿用存档） |
| 树勾选联动（三态 + TwoWay） | UIA `TogglePattern` 勾选根分组 | 通过：目标物品 **937 → 2,816**，根结点复选框显示已选 |
| 源/目的市场从存档读出 | 实机截图 | 通过：源=伏尔戈、目的=多美 |
| 市场位置选择器弹层 | 窗口枚举 + UIA | 通过：`Popup` 宿主为 `TOPMOST,TOOLWINDOW` 320x424，内含 **星域 / 星系 / 建筑** 三页签与星域列表；选中"伏尔戈"后弹层自动关闭、按钮文本更新 |
| 未取订单时点"分析推荐" | 实机截图 | 通过：显示红色"请先获取订单"（`Message` + `IsMessageError` 配色生效） |

未覆盖：`获取订单` → `分析推荐` 的完整数据链路（源=伏尔戈整星域全量订单 + 全物品历史，分钟级、大量 ESI 调用，本轮未触发）；物品详情窗（需先有分析结果）；购物车复制/粘贴/保存与编辑对话框的实际交互。

构建状态：**0 错误**，剩 3 个既有警告（`NU1903`：Core 传递依赖 `SQLitePCLRaw.lib.e_sqlite3 2.1.10` 的漏洞通告，与本次改造无关）。

---

## 8. 已知限制与待办

### 功能降级（为保证可编译而暂缓，补起来各需数分钟）
1. **邮件正文链接不可点击** —— `HtmlPanel.LinkClicked` 委托签名与本项目用法不匹配（阶段 8 起遗留）。
2. **合同详情窗口未实现**（WinUI 点击合同行会打开详情；WPF 侧只有列表 + 分页）。
3. **Syncfusion 的表格能力未平替**：WinUI 的 `SfDataGrid` 支持分组拖放区、列筛选、**分组合计行**（钱包"分组合计 ISK"），WPF 标准 `DataGrid` 只保留排序/列宽/列重排。
4. **等待遮罩/应用内通知只接入了倒货页**：全局的 `WaitingOverlay` + 右下角通知栈（`PageNotifyService`，阶段 32）已可用，倒货的取订单/分析已接入；市场/订单/角色等页面仍是静默异步加载 + 页内文字提示，可按需逐步换成 `ShowWaiting` / `Success` / `Error`。
5. **结构位置名的解析范围**：结构名称的 ESI 解析已可用（`StructureService.QueryStructureAsync`，阶段 27），但**订单/合同富化里的结构位置名仍只查本地列表**（`LocationNameResolver` 没有角色上下文），解析不到时回退显示原始 ID。
6. **ZKB 卡数据可能为 0**：ZKB 是第三方服务，同一角色两次启动取到的统计会不同（服务端按周期归零/波动），非本地缺陷。
7. 技能组"组内技能个数"口径略窄：WinUI 显示该组**全部**技能数，WPF 显示的是本地库+账号都命中的技能数（服务 DTO 限制）。
8. 技能名 ToolTip（WinUI 用 `InvType.Description`）未实现，等级/技能点提示已有。
9. **建筑（结构）市场的限制**：建筑订单需要角色授权（`esi-markets.structure_markets.v1`，且该角色对该建筑有市场访问权），失败时页面给提示；"建筑历史"实为**该建筑所在星域**的历史（与 WinUI 一致）；建筑订单是**整建筑全量**拉取（实测某公共市场建筑 ≈ 7 MB），按 TTL 缓存，首次较慢。
10. **市场表格未按安全等级着色**：WinUI 用 `SystemSecurityCellStyleSelector` 给 `Security` 单元格上色，WPF 侧是普通数字。
11. **市场树的选中态是 WPF 默认样式**（未写 `TreeViewItem` 自定义模板）；左侧三个 `ListBox` 未显式设 `ItemContainerStyle`，走 WPF-UI 隐式样式。
12. **市场物品树为一次性全量构建**：`InvTypeService.QueryMarketTypesAsync()` 加载全部市场物品（与 WinUI 相同），首次进入市场页有短暂耗时；树节点已按名称排序。
13. **市场刷新语义**：星域订单走 ESI 实时（无缓存），历史统计按设置 TTL 走磁盘缓存；页面"刷新"按钮对历史传 `forceRefresh`（WinUI 版没有刷新按钮，只能清缓存）。
14. **订单页两列缺真实数据核验**：核验账号当前没有任何个人/军团未结订单，因此「订单状态」「与市场价差」两列只走通了空态（算法是 Core `StatusOrder`，与 WinUI 共用未改）。
15. **订单页无分组 / 列过滤 / 分组合计**：WinUI 的这些能力来自 `SfDataGrid`，WPF `DataGrid` 只保留排序/列宽/列重排（与合同/钱包页一致）。
16. **订单页两个倒货右键动作未接线**（"添加到倒货排除列表"、"从购物车减去数量"）：依赖的服务层已就绪（`BusinessService.AddToFilter` / `NotifyTypeCountChanged` / `ShoppingCart`），但订单页的右键菜单尚未挂上（阶段 28 遗留）。
17. **订单页过期时间格式**：直接显示 `Order.RemainTime`（`dd.hh:mm:ss`），未做 WinUI 那种本地化"天/小时/分钟/秒"拼接。
18. **倒货链路已端到端跑通**（阶段 32 补测）：源=伏尔戈（411 页并发抓取）→ 全物品历史 → 计算引擎，得到 528 条推荐结果，推荐度/回报率/净利润等列数值合理；取数期间有全屏等待遮罩与实时进度、完成后右下角成功通知，取消则中止并提示。计算引擎为 WinUI `Cal*` 的逐条移植，数值口径仍以 WinUI 为准（未做逐项对拍）。
19. **倒货物品详情窗未实机打开**（需先有分析结果）；窗内图表/Git 表格沿用市场页同一套 LiveCharts 用法。
20. **倒货购物车的复制/粘贴/保存与编辑对话框未实机验证**：粘贴解析假定剪贴板每行逗号分隔且 ≥24 列（第 1 列物品 ID、第 14 列剩余数量），与 WinUI 相同。
21. **倒货排除清单的入口**目前只有倒货页自身的"排除列表"页签（手动移除）；市场/订单页的"加入排除列表"右键尚未接线（见 16）。
22. **倒货切换"星系"市场仍会先拉整星域**（`GetSolarSystemOrdersAsync` 内部复用 `GetAllRegionOrdersAsync`，与 WinUI 一致），因此星系口径并不比星域便宜。
23. **倒货市场树分组的复选框语义**：WPF 三态复选框点击循环 `Off → On → Indeterminate`，与 Core `SelectableMarketItem`（WinUI 共用）的联动逻辑一致——分组停在 `Indeterminate` 时不再回写子项；该行为在 WinUI 侧同样存在，未改 Core。

### 本次核验结论（阶段 9）
- 克隆 / 邮件（含详情窗 HTML 渲染）/ 合同 / 工业 **已完成逐页实机截图核验**，结论见 §7。
- 核验方式：实机点击逐页截图目检（`EnumWindows` + `PrintWindow` 抓取窗口，截图需先开启 PerMonitorV2 DPI 感知）；截图与抓图脚本为一次性产物，已清理。
- 合同 / 工业两页在该账号下**列表为空属正常空态**（账号无对应数据），空态下不触发名称解析，因此这两页的"名称解析"路径未被覆盖。

### 计划内未做
- 角色卡片**拖拽排序**（WinUI 用 `GridView` 的 `CanReorderItems`；WPF 的 `ItemsControl`+`WrapPanel` 需自行实现拖放）——排序数据层 `CharacterStore.Move` 已具备。
- 合同**详情窗口**；钱包 `SfDataGrid` 的分组/筛选/分组合计（见上「功能降级」3）。
- **商业其余子项**：~~倒货~~（阶段 28 已完成）、~~估价~~（阶段 34 已完成：本地解析 + ESI 取价 + 可配置估价口径与百分比）。
- **星域市场里合并结构卖单**（WinUI 的 `MarketSkipStructure=false` 路径：遍历该星域的建筑逐个拉订单再按 `OrderId` 去重合并）：WPF 目前恒按"跳过结构"处理——每个建筑要全量拉一次（7 MB 级），开启后首次会非常慢，需要时再做。
- 结构详情窗口、"按角色搜索建筑"（`esi-search.search_structures.v1`）。
- 国服授权的**自动化**：国服没有回调地址，只能手动粘贴 code（与 WinUI 一致）；若日后拿到可用的回调/内部协议，可把 `SerenityAuthWindow` 退化为"打开授权页 + 等待回调"。
- 玩家建筑页的"按角色搜索"（依赖 OAuth 授权链路打通后的 ESI 结构查询）。
- **结构名称解析**：把 WinUI `StructureService.QueryStructureAsync`（`Universe.GetStructureInfoAsync` + `Structures.json` 缓存）移植到 WPF 版；这是"structure id 请走 StructureService"约定在 WPF 侧落地的前提（详见文末同名小节）。
- ZKB 页本身仍是导航占位（工作区 ZKB 卡的"ZKB"按钮跳到该占位页）。
- 软件更新页"安装"仍交外部 Updater。

### 环境/协作注意
- **代码调整不需要截图验证**：改动完成、构建通过后直接说明结果即可，由使用者自行查看界面效果。
- 设置文件与 WinUI 版**共用** `Configs/settings.json`：两版同时运行会互相覆盖，迁移完成后建议只保留 WPF 版。
- 运行时资源以链接方式引用 WinUI 项目的 `Resources/*`：**若删除 WinUI 项目，需改为复制或迁移资源**。
- **构建前必须先退出应用**：应用运行时锁定输出目录的 `*.dll`/`*.exe`（以及 `Resources/Database/*.db`），`Rebuild` 会以 `MSB3061` 警告跳过复制，导致"改了代码但运行的是旧程序集"（本次排查名称解析时踩到）。改动 Core 后若行为未变，先核对 `bin\...\TheGuideToTheNewEden.Core.dll` 的时间戳。
- 本机显示器为 2560x1440 @125%：未声明 DPI 感知的进程（如默认的 PowerShell）拿到的窗口坐标是按 1.25 缩放后的 **DIP**，直接当物理像素用会抓错区域或"看起来右侧被裁"；需要截图时先开启 PerMonitorV2 DPI 感知。
- **structure id 请走 `StructureService`**：`Core/Services/IDNameService` 的 ID 是 `int`，结构（structure）ID 约 1e12 会被**静默截断**并解析出错误名称。详见文末「结构（structure）ID 解析约定」。
- **改完 XAML 界面没变 → 先怀疑 BAML 陈旧**：并行构建/中断过的构建会让 `obj` 里的 `.baml` 落后于 `.xaml`，程序集里嵌旧标记。删 `obj` 重建即可（详见 §9 第 16 条）。
- **不要使用 `ProgressBar`**：WPF-UI 隐式样式下会栈溢出，用 `Controls/RatioBar.cs`（详见 §9 第 17 条）。

---

## 9. WPF / WPF-UI 踩坑备忘

1. **`Page` 的父级限制**：`Page` 只能由 `Window` / `Frame`（或 `NavigationWindow`）承载。放进 `ContentControl` / `TabItem.Content` 会抛"Page 只能具有 window 或 frame 父级"。→ 统一用 `Frame` 托管（`NavigationUIVisibility=Hidden`）。
2. **`ScrollViewer.CanContentScroll` 决定页面能否自滚**：WPF-UI 导航完成后按该附加属性决定是否给页面套 `DynamicScrollViewer`；**默认 true** 会让页面被无限高度测量、内部滚动条失效。自管滚动的页面必须显式设为 `False`。
   - 该属性的元数据是 `Inherits=False`（已实测），**不会**从页面传给内层列表；因此内层 `ListBox`/`DataGrid` 的虚拟化不受页面根节点这句影响（长列表若担心受外层影响，可显式 `CanContentScroll="True"` + `VirtualizingStackPanel.IsVirtualizing="True"` 兜底）。
3. **`FrameMargin` 会被库覆盖**：绑定 `TitleBar` 后库会重设 `FrameMargin`；需在 `Loaded`/属性变更后重新应用。
4. **`Appearance` 属于 `ui:Button`**：标准 `Button` 没有该属性。
5. **`TabItem` 没有 `Selected` 事件**：用 `TabControl.SelectionChanged`。
6. **WPF 没有 `{ThemeResource}`**：WinUI 的 `{ThemeResource}` 对应 WPF 的 `{DynamicResource}`；主题键名需自行对齐。
7. **`CenterScreen` 按主显示器居中**：多显示器应用应自行定位。
8. **DPI 与坐标**：跨不同缩放比例的显示器，用 WPF `Left/Top`（DIP）持久化会算错；应用 Win32 物理像素 API。
9. **NuGet 目标框架陷阱**：包若只提供 `net10.0`/`net462` 而项目为 net9，会**静默回退到 .NET Framework**（`NU1701`，可能运行期异常）。选包前先核对包内 `lib/` 的目标框架。
10. **第三方库的 `[NotNull]` 元数据**易触发 `CS8622` 噪音告警，可局部 `#pragma warning disable`。
11. **`ListBox` 的横向滚动会让行模板"无限宽"测量**：一旦允许横向滚动，行内 `*` 列不再收缩，右侧 `Auto` 列（如日期）会被推出视口且看不到滚动条提示。列表类控件若要"右侧固定列"，显式 `ScrollViewer.HorizontalScrollBarVisibility="Disabled"`。
12. **栈溢出是"静默死亡"**：`StackOverflowException` 无法被 `try/catch`、`DispatcherUnhandledException` 或 `AppDomain.UnhandledException` 捕获，进程直接退出，事件日志/WER 可能什么都留不下。遇到"进程凭空消失"时，用 `Start-Process -PassThru` + `WaitForExit()` 读 `ExitCode`（`0xC00000FD` = 栈溢出，`0xC0000005` = 访问冲突）比翻日志更快定性。
13. **C# 重载陷阱**：`List<long>` 与 `List<int>` 两个重载并存时，`ids.Select(p => (long)p).ToList()` 会解析回**自身**（无限递归）。做"薄转发"重载时要确认目标类型真的不同。
14. **`long` → `int` 截断是静默的**：EVE 的结构（structure）ID 约 1e12，`(int)` 转换既不报错也不溢出异常，只会解析出**另一个 ID 的名称**。凡 ID 可能 ≥ `int.MaxValue` 的场合都要用 `long` 通路（结构名称走 `StructureService`，见文末约定小节）。
15. **`Freezable` 的线程亲缘性**：`BitmapImage`/`ImageSource` 等在 `Freeze()` 之前归属创建它的线程，`Freeze()` 必须与创建同线程，否则访问内部状态（`IsDownloading` 等）会抛「调用线程无法访问此对象」。要跨线程使用图片：**在同一工作线程上创建 + 冻结**（或用 `HttpClient` 取字节后在池线程解码），冻结后即可安全绑定；不要"UI 线程创建、后台线程 `Freeze`"。这类异常常被 `catch` 吞掉，表现为"图/头像永远不显示"。
16. **并发构建会让 XAML 的 `.baml` 变陈旧**：若在同一项目上并行跑多个 `dotnet build`，`obj/**/<Page>.baml` 可能停留在旧时间戳（本次实测：`.xaml` 已是 09:43，`.baml` 仍是 09:30，MSBuild 没重编该标记，程序集里嵌的是**旧 BAML**），现象是"代码明明改了、界面纹丝不动"。排查手段：比对 `Views/**/*.xaml` 与 `obj/**/*.baml` 的时间戳；确认手段：在生成的 `.dll` 里用字符串探针找新/旧 XAML 里独有的名字（如新加的 `x:Key`/样式名对旧 `x:Name`）。修复：删掉 `obj`（必要时连 `bin`）全量重建。**多任务并行改同一项目时，收尾务必做一次干净重建。**
17. **`ProgressBar` 在 WPF-UI 隐式样式下会栈溢出**：本项目实测——卡片模板里出现标准 `ProgressBar`（带 `Height`/`Background`/`Foreground`/`Value` 绑定），切到该页进程即以 `0xC00000FD` 退出，故障模块是 `dwrite.dll`（栈耗尽发生在文本/渲染栈里），移除后立即恢复。WPF-UI 会为 `ProgressBar` 提供隐式样式，本项目此前从未用过该控件，属首次暴露。**细进度条建议自绘**（见 `Controls/RatioBar.cs`：`OnRender` 画两条圆角矩形，无模板无样式，行为可控）。**需要"转圈"这种不确定进度指示时，用旋转的 `ui:SymbolIcon` + `RotateTransform` 动画**（见 `Controls/WaitingOverlay.xaml`：`ArrowSync24` 字形 + `DoubleAnimation` 转 `Angle`），同样绕开 `ProgressBar`/`ProgressRing` 的库模板风险。
18. **"颜色不跟随主题"有五种来源**，改动界面时对照排查：
    - 用**标准控件**而非 WPF-UI 版本：WPF-UI 的主题样式只作用于自己的子类（`ui:DataGrid`、`ui:Button`…）。标准 `DataGrid`/`ComboBox` 取的是**系统主题色**，与应用主题无关。
    - **在代码里抓资源**：`TryFindResource("...Brush") as Brush` 拿到的是当前主题的**实例**，主题切换后 WPF-UI 会换掉资源字典里的对象，缓存的实例不会更新。要么在 XAML 里用 `{DynamicResource}`，要么用 `DataTrigger` + `DynamicResource` 组合。
    - **字面量颜色**（`#RRGGBB`、`OrangeRed`、`White`…）在两种主题下恒定；语义色请用 `SystemFillColorSuccessBrush` / `SystemFillColorCautionBrush` / `SystemFillColorCriticalBrush` / `TextOnAccentFillColorPrimaryBrush`。
    - **`static readonly Brush` 字段**（如 VM 里的 `Brushes.SeaGreen`）同样是恒定色；改用布尔语义标记，把颜色交给 XAML 的 `DataTrigger`。
    - **显式 `Style` 会整体替换隐式样式**：给控件套自定义样式（带自己的 `Template`，如 `SettingsRowButton`，或只是覆盖几个属性，如三个表格页的 `*GridStyle`）时，WPF-UI 隐式样式里的 `Foreground`、模板一并失效 → 文字退回**控件默认的系统色**（深色下变黑）、控件退回**原生模板**（表格变原生外观）。两条出路：样式里自己补 `Foreground`/模板，或 `BasedOn` 隐式样式。`BasedOn` 的写法要求 **`TargetType` 必须与基样式一致**：WPF-UI 的子类控件（`ui:DataGrid`）用 `TargetType="{x:Type ui:DataGrid}" BasedOn="{StaticResource {x:Type ui:DataGrid}}"`；而 `{x:Type ListBoxItem}` 这类**未验证存在**的隐式样式不要写（找不到会在页面构造时抛 `XamlParseException`），改为在行/单元格模板里显式给颜色。
      **同样适用于"不带 `x:Key` 的本地隐式样式"**：在页面/控件的 `Resources` 里写 `<Style TargetType="Expander">`（哪怕只是为了设一个 `Margin`），也会把库的隐式样式整体顶掉（阶段 31 实测：倒货进阶设置的 `Expander`/`ComboBox`/`NumberBox` 一起退回原生外观）。**只想加布局属性就直接写在元素上**（技能页 `Expander` 就是这么做的：`HorizontalAlignment`/`HorizontalContentAlignment`/`Margin`/`Background` 逐项内联），本地 `Resources` 里只放带 `x:Key` 的样式。
    另：VM 里**缓存本地化字符串**（`TryFindResource(key) as string`）有同类问题——切语言后不会更新，页面需在 `LanguageChanged` 后重建/刷新这些文本。

19. **LiveCharts / SkiaSharp 要求 TFM 带平台版本，且图表配色不会自动跟随主题**：`LiveChartsCore.SkiaSharpView.WPF 2.0.5` 依赖 `SkiaSharp.Views.WPF 3.119.0`，后者的资产只有 `net462` / `net8.0-windows10.0.19041`；项目若写 `net10.0-windows`（隐含 `TargetPlatformVersion=7.0`）就会回退到 .NET Framework 资产（`NU1701`）或报 `NU1202`，把 TFM 写成 **`net10.0-windows10.0.19041`** 即可（阶段 25 已改）。另外两点：
    - LiveCharts 的 WPF 实现里 `IChartView.IsDarkMode` **恒为 `false`**，默认主题不感知应用深浅色；
    - `Paint`/`SolidColorPaint` 只接受 `SkiaSharp.SKColor`（XAML 里的 `Stroke="#RRGGBB"` 靠内置转换器，**接不了 `DynamicResource` 的 Brush**）。
    因此图表颜色必须在代码里从主题 Brush 转成 `SKColor`，并在主题切换时重新赋值——本项目做法见 `ThemeService.ThemeChanged` 与 `MarketPageViewModel.ApplyThemeColors()`。

20. **封装的工具窗口不要再自绘标题栏**：WPF-UI 的 `ui:TitleBar` 已经提供 `Title` / `Icon` / `ShowMinimize` / `ShowMaximize` / `ShowClose` / `CanMaximize` / `TrailingContent`（右侧自定义内容，本项目用来放"置顶"图钉按钮）以及拖拽、双击最大化等行为；`ToolWindow` 只做"属性 → 标题栏"的转发即可（见 `Views/Windows/ToolWindow.xaml.cs`）。两点细节：
    - 窗口内容用 **`Frame`** 承载：`Page` 只能由 `Window`/`Frame` 承载（§9 第 1 条），传 Page 时若用 `ContentPresenter`/`ContentControl` 会抛异常；Frame 同时也能放 UserControl。
    - `SymbolRegular` 的枚举名写错会导致 **XAML 解析失败（整个窗口都打不开）**；新增图标前先确认名字存在（可用一个引用 WPF-UI 的临时控制台工程 `Enum.GetNames(typeof(SymbolRegular))` 反射核对，本项目已确认 `Pin24`/`PinOff24`、`ControlAppearance.Transparent` 可用）。

21. **从 WinUI 照搬标记时的三处结构性差异**（阶段 28 踩完）：
    - **`Header` 不是通用属性**：WinUI 的 `ComboBox` / `NumberBox` 有 `Header`，WPF 的 `ComboBox` 与 WPF-UI 的 `ui:NumberBox` **都没有**。照搬会得到 `MC3072 ... 命名空间中不存在属性"Header"`，且 XAML 编译器一次只报第一个。改为控件前放 `TextBlock` 标签。
    - **`Expander` 只接受一个子元素**：WinUI 的 `Expander` 可放多个子元素，WPF 的不能（`MC3089 已具有子级，无法添加`）。多子元素要自己包一层 `StackPanel`。
    - **WPF-UI `NumberBox.Value` 是可空 `double?`**：与领域模型的 `double` 交互处要么 `?? 0`，要么先把值取出再做运算；`double?` 上没有 `ToString("N2")` 重载。
    另：需要 `DataContext` 的自定义 `UserControl`（如树/选择器）**不要在构造函数里 `DataContext = this`** —— 那会切断外部对它自身 DP 的绑定（`{Binding TargetMarketTypes}` 会去控件上找属性）。正确做法是把内部列表直接赋给内部控件的 `ItemsSource`，让外部绑定继续沿用页面 DataContext。

22. **核对 JSON 缓存要用解析器，别用正则**：阶段 30 核验时用 `\"TypeId\":645` 数某物品的订单条数得到 9，与页面显示的 2 不符，一度怀疑并发翻页丢数据；改用 `ConvertFrom-Json` 逐条解析后确认只有 2 条——那个正则没有锚定结尾，把 `\"TypeId\":64500` 之类的类型号也算进去了。**凡是"数字是否相等/包含"的比对，正则里的数字后面要补边界（`\"TypeId\":645,` 或 `[^0-9]`），或者干脆解析**。

---

## 10. 主要文件清单

### 项目与服务
```
TheGuideToTheNewEden.WPF.csproj        包/引用/链接资源
App.xaml(.cs)                          启动、单实例、主题/语言/设置初始化
app.manifest                           PerMonitorV2 DPI
Services/CoreInitializer.cs            Core 路径/DB/ESI 凭据初始化
Services/SettingsService.cs            统一设置存储（共用 settings.json）
Services/ThemeService.cs               浅/深主题 + 强调色
Services/LanguageService.cs            运行时语言切换
Services/NotificationService.cs        托盘通知
Services/PageNotifyService.cs          页面级等待遮罩 + 右下角通知的统一入口（ShowWaiting/Success/Error…，阶段 32）
Services/StructureService.cs           市场结构列表 + 结构名称 ESI 解析（QueryStructureAsync）
Helpers/AuthHelper.cs                  协议注册/回调等待/回调解析
Helpers/SerenityAuthHelper.cs          国服授权地址
Helpers/WindowPlacementHelper.cs       Win32 窗口位置（物理像素）
Converters/InverseBooleanToVisibilityConverter.cs
```

### 设置模块
```
Controls/SettingCard.cs + SettingCardStyles.xaml     Win11 设置行控件与样式
Controls/PagerControl.xaml(.cs)                      通用分页
Views/Pages/SettingsPage.xaml(.cs)                   设置壳页（分类列表 + 子页 Frame）
Views/Pages/Settings/{General,GameLog,Market,Structures,ESIScope,ZKB,KeyboardList,Test,Update}*.xaml(.cs)
Services/Settings/*.cs                               10 个设置子服务
```

### 角色模块
```
Services/Characters/CharacterAuthService.cs   授权编排
Services/Characters/CharacterStore.cs         令牌存储
Services/Characters/CharacterContext.cs       角色上下文
Services/Characters/CharacterCache.cs         缓存层
Services/Characters/PagedResult.cs            分页契约
Services/Characters/Character{Overview,Skill,Wallet,Clone,Industry,Mail,Contract}Service.cs
Services/Characters/CharacterOverviewExtraService.cs  总览补充数据（舰船个体名/空间站/星系与安全等级）
Services/Characters/LocationNameResolver.cs   地点/结构名称分流解析（int 走 IDNameService，结构走 StructureService）
ViewModels/Characters/CharactersViewModel.cs          卡片页集合与汇总
ViewModels/Characters/CharacterCardViewModel.cs       卡片（头像/SP/ISK/技能队列/离线时长）
ViewModels/Characters/CharacterWorkspaceViewModel.cs  工作区左栏（含 ZKB）
ViewModels/Characters/{Overview,Skill,Clone,Wallet,Mail,Contract,Industry}PageViewModel.cs  各子页 VM
Views/Pages/Characters/CharactersShellPage.xaml(.cs)    壳（Tab）
Views/Pages/Characters/CharacterCardsPage.xaml(.cs)     卡片页
Views/Pages/Characters/CharacterWorkspacePage.xaml(.cs) 工作区（左信息栏 + 子页 Tab + 双刷新）
Views/Pages/Characters/ICharacterSubPage.cs             子页刷新契约（不重建实例）
Views/Pages/Characters/{Overview,Skill,Clone,Wallet,Mail,Contract,Industry}Page.xaml(.cs)
Views/Windows/MailDetailWindow.xaml(.cs)               邮件详情（HtmlPanel）
Views/Windows/SerenityAuthWindow.xaml(.cs)             国服授权向导（登出→授权页→粘贴→校验）
Controls/PagerControl.xaml(.cs)                        分页
Controls/RatioBar.cs                                   自绘细进度条（替代会栈溢出的 ProgressBar）
Services/Navigation.cs                                 主窗口 NavigationView 跳转（供子页调用）
```

### 通用窗口外壳（阶段 25 起）与全局 UI 层（阶段 32 起）
```
Views/Windows/ToolWindow.xaml(.cs)   工具窗口：统一标题栏（logo + 名称）、标题按钮/置顶/任务栏可配置，内容可传 Page/UserControl
Controls/WaitingOverlay.xaml(.cs)    全屏等待遮罩：半透明背景 + 旋转指示 + 文案 + 可选取消（对齐 WinUI ShowWaiting）
Controls/MessageHost.xaml(.cs)       右下角通知栈：淡入滑入、超时自动消失、可手动关闭（对齐 WinUI InfoBar）
```
（两者由 `MainWindow` 托管、经 `PageNotifyService` 全局调用；`MessageHost` 必须按 Bottom/Right 对齐，否则会铺满窗口挡住点击。）

### 商业模块（阶段 25 起：市场 / 订单；阶段 28：倒货；阶段 34：估价）
```
Services/Business/MarketOrderService.cs        星域订单（按物品/整星域/星系）+ **建筑订单** + 历史统计 + **批量历史** + 订单富化；分页与缓存统一走 `FetchOrderPagesAsync` / `ReadJsonFileAsync` / `WriteJsonFileAsync`（阶段 29 整理、阶段 30 并发翻页）
Services/Business/MarketStarService.cs         市场物品收藏（Configs/StaredMarketInvType.json，与 WinUI 共用）
Services/Business/CharacterOrderService.cs     个人/军团未结订单 + 与市场参考价的差值 + 游戏内查看
Services/Business/BusinessService.cs           倒货排除清单 + 物品数量变化通知 + **共享购物车**（单例）
Services/Business/ShoppingRecordService.cs     购物记录（Configs/ShoppingRecords/*.json）
Services/Business/ScalperSettingService.cs     倒货设置（Configs/ScalperSetting.json，与 WinUI 同文件）
Services/Business/ScalperCalculator.cs         倒货计算引擎（Cal* 全套公式）
Services/Business/AppraisalTextParser.cs       估价输入解析（合同/货柜/资产等复制文本 → 物品名 + 数量，阶段 34）
Services/Business/AppraisalService.cs          估价服务（ESI 取价 + PriceType 口径 + 百分比；结果模型 AppraisalResult/AppraisalItem，阶段 34）
Services/Business/AppraisalSettingService.cs   估价设置（Configs/AppraisalSetting.json，阶段 34）
ViewModels/Business/MarketPageViewModel.cs     市场页 VM（选择/统计/计算器/LiveCharts 系列与配色）
ViewModels/Business/OrderPageViewModel.cs      订单页 VM（角色/订单类型/来源过滤器、状态、剪贴板文本）
ViewModels/Business/ScalperPageViewModel.cs    倒货页 VM（设置/取数编排/进度与提示/取消）
ViewModels/Business/ScalperShoppingCartViewModel.cs   购物车 VM（合计/复制/粘贴/保存）
ViewModels/Business/ScalperShoppingRecordViewModel.cs 购物记录 VM（列表/载入/删除/加回购物车）
ViewModels/Business/AppraisalPageViewModel.cs  估价页 VM（输入/设置/结果、等待与进度、复制结果，阶段 34）
Views/Pages/MarketPage.xaml(.cs)               市场页（占位页已替换；MainWindow 注册不变）
Views/Pages/OrderPage.xaml(.cs)                订单页（占位页已替换；MainWindow 注册不变）
Views/Pages/ScalperPage.xaml(.cs)              倒货壳页（倒货/购物车/记录三页签；原占位页已替换）
Views/Pages/AppraisalPage.xaml(.cs)            估价页（原占位页已替换；MainWindow 注册不变，阶段 34）
Views/UserControls/MarketTypeInfoView.xaml(.cs)       物品简介内容（ToolWindow 承载；后续扩展 SDE 属性）
Views/UserControls/MarketCalculatorView.xaml(.cs)     买入/卖出计算内容（含"计算明细"）
Views/UserControls/MarketSelecteTreeView.xaml(.cs)    三态物品多选树（SelectedItems/SelectedItemsCount DP）
Views/UserControls/MarketLocationSelectorView.xaml(.cs) 市场位置选择器（独立控件：按钮+弹层+星域/星系/建筑三页签；SelectedItem DP；倒货/市场/估价共用，阶段 35 自包含化）
Views/UserControls/ScalperAnalyseView.xaml(.cs)       倒货：设置三页签 + 18 列结果表
Views/UserControls/ScalperShoppingCartView.xaml(.cs)  倒货：购物车
Views/UserControls/ScalperShoppingRecordView.xaml(.cs) 倒货：购物记录
Views/Windows/ScalperItemDetailWindow.xaml(.cs)      倒货物品详情（指标 + 源/目的市场订单与历史图）
Views/Windows/ScalperShoppingItemEditWindow.xaml(.cs) 购物车条目编辑对话框
Converters/FileNameConverter.cs                       文件路径 → 文件名（购物记录列表）
```

### 主窗口
```
Views/MainWindow.xaml(.cs)   左菜单 + 内容区、标题栏图标、窗口位置持久化、托盘
```

### 验证产物说明
```
核验截图与抓图脚本（capture_window.ps1）均为一次性产物，核对后已清理，不随仓库保留。
需要再次抓图时的要点：EnumWindows + PrintWindow，且抓图进程必须先开启 PerMonitorV2 DPI 感知。
```

### 本次修改的 Core 文件（与 WinUI 共用，注意回归）
```
Core/Services/IDNameService.cs   GetByIds(List<long>) 无限递归 → 转发到 List<int>；空待查列表跳过 ESI
Core/DBModels/IdName.cs          ESI 小写类别字符串 → 按 [EnumMember] 显式映射
```

回归核验：改动后 `dotnet build` 整个 WinUI 项目 **0 错误**（Core 为共享代码，WinUI 编译通过即无接口级回归）；且该递归缺陷在 WinUI 侧同样会被触发（`ViewModels/KB/StatistTopAllTimeViewModel.cs:82`、`ViewModels/KB/StatistSuperViewModel.cs:57` 调用的正是 `GetByIds(List<long>)`），因此上述修复对 WinUI 也是净收益。

### 结构（structure）ID 解析约定 —— **structure id 请走 StructureService**

> `Core/Services/IDNameService.cs` 中已写入同名 XML 注释，以下为约定说明。

- `IDNameService` 的 ID 一律是 **`int`**（`Core/DBModels/IdName.Id` 即为 `int`），只覆盖军团 / 角色 / 联盟 / 星系 / 空间站 / 物品类型等**处于 `int` 范围内**的 ID。
- 结构（structure）的 ID 约 **1e12**，远超 `int.MaxValue`（2,147,483,647）。`IDNameService` 没有任何 `long` 通路能安全承载它：
  - `GetByIds(List<long>)` / `GetByIdsAsync(List<long>)` 内部是 `ids.Select(p => (int)p)`，**超出部分被截断**；
  - 截断后既不抛异常也不报错，而是**静默解析出另一个（错误）名称**，是最难发现的一类 bug。
- 因此 **结构名称一律走 `StructureService`，不要用 `IDNameService`**：`QueryStructureAsync(long id)` / `QueryStructureAsync(long id, long characterId)` 先查本地 `Configs/Structures.json`（ESI 解析缓存）与 `MarketStructures.json`（用户自建市场建筑），未命中再用该角色的授权调 `Universe.GetStructureInfoAsync` 并回写缓存（`characterId <= 0` 只查本地、不发 ESI，与 WinUI `-1` 同义）。**WPF 侧已于阶段 27 补齐**（同时修掉了 `Init()` 从未被调用、市场建筑列表不加载反被覆盖空的既有缺陷）。
- 同理，任何"可能是建筑/结构 ID"的字段（如 `IndustryJob.FacilityId`、合同 `StartLocationId/EndLocationId`、`LocationId` 等）在解析成名称前，都要先判断是否落在地点/结构域，是则走 `StructureService` 而非 `IDNameService`。
- 新增调用点时自查：这一列 ID 有可能 ≥ 1e12 吗？会 → `StructureService`；不会 → `IDNameService`。
