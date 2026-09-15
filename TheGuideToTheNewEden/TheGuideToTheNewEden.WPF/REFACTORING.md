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
| UI 框架 | **.NET 8（LTS）** + WPF + **WPF-UI 4.3.0** | 默认样式/主题/导航控件开箱即用，避免自写控件模板；目标框架定 **net8** 而非 net10——.NET 9/10 的 WPF 在未更新的 Win10 21H1 上会启动即崩，改用 net8 后**已实机验证可正常运行**（见 §3「环境变更」与阶段 49） |
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
TargetFramework      net8.0-windows10.0.19041    （net8 = LTS，覆盖未更新的老 Win10，见阶段 49；
                                                平台版本须显式写 10.0.19041：LiveCharts 的 SkiaSharp 资产要求 TPV ≥ 10.0.19041，阶段 25）
AssemblyName         TheGuideToTheNewEden        （输出 TheGuideToTheNewEden.exe；协议注册依赖真实进程路径，见阶段 48）
UseWPF               true
ApplicationIcon      Assets\app.ico
ApplicationManifest  app.manifest (PerMonitorV2 DPI)
EnableWindowsTargeting true
```

包引用：

| 包 | 版本 | 用途 |
|---|---|---|
| WPF-UI | 4.3.0 | 默认样式、主题、NavigationView、DataGrid、NumberBox、Snackbar 等 |
| H.NotifyIcon.Wpf | **2.3.0** | 托盘图标 + 系统通知。2.4.1 只带 `net462`/`net10.0-windows7.0` 资产，net8 下会回退到 .NET Framework（`NU1701`）；**2.3.x（2.3.0 / 2.3.1）都有 `net8.0-windows7.0` 资产**，本项目取 2.3.0（阶段 49，`project.assets.json` 已确认命中 `lib/net8.0-windows7.0/`） |
| Newtonsoft.Json | 13.0.4 | 设置/令牌/缓存序列化 |
| HtmlRenderer.WPF | 1.6.1 | 邮件正文 HTML 渲染（替代 WebView2） |
| LiveChartsCore.SkiaSharpView.WPF | 2.0.5 | 市场历史图表（替代 Syncfusion 图表）；传递带入 SkiaSharp 3.119.0 |

项目引用：`TheGuideToTheNewEden.Core`（其再传递引用 `ZKB.NET`）。

链接的运行时资源（`Link` + `CopyToOutputDirectory`）：
`Resources/Configs/**`、`Resources/Database/**`、`Resources/default.mp3`，并把 `log4net.config` 单独放到输出根（`Core.Log.Init()` 从根目录读取）。

### 环境变更

- 本机 SDK：**9.0.203 + 10.0.401**（`dotnet --list-sdks`），构建由 **.NET 10 SDK 10.0.401** 驱动（`dotnet --version` 即它）；
  桌面运行时 6/7/8/9/10 均在位，net8 目标实际跑在 `Microsoft.WindowsDesktop.App 8.0.31` 上。
  （早期只装了 .NET 10 SDK 10.0.401 / 运行时 10.0.12，实测无需重启；后续为 net8 目标补齐了 8.0.x 运行时。）
- TFM 演进：`net9.0-windows` → `net9.0-windows10.0.19041.0`（为 Toast 临时启用）→ `net10.0-windows`（阶段 6）
  → `net10.0-windows10.0.19041`（阶段 25，为 SkiaSharp 资产补平台版本）→ **`net8.0-windows10.0.19041`（阶段 49 回退，现行）**。
- **回退 net8 的原因**（阶段 49）：.NET 9/10 的 WPF 在**未更新的 Windows 10 21H1（19043.985）**上**一启动就崩**
  （`0x80131506`，KERNELBASE 里的运行时 fail-fast），同机 .NET Framework / .NET 6 / .NET 8 的 WPF、以及任何控制台程序都正常
  → 即 .NET 9 起的 WPF 与该系统补丁级别不兼容；net8 是 LTS，用它可覆盖这些老系统。
  **该结论已实机验证**：回退后的 `TheGuideToTheNewEden.exe` 在那台 Win10 21H1 上能正常跑起来（阶段 49 的未覆盖项已闭环）。
- 随 TFM 回退的两处连带项：`AssemblyName` 由 `TheGuideToTheNewEden.WPF` → **`TheGuideToTheNewEden`**（输出 `TheGuideToTheNewEden.exe`）；
  `H.NotifyIcon.Wpf` 2.4.1 → **2.3.0**（见上表）。

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
- **踩坑记录（重要）**：`H.NotifyIcon.Wpf 2.4.1` 仅提供 `net462` 与 `net10.0` 目标，在 .NET 9 下会回退到 .NET Framework（`NU1701`）→ 当时固定 **2.3.1**；升级到 .NET 10 后再升回 **2.4.1**；**阶段 49 回退 net8 时又锁回 2.3.0（现行版本，`lib/net8.0-windows7.0/`）**。

### 阶段 6：升级 .NET 10
> **注（已被阶段 49 取代）**：本阶段引入的 `net10.0-windows` 目标已于阶段 49 **回退为 net8 LTS**，
> `H.NotifyIcon.Wpf` 随之由 2.4.1 锁回 2.3.0。以下为当时的历史记录，**不代表现行配置**（现行 TFM 见 §3）。
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
  - **`TargetFramework` 由 `net10.0-windows` 改为 `net10.0-windows10.0.19041`**：LiveCharts 传递依赖的 `SkiaSharp.Views.WPF 3.119.0` 只提供 `net462` / `net8.0-windows10.0.19041` 资产，不写平台版本（隐含 TPV=7.0）会触发 NU1701/NU1202；改后 restore/build 干净，`runtimes/win-{x64,x86,arm64}/native/libSkiaSharp.dll` 正常随包复制。（阶段 49 回退 net8 后，**「必须带平台版本 `10.0.19041`」这条结论依旧成立**，只是 TFM 前缀由 `net10` 变为 **`net8.0-windows10.0.19041`**。）
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

### 阶段 38：IDNameService 同步解析在 UI 线程永久死锁（钱包交易客户名）

- 现象：`IDNameService.GetByIds` 内的 `ESIService.Current.EsiClient.Universe.GetNamesAndCategoriesFromIdsAsync(noInDbs).Result` 在 `noInDbs = [2124358151]`（一个首次出现的新角色，本地 IdName 库无缓存）时**永久卡住**，页面冻结、无异常无日志。
- 根因（**UI 线程 sync-over-async 死锁**，与该 ID 本身无关）：
  - 唯一的 UI 线程触达链路：钱包交易页 → `CharacterWalletService.EnrichNamesAsync`（async，续体在 UI 线程上执行）→ 逐行调用**同步** `IDNameService.GetById` → `GetByIds` → `.Result` 阻塞 UI 线程。
  - EVEStandard 库内部的 `await` 链没有全程 `ConfigureAwait(false)`：请求虽已由 HttpClient 发出并完成，但续体被排回被 `.Result` 阻塞的 UI `SynchronizationContext` → 永远执行不到 → 死锁。**HttpClient 超时不会触发**（请求其实成功了，只是续体无法运行）。
  - 实测佐证：直接 POST ESI `/universe/names/` `[2124358151]` 秒回 `{"category":"character","id":2124358151,"name":"Aussin Preldent"}`——服务端无任何问题。
  - 之前一直没暴露：已解析过的 ID 都命中本地 IdName 缓存（`GetByIds` 直接走缓存分支，不发 HTTP）；第一个"从未见过"的客户 ID 才会真正发起请求并触发死锁。
- 修复：
  - **Core `IDNameService`**：抽出不抛异常的异步核心 `GetByIdsCoreAsync(List<int>)` / `SearchByNameCoreAsync(string)`（内部 `await`）；同步入口 `GetByIds(List<int>)` / `SerachByName(string)` 改为 `Task.Run(() => 核心).GetAwaiter().GetResult()`——异步链在线程池上执行、续体不回 UI 线程，死锁消除；`GetByIdsAsync` / `SerachByNameAsync` 保持原语义（`Task.Run` 包裹）。**方法签名与返回语义全部不变**（WinUI 侧既有的同步调用方 `StatistTopAllTimeViewModel` / `StatistSuperViewModel` 无需改动）。
  - **WPF `CharacterWalletService.EnrichNamesAsync`**：客户名回填由"逐行同步 `GetById`"改为**一次批量 `GetByIdsAsync`**（整页所有未缓存客户 ID 合并成一次 ESI 调用后按 `Id` 建映射回填）——既彻底离开 UI 线程，又把 N 次 ESI 请求降为 1 次。
- 遗留同类模式（本轮未动）：Core `KBHelpers.cs` 115/196/231 行与 `CharacterScanInfo.cs` 222/227/372 行也是 `.Result`，但 WPF 无任何调用方（仅 WinUI 击杀榜/扫描功能在后台线程使用），暂无死锁暴露路径；日后若从 UI 线程调用需照同样方式处理。
- 构建状态：**0 错误 0 新警告**。验证建议：重启应用后打开钱包"交易"页签，含新对手方的流水应正常显示出角色名（如 Aussin Preldent），页面不再冻结。

---

### 阶段 39：频道-预警迁移（Channel Intel）

- 目标：把 WinUI 的「频道预警」迁到 WPF——监控 EVE 聊天日志（情报频道），按星系名语言库匹配关键词，按 N 跳星系图计算跳数，触发 预警小窗（星图）/声音（每跳可配）/系统通知，支持自动定位、自动解除/降级、ZKB 击杀预警。
- **复用 Core（零改动）**：`GameLogHelper`（扫描 `Chatlogs` 目录与日志头解析）、`ObservableFileService`（300ms 轮询文件大小 + 增量读 UTF-16 日志 + EVE 重登换文件检测）、`ChannelIntelObserver`（关键词匹配/跳数计算/舰船名片段）、`SolarSystemPosHelper` + `IntelSolarSystemMap`（N 跳星系图 BFS）、`ChatLogHelper`（本地频道自动定位）、`MapSolarSystemNameService`（多语言星系名库）、`ShipNameCacheService`（舰船名匹配缓存）、`ZKBIntel`（zkillboard 击杀流）、全部模型（`ChannelIntelSetting`/`EarlyWarningContent`/`ChatChanelInfo` 等）。
- **新增 WPF 代码**：
  - `Services/Settings/IntelSettingService.cs`：每角色预警配置持久化 `%LocalAppData%\TheGuideToTheNewEden\Configs\IntelSettings.json`——**与 WinUI 同路径同格式**，两边配置互相沿用。
  - `Services/ChannelIntel/ChannelIntelSession.cs`：每角色预警会话（对齐 WinUI `Models/ChannelIntel`）：Start（校验 → 建 N 跳星系图 → 注册预警小窗/声音 → 每个勾选频道挂 `ChannelIntelObserver` → 自动定位挂本地频道监控 → 可选 ZKB 订阅）、Stop、自动定位回写（重算星系图并更新全部观察者与小窗）、`IntelJumps` 变化自动增删每跳声音配置。Observer 事件来自后台线程，会话层统一 `Application.Current.Dispatcher` 调度。WinUI 的 `ChannelIntelManager`（为星图工具聚合"无视跳数"情报）WPF 侧尚无消费方，未移植。
  - `Services/ChannelIntel/IntelWarningService.cs`：预警通知编排（对齐 WinUI `Services/WarningService`）——一个角色一个置顶小窗 + 一个声音播放器；`SoundNotifyItem` 用 WPF `MediaPlayer`（**循环通过 MediaEnded 重播实现**，WPF 没有 WinRT 的 `IsLoopingEnabled`），跳过自己发言的声音；系统通知走托盘气泡（`NotificationService` 新增 `NotificationClicked` 事件，**点击气泡停止全部报警声音**，对应 WinUI Toast 点击行为）。
  - `Views/Windows/IntelWindow.xaml(.cs)`：置顶预警小窗（FluentWindow，仅最小化+关闭，`ShowInTaskbar=false`）：星图 + 底部三按钮（清除所有预警 / 最新预警信息——**点击前置 EVE 游戏窗口** / 停止声音）；透明度=`OverlapOpacity/100`；位置尺寸变化即写回设置；自动解除/降级由 10 秒 `DispatcherTimer` 驱动；"必要时显示"模式在全部预警解除后延迟 30 秒自动隐藏（隐藏即停声）；标题栏关闭 = 停止该角色预警（`StopRequested` → VM 复位）。`ShowWindow()` 用 `Show()` 不抢焦点（游戏内不被打断，视觉上同为置顶覆盖）。
  - `Views/UserControls/IntelMapView.xaml(.cs)`：星图画布（对齐 WinUI **Default（Neweden）**样式）：星系节点按归一化坐标布点 + 星门连线（按两端 ID 去重）、家=海绿/常规=暗灰/预警中=橙红放大 1.5 倍/降级=黄、悬停显示"星系名 N 跳 + 最新预警内容"并高亮相邻星门、右键预警中的星系可单独解除。缩放用 `RenderTransformOrigin=0.5,0.5`（替代 WinUI 的手工平移补偿）。
  - `ViewModels/Channel/ChannelIntelViewModel.cs`：角色集合/选中角色→惰性会话缓存/频道内容与 ZKB 内容集合/开始·停止·全部开始·全部停止·刷新·停止声音·重置位置·**同步全部设置**（确认框后深拷贝各字段到其他角色）；等待/成功/错误提示走 `PageNotifyService`。
  - `Views/Pages/ChannelIntelPage.xaml(.cs)`：替换占位页（`MainWindow` 注册不变）。三栏：角色列表（预警中橙条指示，WinUI 的拖拽排序未做）/频道勾选列表（复选框+频道名+会话时间，右键打开文件/文件夹留待后续）/右侧两页签——预警设置（范围/通知方式/预警弹窗/每跳声音行（跳数+音量+循环+文件+拾取）/关键词/其他 六个 Expander，运行中整体禁用）与频道内容（只读 RichTextBox 按类型着色：预警=危险色+舰船名加粗、解除=成功色，超 `MaxShowItems` 裁剪，右键清空）。本地位置选择 = 按钮+Popup 搜索列表（含特殊星系，对齐 WinUI ShowSpecial=True）。
  - `Helpers/GameWindowHelper.cs`：按角色名找 `exefile` 进程主窗口并 `SetForegroundWindow`（前置游戏）。
  - `App.OnExit`：`IntelWarningService.Dispose()` + `ObservableFileService.StopAll()` + `ShipNameCacheService.Dispose()`。
- 本地化：中英各补 63 个键（`ChannelIntelPage_*` 53 个 + `EarlyWarningPage_*` 3 个 + `EarlyWarningItemPage_*` 4 个 + 新增 `EarlyWarningItemPage_Pick`），键值沿用 WinUI 原文；键名保留 WinUI 的历史拼写（`Alwasys`/`Succes`/`Faild`）以保持两侧一致。
- 实现中踩坑：`TextBlock.MaxLines` 是 WinUI 专有属性（WPF 没有）；`RenderTransformOrigin` 属于 `FrameworkElement` 而非 `ScaleTransform`；`BlockCollection` 没有 `RemoveFirst()`（用 `FirstBlock` + `Remove`）；`ClearAll24` 图标名不存在（改 `Eraser24`，`SpeakerMute24`/`Play24`/`RecordStop24` 已核对存在）。
- 构建状态：**0 错误 0 新警告**（仅剩既有 `NU1903`）。按 §8 约定未做实机截图核验。
- 未做/后续：预警小窗的 SMT/Near2 两种显示样式暂以默认星图样式呈现（设置项保留、生效均为默认样式）；频道列表右键"打开文件/打开文件夹"未接线；角色列表拖拽排序（WinUI `CanReorderItems`）未做；星图工具的"无视跳数情报"旁路（`ChannelIntelManager` + `OnIgnoreJumpsIntelUpdate`）待星图功能迁移时一并接入。

---

### 阶段 40：频道-监控 / 频道-统计 / 频道-查价 迁移（频道四件套收官）

- 目标：把 WinUI 频道组剩余三个子页迁到 WPF。Core 侧能力（`ChatlogObservableItem` 正则监控、`ChannelMarketObserver` 查价识别、`CharacterScanInfo`/`ChannelScanConfig`、`GameLogHelper`、`ObservableFileService`、`IDNameService.GetByNames` 等）零改动复用；WPF 侧新写设置服务与 UI。
- **频道监控（ChannelMonitorPage）**：
  - `Services/Settings/ChannelMonitorSettingService.cs`：每角色配置持久化 `Configs/ChannelMonitorSetting.json`（与 WinUI 同路径同格式）。
  - `Views/Windows/GameLogMsgWindow.xaml(.cs)`：置顶消息弹窗（仅关闭键、关闭即隐藏、400×300），新消息加粗/上一条恢复、自动滚底，底部"显示游戏窗口"按钮（`GameWindowHelper` 前置 EVE 客户端）。
  - `Services/ChannelIntel/ChannelMonitorNotifyService.cs`：通知编排（对齐 WinUI `ChannelMonitorNotifyService`）——弹窗 + 提示音（`RepeatSound` 用 MediaEnded 重播实现循环）+ 托盘气泡；`Stop` 隐藏弹窗停声音、`Remove` 释放资源。
  - `ViewModels/Channel/ChannelMonitorViewModel.cs` + `Views/Pages/ChannelMonitorPage.xaml(.cs)`：角色列表（复用 Core `ChannelMonitorItem` 直接绑定）/频道勾选/设置（通知四开关、声音文件、正则关键词增删）与命中内容页签（仅 Important 消息，格式与最后一条加粗同 WinUI，超 `MaxShowItems` 裁剪）。
- **频道统计（ChannelScanPage）**：
  - `Services/Settings/ChannelScanSettingService.cs`：`Configs/ChannelScanSetting.json`（单配置，属性变更即保存）。
  - `ViewModels/Channel/ChannelScanViewModel.cs`：完整移植 WinUI `StartCommand` 流程——名单按行解析 → 忽略名单过滤 → `IDNameService.GetByNames`（本地库+ESI）→ ESI `Character.AffiliationAsync`（1000/批）→ `ThreadHelper.RunAsync` 构建 `CharacterScanInfo` → 军团/联盟人数统计（剔除忽略成员的实际数量）→ 按输入顺序重排 → 可选多线程拉 ZKB 战绩；忽略名单增删/查重/表格右键忽略、`ReloadZKBInfoAsync` 原位重取。
  - `Views/Pages/ChannelScanPage.xaml(.cs)`：左名单输入（占位提示同 WinUI）、中设置面板（ZKB 开关/上限、忽略名单管理）、右"统计"（军团/联盟人数列表）与"详细"（14 列 `ui:DataGrid` + 右键忽略角色/军团/联盟 + 重新获取 ZKB，阶段 22 滚动方案）。表格列头/取值对齐 WinUI（威胁值/单挑占比/总击杀/总损失/抱团概率/超期/可开黑诱导/常用船与船型/常出没星系星域）。
- **频道查价（ChannelMarketPage）**：
  - `Services/Settings/ChannelMarketSettingService.cs`：`Configs/ChannelMarketSettings.json`（与 WinUI 同路径）。
  - `Services/ChannelIntel/ChannelMarketSession.cs`：每角色会话（对齐 WinUI `Models/ChannelMarket`）——勾选频道建 `ChannelMarketObserver`，命中（触发关键词 + 分隔符拆分 + 本地 SDE 市场物品匹配）即交编排服务。
  - `Services/ChannelIntel/ChannelMarketService.cs`：引用计数持有常驻置顶结果窗；`Query` 把窗口弹到前台、标题带市场星域名、更新内容。
  - `Views/Windows/ChannelMarketWindow.xaml(.cs)` + `ViewModels/Channel/ChannelMarketResultViewModel.cs`：结果窗（多物品 = 汇总卡 + 物品卡片列表；单物品 = 卡片 + **LiveCharts 近三个月价格三线图**，配色走主题资源并随 `ThemeService.ThemeChanged` 重刷，替代 WinUI 的 Syncfusion 图表）；取价走 WPF `MarketOrderService`（星域订单/历史，TTL 缓存）；"详细"跳转商业-市场页（暂不支持带物品选中）。结果模型直接复用 Core `ChannelMarketResult`（卖/买 × 5%/最优、总量、近 7 天高低均价）。
  - `ViewModels/Channel/ChannelMarketViewModel.cs` + `Views/Pages/ChannelMarketPage.xaml(.cs)`：角色/频道勾选/基本设置（关键词、分隔符）/市场星域选择（按钮+弹层搜索列表）+ 开始/停止/全部/重置弹窗位置/同步全部设置。
- 本地化：中英各补 92 个键（`ChannelMonitorPage_*` 9 + `GameLogMonitorPage_*` 14 + `ChannelScanPage_*` 41 + `ChannelMarket_*` 21 + `ChannelMonitorPage` 窗口标题键等），键值沿用 WinUI 原文；复用既有 `MarketPage_Sell/Buy/Top/Amount`、`General_*`、`ChannelIntelPage_*` 系列键。
- 构建状态：编译 **0 错误 0 新警告**（仅剩既有 `NU1903`）；最终输出复制因应用正在运行被锁（MSB3027，关闭应用重构建即可）。按 §8 约定未做实机截图核验。
- 未做/后续：统计页"打开 KB 详情"跳转（依赖未迁移的击杀榜页）；查价"详细"暂不带物品选中；频道列表右键"打开文件/文件夹"（与预警页同）未接线；角色列表拖拽排序未做。
- **修复（用户实机反馈）**：频道查价结果窗运行时抛 `XamlParseException`——"无法找到名为 HasResultCard 的资源"。根因：阶段 40 当轮用 PowerShell 批量替换改 `ChannelMarketWindow.xaml` 时，向 `Window.Resources` 插入 `HasResultCard` 样式的替换串因换行符不匹配**静默未生效**，而两处 Border 引用已就位——`StaticResource` 解析失败是**运行时**异常（编译与 BAML 生成都不报），属 §9 第 16 条"改了没生效"的变体（这次是"改引用了、定义没改成"）。修复：补上 `HasResultCard`（`BasedOn IntelCardBorder` + Result 为 null 时 Collapsed 的触发器）；并用脚本对全部新增 XAML 做了一次 `StaticResource` 键引用审计（引用 vs 本文件 `x:Key` 定义 + App 级全局键比对），确认无其他同类问题。
- **修复（用户实机反馈）**：频道统计"未识别到有效角色名称"——粘贴的频道成员名单识别不出来。根因：`GetNames` 沿用 WinUI 的 `str.Split('\r')`，而**游戏中复制的成员名单是 `\n` 换行**（用户实测确认），只按 `\r` 切分时整段被当成一个名字去查 → 必然查不到。修复：改为 `Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)`（CRLF / LF / CR 通吃）。实证：用控制台程序引用 Core 跑真实链路（与 `CoreInitializer` 同参数初始化 + 用户样例 13 个名字），本地缓存库命中 10 条 → ESI `BulkNamesToIds` 返回 13 角色 + 1 军团 → `GetByNames` 共 14 条，链路完全正常；`grep` 确认全项目已无其他只按 `\r` 切分的位置。顺带修掉同流程里一个分页步进错误：ESI Affiliation 分批循环写成 `start += found`（`found = start + length` 是"已处理到的位置"），名单 >1000 时**会跳批漏人**，改为 `start += length`。
- **新增（用户需求）**：频道统计的军团/联盟统计项**显示徽标**。新增 `ViewModels/Channel/ScanStatisticsItem.cs`（实体 + 人数 + 异步加载的徽标，规则与 WinUI `GameImageConverter` 及角色总览页一致：国际服 `images.evetech.net/{corporations|alliances}/{id}/logo`，国服 `image.evepc.163.com/..._{id}_64.png`；下载失败保持为空只显示文字）；`ChannelScanViewModel` 的统计集合由 `List<Tuple<IdName,int>>` 改为 `List<ScanStatisticsItem>` 并在构建后逐项异步取图；统计列表模板改为"徽标 + 名称 + 数量"。
- **修复（用户实机反馈）**：频道监控/频道查价的"开始/停止"按钮**在不该显示时一直可见**。根因是**绑定到 VM 中不存在的属性会静默失败**——WPF 不会报错（编译期与运行时都不报，不同于 HasResultCard 那种 StaticResource 缺失），绑定表达式解析不到就把控件留在默认值 `Visible`：
  - `ChannelMonitorViewModel` 只有 `SelectedNotRunning`，而停止按钮绑定 `SelectedRunning` → 停止按钮恒显；
  - `ChannelMarketViewModel` 只有 `SelectedRunning`，而开始按钮/设置区绑定 `SelectedNotRunning` → 开始按钮恒显、设置区恒可用；
  - `ChannelIntelViewModel` 两个属性都在（该页当轮实际未受影响，但写法同源）。
  修复：三个 VM 统一**同时提供** `SelectedRunning` 与 `SelectedNotRunning`（后者由前者取反），并在**每一处**状态变化点（选中角色、开始成功、停止）成对 `OnPropertyChanged`；并写了脚本做"XAML 绑定路径 vs VM 属性定义"的交叉校验（仅剩项级模板绑定与 `ElementName`/`ViewportWidth` 等框架属性为正常豁免），确认频道四页无 VM 级漏绑定。
  说明：这类"静默失败"的排查手段是核对绑定的**属性名是否存在且被通知**，不能只看 XAML 是否写对——同一症状也可能是控件模板或陈旧 BAML 造成（§9 第 16 条）。

---

### 阶段 41：通用卡片控件 `CardControl`（Header / Content / Footer）+ 频道页改造

- 背景：频道四个页面此前各自手写"Border + Grid 三行 + TextBlock 标题 + 按钮行"，卡片样式（背景/描边/圆角/内边距）在每页复制一遍（`IntelCardBorder`/`MonitorCardBorder`/`ScanCardBorder`/`MarketCardBorder` 四个几乎相同的本地样式），新增页面还得重抄。
- **新增控件 `Controls/CardControl.cs`**（对齐 WinUI 版 `Controls/CardControl` 的语义，但修正拼写并简化）：
  - 继承 `ContentControl`：**Content = 主内容区**（默认内容属性，任意 UI，自适应填满剩余高度）；
  - `Header`（object + `HeaderTemplate`）：标题行，未设置则整行与分隔线都不占空间；默认排版 14px/SemiBold/主题前景色（直接传字符串即可得到样式化标题）；
  - `Footer`（object + `FooterTemplate`）：底栏（放操作按钮），未设置则整行不占空间；上方带一条半透明强调色分隔线（对应 WinUI 的 FonterLine）；
  - 另有 `CornerRadius`、`ShowHeaderSeparator` 两个可选 DP。
  - **外观只由 Background/BorderBrush/BorderThickness/CornerRadius/Padding 决定**，模板里再无页面级布局假设——真正只统一"整体布局与背景"。
- **默认样式 `Controls/CardControlStyles.xaml`**（隐式样式，随 App.xaml 合并，与项目既有 `SettingCard`/`GlobalControlStyles` 的做法一致：ContentControl + App 级隐式样式，不引入 `Themes/Generic.xaml`）：圆角卡片背景 + 描边，行结构 Header → 分隔线 → Content → 强调色分隔线 → Footer，Header/Footer 为空时用 `Trigger` 自动折叠（含各自分隔线）。
- **频道四页改造**：`ChannelIntelPage` / `ChannelMonitorPage` / `ChannelScanPage` / `ChannelMarketPage` 的所有卡片区域改为 `controls:CardControl`——
  - 标题由 `Header`（资源字符串）承担，省掉每页的 TextBlock 样板；
  - 操作按钮统一移入 `Footer`（对齐 WinUI 的 Fonter 位置：此前在右栏顶部的按钮现在在卡片底部；左栏的"开始全部/刷新"等原本就在底部，位置不变）；
  - 统计页的两个内层"军团/联盟"小卡也改为 `CardControl`（Header = 军团/联盟），结果条数移入右卡 `Footer`；
  - 删除四个页面各自的 `*CardBorder` 样式（已无引用）。`ChannelMarketWindow` 与 `MarketPage` 另有同名本地样式，属各自独立用途，未动。
- 踩坑：WPF 的 `ContentPresenter` **没有 `Padding` 属性**（WinUI 有），模板里对三个区域改用 `Margin`（主内容区 `Margin="{TemplateBinding Padding}"`）；模板绑定 `Padding` 与显式 `Margin` 同时写会报重复属性。
- **追加改造（用户要求"其余手写卡片也切过来"）**：`MarketPage`（物品头卡片）、`AppraisalPage`（汇总条 + 两条警告条，改为仅内容区的 CardControl）、`ChannelMarketWindow`（多物品汇总卡 / 物品卡片 / 价格曲线卡；"有结果才显示"的触发器改为 `Style BasedOn="{StaticResource {x:Type controls:CardControl}}"`——显式样式必须 `BasedOn` 隐式样式，否则会顶掉卡片外观，见 §9 第 18 条）、`ScalperShoppingCartView`（统计面板）、`ScalperShoppingRecordView`（文件列表 / 明细两个面板）也全部改用 `CardControl`。至此项目内已无 `*CardBorder` 之类的重复卡片样式，共 9 个文件 36 处使用。
- **有意未改的两处**：`CharacterCardsPage` 的角色卡片是"Button 包内层 Border"的**可点击卡片**（含悬停/选中态，radius 12），改造需要让 CardControl 支持点击交互，超出一轮范围；`OverviewPage` 的 `OverviewCard` 是**页面内单处定义**的局部样式（radius 4、无内边距，6 处使用），不存在跨页面重复，保持原样。
- **修复（用户实机反馈）**：卡片 Header 文字**贴左上角、未垂直居中**，且**未设置 Header 时仍留一条空带**。上一轮把高度下限写在 `RowDefinition` 上是错的——**`RowDefinition.MinHeight` 在子元素被折叠后仍会占住该高度**（截图里右侧"无 Header"卡片顶部的空带即由此而来），而 30px 的行即使文字居中视觉上仍然贴顶。正确做法：行高保持 `Auto`，把高度下限放到**可折叠的宿主 Border** 上——`HeaderHost`（`MinHeight=44`）内放 `VerticalAlignment=Center` 的 ContentPresenter；Header 为 null 时触发器折叠整个宿主 Border，行高随之为 0。底栏同理（`FooterHost`、`MinHeight=40`，按钮在其中垂直居中）；两个宿主的 `Padding` 绑定卡片的 `Padding`，使标题/底栏与内容区左边缘精确对齐。
- 同时把**所有使用处的 Footer 内容改为居中**（原先按钮行靠左、频道刷新类单按钮显式 `HorizontalAlignment="Left"`）：按"footer 区间内"批量调整 11 处（频道四页），并把统计页设置卡里的"关闭"按钮从 Expander 内容移入该卡 Footer（居中），与"操作按钮放底栏"的约定一致。
- 另修复：`CardControlStyles.xaml` 的默认 `Padding` 在本轮此前的 PowerShell 批量替换中被误改成 `4`，已恢复 `10,8`（批量替换脚本不宜用于含相似片段的属性值，改完需回读核对）。
- **追加（用户要求）**：频道四页 footer 里的按钮全部改为**纯图标按钮 + 鼠标悬浮提示**（27 个）。图标按动作选取并已核对存在：开始/分析=`Play24`、停止=`RecordStop24`、刷新=`ArrowSync24`、清除声音=`SpeakerMute24`、重置位置=`ArrowReset24`、同步全部设置=`Copy24`、清除通知=`AlertOff24`、设置=`Settings24`、关闭=`Dismiss24`；`ToolTip` 与 `AutomationProperties.Name` 都直接沿用按钮原文案的本地化键（一处文案两用，无需新增键）。内容区里的功能性按钮（选音频文件、添加/确定/取消忽略名单、添加关键词、删除关键词等）保持文字不变。
- **追加（用户要求）**：6 个"停止"按钮（预警/监控/查价各 2 个）**去掉 `Appearance="Danger"` 的红色背景**，恢复主题色按钮，只把**图标染成红色**。做法：不用 `Icon="{ui:SymbolIcon …}"` 内联写法（无法给图标单独设色），改用**属性元素**写法 `<ui:Button.Icon><ui:SymbolIcon Foreground="{DynamicResource SystemFillColorCriticalBrush}" Symbol="RecordStop24"/></ui:Button.Icon>`——`ui:SymbolIcon` 继承自 FontIcon/Control，有 `Foreground`（估价页的警告图标已在用），因此图标可单独着色，按钮背景仍是主题色。两处内容区的"删除"按钮（删除监控关键词、删除忽略名单项）保留 `Appearance="Danger"`，属破坏性操作、红色背景恰当。
- 踩坑（本轮实际踩到，值得记）：用 PowerShell 脚本批量改 XAML **属性行**时连续出错——① 把 `Content="…" />` 整行替换成多行时**丢掉了行尾的 `/>`**（标签未闭合，MC3000）；② 已有 `Icon` 的按钮被插入第二个 `Icon`（重复属性，MC3000）；③ 用"原地 `RemoveAt/Insert` 后按旧索引推进"的方式修补，**索引偏移导致漏改与错位**（一个按钮的属性被贴到了下一个按钮上）。教训：**这类结构化改动不要用行内原地增删的脚本**——要么用"重建行列表"的幂等规范化（本轮最终采用的写法：解析每个 `ui:Button` 块，剔除受管属性后按固定顺序重排，把 `/>` 统一放到最后一行），要么直接手工编辑；改完必须跑一次"每个按钮恰好一个 Icon/ToolTip/无 Content"的结构校验（本轮已加）。
- **追加（用户反馈）**：预警小窗标题栏**没有 logo**。根因：这些自绘标题栏的窗口只设了 `Window.Icon`（任务栏/Alt-Tab 图标）或什么都没设，**`ui:TitleBar.Icon` 必须显式给**（`Window.Icon` 不会画到标题栏上）。修复：给全部 7 个自绘标题栏的窗口补上 `<ui:TitleBar.Icon><ui:ImageIcon Source="pack://application:,,,/Assets/logo_32.png"/></ui:TitleBar.Icon>`——预警小窗（`IntelWindow`）、频道监控消息弹窗（`GameLogMsgWindow`）、频道查价结果窗（`ChannelMarketWindow`）、邮件详情、国服授权向导、倒货物品详情、购物车条目编辑；与主窗口/`ToolWindow` 用的是同一个 `Assets/logo_32.png`。至此项目内 8 个窗口的标题栏图标一致。
- **改造（用户反馈"预警小窗文字发灰"）**：把"不透明度"从**整窗 alpha** 改为**只作用于背景**，文字与星图始终实色。
  - 原因：原实现 `Window.Opacity = OverlapOpacity/100`（与 WinUI 的 `SetLayeredWindowAttributes(LWA_ALPHA)` 同机制）会让**整个窗口一起变淡**——用户把不透明度设为 53% 后，黑字混成中灰、星图节点也同比例变浅。用户要求只淡背景。
  - 方案：**逐像素透明**——窗口 `Background=Transparent` + `AllowsTransparency=True`，另放一层背板 `Border`（`BackdropPlate`），其背景色取主题背景色、**alpha 由设置决定**（`ApplyBackdrop()`，颜色取自 `ApplicationBackgroundBrush`，主题切换时经 `ThemeService.ThemeChanged` 重算）；文字/星图在背板之上保持不透明。顺带修掉了底部"最新预警信息"靠继承取色的隐患（Run 显式用 `TextFillColorPrimaryBrush`）。
  - **关键限制（探针实测）**：`ui:FluentWindow` **不能**用于 `AllowsTransparency`——它会把 `WindowStyle` 重置为单边框，`Show()` 时抛 `InvalidOperationException`（"当 AllowsTransparency 为 true 时，WindowStyle.None 是唯一有效值"）。因此 `IntelWindow` 改为**普通 `Window`** + `WindowChrome`：
    - 标题区用 `CaptionHeight=40` 提供**原生拖动**（实测：模拟鼠标拖动后窗口从 (900,260) 移到 (1100,380)）；
    - 缩放由 `ResizeBorderThickness=6` 提供；
    - 标题栏改为自绘（logo + 标题 + 最小化/关闭两个 `ui:Button`），按钮加 `WindowChrome.IsHitTestVisibleInChrome="True"` 才不会被标题区吞掉（实测：模拟点击后按钮回调触发，日志出现 `CHROME-BUTTON-CLICKED`）；
    - 因 `ui:TitleBar` 的按钮不带该附加属性、且其背景透明导致挂在其上的拖动处理器收不到事件（实测拖动失败），故不再用它。
  - 已知代价：`AllowsTransparency=True` 会使该窗口回退到**软件渲染**（WPF 限制）；窗口内是几百个画布节点，可接受。
  - **修复（用户实机反馈的崩溃）**：点预警小窗的 X 抛 `InvalidOperationException`（"在窗口关闭期间，无法…调用 Close"）。调用链：`WmClose → OnClosing → StopRequested → 会话 Stop → IntelWarningService.Remove → IntelWindow.Dispose → Close()`——**在窗口关闭过程中又调了 `Close()`**。修法：`OnClosing` 里置 `_closing = true`，`Dispose()` 在 `_closing` 时**不再重复 `Close()`**（只做退订与定时器停止，窗口本来就在关闭中）；程序化 Stop 的路径（Dispose 先置 `_disposed` 再 `Close()`）不受影响、也不会再触发 `StopRequested`。
  - **同类隐患一并修掉（本次自查发现）**：`GameLogMsgWindow`（频道监控消息弹窗）与 `ChannelMarketWindow`（频道查价结果窗）都是"创建一次、长期复用"的窗口，用户点 X 直接关闭后，下次 `Show()` 会抛"窗口已关闭"异常。两者改为**关闭即隐藏**（`OnClosing` 里 `e.Cancel = true` + `HideWindow()`，对齐 WinUI 的 `SetCloseToHide`），并各自提供 `CloseWindow()` 供真正销毁用（监控通知的 `Remove` 已改用它）。`IntelWindow` 不走这条——点 X 的语义就是"停止该角色预警"，窗口随之销毁。
  - 新增语言键 `ToolWindow.Minimize`（中英），关闭复用 `General_Close`。
  - 验证方式：用独立探针程序（引用 WPF-UI 4.3.0）实测了"窗口能否创建、拖动是否生效、标题区按钮是否可点"三项；**真实预警小窗的外观仍需实机目视**（只有触发预警才会出现该窗口）。
- **修复（用户反馈"Paragraph 的默认字体与其他 UI 不一样"）**：`FlowDocument`（RichTextBox 内的文字）**不继承控件树的字体**，用的是 WPF 自己的默认值——探针实测为 **`Georgia` / 16**，而界面其余部分是 `Microsoft YaHei UI` / 14，所以中文看起来明显不对（衬线且偏大）。修法：在 `Controls/GlobalControlStyles.xaml` 增加**应用级隐式 `FlowDocument` 样式**，`FontFamily` 取系统消息字体（`{x:Static SystemFonts.MessageFontFamily}`，随系统语言变化、不写死字体名），`FontSize=14`（与 WPF-UI 基础字号一致）。已用探针验证"应用级隐式样式确实能纠正 FlowDocument 字体"（改后为 `Microsoft YaHei UI / 14`）；受影响的是 4 处 `RichTextBox`：频道预警/频道监控的频道内容、监控消息弹窗、预警小窗的"最新预警信息"。
- **修复（用户反馈"频道列表选中项变成 WPF 默认样式"）**：这三个页面的频道 `ListBox` 各自写了一个**只设 `HorizontalContentAlignment=Stretch` 的局部 `ItemContainerStyle`**——按 §9 第 18 条，**显式/局部样式会整体顶掉 WPF-UI 的隐式 `ListBoxItem` 样式**，选中高亮于是退回系统默认蓝。修法：把邮件页里已有的正确样式 `StretchListItem`（自带 `ControlTemplate` + 主题色悬停/选中底，且不 `BasedOn` 未验证的隐式样式）**提升为全局键控样式**（`Controls/GlobalControlStyles.xaml`），三个频道列表改为 `ItemContainerStyle="{StaticResource StretchListItem}"`，并删掉邮件页的重复定义（其两处引用照旧、解析到全局）。至此项目内已无只设属性的局部 `ListBoxItem` 样式；未设 `ItemContainerStyle` 的列表（角色列表、星域/物品列表等）走库的隐式样式，观感本就正常，未动。
- **修复（用户反馈"频道查价查阅多个物品时报错"）**：报错为 `在"System.Windows.Baml2006.TypeConverterMarkupExtension"上提供值时引发了异常`（行号指向 `ChannelMarketWindow.xaml` 的 `Appearance="Link"`）。根因：`Appearance` 是枚举属性，靠 TypeConverter 转换，而 **WPF-UI 的 `ControlAppearance` 枚举里没有 `Link`**（实测成员仅 Primary/Secondary/Info/Dark/Light/Danger/Success/Caution/Transparent）——非法枚举值**不在编译期报错**（模板内的取值要到实例化时才转换），于是**只在多物品模板被实例化时抛异常**，正好对应"查单个物品正常、查多个物品报错"；又因每次刷新都会重建模板，异常反复触发，`AppDispatcherUnhandledException` 逐个弹框，于是出现一叠"未处理的错误"窗口。修法：`Appearance="Link"` → `Appearance="Transparent"`（扁平链接观感且为合法成员）。顺带用探针**把项目内全部 `Appearance` 取值（Primary/Danger/Transparent）与全部 31 个 `SymbolRegular` 图标名逐一校验**，确认无其它同类非法枚举值。
- **追加（用户反馈"频道查价的『详细』只切页不加载物品，且不把主窗口置前"）**：
  - **带物品跳转**：`Services/Navigation.cs` 新增 `NavigateToMarket(long typeId)` 与请求通道 `MarketTypeSelectionRequested` 事件 + `TakePendingMarketType()`。`MarketPageViewModel` 构造时订阅该事件（页面已就绪→立即选中），`LoadAsync` 建完市场树后再消费一次待选 ID（覆盖"市场页尚未创建/树未建好时点击"的首次导航路径）；选中即走既有的 `SelectedInvTypeItem` → 拉取订单/历史。频道查价结果窗的"详细"按钮从 `DataContext`（多物品卡片）或 VM（单物品）取出 `TypeID` 后调用它；传 0/无效则忽略。
  - **主窗口置前**：`Navigation.Activate()` 在原有"还原最小化 + `Activate()`"之后补一次 Win32 `SetForegroundWindow`——仅 `Activate()` 在调用方不是前台进程时可能只闪烁任务栏，`SetForegroundWindow` 才能可靠置前（与游戏窗口置前用的是同一思路）。`NavigateToMarket` 里按"导航 → 触发选中 → 置前"顺序执行。
  - 已知小差异：跳转后只选中**右侧物品**（订单/历史随之加载），左侧市场树不显示高亮（树的选中未做双向绑定，与 WinUI 的 `SelecteTypeTreeControl` 行为差异），如需一并高亮可后续补。
- **追加（用户要求"频道查价窗口的物品名字左侧加图片"）**：新增 `Helpers/GameImageHelper.cs`（物品图片地址，规则与 WinUI `GameImageConverter` 一致：国际服舰船/无人机 `types/{id}/render`、其余 `types/{id}/icon`，国服 `image.evepc.163.com/types/{id}_{size}.png`）与 `Converters/TypeImageConverter.cs`（`TypeID → BitmapImage`，**用 `UriSource` 默认按需下载**——不设 `CacheOption=OnLoad`，否则会在绑定时同步下载、列表长时卡 UI；WIC 进程内按 URI 缓存，重复物品不重复下载；失败返回 null、不影响布局）。结果窗的**多物品卡片**与**单物品卡片**在物品名前各加 32px 图标（多物品卡片改为两列布局，名称列右侧留 80px 给"详细"按钮，避免文字与按钮重叠）。
- 构建状态：**0 错误 0 新警告**（仅剩既有 `NU1903`）。按 §8 约定未做实机截图核验；后续新页面直接 `<controls:CardControl Header="…"><controls:CardControl.Footer>…</controls:CardControl.Footer>内容</controls:CardControl>` 即可，不必再手写卡片布局。

---

### 阶段 42：日志监控（GameLogMonitorPage）迁移

- 目标：把 WinUI 频道组最后一个子页（日志监控）迁到 WPF。Core 侧 `GameLogItem` / `GameLogInfo` / `GameLogSetting` / `GameLogItemConfig` / `GameLogMonityKey` / `GameLogHelper` / `ObservableFileService` 零改动复用（配置 `Configs/GameLogInfoSettings.json` 与 WinUI 同文件同格式），WPF 侧新写会话服务、VM、页面。
- **Core 修复**：`GameLogItem.IsMatch` 原实现 `foreach (var key in _keyTimes) { return Regex.IsMatch(...); }` **在第一个关键词上就 return**，其余关键词全部失效（等于只有一个关键词能命中）。改为逐个匹配、命中任意一个即 true，并用 try/catch 隔离单个非法正则。该缺陷 WinUI 侧同样存在，属净收益修复。
- 新增 `Services/ChannelIntel/GameLogMonitorSession.cs`：一个"角色 + 一个日志配置"对应一个会话。把 Core `GameLogItem` 注册进 `ObservableFileService`；模式 0 命中即通知，模式 1 用 1 秒 `System.Timers.Timer` 轮询、超过"判定时间间隔"未再命中才通知一次；通知三通道 = 弹窗（复用 `GameLogMsgWindow`，标题为"日志监控 - 角色 - 配置名"）/ 声音（复用频道预警的 `SoundNotifyItem`——WPF 无 `IsLoopingEnabled`，靠 MediaEnded 重播实现循环）/ 托盘气泡；`StopNotify` 只停提醒，`Stop` 彻底退订并销毁窗口。
- 新增 `ViewModels/Channel/GameLogMonitorViewModel.cs`：角色列表（`GameLogHelper.GetLatestGameLogInfos` 取每人最新一天日志）/ 每角色配置集合 / 选中配置。`LogType=1`（异常日志）的配置在启动时由文件名解析出日期与线程号（`GameLogHelper.GetGameLogDateAndThreadId`），改监控同目录的 `{date}_{thread}.txt`，文件不存在给出明确提示；配置增删与属性修改**立即持久化**（有意优于 WinUI 的"仅开始监控时落盘"，避免新增配置后直接关窗丢失）。
- 新增 `Views/Pages/GameLogMonitorPage.xaml(.cs)`：三列卡片（角色列表 / 监控设置 / 实时日志），全部走阶段 41 的 `CardControl`；设置卡为 `TabControl` + 可关闭页签头（配置名 + ×），四个 `Expander`（配置信息 / 监控模式 / 通知方式 / 监控关键词），footer = 添加配置（`ContextMenu` 选游戏/异常日志）+ 开始 + 停止 + 清除通知；日志区 `RichTextBox` 命中行标红（`SystemFillColorCriticalBrush`）、最新一条加粗、超 `MaxShowItems` 裁剪并自动滚底；角色项右键打开日志文件/文件夹。
- 新增 `Converters/EqualsToVisibilityConverter.cs`：`value == ConverterParameter` 才显示，用于"仅模式 1 显示判定时间间隔"。用转换器而非 `Style`/`DataTrigger` 是为了避开 §9 第 18 条——给元素加显式/局部 `Style` 会整体顶掉 WPF-UI 的隐式样式。
- `GameLogMsgWindow` 增加可选 `displayTitle` 构造参数（频道监控与日志监控共用同一窗口，标题各自不同）；`Services/Settings/GameLogInfoSettingService` 沿用阶段 4 已有实现。
- 本地化：中英各补 26 个 `GameLogMonitorPage_*` 键（角色列表 / 监控设置 / 配置信息 / 监控模式 / 判定间隔 / 实时日志等），键值沿用 WinUI 原文。
- **修复（本轮自查发现，与阶段 40 的"静默失败"同源）**：`SelectedRunning` 是只读计算属性且**从未发通知**——开始监控后 footer 的"开始/停止"不互换。修法：在首次求值的两个入口（选中角色、`RefreshRunningState`）补 `OnPropertyChanged(nameof(SelectedRunning))`。
- **修复（本轮自查发现）**：`LogContents` 无人累积，且后台日志线程直接追加会与切换角色重绘并发读写。`Session_OnContentUpdate` 改为在 UI `Dispatcher` 上 `AddRange` 到 `item.Info.LogContents`（按 `MaxShowItems` 裁剪）后再通知页面。另：`InitAsync` 刷新时**复用仍在监控的 `GameLogInfo` 实例**（对齐 WinUI），否则会话持有的旧对象与新列表脱节、切换角色时历史日志丢失。
- **修复（本轮自查发现）**：本页 footer 的停止按钮初版为 `Icon="{ui:SymbolIcon RecordStop24}"`、没有红图标，与阶段 41 确立的约定不一致；改为属性元素写法 + `Foreground="{DynamicResource SystemFillColorCriticalBrush}"`，footer 按钮一并补 `AutomationProperties.Name`。
- 验证：实机启动应用 → 导航到"频道-日志监控" → 选中角色（设置卡出现）→ 点开始（底部提示"开始监控：游戏日志"、footer 开始→红色停止、左卡"停止全部"出现）→ 点停止（两者恢复）均正常。
- **追加（用户反馈"异常日志现在不能用，先隐藏、不要删除"）**：异常日志整套逻辑（`CreateErrorLogConfig` / `ReadErrorRegex` / `AddConfig(1)` / 启动时按文件名解析线程日志文件）与已有配置数据**全部保留**，只做 UI 隐藏：
  - VM 新增常量 `ErrorLogVisible = false` 作为唯一开关；
  - 页面可见集合改为独立的 `ItemConfigs`（由 `Setting.ItemConfigs` 过滤 `LogType != 1` **重建**而来），不修改 `Setting.ItemConfigs` 本身——隐藏的配置仍会原样落盘，`SaveSetting()` 不会把它们写掉；
  - `AddConfig` 仍会写入 `Setting.ItemConfigs`（保留数据），但隐藏期间只把可见的类型加入 `ItemConfigs` 并选中；
  - `StartAll` 改为遍历可见集合，避免启动看不到的配置；
  - 新建角色默认配置只建"游戏日志"（`ErrorLogVisible` 为 true 时恢复同时建两个）；
  - "添加配置"菜单里的"异常日志"项加 `Visibility="Collapsed"`（代码与其 Click 处理器保留）。
  - 恢复方式：VM 的 `ErrorLogVisible` 置 `true` + 菜单项 `Visibility` 去掉/改回 Visible。
- **追加（用户要求"添加日志时不用再选游戏日志"）**：异常日志隐藏后菜单只剩一项，遂把"添加配置"按钮的 `ContextMenu` 整个去掉，点击直接 `AddConfig(0)`（新建"游戏日志"配置）；`OnAddGameLogConfigClick` / `OnAddErrorLogConfigClick` 两个转发处理器一并删除（`AddConfig(1)`、`CreateErrorLogConfig`、`ReadErrorRegex` 与 `LogType==1` 启动分支均保留，恢复异常日志时重新接一个入口即可）。
- 构建状态：**0 错误**（仅剩既有 `NU1903` 与 `IntelWindow` / `ChannelIntelSession` 的既有可空性警告，另有 Core `GameLogItem._threadErrorGameLogItem` 未被使用的既有 CS0169）。

---

### 阶段 43：多开（GamePreview）重构 —— 拆服务 + 废弃 IPC 预览模式

- 目标（用户要求）：重构多开功能；**废弃 IPC 版本的预览窗口模式**（不需要该功能）；旧架构（WinUI 侧是一个 ~1600 行的上帝 VM + 三套预览窗口 + 独立的 IPC/子进程工程）问题多，本次一并优化架构。
- **废弃范围（明确）**：
  - WPF 侧**完全不引入** IPC 预览：不引用 `TheGuideToTheNewEden.PreviewIPC` / `TheGuideToTheNewEden.PreviewWindow`，不启动任何子进程，不做内存映射文件通信。
  - 预览窗口样式只保留两种：`ShowPreviewWindowMode` **0 带标题栏 / 1 无标题栏**；历史配置里的 `2`（IPC 无标题栏）在加载时并入 1（`NormalizeSettings`），UI 只提供 0/1 两项。
  - 旧工程 `PreviewIPC` / `PreviewWindow` **未删除**（WinUI 工程仍引用它们），仅 WPF 不再涉及；`PreviewWindow.exe` 不再需要随 WPF 发布。
  - 附带收益：去掉了旧实现里 `PreviewWindow.exe` 未随构建复制、MemoryIPC 标志位不清零、无超时忙等、`ChangeName` 是 TODO 等一整类问题。
- **新架构（WPF，按职责拆分）**：
  ```
  Services/GamePreview/GameClientService.cs        客户端发现（进程关键词→ProcessInfo）+ 3 种前台激活策略
  Services/GamePreview/ForegroundWatcher.cs        前台窗口轮询（UI 线程 DispatcherTimer，120ms）
  Services/GamePreview/GlobalHotkeyService.cs      RegisterHotKey + HwndSource 钩子（WM_HOTKEY）+ 组合键解析
  Services/GamePreview/PreviewWindowManager.cs     预览窗口的创建/销毁/批量操作（尺寸·排列·显隐·高亮）+ 每项快捷键
  Services/GamePreview/PreviewLayoutCalculator.cs  自动排列的纯计算（可单测，无窗口依赖）
  Services/GamePreview/SelectionPreview.cs         页面内"选中进程"实时画面（DWM 缩略图直接画到主窗口）
  Services/GamePreview/PreviewColorHelper.cs       System.Drawing.Color ↔ WPF 画刷 / #RRGGBB
  Services/Settings/GamePreviewSettingService.cs   Configs/GamePreviewSetting.json（与 WinUI 同结构）
  ViewModels/GamePreview/GamePreviewViewModel.cs   进程列表/排序/分组/快捷键分发（只管业务编排）
  Views/Windows/GamePreviewWindow.xaml(.cs)        预览窗口（DWM 缩略图，两种样式共用一个窗口类）
  Views/Pages/GamePreviewPage.xaml(.cs)            三列页面（进程列表 / 设置 / 预览）
  ```
  Core 的 `PreviewSetting` / `PreviewItem` / `ProcessInfo` **原样复用**，所以 WinUI 时代的 `GamePreviewSetting.json`（含用户已保存的角色名、颜色、位置、快捷键）可直接沿用——实测本机旧配置被正确加载（自定义颜色 #008000 / #80572F / #421A80 与自定义快捷键 Tab / Tab+Ctrl 均原样出现）。
- **IPC 模式的替代方案（关键架构决策）**：预览窗口一律用 **DWM 缩略图**（`DwmRegisterThumbnail`）把客户端画面实时合成进预览窗口。
  - 只用 `DWM_TNP_SOURCECLIENTAREAONLY` 让系统裁掉源窗口非客户区，**不再手工计算标题栏高度/边框宽度**——旧实现按 DIP 与物理像素混算，是"不同 DPI 下画面错位/尺寸对不上"的根源。
  - 不使用透明窗口（`AllowsTransparency`）：由 **DWM 缩略图自身的 opacity** 表示"不透明度"，只有游戏画面变淡，名称条与高亮边框始终实色（旧实现是整窗 alpha，名称一起变灰）。
  - **画面按源比例居中留边（letterbox / pillarbox）**，不把游戏画面拉伸到窗口比例：源比例取源窗口**客户区**尺寸（`GetClientRect`，与 `SOURCECLIENTAREAONLY` 显示的正是客户区一致），超出部分留黑边；滚轮缩放也按同一比例（`PreviewGeometry.ScalePreservingAspect`），避免窗口比例跑偏后一直留边。留边计算集中在 `PreviewGeometry.FitAspect`，浮层预览与页面内预览共用。
  - 页面内的选中预览同理：DWM 缩略图的**目标窗口就是主窗口**，离开页面时注销（否则会留下残影）。
- 窗口几何一律以**物理像素**经 Win32 读写（`GetWindowRect`/`SetWindowPos`），保存值与实际显示一致；预览窗口加 `WS_EX_TOOLWINDOW|WS_EX_NOACTIVATE`，显示/点击都不抢游戏焦点，也不进 Alt+Tab。
- **本轮修掉的旧架构问题（均为设计层面修正，非逐条打补丁）**：
  1. 上帝 VM 拆分为 5 个服务 + 1 个纯计算类，页面只做展示与转发。
  2. 进程扫描**重入竞态**：旧实现是 `async void` 的 1 秒定时器且无重入保护，扫描超过 1 秒时两轮刷新会互相踩踏列表与配置；现在用 `_refreshing` 门闩，并且**只有顺序真的变化才落盘**（旧实现每轮刷新都写一次整份 JSON）。
  3. 关键词解析：Trim + 丢弃空项（旧实现空关键词会 `Contains("")` 匹配到所有进程）。
  4. 重复句柄不再 `ToDictionary` 抛异常（改用 `TryAdd`）。
  5. 切换角色的配置继承统一：无论"原配置无名"还是"*Untitled"，都按新角色名**先查已保存配置、没有则复制外观**，并**先解绑旧配置的 ProcessInfo**（旧实现不解绑，导致这些配置再也匹配不到、列表越积越多）。
  6. `ChangeSetting` 只是字段赋值：旧实现改设置后不重新注册快捷键、不重建画刷/不透明度/缩略图源；现在 `ApplySettings`/`Rebind` 是完整的重套用，且**每个窗口的缩略图源可重新绑定**（旧实现 `UpdateSourceHwnd` 从未被调用）。
  7. "应用到全部"旧实现会 `StopAll` 后**从不重启**，等于改了个寂寞；现在改为：显示方式变了才重建窗口，其余直接 `ApplySettings` 立即生效。
  8. 自动排列重写为"先切行、再逐行定位、最后夹进工作区"：修掉旧实现换行后从锚点自身边缘起排（第二行压住锚点）、两处把宽高写反、单点判断导致窗口跑出屏幕等问题。
  9. 排序：上移/下移与"用当前列表填充"都会**同步写入 ProcessOrder 并保存**（旧实现 `Move` 不触发保存，拖动排序会丢）。
  10. 前台监测用 UI 线程定时器（旧实现线程池定时器 + 跨线程派发，且 `Stop()` 后 `_isDisposing` 永不复位，重启后不再上报）。
  11. 快捷键：不再依赖 Vanara，注册结果即真实结果（旧实现子类化失败会在注册成功的情况下返回 false）；`UnregisterHotkeys` 在无运行时短路，分组失败不再留下陈旧 id。
  12. 每项 `HotKey` 在编辑后会**重新注册**（旧实现只在窗口构造时注册一次）。
  13. 停止预览后设置面板不再是空白：`process.Setting` 会重新挂上"该角色的已保存配置"（用户反馈"以为设置丢了"）。
  14. 设置落盘改为"写临时文件再替换"，避免写坏；拖动窗口等高频变更统一走 500ms 节流。
  15. 死设置清理：`StartAllWithNoneSetting` **真正实现**（关闭时跳过没有已保存配置的进程）；含义含糊的 `StartAllDefaultLoadType` 不再暴露在 UI 上（模型字段保留以兼容旧 JSON）。
  16. 激活策略由 5 种脆弱的实现收敛为 3 种并写清语义（标准 / AttachThreadInput / SwitchToThisWindow），且 `AttachThreadInput` 必然成对 detach。
- 本地化：中英各补 82 个 `GamePreviewPage_*` 键（进程列表 / 设置分栏 / 显示样式 / 颜色 / 高亮边距 / 激活模式 / 自动排列 / 分组快捷键 / 顺序 / 各类提示），复用 `General_StartAll|StopAll|RefreshList`。
- 验证（实机，进程为真实 EVE 客户端 `exefile.exe`，窗口标题 `EVE - QEDSD`）：
  - 进入多开页 → 自动发现客户端，列表显示 `QEDSD / EVE - QEDSD`；
  - 选中进程 → 右侧"预览"卡内**实时显示该客户端画面**（DWM 缩略图画到主窗口），设置面板加载出该角色已保存的配置；
  - 自动开始 / "开始全部" → 生成浮层预览窗口：无标题栏样式 + 棕色名称条（取自保存的 `#80572F`）+ 实时游戏画面，浮在其它窗口之上；`停止全部` → 窗口销毁、设置面板保留配置；
  - Alt+F4 正常退出应用（走 `Application.Exit` → VM/管理器 Dispose 路径）无异常。
  - 探针/核对：所有 `SymbolRegular` 图标名与主题画刷键在写入 XAML 前已确认存在（`Resize24`/`Grid24`/`SlideSettings24`/`Delete24` 等），避免重演 `Appearance="Link"` 那类"编译通过、运行时抛异常"。
- 构建状态：**0 错误**（仅剩既有警告）。
- **修复（用户反馈"设置界面为什么有整个多开功能的垂直滚动条"）**：现象是滚轮在**右侧预览卡**上滚动时，三张卡片整体上移——即滚动条不属于设置卡，而是**承载页面的外层 ScrollViewer**。
  - 根因：页面被外层以**无限高度**测量。WPF 的 `ScrollViewer` 在高度不受限时不会滚动，而是把内容完整撑开，于是设置卡里那个 `ScrollViewer` 永远拿不到受限高度、不滚动，反过来把页面撑得比视口高 → 滚动条落到整个页面上（"整个多开功能的滚动条"）。
  - 修法（即用户指出的方向：让滚动发生在卡片内容里）：页面根元素加一层 `MaxHeight`，绑到 `RelativeSource AncestorType=ScrollViewer` 的 `ViewportHeight`（新增 `Converters/ViewportMaxHeightConverter.cs`，减去 24 的内边距；视口未测量为 0 时返回 `PositiveInfinity`，避免首帧被压成 0）。页面不再被撑高，滚动自然回到设置卡/进程列表各自的 `ScrollViewer`。
  - 验证（实机）：滚轮在右侧预览卡上 → 页面纹丝不动；滚轮在设置卡上 → 只有设置内容滚动，卡片位置与卡片 footers 不变。
  - 注意：**不要**把这条全局加到所有 Page 上——依赖"整页滚动"的页面（内容超长且没有内部滚动区）会被夹住导致内容被裁掉。只对"卡片内自带滚动区"的页面逐页加。
- **修复（用户反馈"设置界面的窗口内容预览背景一片黑色"）**：两层原因，均已处理。
  - ① **黑边**：页面预览卡是竖长的（约 670×780），而客户端画面是 16:9；"按源比例居中留边"之后画面只横向占四成高度，其余全是留边。改法：**预览区自己跟源画面同比例**——宽度占满卡片，高度 = 宽度 ÷ 源比例（`PreviewArea.SizeChanged` 时重算，并夹在卡片可用高度内；源比例未知时用 16:9 兜底）。这样画面与区域严丝合缝、不再有黑边，剩余空间是普通卡片背景。
  - ② **写死的黑底 + 空状态文字不可读**：预览宿主原来硬编码 `Background="#FF101010"`，且空状态提示用的是主题次要文字色——浅色主题下深灰字画在近黑底上等于看不见，于是"一片纯黑"。改法：宿主底色换成主题画刷 `CardBackgroundFillColorSecondaryBrush`，占位提示移入宿主内部并保留主题文字色（同主题配色，必然可读）。
  - 顺带让空状态更准确：`SelectionPreview` 新增 `SourceAspect` / `IsSourceAvailable` / `StateChanged`，页面据此判断。未选中时提示"选择左侧进程后，这里显示它的实时画面"，**选中但取不到画面时**（客户端已退出）提示新增键 `GamePreviewPage_PreviewUnavailable`。
  - 验证（实机）：未选中/选中两种状态都看过——画面填满 16:9 面板、无黑边、面板下方是卡片背景；窗口宽度变化时面板按比例跟随。**未实测**的是"选中后客户端退出"这条分支（需要关掉正在运行的游戏），该分支只按逻辑走通。
- **追加（用户澄清："要跟主窗口、频道预警小窗那样的标题栏，相当于把游戏画面放进一个普通 WPF-UI 窗口的 content 里"）**：预览窗口由"普通 `Window` + 自绘细条"改为 **`ui:FluentWindow` + 标准 `ui:TitleBar`**（与主窗口 / 频道预警小窗同款）。
  - **样式 0（带标题栏）**：`ExtendsContentIntoTitleBar="True"`，Grid 第一行放 `ui:TitleBar`（app logo 图标 + 标题=角色名 + 关闭键），**游戏画面只占第二行内容区**。
  - **高度**：`Height/MinHeight=28`（常规约 40）。踩坑：只设 `Height` 无效——标题栏按钮是 32×32，会把高度顶回去；在 `ui:TitleBar.Resources` 里加一条 Button 隐式样式把按钮缩到 24×24 之后，实测条高从约 26 图像像素（≈40 DIP）降到约 16（≈28 DIP）。
  - **样式 1（无标题栏）**：保持彩色细名称条（高 20，颜色仍取"名称条颜色 / 名称条高亮颜色"，只作用于这个样式）。因为它落在 WindowChrome 的标题区，必须加 `WindowChrome.IsHitTestVisibleInChrome="True"` 才能收到鼠标，否则被系统拖动逻辑吞掉。
  - **窗口样式**：`WS_EX_TOOLWINDOW|WS_EX_NOACTIVATE` 改到 `OnSourceInitialized`（base 之后）施加——WPF-UI 在该阶段设置窗口外观，原先在构造函数里设会被覆盖；另补 `OnStateChanged` 兜底，双击标题栏被系统最大化/最小化时立即还原（预览窗口不在任务栏，最小化后找不回来）。
  - **关闭**：改为 `TitleBar.CloseClicked → StopRequested`（与其他窗口一致），删掉自绘条上的关闭键。
  - 验证（实机）：放大截图确认标准标题栏（logo + 角色名 + ×）与降高效果；点 × 能正常关闭预览；页面上的"停止预览/停止全部"路径关闭后设置面板仍保留配置。
  - **遗留（本轮发现，未解决）**：**从预览窗口自己的 × 关闭后，进程列表的选中项会丢失**，设置面板因此变空白（页面按钮停止的路径不受影响）。已在 `RunningChanged` 里"重新挂回已保存配置"做了缓解但**没解决**——根因是选中项本身丢了，疑似预览窗口关闭导致页面被重新创建/重置，需要单独排查。
- 未做/后续：**"从预览窗口的 × 关闭会让进程列表丢失选中项、设置面板变空白"**（见上，根因待查，优先）；窗口拖拽排序（当前用右键上移/下移 + 顺序编辑框）；预览窗口的多显示器 DPI 混用场景未实测；`PreviewIPC` / `PreviewWindow` 两个旧工程是否从解决方案删除待定（删了会破坏 WinUI 构建）。另：预览窗口**启动时按 `Setting.Highlight` 直接高亮**，而前台状态要等下一次前台变化才应用，所以刚启动的预览会先显示高亮色（名称条变高亮色）直到切换一次前台——修法是在启动成功后立刻用当前前台状态同步一次（`manager.ApplyForeground(watcher.Current)`）。

---

### 阶段 44：预览浮窗按游戏画面比例显示（可自由拖动，另一条边实时联动）

- **现象**（用户反馈）：多开预览浮窗画面**上下（或左右）有黑边**——窗口比例与游戏画面比例不一致时，`PreviewGeometry.FitAspect` 按比例居中留边（letterbox/pillarbox），留边露出的就是 `ThumbnailArea` 的黑底。
- **要求（演进）**：① 把窗口尺寸比例与游戏窗口比例**固定**为同一个比例；② 后来明确"**保留拖动改尺寸**，拖完 X 或 Y 后自动调整另一条边，让画面区域的比例永远跟游戏一致"。
- **最终行为**：窗口可自由拖拽边框，**画面区域**（客户区 − 标题栏/名称条 − 高亮留白）**恒等于游戏客户区比例**，因此画面永远铺满、没有黑边。
  - **注意**：窗口**外框**比例不可能等于游戏比例——标题栏/名称条是固定高度外加的。要锁的是画面区域。
  - 拖动时以用户正在拖的那条边为准：**左右边**固定宽度、反算高度；**上下边**固定高度、反算宽度；**角**固定宽度、反算高度。锚点在被拖边的对侧（拖左边则右边缘不动），所以鼠标始终控制他正在拖的那条边。
  - 高亮边框的 `Highlight*Margin` 是画面外的固定留白，一并计入。**带标题栏的样式 0 不画上边**（用户要求）：高亮区紧挨标题栏，再留上边会像多余空隙，故该样式下"上边距"设置不生效，只画左/右/下三边、画面从标题栏正下方开始；无标题栏的样式 1 四条边照旧。
    - 实现上把"实际留白"收敛成单一来源 `GetHighlightThickness()`（`ApplyVisuals` 设 `ThumbnailFrame.Padding` 与比例校正用的 `GetHighlightPadding()` 都由它导出），避免"界面留白"与"校正算的画面区域"各算一套而对不上。
- **实现**（`Views/Windows/GamePreviewWindow`）：
  - `WM_SIZING` 钩子（`OnSourceInitialized` 里 `HwndSource.AddHook`）→ `LockClientAspect`：拖动中把消息里的矩形改写成同比例。**只改消息载荷、不调用 `SetWindowPos`**（这是与下面那次卡死的关键区别）。
  - `CorrectWindowSizeToAspect` + 80ms 防抖定时器：兜住启动、`ApplySettings`（改高亮边距会改变画面区域比例）、`Rebind`、`ShowWindow` 等非拖动路径；带 `_applyingAspectCorrection` 重入保护与 `_lastCorrectedSize`（同尺寸不重复下发）。
  - `XAML`：`ResizeMode="CanResize"` + `MinWidth/MinHeight`（WPF-UI 的 `WindowChrome` 本来就会给边框出调节箭头，`NoResize` 拦不住它——这也解释了用户"设了 NoResize 仍能拖"的现象）。
  - 尺寸一律夹进当前显示器工作区，拖大不会跑出屏幕。
- **踩坑 ①：不要用布局值反算尺寸，也不要"在尺寸消息里改尺寸"**
  - 校正基准量取 WPF 布局的 `ActualWidth/ActualHeight` 时，**误差每轮累积**（DIP 经 `UseLayoutRounding` 取整后再乘 DPI，与物理像素差 1~2px，被反算放大）：数值探针实测 533×300 在 7 轮内被缩到 200×120 最小尺寸。客户区尺寸必须用 **Win32 `GetClientRect`**。
  - **首版把校正挂在 `WM_WINDOWPOSCHANGING` 里**（想让窗口"一开始就是正确比例"）：`SetWindowPos` 会**同步重入同一条消息**，钩子又改一次 → **UI 完全卡死**，且因为卡死在尺寸事务里，缩略图更新停在 `DwmUpdateThumbnailProperties` 上，看起来像 DWM 调用挂了。**教训：数值探针只能证明算术收敛，证明不了 Win32 消息层的可重入性**；"在尺寸/位置消息里改尺寸"要先假定它会自循环（改用 `WM_SIZING` + 不调 `SetWindowPos` 即可）。
- **踩坑 ②（本节真正的元凶，用户实测日志定位）**：**WPF-UI 的 `FluentWindow` 把标题栏画在客户区内部**，`GetClientRect` 返回的就是整个窗口，于是 `窗口高 − 客户区高` **恒为 0**，标题栏那 ~40px 从没被扣掉 → 算出的画面区域虚高 → 窗口被算宽约 77px → **恒定的左右黑边**（与窗口大小、拖动无关，只由标题栏高度决定）。
  - 实测日志：`窗口=1595x842 客户区=1595x842 非客户区高=0`、`Area原点=4.0,32.0(DIP) DPI=1.25`。
  - 修法：标题栏/名称条高度改为**直接量元素**（`GetChromeHeight()`：`ui:TitleBar` 或 `StripBorder` 的 `ActualHeight × DPI`），两条路径（`WM_SIZING` 与 `CorrectSizeToAspect`）统一使用。改后同一窗口的**画面区域 1500×792 = 1.8939 ≈ 游戏 2560×1351 = 1.8949**，`FitAspect` 走"铺满"分支。
  - 一句话口径：**客户区尺寸用 Win32，标题栏高度用元素渲染高度**。
- **配套调整**：`FitAspect` 增加"比例一致（取整差 ≤1px）则直接铺满"的短路——比例已锁定时不必再按比例收缩，否则边缘会留 1px 缝（缩略图背后是黑底，看起来就是细黑边）。该短路对页面内的选中预览同样生效。
- **顺带修掉的既有缺陷**：`PreviewGeometry.ScalePreservingAspect` 在宽高**同时**超过上限时，会以宽度为准收缩后返回一个仍大于 `maxHeight` 的高度；已补"再以高度为准收缩一次"。
- **源窗口最小化时隐藏缩略图**（用户反馈"游戏最小化后会显示一个压缩的画面残留"）：源窗口最小化后没有实时画面，DWM 会退化成一张被压缩的残留快照。现在最小化期间直接 `_thumbnail.Hide()`，只留预览窗口自身背景，还原后自动重新画上（用户确认）。
  - **踩坑（用户反馈"游戏最小化后启动预览，高度明显变小"）**：最小化时源窗口的 `GetClientRect` 返回的是**任务栏缩略图**那种又宽又扁的小矩形（比例可达 5:1 以上）。`CorrectSizeToAspect` 拿它当比例依据，就会把预览窗按那个扁比例重算——高度被压掉大半、宽度几乎不变（实测 533×300 会被算成约 533×93），于是"保存的尺寸没被沿用"。
    - 修法：**最小化期间一律不做比例校正**（`CorrectSizeToAspect` 与 `LockClientAspect` 开头直接返回），保持调用方给的尺寸；游戏还原后下一次校正自然按真实比例修正。
    - 同类隐患一并收口：把"源画面宽高比"提成 `IPreviewWindow.SourceAspect`（**最小化/取不到时返回 0**），"统一尺寸"与"恢复位置"都改用它，比例未知时**保留原有高度**而不是拿坏比例去压扁；`ResolveStartBounds` 在无存档尺寸时用 16:9 兜底。
  - 检测方式为**轮询**（专用 `_minimizeWatchTimer`，300ms，`IsIconic`）：最小化**不会**改变本窗口尺寸，`OnRenderSizeChanged` 不会触发；而监听源窗口的 `WM_SIZE` 需要跨进程子类化（项目已移除 Vanara）。
  - 为不留"闪一下快照"的窗口：`ScheduleThumbnailUpdate` 在**排队前**刷新 `_sourceMinimized`，因此最小化后的第一次绘制就直接是"隐藏"，不必等下一次轮询。
  - 该定时器随窗口隐藏停止（`HideWindow`）、显示时重启（`ShowWindow` 里立即对齐一次），不常驻空转。
  - **同一问题在"设置界面右侧的选中预览"也要修**（用户反馈）：那是另一套渲染路径 `Services/GamePreview/SelectionPreview`（把同一源窗口用 DWM 缩略图直接合成到**主窗口**上）。它自带 150ms 刷新定时器，因此不需要新增轮询：`UpdateState` 里把"已最小化"并入"没有画面"（新增只读属性 `IsSourceMinimized`，并计入状态变化判定），`Refresh` 在最小化时 `_thumbnail.Hide()`，页面据此显示既有的占位提示（`GamePreviewPage_PreviewUnavailable` 原文已含"已最小化"，无需新增语言键）。
  - **无画面时的呈现**（用户要求"最小化后不要留黑底，改成高亮同色并加上与设置预览一致的提示文字"）：`ThumbnailArea` 的黑色底只在**有画面**时保留（缩略图按源比例居中摆放，留边露出的本就该是黑边）；没有画面时改为 **高亮色（`Setting.HighlightColor`）/无高亮时主题底色**，并叠上居中的提示文字。
    - 提示文字复用**同一个本地化键** `GamePreviewPage_PreviewUnavailable`，与设置界面右侧预览完全一致；文字颜色按底色取黑/白（`PreviewColorHelper.ContrastForeground`），无高亮时用主题次要文字色，保证任何高亮色下都可读。
    - 判定收敛在一处 `ApplyPreviewPlaceholder()`，由 `ApplyVisuals` / `Start` / `ShowWindow` / 最小化状态翻转时调用；源窗口退出（`TryGetSourceSize` 失败）同样走这条"不显示黑底"的分支。
  - **样式 1：画面占满整个窗口；角色名改为独立叠加窗**（用户要求"无标题模式下顶部还有一个纯色标题，内容应完全显示游戏画面"+"左上角叠加显示角色名"）：
    - 原来彩色名称条占第 0 行（20px），画面只能占下面。现已**删除名称条**；`GetChromeHeight()` 在样式 1 下**返回 0**，于是"画面区域 = 整个客户区"，窗口外框本身就与游戏同比例（拖动联动与防抖校正自动跟随，无需另写逻辑）。这一条与前面踩坑 ② 是同一类错误的两面："画面之外还有什么固定消费高度的东西"必须与实际布局一致。
    - **踩坑 ③（关键）：DWM 缩略图会盖住本窗口自己画的任何元素。** 用户实测"文字看不见"——我先把名称条改为叠加层（画面之上），结果名称条与角标**都**不可见：DWM 缩略图由系统在窗口自身内容**之上**合成，同一个窗口里的 WPF 元素无论 Z 序都盖不过它。凡是要显示在画面之上的内容（角色名、水印、提示），都必须放到**独立的窗口**里。
    - 修法：新增 `Views/Windows/PreviewNameOverlayWindow.xaml(.cs)` —— 预览窗口左上角的角色名叠加窗。要点：
      - 无边框、背景透明（`WindowStyle=None` + `AllowsTransparency=True`）、`ShowActivated=False`，并加 `WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT`：**点击穿透**到下面的预览窗口，拖动与滚轮缩放不受影响，也不进 Alt+Tab/任务栏、不抢游戏焦点。
      - 位置尺寸由 `GamePreviewWindow` 用**物理像素**同步（`SetBounds`），跟随窗口移动（拖动中每次 `MouseMove` 跟随）、缩放、高亮留白（角标内缩到高亮边框内侧）与隐藏/显示；`Owner` 指向预览窗口，因此始终压在它之上。
      - 刻意用普通 `Window` 而非 `ui:FluentWindow`：`AllowsTransparency` 与 FluentWindow 冲突（阶段 43 实测会抛 `InvalidOperationException`），而这个窗口完全透明、无标题栏，不需要 WPF-UI 外观。
      - 两种样式都显示（样式 1 已无名称条，角色名全靠它）。
      - **只在无标题栏样式显示**（用户要求）：带标题栏时名字已经写在标题栏里，再叠一个角标是重复信息。
        判定收敛在 `ShowNameOverlayIfNeeded()`（`HasTitleBar || !_isShowing` 即隐藏），由 `ApplyVisuals` /
        `Start` / `ShowWindow` 调用；叠加窗本身不销毁，切回无标题栏样式要能立刻用上。
      - **点 X 关闭预览窗口时崩溃**（用户实机反馈 `InvalidOperationException`："在窗口关闭期间，无法……调用 Close"）：调用链是 `WmClose → OnClosing → StopRequested → Manager.Stop → Stop() → Close()` —— **在窗口关闭过程中又调了一次 `Close()`**。这与阶段 43 给 `IntelWindow` 修的是同一个坑，做法同样：`OnClosing` 里置 `_closing = true`，`Stop()` 与 `DestroyNameOverlay()` 在 `_closing` 时**不再调用 `Close()`**（窗口本来就在关闭中；叠加窗是从属窗口，随所有者销毁）。
  - **角色名叠加的样式可设置**（用户要求）：新增 7 个设置项，落在 `Core` 的 `PreviewItem` 上（与既有字段同风格，WinUI 侧不用也不受影响）：
    | 设置 | 字段 | 默认值 | 说明 |
    |---|---|---|---|
    | 字体 | `NameOverlayFontFamily` | `Microsoft YaHei UI` | 下拉框选系统已装字体（`Fonts.SystemFontFamilies`）；名字无效时回退默认字体、不抛异常 |
    | 字号 | `NameOverlayFontSize` | `11` | DIP，基准字号 |
    | 跟随窗口缩放 | `NameOverlayFollowScale` | `false` | 开：字号 = 基准 × (窗口宽 / 960) × 倍率（上限 4×）；关：固定视觉大小 |
    | 缩放倍率 | `NameOverlayScaleFactor` | `1` | 仅"跟随"开启时生效 |
    | 背景色 | `NameOverlayBackgroundColor` | `#99000000` | **带透明度**，一直显示 |
    | 背景色（高亮时） | `NameOverlayBackgroundColorHighlight` | `#CC0078D7` | **带透明度**；该客户端在**前台**时顶上来取代基础底色 |
    | 文字颜色 | `NameOverlayForegroundColor` | `#FFFFFFFF` | **带透明度**；不做自动反差（用户明确指定） |
    - **两段式底色，且与"高亮"开关解耦**（用户要求）：`_isSourceForeground ? 高亮背景色 : 基础背景色`。
      这里**刻意不复用 `_isHighlighted`**：后者是"边框高亮"且被用户的 `Highlight` 开关过滤过，
      用它会导致"关掉边框高亮 → 文字底色高亮也一起失效"（用户实测反馈）。
      因此 `IPreviewWindow` 新增 `SetForegroundState(bool)`，由 `PreviewWindowManager.ApplyForeground` 传入**未过滤的真实前台状态**（三处实现：`GamePreviewWindow` 记录并只刷新叠加窗、`HeadlessPreviewWindow` 空实现）。
      `Start()` 里也用 `GetForegroundWindow()` 现量一次，避免刚开的预览窗沿用上一次的前台状态。
    - 字段名沿用 WinUI 版"名称条颜色 / 名称条高亮颜色"那一对的命名习惯（`Title*Color` / `Title*HighlightColor`）。
    - **背景色的透明度必须能往返**：新增 Core 侧 `Helpers/ArgbColorJsonConverter.cs` 让它落盘成 `#AARRGGBB`（不透明时 `#RRGGBB`，并兼容 `#RRGGBB`/`{A,R,G,B}` 两种历史写法）；同时把 `PreviewColorHelper.ToHex` 改为"带透明度时输出 8 位"，否则设置界面的"显示→回写"会把半透明色静默变成全不透明。
    - **踩坑（用户反馈"新增的配置都保存不了"）**：我一开始给颜色属性写的是 `[JsonConverter(typeof(ColorConverter))]` —— **`System.Drawing.ColorConverter` 是 `TypeConverter`，不是 `Newtonsoft.Json.JsonConverter`**。于是每次 `Save()` 在序列化阶段就抛 `InvalidCastException`（日志里可见 `JsonTypeReflector.GetJsonConverter` 的调用栈），**整个配置文件一个字节都不再更新**（不只是新字段）。
      - 更糟的是它**静默**：`Save()` 把 `_dirty = false` 放在 try 之前，异常被 catch 记日志后脏标记已清掉，界面看起来一切正常。
      - 修法：① 自写 `ArgbColorJsonConverter`（`CanConvert`/`WriteJson`/`ReadJson` 齐全）；② `Save()` 改为**写盘成功后**才清 `_dirty`、失败保留脏标记以便节流重试，并且只记日志不再假装成功。
      - 验证：用独立探针直接对 `PreviewSetting` 做序列化→反序列化往返，输出 `PASS 全部字段往返一致（含透明度）`，并确认 6 位色值与对象形式都能读回。
    - 设置界面新增"角色名叠加"分组（字体/字号/跟随/倍率/背景色/背景色 2/文字颜色 + 说明），改动立即生效（`ApplySettings` 里同步套用一次外观）。
  - **设置区改为两个并列页签**（用户要求）：中间卡片从"一个按钮在 选中项/全局 间来回切"改成 `TabControl`，
    两个 `TabItem`；**未选中进程时"选中项设置"页签隐藏**，只剩全局设置（`Visibility` 绑 `HasSelection`）。
  - **踩坑（用户反馈"未选中进程时改不了全局配置"）**：最初把 `SelectedIndex` 双向绑到"是否全局"这个 bool，
    未选中进程时 `SelectedIndex=0`，而 0 号页签已 `Collapsed` —— **WPF 允许选中隐藏的 `TabItem`**
    （写了个独立 WPF 探针实测确认：`SelectedIndex=0`、`SelectedItem` 就是那个隐藏项、`TabControl` 显示的也是它的内容），
    于是页面停在隐藏页上，全局设置既看不到也改不了。
    - 修法：索引改由 VM 的 `SettingsTabIndex` 提供（**未选中进程时恒为 1**），双向绑定；VM 在 `HasSelection`
      与 `IsGlobalSetting` 变化时都发通知，保证"索引永远指向一个可见页签"。随之无用的 `BoolToTabIndexConverter` 已删除。
    - 另一处对称修正：`SelectedProcess` 由"选中→`IsGlobalSetting=false`"改为 `IsGlobalSetting = value is null`，
      否则取消选中后会停在一个已被隐藏的页签上。
- 验证：`dotnet build` **0 错误 0 新警告**；收敛性与口径先用独立数值探针验证，再用**用户实机日志**定位到踩坑 ②，修后用户确认"没问题了"（按 §8 约定未做截图核验）。
- 未做/后续：游戏窗口本身改变宽高比（切全屏/改分辨率）后，窗口要到下一次尺寸变化才跟着走——当前没有监听源窗口的 `WM_SIZE`。**滚轮缩放保留**（按比例缩放，不产生黑边）。

---

### 阶段 45：发布（`dotnet publish`）失败修复 —— `log4net.config` 重复输出（NETSDK1152）

- **现象**：`dotnet publish` WPF 项目**必定失败**（与配置、RID 无关；RID 无关是因为冲突文件是框架相关的内容项）：
  ```
  error NETSDK1152: 找到了多个具有相同相对路径的发布输出文件:
    ...\TheGuideToTheNewEden.Core\log4net.config,
    ...\TheGuideToTheNewEden.WinUI\TheGuideToTheNewEden.WinUI\Resources\Configs\log4net.config
  ```
  报错来自 SDK 的 `Microsoft.NET.ConflictResolution.targets:_HandleFileConflictsForPublish`（按 `ResolvedFileToPublish` 的 `RelativePath` 查重）。`dotnet build` 正常，所以此前只跑 build 的开发流程没暴露。
- **根因**：本项目**同时**存在两份会落到输出根的 `log4net.config`：
  1. Core 工程的 `log4net.config`（`None` + `CopyToOutputDirectory=PreserveNewest`）随**项目引用传递**进本项目；
  2. 本项目 csproj 里**显式链接**的 WinUI `Resources\Configs\log4net.config`（`<Link>log4net.config</Link>`）。
  两者的目标相对路径都是 `log4net.config`。`dotnet build` 不校验重复输出（两份都拷、只按复制顺序留下一份，内容实际不确定），`dotnet publish` 会显式查重并报错。
- **修复（`TheGuideToTheNewEden.WPF.csproj`，两个 target）**：摘掉**来自 Core 目录**的那一份，显式链接的 WinUI 版保持不动。
  - `ExcludeCoreLog4NetConfigFromOutput`（`BeforeTargets="GetCopyToOutputDirectoryItems"`）：让 `bin` 输出也不再有两份、内容不再取决于复制顺序；
  - `ExcludeCoreLog4NetConfigFromPublish`（`AfterTargets="ComputeResolvedFilesToPublishList"`）：从 `ResolvedFileToPublish` 中移除，赶在其后的冲突检查之前。
  - 判定一律用**完整路径相等**锚定 Core 那份，避免误伤同名文件。
  - **保留 Core 版的理由**：`Core.Log.Init()` 从程序根目录读 `log4net.config`，本项目既有的基准是 WinUI `Resources\Configs` 版（`<file value="Log\" />`，对应文档里 `Log\yyyyMMdd.txt` 的路径），Core 版写的是 `%LocalAppData%\...\Logs\`。所以摘 Core 的那份、留显式链接的那份。
- **踩坑（本轮卡了三次的关键）**：
  1. **`AfterTargets="GetCopyToPublishDirectoryItems"` 不会在 `dotnet publish` 里触发**——`dotnet publish` 把 Build 与 Publish 分成**两次 MSBuild 调用**，build 那次算完这些列表，publish 那次会**重新**算一遍，build 期间的清理对发布不生效。发布侧的钩子必须挂在**发布链**上（`ComputeResolvedFilesToPublishList` 之后、`_HandleFileConflictsForPublish` 之前）。
  2. **`ContentWithTargetPath` 里只有本项目自己的内容**，项目引用传递来的 `log4net.config` **不在其中**（实测 dump 为空）——它是在 `GetCopyToPublishDirectoryItems` 内部经子工程 MSBuild 调用直接进了 `ResolvedFileToPublish`。所以"从本项目内容项里删"这条思路对发布侧无效。
  3. **路径比较必须两边都规范化**：`$(MSBuildThisFileDirectory)..\X` 里是**字面量 `..\`**，而项元数据 `FullPath` 是规范化过的绝对路径，直接 `==` 恒为 false（表现为"target 明明跑了、项却没被删掉"）。改用 `$([System.IO.Path]::GetFullPath(...))` 归一化后即匹配。
- **验证**（本机 .NET 10 SDK 10.0.401）：
  - `dotnet publish -c Release -r win-x64 --self-contained false -o <dir>`：**发布成功**，末尾输出 `TheGuideToTheNewEden.WPF -> <dir>\`；
  - 发布目录与 `bin` 目录的**文件数一致（110）**、均只剩**一份** `log4net.config`，且内容为 WinUI 版（`<file value="Log\" />`）；`Resources\Configs`、`Resources\Database`（含 `Local\zh.db`）、`Resources\default.mp3`、`libSkiaSharp.dll` 等改前输出内容齐备；
  - `dotnet build -c Release -r win-x64 --self-contained false -t:Rebuild`：**0 错误**（35 个既有警告）；
  - `dotnet publish -c Debug`（不带 RID）：**发布成功**（exit 0），说明修复与 RID/配置无关；
  - 发布产物**冒烟**：启动 `_pubtest\TheGuideToTheNewEden.WPF.exe` → 进程存活、主窗口标题「新伊甸漫游指南」，并在**应用目录**下生成 `Log\20260913.txt`——同时证明发布版的 `log4net.config` 是有效的、且用的是「相对 `Log\`」那一份（若是 Core 版则会写到 `%LocalAppData%`）；核验后已正常关闭进程并删除临时发布目录。
- 说明：本次只改 csproj，未动任何源码；`_pubtest` 等临时发布目录为一次性产物，已清理。
  （**产物名注**：本轮 `TheGuideToTheNewEden.WPF.exe` 属 `net10` + `AssemblyName=TheGuideToTheNewEden.WPF` 时期；
  阶段 49 回退 net8 后程序集名改为 `TheGuideToTheNewEden`，发布物是 `TheGuideToTheNewEden.exe`——
  阶段 48 的注册表残留正来自这一点。）

---

### 阶段 46：翻译迁移（本地 SDE 数据库专有名词互译，**废弃有道 API**）

- 目标（用户要求）：把「翻译」页迁到 WPF，**不使用旧的有道 API**（原实现把文本 POST 到 `openapi.youdao.com`，依赖 `Configs/YoudaoLicense.txt` 里的 appKey/appSecret → `AppKey`/`AppSerct` 与两个词典 id），**先把本地数据库翻译做完整**：用 SDE 主库（英文）与本地化库（中文 `Local/zh.db`）互译 EVE 专有名词（物品 / 星域 / 星系 / 空间站），完全离线。
- **架构：可插拔的「翻译源」**（页面不关心译文从哪来，将来加在线/本地模型翻译只加一个实现）：
  - `Services/Translation/ITranslationProvider.cs`：`ITranslationProvider`（`Key` / 显示名键 / `IsAvailable` / 不可用原因键 / `TranslateAsync`）+ `TranslationDirection`（自动 / 英→中 / 中→英）+ `TranslationRequest` / `TranslationOutcome`（结果条目**直接复用 Core 的 `TranslationItem`**，靠 `IsFromDataBase` 区分数据源）。
  - `Services/Translation/LocalDbTranslationProvider.cs`：本地数据库源（当前唯一实现）。**自动方向**先按原文是否含中文判定，若该方向一条都没命中就**再试另一方向**（中英混排 / 输入缩写时更不容易"查不到"）。
  - `Services/Translation/TranslationService.cs`：已注册源列表 + 按 `Key` 取源（找不到退回第一个）。**接入在线翻译只需在这里多注册一个实现**，页面上的来源下拉会自动多出选项、不再是禁用态。
  - `Services/Translation/TranslationLanguageHelper.cs`：按正文脚本判定源语言（汉字/假名/谚文/西里尔）、(源,目标) 方向解析、语言代码 → 本地化键。
  - `Services/Settings/TranslationSettingService.cs`：`TranslationPage.Provider` / `TranslationPage.Direction` 写进共用的 `settings.json`（由 `CoreInitializer` 初始化）。WinUI 的 `TranslationSetting.FromLanguage/ToLanguage` 是**给有道用的语言代码**，不再沿用。
- **Core 新增/改动**（两侧共用，注意回归）：
  - 新增 `Services/DB/TranslationDbService.cs`：按名称在**原文语言**的库里模糊匹配（每类名词 `Take(100)` 封顶），再用这批 ID 去**译文语言**的库**批量**取对照行——每类名词固定 2 次查询，不做逐条查询；结果按「完全匹配优先、其次名称升序」排序；译文库里没有该 ID 时 `Translation` 为 null，界面显示「无译文」。`IsAvailable` 要求主库与本地化库都已载入。
  - `Services/DB/DBService.cs`：新增公开只读属性 `MainDbReady` / `LocalDbReady`。`MainDb`/`LocalDb` 是 `internal`，外部（含 WPF）无法判断数据库是否真的载入，而未载入时任何按库查询都抛 `NullReferenceException`；有了这两个属性，翻译源才能给出「本地化数据库不可用」的明确提示而不是异常。
  - `Models/TranslationItem.cs`：类注释更新（不再是"兼容数据库、有道翻译两种结果"）。
  - `Services/YDTranslationService.cs`：加类注释说明**WPF 不再使用**、仅为 WinUI 保留（待 WinUI 退役后连同 `ITranslationService` 删除）。**文件本身未改**，WinUI 侧编译不受影响。
- **界面**（新增 `Views/UserControls/TranslationPanelView.xaml(.cs)` + `Views/Pages/TranslationPage.xaml(.cs)`，删除占位页 `Views/Pages/TranslationPage.cs`；页面类名/命名空间不变，`MainWindow.xaml` 的导航注册无需改动）：
  - 左卡片「翻译」= **来源**（下拉；只有一项时禁用、当只读标签用，将来多源时自动可交互）+ **方向**（自动 / 英→中 / 中→英）+ 输入框 + 匹配列表。列表每行直接显示「原文 + 译文 + 类型」，**不用点选就能看到译名**。
  - 输入停顿 **350ms 防抖**自动查询；回车、`翻译` 按钮立即查询；`清空` 清空输入与结果；底栏另有 `弹窗`。
  - 右卡片「结果」= 物品图标 + 原文 + 类型/方向 + **译文** + 原文描述 + 译文描述；底栏 `复制译文` 复制**译名**（该条无译文时复制原文，并提示已复制）。未选中时显示引导文案。
  - 来源不可用时左上角显示黄色警告条（文案指向「设置 → 一般」的本地化数据库项），并在查询前**短路**，不去打注定失败的查询。
  - 切语言时 VM 会**重建匹配项包装**（`TranslationMatchViewModel` 里的类型/语言标签是构造时取好的本地化文本，见 §9 第 18 条末句的同类问题）。
  - `弹窗` 用既有 `ToolWindow` 承载**同一面板**的另一个实例（`ShowPopWindowButton=false`，避免层层弹窗；单实例、重复点击仅激活）。
- **与 WinUI 版的有意差异**：① 「原文语言 / 译文语言」两个下拉换成**一个方向选择**（那是给在线 API 的语言代码，本地库只分中/英）；② 不再有"回车联网翻译通用文本"，本地库只认 SDE 里的专有名词，查不到就明确提示；③ 列表行直接给译文、右侧详情补物品图标与描述（WinUI 需先点选才看得到译文）。
- **实现中自查修掉的一处**：物品图标最初用共用的 `Converters/TypeImageConverter`（`BitmapImage.UriSource` 走 WPF 的 URI 下载路径），核验时它**每次都抛 `COMException 0x80072EE4`（没有注册类）于 `MS.Win32.WinInet.get_InternetCacheFolder()` 并记一条 ERROR**。翻译页改用估价页同一套做法（`HttpClient` 取字节 → **同一线程池线程**解码 + `Freeze()`，并按类型 ID 进程内缓存）：既绕开 WinINet 缓存路径，也避开 `Freezable` 的线程亲缘性（§9 第 15 条）。共用转换器本身未改（其他页面的行为不受影响）。
- 本地化：中英各补 **31 个 `TranslationPage_*` 键**（键值沿用 WinUI 原文的 `TranslationPage_Input/Result/Input_Tip/Query*/Translation*/NoResultTip/PopWindow/Copy*/EN`，新增 来源/方向/类型标签/无译文/无选中/清空/翻译/匹配条数/失败/本地库不可用 等），两文件键数均为 **998**。
- 构建状态：`dotnet build` **0 错误**（仅剩既有警告）。
- **核验**（实机运行 + UIA 自动化；一次性脚本与探针在核对后已清理，不随仓库保留）：
  | 核验点 | 结果 |
  |---|---|
  | 导航到「翻译」页 | 页面正常渲染出 来源 / 方向 / 结果 / 翻译 / 清空 / 弹窗 / 复制译文 等元素，**无 XamlParseException** |
  | 输入 `Rifter`（方向=自动） | 列表 100 条匹配（受每类上限），首条为 `Rifter`；详情区显示译文「裂谷级」+ 英文原文描述 + 中文译文描述；状态行「匹配 100 条 · 英文 → 中文」 |
  | `复制译文` | 剪贴板 = `裂谷级` |
  | `清空` | 输入框与列表清空 |
  | `弹窗` | 进程内新增**可见顶层窗口**（标题「翻译」），窗口内是同一面板（有 来源/方向/结果，**无**「弹窗」按钮）；在其输入框输入 `Jita` → 显示「吉他」 |
  | 运行期间应用日志 | **0 字节新增**（无 ERROR / 无异常） |
  | Core 独立探针（12 项断言全 PASS） | 双向命中：`Rifter`↔`裂谷级`（物品）、`Jita`↔`吉他`（星系）、`The Forge`↔`伏尔戈`（星域）、`Jita IV - Moon 4 - Caldari Navy Assembly Plant`↔`吉他 IV - 卫星 4 - 加达里海军 组装车间`（空间站），且完全匹配排首位；物品描述双向对照非空；`Search("级")` 24 条、27ms；空输入与无匹配返回空 |
- 未做/后续：① **通用文本翻译**（非专有名词）本地库做不到，需要接入在线 / 本地模型翻译源（`ITranslationProvider` 已为此预留）；② **按行批量翻译**（粘贴一份物品清单逐行出译文）未做；③ **频道翻译**页仍是导航占位（同样依赖通用文本翻译能力）。
- **追加（用户要求"结果显示的所有文字需要可以容易选择复制"）**：WPF 的 `TextBlock` **不能选中文本**，因此结果卡片的内容全部改用**只读 `TextBox`**（新增 `TranslationSelectableText` / `…Title` / `…Result` / `…Value` / `…Secondary` 一组样式：透明底、`BorderThickness=0`、无内边距、`TextWrapping=Wrap`、`FocusVisualStyle=null`、`IsInactiveSelectionHighlightEnabled=True`，选中色取主题强调色 + 0.4 透明）——外观与普通文本完全一致，但可**拖选、Ctrl+A/Ctrl+C、右键复制**；「无译文」的警示色由该系列的 `DataTrigger` 负责（原来挂在 `TextBlock` 上）。同时把标题行从"横向 `StackPanel`"改为**两列 `Grid`（`Auto` + `*`）**：横向 `StackPanel` 给子元素的可用宽度是**无限**的，长名词不会换行而会溢出卡片。左侧匹配列表加**右键菜单「复制原文 / 复制译文」**（新增键 `TranslationPage_CopyQuery`，两语言各 1 个；`CopyQuery`/`CopyTranslation` 收敛到 VM 的 `CopyToClipboard`），并在 `PreviewMouseRightButtonDown` 里先选中鼠标下的那一行——否则会出现"右键 A 行、复制到的是 B 行"。
- 该追加的构建校验：应用正在运行（VS 调试会话）锁住 `bin`，`dotnet build` 报 `MSB3027/MSB3021`；改用 **`-p:OutDir=<临时目录>\`** 重定向输出校验，**0 错误**，并做了一次 `StaticResource` 键引用审计（13 处引用全部命中本地或全局定义，无"MISSING"）。**实机核验未做**（需先停止调试会话）——按用户选择，由其自行重新生成后查看效果。
- **追加（用户反馈"翻译弹窗的置顶按钮没有生效、一直都是置顶，且与最大最小关闭按钮错位"）**：
  - **错位**：WPF-UI 的 `TitleBarButton`（最小化/最大化/关闭）固定 **44×30 + `VerticalAlignment=Top`**，而 `TrailingContent` 的 `ContentPresenter` 没设垂直对齐（默认拉伸到整条标题栏 48）→ 放在里面的图钉被撑高、图标居中后比三个系统按钮**低 11 DIP**（探针实测 11.0）。`ToolWindow.xaml` 的置顶按钮改为 `Width="44" Height="30" VerticalAlignment="Top" Margin="0,0,6,0"`，与系统按钮同高同顶（实测高度差 0.0）。
  - **"置顶没生效"**：置顶开关本身没坏——探针实测点击后 `Topmost` 与 Win32 的 `WS_EX_TOPMOST` 位确实翻转；真正的原因是**弹窗设了 `Owner`**：被拥有的窗口按 Windows 规则**恒在宿主窗口之上**，于是"未置顶"在应用内看起来与"置顶"完全一样。翻译弹窗因此**不再设 `Owner`**，改为 `WindowStartupLocation=Manual` + 手动居中到宿主窗口（并夹进工作区）：未置顶时是普通窗口（可被主窗口盖住），置顶时才浮在最前。（市场页的"简介/买入"窗口仍保留 `Owner`，未动。）
  - **状态更易读**：置顶时按钮切到 `Appearance="Secondary"`（填充底色）、图标 `PinOff24`、提示"取消置顶"；未置顶切回 `Transparent`/`Pin24`/"置顶"。只切 `Appearance`，配色交给主题（不缓存 Brush，见 §9 第 18 条）。另在 `ToolWindow` 里补 `WindowChrome.SetIsHitTestVisibleInChrome(TopmostButton, true)` 作双保险（探针显示本就不需要，见 §9 第 26 条）。
  - **核验**（独立探针直接实例化主程序集里**真实的** `ToolWindow`；构建走重定向输出目录，仍不碰被调试会话锁住的 `bin`）：置顶按钮 44×30.4 / `VerticalAlignment=Top` / 与关闭按钮 centerY 高度差 **0.0**（修正前 11.0）；按钮处 `WM_NCHITTEST=HTCLIENT(1)`、命中测试落在按钮 Border 与图标的 TextBlock 上；连点两次依次 `Topmost=True→False`、`WS_EX_TOPMOST=True→False`、图标 `PinOff24→Pin24`、外观 `Secondary→Transparent`、提示 `取消置顶→置顶`。主程序集编译 **0 错误**（重定向输出）。

---

### 阶段 47：翻译接入大模型 AI（OpenAI 兼容 / Azure / Anthropic / Gemini；**不做 Ollama**）

- 目标（用户要求）：把大模型接进翻译功能——**走 OpenAI 协议**，把本地数据库的中英词库喂给 AI，让 AI 输出**句子/上下文翻译**；协议要可扩展（不只 OpenAI）；**明确不做 Ollama 原生协议**（本地模型用 Ollama / LM Studio / vLLM 的 OpenAI 兼容端点即可）。
- **分层**（照阶段 46 留的"可插拔翻译源"接口往下长）：
  ```
  协议层  Services/Translation/Llm/IChatProtocol + 4 个适配器 + ChatClient（重试/超时/流式/错误归一化）
  术语层  Services/Translation/GlossaryService（命中式术语库）+ Core TranslationDbService.ExtractGlossary
  语义层  Services/Translation/AiTranslationProvider（提示词 + 术语注入 + 后校验 + 缓存）
  设置/UI Services/Settings/TranslationSettingService + 设置子页「AI 翻译」+ 翻译页来源下拉
  ```
- **协议层（`Services/Translation/Llm/`）**：
  - `ChatModels.cs`（与协议无关的消息/端点/选项/结果/用量/流式状态/异常）、`IChatProtocol.cs`（`BuildRequest` / `ParseResponse` / `ParseStreamLine` / `DescribeError`）、`ChatProtocolFactory`（注册表）、`ChatClient`（发送、重试、超时、流式、连通性自检）。
  - 四个适配器与差异（**只在四处不同，都收敛在适配器里**）：
    | 协议 | 认证 | 路径 | system 位置 | 输出上限 | 流式帧 |
    |---|---|---|---|---|---|
    | OpenAI 兼容 | `Authorization: Bearer` | `{base}/v1/chat/completions` | `messages[0].role=system` | `max_tokens` | SSE `data:` + `[DONE]` |
    | Azure OpenAI | `api-key` | `/openai/deployments/{部署名}/chat/completions?api-version=` | 同上 | 同上（新模型可置 0 表示不下发） | 同 SSE |
    | Anthropic | `x-api-key` + `anthropic-version` | `/v1/messages` | **顶层 `system`** | `max_tokens` **必填** | `content_block_delta` / `message_stop` |
    | Gemini | `x-goog-api-key` | `/v1beta/models/{model}:generateContent`（流式 `?alt=sse`） | `systemInstruction` | `generationConfig.maxOutputTokens` | SSE `data:`（数组分块） |
  - **地址归一化**（手输地址最常见的坑）：已是 `…/chat/completions` 原样用；**裸域名**才补 `/v1`；**已有路径**只补 `/chat/completions`——这样 Ark 的 `/api/v3`、DashScope 的 `/compatible-mode/v1`、OpenRouter 的 `/api/v1` 都不会被写坏。
  - **健壮性**：429/5xx/超时/网络异常重试 2 次（0.6s、1.8s 退避，尊重 `Retry-After`），4xx（除 429）不重试；超时由每次请求自己的 CTS 控制（`Timeout=Infinite` + 每请求 `CancelAfter`）；流式只在建立连接前失败才算失败（已开始输出不再重试，避免重复计费）；错误统一成"HTTP 401：API Key 无效（Invalid API key provided）"这种可直接展示的一句话；流里混入心跳/注释行一律忽略。
  - **不做 Bedrock / Vertex**：前者要 SigV4、后者要 OAuth2 服务账号签名，建议在应用前面挂 one-api / new-api / LiteLLM 网关转成 OpenAI 协议，而不是在客户端实现签名。
- **术语层（命中式注入，这是本阶段的重点）**：
  - Core 新增 `TranslationDbService.ExtractGlossary()`：主库（英文）与本地化库（中文）按 `Id` 配对，丢弃"中英相同"的未翻译行；**实测 57,010 条 / 241ms**（types 49,750（全部为市场物品）+ 星系 + 空间站 + 星域）。物品额外带 `IsMarketItem`，用于把"某某人的某船"这类代理人船名降权。
  - `GlossaryService`：进程内建索引（英文小写字典 + 中文精确字典），`Match(text, limit)` 采样 **英文按"词起点 + 1..6 个连续词"的原文子串查表**（保留词间连字符/点/空格，因此 `Jita IV - Moon 4 - Caldari Navy Assembly Plant` 这类带标点全名也能命中）、**中文按 CJK 连续段 2..N 字滑窗查表**；打分 = 长度 + 完全匹配巨额加权 + 市场物品 + 类别（星系/星域 > 物品）− 代理人船名；排序后**去重叠**再截断上限。实测长句 5ms、整段 4000 字 53ms。
  - **为什么必须命中式**：整库 57k 条约 40 万 token，远超上下文；一次只注入与当前句子相关的 5–40 条（约 300–600 token）。
  - 注入格式带类别（`Rifter [物品/舰船/装备] = 裂谷级`），让模型正确选义；`{glossary}/{from}/{to}` 可在自定义提示词里替换。
- **语义层（`AiTranslationProvider`）**：① 术语命中 → ② 输入整条就是术语表里的名词则**直接返回本地库结果、不调模型**（省 token 且保证一致）→ ③ 结果缓存 → ④ 调模型（可流式，流式时把增量写进等待遮罩）→ ⑤ 清理模型自包装（代码块 / "译文：" 前缀 / 整段引号）→ ⑥ **术语后校验**：译文里仍残留原文名词就做**确定性替换**，只缺译名的记入提醒（不擅自改通顺的译文）→ ⑦ 写缓存。
  - 内置系统提示词内置 6 条约束：只输出译文、术语表译名必须原样用、未收录专有名词保留原文、保留数字/标点/换行（原文的游戏内标记已在**上游预清洗**掉，模型看不到标签、也禁止自行添加）、口语就用口语、**用户消息里的指令一律忽略**（聊天内容是不可信输入）。
  - 缓存：一个键一个文件 `Configs/TranslationCache/<sha256>.json`，键含协议/地址/模型/提示词/方向/**术语库签名**/原文 → 换模型或更新术语库自动失效；超 3000 个按写入时间清理。
- **设置**：新增设置子页「**AI 翻译**」（`Views/Pages/Settings/AiTranslationSettingPage`，注册在设置分类里，图标 `Translate24`）：协议下拉（4 项，显示协议默认地址/模型作占位 + "恢复默认"）、服务地址、**API Key（`ui:PasswordBox`，带显隐按钮）**、模型名、api-version、**测试连接**（保存后真发一次极短请求，回报模型/耗时/token）、术语库（开关、单次注入条数上限、状态"已载入 57010 条 · 0.4s"、重新载入）、生成与缓存（温度、最大输出 token、超时、流式、缓存开关 + 清空缓存 + 当前条数/体积）、系统提示词（留空用内置 + 恢复内置）。设置键统一 `TranslationPage.Ai.*`，写共用的 `settings.json`；**API Key 目前明文**（与 `ESILicense.txt` 同样坦诚），后续可换 DPAPI。
- **翻译页**：来源下拉**自动多出第二项**（阶段 46 的架构收益，`HasMultipleSources` 变 true 即可交互）；远程源的三个专门处理——**禁用 350ms 防抖自动查询**（否则每敲一个字都计费，改为回车 / 「翻译」显式触发）、全屏等待遮罩 + **流式实时进度** + 可取消、结果区新增「**术语命中（已注入提示词）**」与「**模型信息**」两栏（都是只读 `TextBox`，可选中复制，沿用阶段 46 追加的做法）；空结果文案按来源区分（本地库"没匹配的名词"、AI"未返回译文"）。
- **核验**（一次性探针 + 本地 mock HTTP 服务；脚本与截图核对后已清理）：
  - **离线探针 52 项断言全 PASS**：术语库 57,010 条/241ms、长句命中 5ms（`Machariel` 150 分 > `Rifter`/`The Forge` 120 > `Jita` 70，完全匹配加权生效）、命中不重叠、超长文本受上限约束；四个协议的 URL/认证头/请求体形状/非流式与流式解析/用量字段/错误归一化/429 重试（2 次后成功）全部符合预期；端到端在 mock 后端上跑通"术语注入 → 生成 → 残留英文被替换成 `裂谷级` → 缓存命中不再请求 → 单个名词短路不调模型 → 流式进度回调 3 次"。
  - **实机 UIA + 截图**：设置页「AI 翻译」正常渲染（协议下拉 4 项、密码框、术语库状态"已载入 57010 条 · 0.4s"、当前 0 条缓存）；把来源设为 AI 后——**未配置黄色横幅**（两行完整显示）、**"按回车或「翻译」才会发送请求"提示**、**输入一句英文不产生任何结果行**（确认没有自动请求）、**按回车后右下角弹出警告通知**；切回本地库源输入 `Rifter` 仍正常出 `裂谷级`；运行期间应用日志无新增错误。
- **实现中修掉的两个 UI 缺陷**（都是实测才发现）：
  1. **警告横幅文字被裁**：横幅内层是**横向 `StackPanel`**，它给子元素**无限可用宽度** → `TextWrapping` 不生效、长文案被裁。改成两列 `Grid`（`Auto` + `*`）后正常换行（与阶段 41 标题行同一个坑）。
  2. **来源下拉选到第 2 项时显示空白**：`RefreshSourceNames()` 先 `Sources.Clear()` 再逐项 Add，`Selector` 在集合被清空时会把 `SelectedIndex` 重置为 **-1**，之后 VM 虽发了通知仍可能被这次重置盖掉 → 下拉框空白（但 VM 里来源是对的，功能正常、只是显示不出来）。修法：**内容没变就不清空集合**（`SequenceEqual` 判断），并在集合确实变动后用 `Dispatcher.BeginInvoke(Background)` **再落一次选中项**兜底。
- 构建状态：`dotnet build` **0 错误**（仅剩既有警告）。
- 未做/后续：① **按行批量翻译**（一次请求多行，编号输入 + JSON 输出）；② 术语库**落盘缓存**（当前每进程重新抽取 ~0.5s，够快但可以更快）；③ **API Key 加密**（DPAPI）；④ **并发/限流**（当前一次一个请求，频道场景可加令牌桶）；⑤ 结构化输出（`response_format=json_object` 已在协议层支持，语义层未用）；⑥ 频道翻译的**按行批量**（S4 已做预清洗，批量未做）。
- **追加（S4 完成）：频道翻译接入**（原占位页 `ChannelTranslationPage.cs` 已替换，类名/命名空间不变）：
  - **设置**：`Services/Settings/ChannelTranslationSettingService.cs` 按角色持久化 `Configs/ChannelTranslationSettings.json`，**与 WinUI 同路径同格式**（JSON 数组），两边配置互通；`CoreInitializer` 里初始化。Core 的 `ChannelTranslationSetting` 补两个字段（`AutoTranslateToZhOnly`、`MinLength`，WPF 新增、WinUI 不使用，JSON 向后兼容）。
  - **聊天标记预清洗** `Services/Translation/ChatMarkupProtector.cs`：`<br>` 先换成换行，**其余 `<...>` 标签整体删掉**（`<font size= color=>`、`<b>/<i>/<u>/<loc>`、`<a href="joinChannel:…">`…），再解 HTML 实体、压缩空行与多空格；对外只有 `ToPlainText(text, out removedTags)`（送模型用）与 `ToSingleLine(text, max)`（列表单行预览用）。**不再有 `{{n}}` 占位符保护/还原**——颜色/字号/链接对翻译没有价值，先删掉更稳、prompt 更短、译文直接可读（见下方"用户反馈：标签必须先剔除"）。
  - **翻译引擎** `Services/Translation/ChatTranslationEngine.cs`：多角色多频道的消息进**同一条队列串行翻译**（AI 按次计费且易限流，串行最省心）；单条流程 = **预清洗**（`<br>`→换行、其余标记删除，`RemovedMarkup` 记录删了几个）→ 跳过规则（空/过短/自己的发言/以中文为主/无字母，**按清洗后的纯文本判断**）→ 调翻译源（默认 AI，构造参数可注入替身）→ 发布 `Translated`（后台线程回调，订阅方自行切 UI 线程）；`ChatTranslationItem` 额外提供 `OriginalPreview`（160 字单行预览）与 `OriginalFull`（完整清洗文本），列表与悬停提示都用清洗后的文字；**"AI 未配置"只提示一次并清空队列**，不刷屏。
  - **会话** `Services/ChannelIntel/ChannelTranslationSession.cs`：每角色按勾选频道创建 Core 的 `ChannelTranslationObserver`（增量读聊天日志 + 触发关键词过滤），命中 `Important` 的消息入队。
  - **界面** `Views/Pages/ChannelTranslationPage.xaml(.cs)`（替换占位页）+ `ViewModels/Channel/ChannelTranslationViewModel.cs` + `Views/UserControls/ChannelTranslationResultView.xaml(.cs)`：三卡片（目标角色 / 频道与参数 / 实时译文），参数 = 跳过自己、只翻译非中文消息、最短长度、触发关键词（**改动即落盘**，运行中禁用）；工具栏为图标按钮（开始/停止/开始全部/停止全部/刷新/清除/弹窗，停止按钮只把图标染红）；译文列表新→旧、上限 300 条，**原文与译文都可拖选复制**，右键可单独复制原文/译文；「弹窗」用 `ToolWindow` 承载**同一个结果视图**并共享同一个 ViewModel（两边内容实时一致；不设 `Owner`，与翻译页弹窗同一策略）。
  - **核验**（离线探针 20 项断言全 PASS + 实机页面核验）：预清洗把 4 个标签全删掉、无标记文本原样通过；引擎只翻译 2 条合规消息（自己、纯中文、过短、无字母各被跳过）、**送翻译的文本不含标签**（`<font>/<loc>` 都没了）、清单带频道/发言人、计数正确、失败路径带 Error 且未配置只发一条、停止后不再处理新消息；设置落盘读回（含两个新字段）+ 深拷贝 + 与 WinUI 同格式（探针结束时**还原了用户的设置文件**）。实机：频道 → 频道翻译页正常渲染（目标角色列出本机两个角色、AI 未配置黄条、空状态引导、未选角色时"频道与参数"卡片按设计隐藏），运行期间日志无异常。
  - **本轮踩到并修掉的严重缺陷（值得单独记）**：追加语言键的脚本把已有的 `AiSettingPage_RestorePrompt` **重复追加**了一次，导致 `zh-CN.xaml`/`en-US.xaml` 出现**同名键**——`ResourceDictionary` 加载时抛 `ArgumentException: Item has already been added`，**应用直接启动崩溃**（`0xE0434352`），而 `dotnet build` **0 错误**、应用日志**一个字节都不写**（异常发生在启动的字典加载阶段）。定位手段：写了一个"加载应用级资源字典 + 直接构造各页面"的探针（`PageProbe`），它把 `XamlParseException → ArgumentException: Item has already been added` 的完整链条打了出来；修复后同一探针 6 个页面全部构造成功。**结论：改语言文件后必须查重键**（脚本或探针皆可），参见 §9 第 33 条。
- **追加（S5 完成）：术语管理 UI + AI 新词回流**（设置 → 术语表）：
  - **用户术语表** `Services/Translation/UserGlossaryService.cs`：持久化 `Configs/UserGlossary.json`（英文名唯一、忽略大小写，重复添加=更新；增删改查 + 深拷贝快照 + 内容签名）。用户术语参与**三条路径**：① **注入提示词**——术语库匹配里 `Id<0` 的用户条目 **+2000 分**，同名词条必然压过 SDE（实测 `Rifter` 命中用户译名而不是"裂谷级"）；② **术语后校验**——命中列表里就已经是用户译名，残留替换随之生效；③ **本地数据库源**——`LocalDbTranslationProvider` 用 `ApplyOverrides` 直接覆盖 SDE 译名（离线源也认用户术语）。`Pinned` 字段保留（当前所有用户术语都按"必须原样使用"处理），UI 未提供开关以免出现无效选项。
  - **AI 新词候选** `Services/Translation/GlossaryCandidateService.cs`：翻译成功后从原文里挑"像专有名词（大写开头、可含数字/连字符/撇号，1–3 词）、术语库（SDE+用户）没有、**且没有原样出现在译文里**"的片段记成候选（计数 + 原文/译文样例 + 忽略名单，上限 300，停用词表挡掉 The/Hello 这类）。**刻意不自动写入术语表**：模型输出无法可靠反推"哪个英文词对应哪个中文词"，自动写入会把错译固化下来（比不写更糟），因此只排队、由人在设置页确认。开关 `TranslationPage.Ai.CollectCandidates`（默认开）。
  - **设置子页** `Views/Pages/Settings/GlossarySettingPage.xaml(.cs)`（分类已注册，图标 `LocalLanguage24`）：用户术语表（英文名/中文名输入 + 添加 + 列表内逐条删除 + 状态提示）、AI 新词候选（列表 + 「加入术语表」把英文填进输入框并聚焦中文框 + 「忽略」+ 「清空候选」）、术语库状态（SDE 条数 / 用户条数 / 重新载入）。
  - **核验**（离线探针 **24/24 PASS** + 实机设置页）：用户术语增删改查/大小写唯一/落盘/签名变化（AI 缓存自动失效）；用户术语**压过 SDE**、用户独有术语（含撇号与空格 `QEDSD's Fortizar`）能命中、中文侧命中、`IsKnownEnglish` 认得用户术语、删除后回落 SDE；本地库源被用户译名覆盖；候选四类规则（未知名词记、SDE 已有不记、原样保留不记、停用词不记）+ 重复计数 + 忽略后不再记 + 清空。实机：设置 → 术语表正常渲染（四个分区、空状态引导），**通过界面添加术语确实落到 `UserGlossary.json` 且列表与右下角提示同步**，日志无异常；探针/核验脚本结束时都**还原了用户的术语文件**。
  - **本轮修掉的两个真实缺陷（都是"看起来能用、其实不生效"）**：① **用户术语只在 SDE 术语库构建时并入索引** → 在设置页新加的术语要等下次重建（或重启）才生效；改为按**签名比对的懒刷新** `EnsureUserTermsFresh()`，在 `Match` 与 `IsKnownEnglish` 入口各调一次（签名没变时零成本）。② **中文匹配只扫纯 CJK 连续段** → `小裂谷2`、`10MN加力燃烧器` 这类**中英/数字混排**的译名（SDE 与用户术语里都常见）永远查不到；改为"词字符段（汉字/字母/数字）里只要含汉字就做 2..N 字滑窗"，并跳过不含汉字的候选。
- **追加（用户反馈"频道置顶信息（MOTD）没有译文输出"）**：用用户给的真实 MOTD（2134 字符、92 个 `<...>` 标签）写了一个复现探针（`MotdProbe`，含本地 mock 模型），定位到**两个独立问题**（外加两处与该场景直接相关的短板）：
  1. **模型返回空 `content` 被当成"成功"**（用户症状的直接原因）：provider 只看 HTTP 是否成功，不看正文——服务端因 `finish_reason=length`（撞上输出 token 上限）或**推理模型只返回 `reasoning_content`** 时 `content` 是空串，于是返回"成功 + 空译文"：**翻译页什么都不显示**（频道页至少会说"没有返回译文"，但不说原因）。修法：正文为空即**明确失败**，并把原因与处置写进消息——`length → 调大「最大输出 token」（或设为 0 交给服务端默认）`；响应里只有 `reasoning_content → 提示换用普通对话模型`。翻译页与频道页都会看到这句。
  2. **长文本没有分片**：MOTD 这类 2000+ 字符的文本原先只发一次请求，很容易撞输出上限。现在按"配置的输出上限 × 2 字符"分片（夹在 400–4000），在段落/句末/换行处断开，逐片翻译后合并（切的是**清洗后的纯文本**，不存在标签跨片问题），meta 里标注"3 片"。
  3. **provider 不做预清洗**：此前只有频道引擎处理 `<font>/<url>/<loc>`，**翻译页直接粘贴的原文**会把标签原样丢给模型。现在 provider 自己也先 `ToPlainText`（引擎已清洗过时再洗一次是 no-op，两条入口安全共用），原文只剩标记时直接失败"原文里没有可翻译的正文（只有游戏内标记）"。
  4. **翻译页输入框是单行的**：粘贴多行 MOTD 会被折平。改为多行（`AcceptsReturn`、最大高 140）并换用 `PreviewKeyDown`：**回车=查询、Shift+回车=换行**——多行 `TextBox` 会在 `KeyDown` 的类处理器里把回车当换行吞掉，所以必须用 Preview 事件。
  5. **频道启动时不再漏掉 MOTD**：Core 观察者只读"启动之后"新增的内容，而「频道置顶信息」是**加入频道时**就写进日志的 → 之前永远翻不到。新增 `Services/ChannelIntel/ChannelChatLogReader.cs`：会话 `Start()` 时补翻**日志尾部 20 行**（含 MOTD 与最近几句；同一段文本第二次会命中 AI 结果缓存，不会重复计费）。
  - **核验**（`MotdProbe` 7 组，全部通过）：① 日志行解析——国际服 `EVE System > Channel MOTD:` 与国服 `EVE系统 > 频道置顶信息：` 两种前缀都能解析出 2134 字符正文；② 预清洗 92 个标签全删、`<br>` 换成换行；③ 真实 provider + mock 模型：译文含中文、**prompt 里不含任何 `<...>` 标签**；③b 分片：`MaxTokens=300` → 3 片，合并后译文完整、meta 标注片数；④⑤ 空 content → provider 与频道引擎都给出可诊断的原因；⑥ 补翻日志尾部能读到 MOTD（2134 字符那条）且关键词过滤生效；⑦ 回归：术语注入 2 条且提示词含类别、后校验把残留 `Rifter` 换成"裂谷级"、缓存命中后不再请求、单个名词短路不调模型。实机：翻译页回车仍出结果（本地库 `Rifter` → `裂谷级`）、**Shift+回车确实插入换行**；设置文件与探针临时文件都已还原，应用日志无异常。
- **追加（用户反馈"应该提前把 html 类型的标签去掉"）**：把 S4/MOTD 那一版的"**占位符保护 + 容错还原**"整体换成"**送模型前预清洗**"——用户给的例子正是 `<a href="joinChannel:player_a81a8acf334211e88b999abe94f5b483">` 与 `<br></font><font size="12" color="#bfffffff">` 这种"标签夹着正文"的写法，占位符方案在这类文本上必然要跟模型博弈（实测 92 个标签的 MOTD 就丢过占位符）。
  - **改动**：`ChatMarkupProtector` 重写为 `ToPlainText`/`ToSingleLine`/`HasMarkup`/`ExtractTags`，删除 `Protect`/`Restore`；`<br>` 先换行，其余标签整体删掉，再解实体（`&amp; &lt; &gt; &quot; &#39; &nbsp;`）、逐行去尾空白、连续空行压到一个、整体 Trim；单条正文里的换行**保留**（MOTD 的段落结构还在，译文也能按行读）。
  - **两条入口都预清洗**：频道引擎（`TranslateOneAsync` 先洗再判跳过规则，`RemovedMarkup` 记下删了几个）与 AI provider（翻译页直接粘贴的原文）各洗一次；内置提示词第 4 条同步改成"标记已在上游删除，译文里不要再出现任何标记"——**提示词里刻意不再出现 `<font>`/`<color>` 这类字面写法**（举例反而可能诱导模型去写标签，而且探针用"请求体里是否出现 font/size="判断有没有漏标签时也会被提示词本身误伤）。
  - **界面不再显示标签，且结果文字都能选中复制**：`ChatTranslationItem` 新增 `OriginalPreview`（160 字单行预览）与 `OriginalFull`（完整清洗文本，悬停 `ToolTip` 显示、右键复制原文复制的也是它）；列表里的原文/译文**改用只读 `TextBox`**（`TextBlock` 选不中文本，而用户明确要求"结果显示的所有文字都要能方便地复制"），外观与普通文本一致（无边框/透明底/无内边距）——之前 MOTD 那种多行原文会把行高撑得很大，右键复制出来的还是带标签的天书。
  - **核验 ①（`StripProbe`，用户真实 MOTD 原文：2134 字符 / 82 个 `<...>` 标签）15/15 PASS**：清洗后 724 字符、10 个换行；链接文字与 `&`/`!` 等正文一字不少；引擎交给翻译器的文本无标签；provider 发出的 prompt 无标签；译文原样返回；meta 含"去标记 82"；术语后校验与名词短路回归正常；纯标记文本 → 明确失败；无标记文本 → 原样通过。
  - **核验 ②（`ViewProbe`，12/12 PASS）**：把 `ChannelTranslationResultView` 挂到离屏窗口渲染，断言 XAML 绑定确实解析——原文行取到 `OriginalPreview`（值等于代码侧计算结果、`NoWrap`）、译文行取到 `Translation`、两者都是 `IsReadOnly=True` 的 `TextBox`、悬停 `ToolTip` 里取到 `OriginalFull`、截图落盘（`view_probe.png`，肉眼确认标题行 + 单行原文 + 译文三行，无 `<font>` 残留）。**这条探针当场抓到一个构建期查不出的崩溃**：`TextBox.Text` 默认 `TwoWay`，绑只读的 `OriginalPreview` 会抛 `XamlParseException`（详见 §9 第 27 条），补 `Mode=OneWay` 后转绿。
  - **核验 ③**：`dotnet build` **0 错误**（WPF + Core）。三个探针（`MotdProbe`/`StripProbe`/`ViewProbe`）与截图、`_buildcheck` 都是一次性产物，核验后已清理（探针源码不再留在仓库里）；运行中的旧进程已退出，语言文件未改动（未引入重复键）。
- **追加（用户要求"把 AI 翻译与本地数据库拆开，首个是 AI；AI 的界面按实际需要重新设计；两种结果都用富文本、方便像网页那样任选一段复制；AI 未配置时先提示去配置"）**：
  - **中间版本（已被下一轮取代，保留结论）**：先做成"同一页 `TabControl` 两个页签（AI 在前 / 本地在后）"。这一版确立了 `Controls/RichTextPresenter.cs`（只读富文本，可任意拖选复制）、`LocalTranslationViewModel`（由 `TranslationPageViewModel` 改名瘦身：删掉"来源下拉"与远程源分支，只认 `TranslationService.LocalDatabase`）与 AI 页的"未配置整页引导 + 「去配置」直达设置页"三件事；页签位置曾用设置 `TranslationPage.ActiveTab` 记住，**下一轮改成导航子菜单后该设置与页签一起删掉**（连同 `TranslationPage.Provider` 一起成为"旧值留在 settings.json 里但不再读写"的历史键）。
  - **核验（当时的 `PanelProbe`，33/33 PASS）**：TabControl 结构/默认页签、两视图 DataContext、未配置门控、富文本只读与局部选中、非流式与流式（SSE）端到端、失败路径、页签持久化、本地词库防抖回归（`Rifter` → `裂谷级`）。
- **追加（用户要求"把 AI 翻译与本地翻译拆成翻译下的两个子菜单（像频道下的频道预警/监控那样）；AI 翻译 UI 改成 AI 桌面端那样的对话记录，显示全部翻译内容、可开多个对话；每条翻译带一个开关决定后续翻译要不要带上它当上下文，上下文长度可设置"）**：
  - **导航改成两个子项**：`MainWindow.xaml` 里「翻译」由"单页导航项"改为**带 `MenuItems` 的分组**，子项 `Nav.TranslationAi`（AI 翻译 → `AiTranslationPage`）与 `Nav.TranslationLocal`（本地词库 → `LocalTranslationPage`），两者都 `NavigationCacheMode="Required"`，与「频道」下的子项写法完全一致。相应地删掉了原来的 `TranslationPage`/`TranslationPanelView`（宿主页 + 页签宿主）与设置里的 `TranslationPage.ActiveTab`。
  - **AI 页 = 对话式记录**（`AiTranslationChatView` + `AiChatTranslationViewModel`）：
    - 左侧**对话列表**（`AiChatSessionViewModel`）：`新建对话`、右键 `删除对话`/`清空当前对话`，每项显示标题（取第一条原文，可自动截断）、最后一条预览、更新时间；最近的排在最上面（产生新记录时把该对话 `Move` 到列表首位）。
    - 右侧**完整记录**：每次翻译渲染成"原文气泡 + 译文气泡"（都用 `RichTextPresenter`，可任意拖选复制），译文气泡里带 `复制译文/复制原文` 与**「加入上下文」勾选框**、以及模型/耗时/token 与本次命中的术语；生成中显示"生成中…"（流式时增量直接长在气泡里），失败时气泡里显示红色原因。
    - 底部**输入区**：方向下拉 + 端点摘要 + 多行输入（回车=翻译、Shift+回车=换行）+ `翻译/停止/清空/弹窗`；输入区上方一行显示"本次带上 N 条上下文"与**上下文条数下拉**（0/2/4/6/10/20）。
    - 记录区用 `ItemsControl` + `ScrollViewer`（不是 `ListBox`）：列表项的选中语义会跟"在气泡里拖选文字"打架；新记录与翻译完成时由 VM 的 `ScrollToEndRequested` 事件驱动滚到底部。
  - **上下文机制（用户要的"选择按钮 + 长度设置"）**：每条形如 `原文：… / 译文：…` 的一段文本，取自**同一对话里更早的、勾了「加入上下文」的、成功的**记录，按设置里的条数上限取**最近 N 条**（`AiTranslationSettings.ContextLimit`，设置键 `TranslationPage.Ai.ContextLimit`，0 = 完全不带；单侧文本超过 400 字符会截断，避免整段 MOTD 撑爆上下文）。这些行走 `TranslationRequest.Context`，由 `AiTranslationProvider.BuildMessages` 拼成"以下是同一对话里已经翻译过的前文（仅供理解上下文，不要翻译它们）"，**另一条对话的记录永远不会混进来**。设置页「AI 翻译 → 上下文条数上限」与 AI 页底部下拉改的是同一个键（页里快捷改会立刻写盘）。
  - **历史落盘** `Services/Translation/AiTranslationHistoryService.cs`：`Configs/AiTranslationHistory.json`，**按对话整体 upsert**（不是整份覆盖，所以页面与弹窗同时开着不会互相抹掉），上限 40 个对话 / 每对话 200 条记录，空对话不落盘（点了「新建对话」没翻译不会留垃圾）；文件损坏时退回空表并记日志。
  - **核验（`ChatProbe`，39/39 PASS）**：探针按真实启动顺序跑 `CoreInitializer.Init()`（真 SDE 数据库）+ 原始 `TcpListener` 的 mock 大模型服务（**记录每一次请求体**，按 `"stream":true` 返回 JSON 或 SSE），把 `AiTranslationChatView` 挂进离屏窗口后断言：
    - 初始：无对话、未配置时整页引导且对话列表隐藏；配置后 `IsConfigured=true`。
    - 记录：第 1 次翻译产生一条记录、译文与 meta 落位、会话标题取自第一条原文。
    - **上下文真的进了请求体**：第 2 次翻译的请求体里出现第 1 条原文；把第 1 条的「加入上下文」关掉后，第 3 次请求体里**不再出现**它、而仍勾选的第 2 条还在；上下文设为 0 时第 4 次请求体里既没有第 1 条也没有第 3 条；设为 2 时第 5 次请求体只带最近两条（含 `Gamma three.`/`Delta four.`、不含更早的 `Alpha one.`）——同时校验了"逐条开关"和"长度上限"两件事。
    - **对话隔离**：新建第二个对话后翻译，请求体里没有任何第一个对话的内容。
    - **持久化**：历史文件已落盘；新 VM 重新载入后两个对话与逐条的上下文开关都保留；删除对话后文件里也只剩一个。
    - 流式：SSE 三段增量拼成译文、meta 带流式用量；失败：连接被拒时该条记录 `ShowError=true` 且**不会被当作上下文**（`CanUseAsContext=false`）；清空当前对话后记录为零；富文本气泡 `IsReadOnly` 且能只选中一段。
    - 本地词库页回归：输入 `Rifter` 不点按钮、350ms 防抖自动查到 100 条，`Rifter → 裂谷级`。
    - 实机：`dotnet build` 0 错误；启动真实应用后用 UIA 展开左侧「翻译」→ 出现「AI 翻译 / 本地词库」两个子项，点开 AI 翻译后页面元素为"对话 / 新建对话 / 上下文 / 输入 / 空状态引导"，端点显示为用户配置的 `deepseek-flash`；点「本地词库」也正常渲染。探针、截图、临时日志目录与临时历史文件核验后全部清理（探针跑前先备份、跑完按原始字节还原 `settings.json` 与 `AiTranslationHistory.json`）。
- **追加（用户对上一轮的四点修正要求）**：①"上下文开关不该每条翻译一个，应该**一个对话一个**，勾上后这个对话每次翻译都带上下文，**也不对上下文数量限制**"；②"发送的原文靠右显示，返回的译文保持靠左"；③"原文超过三行显示省略、末尾带展开按钮，点击才显示全部"；④"等待过程中不要用全局等待效果，改成**针对当前对话/当前这条**的加载效果显示在译文位置，等待期间还能继续发原文，下一条也在自己的译文位置显示等待"。
  - **上下文改成对话级、且不限条数**：开关从"每条译文一个"移到**记录区底栏**（`CheckBox` 绑 `SelectedSession.UseContext`，`AiTranslationSession.UseContext` 落盘），一条对话只有一个。打开后 `BuildContext` 把此前**所有**"翻译成功"的记录按 `原文：… / 译文：…` 顺序全部带上（**不再有条数上限、也不再截断单条文本**；上一版的 `TranslationPage.Ai.ContextLimit` 设置项、设置页的「上下文条数上限」卡片、页底的条数下拉与相关语言键已全部删除）。底栏右侧仍显示状态文字："已开启：每次带上此前 N 条记录" / "未开启：每次只翻当前这一条"。
  - **气泡对齐与三行折叠**：原文气泡 `HorizontalAlignment="Right"`、译文气泡靠左（实测 RichTextBox 在非拉伸时按内容宽度收窄，所以短消息的气泡会自然贴合文字）。原文超过三行时只渲染"前三行 + …"，末尾一个 `透明外观的「展开/收起」按钮`切换全文；三行是按**气泡宽度 + 字号折算的行数预算**（半角 74 单位/行、CJK 算 2 单位，`AiChatTurnViewModel` 里的 `CountLines`/`BuildPreview`），折叠态另外用 `MaxHeight=58` 兜底防止估算偏差时溢出。
  - **每条译文自己的加载效果 + 并发翻译**：新增 `Controls/SpinnerIcon`（`ui:SymbolIcon` 旋转动画，`IsActive=false` 时**自身隐藏**；仍然不用 `ProgressBar`/`ProgressRing`，见 §9 第 17 条），放在译文标题行；没有增量时在译文位置再显示"正在翻译…"。**AI 翻译页不再调用 `PageNotifyService.ShowWaiting/HideWaiting`**（全项目现在只有其他页面还在用全局遮罩），`IsBusy` 门闩也去掉了：每次翻译各自持有一个 `CancellationTokenSource` 与自己的气泡，`_pending` 列表驱动"正在翻译 N 条…"与「停止」（停止 = 取消全部），输入框在发送时立刻清空，所以等待期间可以连着发下一条，第二、第三条的等待效果各自出现在自己的译文位置。并发时"带上下文"只统计**已经成功**的更早记录（还没出译文的并发条目自然被排除）。
  - **核验（`ChatProbe2`，36/36 PASS）**：mock 服务支持按 `ResponseDelayMs` 延迟响应，用来制造真正的并发窗口。断言覆盖：长/短原文的折叠判定（长文预览以 `…` 结尾且 ≤3 行、展开后等于原文、按钮文字切换）、对话级开关默认关 + 关时请求体无历史、开时第 2 条请求带上第 1 条、**不限条数**（第 4 条请求把此前三条全带上）、关闭后又不带、**并发**（延迟 700ms 时连发两条 → `pending=2`、状态文字"正在翻译 2 条…"、输入框已清空、可视树里两个转圈都 `IsActive && Visible`、两条各自拿到自己的译文、完成后转圈全部停下、请求数正确）、**停止**（延迟中取消 → 该条标记"已停止"且不再 pending）、**原文气泡 `Right` / 译文气泡 `Left`**（直接从可视树读 `Border.HorizontalAlignment`）、历史落盘保留对话级开关、流式 SSE、失败记录在气泡里、富文本只读与局部选中、本地词库页防抖回归。截图（折叠态与"两条同时等待"的并发态）都人工看过：转圈只出现在等待中的那两条、失败气泡上没有残留图标。
  - **小改（用户追加）**：气泡底部的操作条收成"一行两端"——左边是模型/耗时/token 与术语命中，**最右侧只留一个「复制译文」图标按钮**（`ui:Button` 只给 `Icon`、`Appearance=Transparent`，配 `ToolTip` + `AutomationProperties.Name`，与 token 信息同一水平线）；**「复制原文」按钮删掉了**（原文仍可在气泡里直接拖选复制，或用右键/快捷键）。实机复核：UIA 按钮集合里 `复制译文` 存在、`复制原文` 已消失，截图确认复制图标在 token 行最右侧。
  - **小改 ②（用户追加："把气泡展示改成走 `ChatMarkupProtector.ToPlainText`"）**：**存盘保留原始文本，展示/复制/上下文一律用纯文本**——`AiChatTurnViewModel` 新增 `OriginalPlain`（构造时洗一次）与 `TranslationPlain`（译文每次赋值时洗）：
    - 原文气泡的显示、**三行折叠判定与预览**都基于 `OriginalPlain`（所以 MOTD 的标签不再占行数，折叠出的三行是真内容）；
    - 译文气泡显示 `TranslationPlain`（模型偶尔把标记照抄回来也挡掉），「复制译文」复制的也是它；
    - 对话列表的**标题与预览**同样取纯文本（之前预览里会出现 `<font si…`）；
    - **作为上下文发出去的也是纯文本**（`原文：{OriginalPlain} / 译文：{TranslationPlain}`）——否则第一条翻过的 MOTD 一旦被当上下文，标签又会从上下文里回到提示词，与"送模型前预清洗"自相矛盾。
  - **核验（`CleanProbe`，17/17 PASS）**：带标签的真实形态 MOTD（`<font>`/`<br>`/`<a href>`/`&gt;`/`&amp;`）走一遍：纯文本里标签全无、`<br>` 变 2 个换行、实体解码正确（`EVE系统 > …`、`RMT & 刷屏`）、**存盘仍保留 236 字符的带标签原文**；折叠预览无标签且正好 3 行、展开后等于纯文本全文；模型返回里带 `<font>/<br>` 时 `TranslationPlain` 同样干净且 `<br>` 转换行；会话标题/预览无标签；**渲染后的可视树里两个气泡的富文本都不含 `<`**；开上下文翻译第二句时，请求体里出现纯文本正文（`Welcome to the EVE Help Channel`）且**不含任何标签属性**（`font`/`size=` 都不出现，连提示词里也不再出现字面标签）；另外写了一份**旧格式历史**（每条带已废弃的 `UseAsContext` 字段）验证仍能载入并按纯文本显示。截图已人工确认两个气泡里都是干净文字。
- **追加（用户反馈"翻译这段内容会显示**已停止**"）——本轮抓到两个真 bug，先用探针复现、再修**：
  - **现象**：把一整段频道置顶信息（带 82 个标记，5KB 上下）+ 聊天记录粘进 AI 页翻译，气泡上出现"已停止"（有时译文只写了一半）。
  - **根因 ①（主因）**：`AiChatTranslationView.Unloaded → ViewModel.Dispose() → StopAll()`。**离开页面（切导航、页面被重新承载等）会把正在跑的请求全部掐掉**，而气泡里留下的文案就是"已停止"。实测复现（探针场景 6）：翻译途中把视图从窗口里摘掉一次 → `Unloaded=1`，该条立刻变成"已停止"、译文只落了 80 字。
  - **根因 ②**：`ChatClient.StreamAsync` 的超时**根本没生效**——`StreamReader.ReadLineAsync(token)` 在 .NET 10（当时 TFM）+ HttpClient 响应流上"服务端卡住不出字"时不会因为令牌取消而返回，所以一个中途卡死的流会一直挂着（探针场景 2 修前：8 秒卡顿 + 2 秒超时设置 → 14.2 秒后**成功**返回，等于没超时）。
  - **修法**：
    1. `AiChatTranslationViewModel.Dispose()` 只退订语言事件、**不再取消请求**（页面是缓存的，切走再回来结果还在）；真正中止只由界面上的「停止」触发，并在日志里记一行"用户点了停止，取消 N 条"。
    2. `ChatClient.StreamAsync` 改成**空闲超时**并用 `WaitAsync(TimeSpan, token)` 强制生效：每收到一段增量就把计时器往后推（`timeout.CancelAfter`），所以"生成很久但一直在出字"不会被砍，只有"连续 N 秒一个字都没有"才判超时（抛 `ChatException("请求超时（超过 N 秒没有收到新的内容）")`）。
    3. VM 的 `catch (OperationCanceledException)` 区分来源：自己的令牌被取消 → "已停止"；否则 → "请求已中断或超时"（不再把超时说成"已停止"），并写日志。
  - **核验（`TimeoutProbe`，10/10 PASS，修前 3 项 FAIL / 修后全 PASS）**：
    - 场景 6（切页面）：修前 `错误=已停止`、修后 `错误=(无)`（请求照常跑完）；
    - 场景 2（服务端卡住）：修前 14.2 秒后"成功"（假成功）、修后 5.3 秒给出"请求超时（超过 5 秒没有收到新的内容）"；
    - 场景 1（**总时长 24.5 秒 > 超时设置 5 秒**、但每 200ms 都在出字）：两片都翻完、译文 341 字 —— 这条专门盯着"空闲超时"语义，若忘了在每片之后重置计时器就会在第 5 秒断掉；
    - 场景 3（用户点停止）仍然是"已停止"；场景 5（正常翻译过程中 `Unloaded` 计数 = 0）；场景 4（非流式短文本）正常。
- **追加（用户追问"最大输出 token=0 也只能 4000 字符？其他 AI 客户端几万字是怎么做到的？"）——按模型真实能力重做分片与思考模式**：
  - **先把事实查清楚**（DeepSeek 官方文档，[Models & Pricing](https://api-docs.deepseek.com/quick_start/pricing)、[Thinking Mode](https://api-docs.deepseek.com/guides/thinking_mode)）：用户配的 `deepseek-flash`（DeepSeek-V4.1-Flash）**上下文 1M token、最大输出 384K token**，而且**思考模式默认开启、effort = high**。也就是说：
    - 原来的 `MaxChunkChars = MaxTokens × 2`（夹 400–4000，`MaxTokens=0` 时按 4000）**纯属自我设限**——4000 字符 ≈ 1000–2000 token，只占这个模型窗口的 0.1%；用户那段 5.7KB 的置顶信息本来一次就能发完。
    - 长文本翻译真正的瓶颈是**输出上限**（模型一次能写多少 token），而不是输入；各家客户端无非两种做法：装得下就整段一次发，装不下就分片-合并（map-reduce，就是本项目的分片逻辑），输出被截断就**从断点续写**。输入与输出是两个独立上限——这也是"AI 客户端能一次吃下几万字"的原因（几万字 ≈ 几万 token，远小于 1M 的输入窗口）。
    - 顺带解释了"为什么这么慢"：DeepSeek 默认的思考模式会在翻译前先写一大段思维链，而且**思考模式下 `temperature` 失效**（官方明确说明）。
  - **改法（用户确认后实施）**：
    1. **分片阈值改成显式设置** `TranslationPage.Ai.MaxChunkChars`（`AiTranslationSettings.MaxChunkChars`，设置页「单次请求最大字符数」），**默认 12000 字符**，**0 = 不分片**（整段一次发）；`MaxChunkChars()` 直接用它的值（不再从 `MaxTokens` 推算，也不再夹到 4000）。
    2. **思考模式** `TranslationPage.Ai.ThinkingMode`（`ThinkingModes`：`auto`/`off`/`low`/`high`，设置页「思考模式」下拉），**默认 `off`**；`OpenAiChatProtocol.ApplyThinking` 写成 `{"thinking":{"type":"disabled"}}`（低/高强度则 `enabled` + `reasoning_effort`）。**安全阀**：`AiTranslationSettings.ResolveThinkingMode()` 只在"模型名或地址里含 deepseek"时才真的下发——OpenAI/Azure/Anthropic/Gemini 与各类网关不认识这个字段，硬发可能 400，所以它们一律退回 `auto`（不下发）。
    3. **输出被截断自动续写**：`finish_reason == "length"` 时，用"原文 + 已译出的结尾 800 字符"再发一次，提示词要求"从断点继续、不要重复、不要解释"，最多续 3 次；仍没写完则在术语提醒栏显示"译文可能不完整：模型的输出上限把这一片截断了（已自动续写，仍未写完）…"。meta 里会标注 `续写 N 次` 与 `思考关闭`。
  - **核验（`ChunkProbe`，15/15 PASS）**：默认值（阈值 12000 / 思考 off）；思考模式判定（DeepSeek 端点 → `off`、`gpt-4o-mini` → `auto`）；**请求体**（DeepSeek 的 body 里确实有 `"thinking":{"type":"disabled"}` 而 OpenAI 的 body 里连 `thinking` 字段都没有）；分片（20000 字符 + 阈值 12000 → 2 次请求；阈值 0 → **1 次请求**）；**续写**（mock 先返回 `PART1` + `finish_reason=length` → 自动再发一次、续写请求体里带着已译出的 `PART1` → 译文 `PART1PART2`；meta 含"续写 1 次 · 思考关闭"）；**续写用尽**（一直返回 length → 共 4 次请求即 1+3 次续写，并给出"译文可能不完整"的提示）。
- **追加（用户要求"对话 header 应该可以重命名、翻译方向不应该只有中英文互译"）**：
  - **方向改成"源语言 → 目标语言"两个代码**（新增 `TranslationLanguages`）：源语言 9 项（`auto` + 中/英/日/韩/俄/德/法/西），目标语言 8 项；`TranslationRequest(Text, From, To)` 与 `TranslationOutcome.From/To` 取代原来的 `TranslationDirection` 枚举（枚举整个删掉）。自动方向按**正文脚本**判定：汉字 → 中文、假名 → 日文、谚文 → 韩文、西里尔 → 俄文，其余按英文；目标语言缺失/与源相同（例如原文是中文却要求译成中文）时自动改成"源是中文就译英、否则译中"，**与原来的自动方向行为完全一致**。Core 的 `zh-CHS` 归一成 `zh`，两边可直接互转。
  - **提示词与术语库**：方向写进**用户消息**（"请把下面的内容从英语 → 日语翻译，只输出译文："），系统提示词保持稳定（对 DeepSeek 的上下文缓存更友好），只在用户自定义提示词里出现 `{from}`/`{to}` 时才替换；**SDE 术语库只在中英这一对时注入**（翻日/俄/德…时注中英术语反而是错的）；续写请求也带上目标语言。缓存键里加了 `from->to`（换目标语言必须重新翻译，不能命中旧语言的缓存）。
  - **本地词库仍然只做中英**：请求非中英组合时**明确失败**并给出原因（不静默返回空）；本地页的下拉也仍然只有"自动 / 英→中 / 中→英"三项，AI 页才是"源语言 + 目标语言 + ⇄ 对调"两个下拉。设置分两套：`TranslationPage.Ai.From/To` 与 `TranslationPage.Local.From/To`；旧的 `TranslationPage.Direction`（0/1/2）只在初始化时读一次用于**升级迁移**，之后不再写。
  - **对话重命名**：`AiChatSessionViewModel` 增加 `BeginRename`/`CommitRename`/`CancelRename` + `TitleDraft`（草稿），标题栏双击、右侧铅笔按钮、会话列表右键「重命名对话」三种入口；回车确认、Esc 取消、失焦即确认；确认后写进 `AiTranslationSession.Title` 并立即落盘（清空标题则恢复成"按第一条原文自动取名"）。
  - **核验（`LangProbe`，26/26 PASS）**：语言归一化（`zh-CHS` → `zh`）与 8 种目标语言；自动方向四种脚本（英→中、中→英、**日→中**、指定 en→ja）；**请求体实证**——从 JSON 里取出 user 消息，英译日时是"请把下面的内容从**英语 → 日语**翻译，只输出译文："，英译中时是"英语 → 中文"；**英译日不注入 SDE 中英术语库**（system 消息里是"本次没有命中术语"）；结果方向回传正确；**同一段文本换目标语言会重新请求**（缓存键含方向）；本地词库英译中正常（`Rifter → 裂谷级`）而 en→ja 明确失败；重命名全流程（自动标题 → 进入编辑态草稿=当前标题 → 确认后标题与模型都更新 → 取消不改 → **重新载入 VM 后自定义标题还在**）；界面（两个语言下拉分别是 9/8 项且是本地化语言名、⇄ 对调把 en→ja 变成 ja→en、未编辑时看不到标题输入框、点重命名后出现可见输入框）。截图人工确认：标题栏显示自定义名字 + 铅笔按钮、输入行是「源语言 ⇄ 目标语言」。
  - **小改 ②（用户反馈："改名后左侧列表更新了但头部还是旧名字"、"语言选项应该提供自动两种语言互译，这样发中文还是英文都自动译成另一种"）**：
    - **改名不同步的根因**：头部标题绑的是**主 VM 的 `SessionTitle`**（`SelectedSession?.Title` 的快照），而左侧列表绑的是**会话自己的 `Title`**。改名只让会话发了 `PropertyChanged`，主 VM 没转发，所以列表更新、头部不动。修法：主 VM 在 `SelectedSession` 变化时**订阅/退订该会话的 `PropertyChanged`**，把 `Title` 的变化转发成 `SessionTitle` 通知（头部随改名立即刷新）。
    - **"自动检测"**（目标位置上的 `auto`，语义是"译成另一种语言"；**界面文案按用户要求就叫「自动检测」**，源/目标两处同名，靠位置区分）：`TranslationLanguages.TargetOptions` 在目标语言下拉**最前面**加了一项 `auto`（本地化键 `TranslationPage_Language_AutoTarget`，与源语言的"自动检测"分开一个键，方便以后改文案）。解析规则：目标为 auto / 为空 / 与源语言相同时，**源是中文就译成英文、否则译成中文**——于是「自动 → 自动」= **中英双向自动互译**：发中文自动译英、发英文自动译中、发日/俄等则译成中文；**新装默认就是 (auto, auto)**（旧的 `Direction=0` 迁移过来也是它），不用再改设置。本地词库页仍固定中英三项（它的"自动"本来就双向）。
    - **核验（`AutoProbe`，15/15 PASS）**：四种解析（中→英、英→中、日→中、指定源+目标自动）；清掉四个方向键模拟"全新安装"后默认是 `auto/auto`；**端到端**——同一份设置下发中文时请求体里是"中文 → 英语"、发英文时是"英语 → 中文"（从 JSON 的 user 消息里取出来断言）；目标语言下拉第一项是「自动检测」（共 9 项）；**改名后头部立即显示新名字**（同时断言旧名字不再可见、VM 的 `SessionTitle` 同步、左侧列表也是新名字）。
  - **小改 ③（用户追问："如果使用者是俄语，就不能自动俄语跟中文或者跟英文互译了"）——自动互译的语言对可配置**：
    - **问题**：上一版把「自动检测」（目标位置）的翻转规则**写死成中↔英**（源是中文就译英、否则译中），所以俄语使用者拿不到"俄语 ⇄ 英语"或"俄语 ⇄ 中文"。
    - **改法**：新增设置 `TranslationPage.Ai.PairA` / `TranslationPage.Ai.PairB`（**默认 中/英**，保持老行为），界面上在**目标语言选「自动检测」时**才显示一行「互译语言对：[A] ⇄ [B]」（带对调按钮；选具体目标语言时整行隐藏）。解析规则（`TranslationLanguageHelper.Resolve` 的 5 参重载）：
      - 原文是 **B** → 译成 **A**；原文是 **A** → 译成 **B**；**其它语言 → 一律译成 A**（所以把 A 设成自己的母语即可：俄语使用者设成"俄语 ⇄ 英语"就是俄英双向 + 中/日等翻成俄语）。
      - 目标语言是具体语言时按目标走（语言对不参与）；A/B 相同时自动把 B 换成另一种语言。
    - **核验（`PairProbe`，18/18 PASS）**：六种解析组合（俄⇄英：俄→英 / 英→俄 / 中→俄；俄⇄中：俄→中 / 中→俄；默认 中⇄英 三条回归含"日文→中文"）；指定目标语言时语言对不参与；语言对落盘且**两种语言不允许相同**（`ru/ru` 自动变成 `ru/zh`）；**端到端**——设成俄⇄英后发俄文时请求体是"俄语 → 英语"、发英文时是"英语 → 俄语"；界面（目标=自动时两个语言对下拉都在、都是 8 种具体语言不含"自动"、对调按钮把俄⇄英变成英⇄俄、**切成具体目标语言后整行隐藏、切回自动又出现**）。截图人工确认输入行是「源语言 自动检测 ⇄ 目标语言 自动检测 互译语言对 俄语 ⇄ 英语」。
  - **小改 ④（用户要求："译文超过 5 行后提供收起按钮，跟原文一样；输入下一条原文后自动把上一次译文收起来，只自动收一次；手动展开过的不再自动收"）**：
    - `AiChatTurnViewModel` 的折叠逻辑抽成**带参数的** `CountLines(text, unitsPerLine)` / `BuildPreview(text, maxLines, unitsPerLine)`，于是原文（3 行、气泡宽 560 → 74 半角单位/行）与译文（**5 行**、整行宽度 → 100 半角单位/行）共用同一套估算；译文侧新增 `CanExpandTranslation` / `IsTranslationExpanded` / `TranslationDisplay` / `TranslationExpandLabel` / `ToggleTranslationExpansion()`，界面在译文文本下方右对齐放「展开/收起」按钮，收起态 `MaxHeight=96`（约 5 行）并隐藏内部滚动条。
    - **自动收起（只收一次）**：`TranslationExpandTouched` 记录"使用者亲手点过展开/收起"；`AiChatTranslationViewModel` 在**新的一条原文入队后**调用 `session.AutoCollapsePreviousTranslations(newTurn)`，只对"没被干预过且确实超行"的更早记录收起。新来的那条默认**展开**（能看着译文流式长出来），重新载入历史时只有最后一条展开、其余收起（长对话不会一打开就铺满屏幕）。
    - **核验（`CollapseProbe`，17/17 PASS）**：长译文出现收起按钮、新来的默认展开且按钮是「收起」、短译文没有按钮；自动收起后**正好 5 行 + 省略号**、按钮变「展开」、重复自动收起幂等；**人工展开过之后再自动收起不动它**；流式增量里"长过五行"时按钮才出现；**端到端**三条消息——发第 2 条后第 1 条被自动收起（`t1=False, t2=True`），人工展开第 1 条后再发第 3 条 → **第 1 条保持展开**、第 2 条被自动收起（`t1=True, t2=False, t3=True`）；界面（可见的展开/收起按钮 3 个、最新译文气泡是全文、被收起的气泡 `MaxHeight=96`）。截图人工确认：上一条译文显示 5 行 + 「展开」，最新一条显示全文 + 「收起」。
- **追加（用户要求"把频道翻译移到翻译子菜单下面，然后根据 AI 翻译把频道翻译功能重构"）**：
  - **导航**：频道翻译从「频道」组移到「**翻译**」组，现在是 `AI 翻译 / 频道翻译 / 本地词库` 三项（`MainWindow.xaml` 里只搬了 `NavigationViewItem`，「频道」下留 5 项）。
  - **按 AI 翻译页那一套重构**（用户选的是"保留三卡片，只把渲染/交互升级"+"加上下文、默认开"）：
    1. **抽公共实现**：行数预算抽成 `Services/Translation/TextCollapse.cs`（`CountLines` / `BuildPreview` / `IsWide`），AI 页气泡与频道列表共用——原来 `AiChatTurnViewModel` 里那份私有实现已删除。
    2. **渲染与交互**：频道结果列表换成 `RichTextPresenter`（可任意拖选复制）——原文保持**单行预览**（频道刷屏，列表要紧凑）+ 悬停看全文；译文**超过五行折叠**并提供「展开 / 收起」（`ChannelTranslationItemViewModel` 包一层，与 AI 页同一套 96px 收起高度）；每条的 **meta**（模型·耗时·token）与**术语命中/术语提醒**、`去标记 N` 提示都按 AI 页的样式显示，**复制译文图标放在 meta 行最右侧**；右键菜单里的复制原文/译文保留。
    3. **meta/术语真正落进条目**：引擎把 `TranslationOutcome.Meta` / `GlossaryHits` / `GlossaryWarning` 写进 `ChatTranslationItem`（以前只有译文和错误）。
    4. **带上上下文（默认开、条数可设）**：`ChatTranslationEngine` 维护"每个 **角色+频道** 最近若干条已译记录"（形如 `原文：… / 译文：…`，内存上限 20 条），按 `ContextLimit`（默认 4）取最近几条作为下一条的上下文发给模型；参数卡里多了「带上上下文」开关与「上下文条数」。**不同频道互不串**，关掉就逐条独立。
    5. **方向跟共享设置**：不再写死"外文→中文"，改用「AI 翻译」页的源/目标语言与互译语言对（引擎的默认翻译委托读 `TranslationSettingService.AiFrom/AiTo`，provider 自己解析语言对），频道页只读显示当前方向（改一处两处生效）。
    6. **按角色的开关随消息入队**（`ChatTranslationOptions.FromSetting`）：跳过自己/只译非中文/最短长度/上下文这些**原来是挂在共享引擎上的**，多开两个角色时后启动的会覆盖前一个；现在每条消息带自己的开关快照，顺带修掉这个老问题。
    7. **未配置整页引导**：AI 未配置时整页只显示引导（与 AI 翻译页同一套文案/按钮：「去配置」直达设置页、「重新检测」），不再只是顶部一条黄条。
- **追加（用户报的崩溃："首次打开/Configs 里没有 ESILicense.txt 时 System.NullReferenceException，栈顶在 Core.Log.Error 第 39 行"）**：
  - **根因**：`CoreInitializer.Init()` 里 `Log.Init()` 原来排在**很后面**（数据库初始化之前才调），而它前面的 `ApplyEsiCredentials()` 在找不到 `Configs/ESILicense.txt` 时会 `Log.Error("未找到 …")`；此时 log4net 的 `log` 字段还是 **null** → `log.Error(...)` 抛 NRE。**真正的信息被 NRE 盖掉，App.OnStartup 直接崩**（首次运行必然复现：Configs 是新建的，里面当然没有 ESILicense.txt）。
  - **修法**：
    1. `Log` 全面加固：**懒初始化**（任何一次写日志发现没初始化就先 `Init()`）、所有级别（Info/Error/Warn/Debug/Fatal）**全程 try/catch、绝不抛异常**（写不进去就退到 `Debug/Console` + `Logs/fallback.log` 兜底）、事件订阅者抛异常也吞掉、`Init()` **幂等**、`log4net.config` 缺失时自动挂一个最小 `RollingFileAppender` 兜底、`GetLogFile()` 空安全。
    2. `GetLogPath()` 兜底：`Config.AppDataPath` 在 `CoreInitializer` 里是**晚于**日志初始化才赋值的（null 会让 `Path.Combine` 抛异常、日志就没了），现在为空时退回 `%LocalAppData%\TheGuideToTheNewEden`；`Init()` 会先确保日志目录存在。
    3. `CoreInitializer.Init()` 把 `Log.Init()` 提到**第一行**（在设置/凭据/角色/结构/数据库之前），后面的那次 `Log.Init()` 删掉。
    4. 首次运行"没有 ESILicense.txt"是**正常状态**（要等用户授权），那条日志由 `Log.Error` 降为 `Log.Warn`，不再污染错误计数（`ScalperPage` 会读 `Log.GetErrorCount()`）。
  - **核验（`LogProbe` 两个模式，8/8 + 6/6 PASS）**：
    - `lazy` 模式（复现旧崩溃点）：进程刚起来 `Log.GetInstance()==null` → **在 `Log.Init()` 之前调 `Log.Error` 不再抛异常**、`GetLastError()` 记得住、错误计数 +1、Info/Warn/Debug/Fatal/`Error(Exception)` 全部安全、`Init()` 可重复调用、`GetLogFile()` 能拿到路径且那条提示**确实写进了日志文件**。
    - `firstrun` 模式（用户的真实场景：Configs 里没有 `ESILicense.txt`）：直接 `CoreInitializer.Init()` **不抛异常**、Logger 就绪、日志里能看到「未找到 Configs/ESILicense.txt」这条 WARN、Configs 自动创建、`settings.json` 生成、`DatabaseReady=True`。
    - 实机日志佐证（`%LocalAppData%\TheGuideToTheNewEden\Logs\20260913.txt`）：`2026-09-13 23:49:53,793 [1] WARN : 未找到 Configs/ESILicense.txt，无法进行新的 ESI 授权` —— 以前就是这一行把界面写崩的。  - **核验（`ChannelProbe`，19/19 PASS）**：开关快照五项映射；**上下文**——第 1 条无上下文、第 2 条带上第 1 条的原文+译文、**第 4 条只带最近 2 条**（上限生效、最早那条被丢掉）、换频道不带另一频道的、关掉开关后每条都不带；meta 与术语命中确实进了条目；条目包装（原文单行去标记、长译文默认收起 5 行 + 省略号、展开变全文且按钮变「收起」、复制的是纯文本、meta/术语/去标记的显隐标记正确）；界面（有「展开」按钮、有复制译文图标、译文富文本收起态 `MaxHeight=96`）；方向——频道页显示的方向来自共享设置（俄语 ⇄ 英语 自动互译），且**用本地 mock 收请求体验证**：频道翻译走 provider 时 user 消息是"请把下面的内容从俄语 → 英语翻译"。










### 阶段 48：欧服授权回调链路的 6 处缺陷修复（注册表指向 / 权限 / 超时 / 单实例隔离）

- **背景**：为回答"欧服角色授权是怎么从浏览器调回客户端的"而通读了整条链路
  （`CharacterCardsPage.AddCharacterAsync` → `CharacterAuthService.LoginAsync` → `AuthHelper.WriteProtocol` +
  `ESIService.GetAuthorizeUrl` → 浏览器 → Windows 拉起第二进程 → `Core.SingleInstanceHelper` 转发 →
  `AuthHelper.WaitForCallbackAsync` → 解析 code → `CharacterStore.Add`），在链路上查出 6 处缺陷，本次全部修掉。
- **P0-1（致命，已实测复现）：注册表里的 exe 路径指向一个不存在的文件**。
  - `AuthHelper.WriteProtocol()` 把命令写死为 `TheGuideToTheNewEden.WPF.exe`，但 csproj 早已改成
    `AssemblyName=TheGuideToTheNewEden`（阶段 45 记录 `.WPF.exe` 是 **net10 时期**的事实，之后回退 net8 时改了程序集名，这行没跟着改）。
  - **实测证据**（只读查询本机注册表）：
    - `HKLM\Software\Classes\eveauth-qedsd-neweden3\shell\open\command`
      = `"…\net8.0-windows10.0.19041\TheGuideToTheNewEden.WPF.exe" "%1"`；
    - `TheGuideToTheNewEden.WPF.exe` **不存在**（`Test-Path` = False），同目录的 `TheGuideToTheNewEden.exe` 存在。
  - 即：浏览器授权后重定向到 `eveauth-…://`，Windows 按注册表去启动一个**不存在的 exe** → 回调永远送不回客户端。
  - **修法**：改用 `Environment.ProcessPath`（当前进程的真实路径），不再硬编码文件名。
- **P0-2：非提权运行时写注册表抛异常，把整个授权流程挡在开浏览器之前**。
  - `HKEY_CLASSES_ROOT` 的写入实际落在 `HKLM\Software\Classes`，**需要管理员权限**；而 `LoginAsync` 里
    `WriteProtocol()` 裸调、没有 try/catch，异常向上冒到 `AddCharacterAsync` 的 catch 里被吞成一条日志
    → 表现为"点添加角色什么都没发生"（浏览器也没打开）。
  - **修法**：① `WriteProtocol()` 改为**幂等**——当前值已正确就直接返回，不做写操作（安装包写好的机器级注册
    对普通用户是"可读不可写"的，硬写必抛）；② 机器级写失败**退回当前用户级**（`HKCU\Software\Classes`，无需管理员，
    HKCR 合并视图里同名时 HKCU 优先），成功则记 Warn；③ 只有两级都失败才抛；
    ④ `LoginAsync` 用 try/catch 包住它并降级为日志——注册失败不代表授权不了（安装包通常已注册过）。
  - 顺带修正读取顺序：`ReadProtocol()` 先读 HKCU 再读 HKLM（**HKCU 优先**才对；顺序反了会把"仅机器级过期"
    误判成"当前值就是过期的"，从而每次启动都白试一次机器级写入）。
- **P1-1：`DeleteProtocol()` 是"写空串"，不是删除** → 留下一个坏关联（协议还在、命令为空）。
  改为对机器级与用户级分别 `DeleteSubKeyTree`（`throwOnMissingSubKey: false`，无权删除只记 Warn）。
- **P1-2：等待回调没有超时 → 授权流程永久挂起**。`LoginAsync()` 以默认 `CancellationToken.None` 调用，
  用户放弃授权后那个 Task 永远不完成（按钮点了没反应、也没有任何提示）。
  改为 `AuthHelper.CallbackTimeout`（默认 5 分钟，可设）→ `CreateLinkedTokenSource` + `CancelAfter`，
  超时返回 null 并记 Warn；卡片页在该分支弹出失败提示（复用既有 `Characters.LoginFailed`/`Characters.Login` 两个键，不新增语言键）。
  同时收紧完成条件：**只有命令行里确实带 `eveauth` 参数才结束等待**——原先任何一次激活（例如用户在等授权时又双击了一次图标）
  都会以 null 结束等待、把登录流程打断。
- **P2-1：转发用的第二进程会跑完整 `CoreInitializer.Init()`**（日志、设置载入、DB 打开、`CharacterStore.Init()`、
  `StructureService.Init()` 全做一遍才退出），与正在运行的主实例争抢 SQLite / `settings.json`。
  改为把单实例注册**提到 Core 初始化之前**（`SettingsService.DataPath` 是静态计算的，不需要 `Initialize()`），
  第二进程只做"落盘命令行 + 唤醒主实例"就退出；`Activated` 订阅也一并提到初始化之前，避免中间出现订阅空窗。
- **P2-2：单实例标识全局固定**（`AppName = "TheGuideToTheNewEden"`），WinUI 版与 WPF 版同时运行会互相抢占：
  后启动的一版会把命令行交给对方然后自己退出（"点了没反应"）。
  `Core.SingleInstanceHelper` 新增 `(instanceName, tempFileName)` 构造函数（**无参构造保持原样，WinUI 侧零改动**），
  WPF 版使用 `TheGuideToTheNewEden.WPF` / `SingleInstanceTemp.WPF`。
- **顺带加固 Core 的转发实现**（同一文件、同一链路）：
  1. 非首实例**先删旧临时文件再写**，且**写失败就不发信号**——否则主实例会读到上一次的残留内容
     （对授权来说就是把一条过期 code 又走一遍）；
  2. 主实例**读完即删**临时文件（原先不删，下一次普通激活会拿到旧命令行；`App.OnSingleInstanceActivated`
     会把一条陈旧的 eveauth 参数当成回调而直接 return，窗口反而不激活）；
  3. 监听线程整体 try/catch（原先 `File.ReadAllLines` 一旦抛异常，`while(true)` 后台任务直接死掉 → 此后再也收不到任何激活）。
- **验证**：`dotnet build -t:Rebuild`（应用正在运行，按 §环境注意用 `-p:OutDir=` 重定向输出目录）
  → **0 错误**、35 个警告（与改动前逐条一致，均为既有的 CS0108/CS0144/CS8622/CS0162/CS8632，
  **无一条指向本次改动的 5 个文件**）；构建产物 `TheGuideToTheNewEden.dll` 正常生成。
  临时输出目录与探针产物已清理。
- **未覆盖**：真实账号的欧服完整授权（需要一次真实的浏览器授权 + 一条真实的 eveauth 回调），
  以及非提权环境下 HKCU 回退路径的实机验证（本机注册表里那条机器级值是**提权写入**的历史残留，
  普通权限下重跑一次授权即可顺带验证回退；届时预期日志出现一条"已退回当前用户级"的 WARN）。
- **附带发现（已在阶段 49 订正）**：`REFACTORING.md` §3/阶段 6 原先记的 TFM 仍是 `net10.0-windows`，
  与 csproj 实际的 `net8.0-windows10.0.19041`（注释写明是为兼容未更新的 Win10 21H1 而回退）不一致——
  正是这处**文档滞后**误导了对 exe 名的判断（P0-1）。全文的 TFM / 程序集名 / 包版本已统一订正为 net8，见阶段 49。

### 阶段 49：TFM 由 net10 回退到 net8 LTS（文档追记）

> **本阶段是追记**：代码侧的改动发生在阶段 45 之后、本轮文档订正之前（旧产物 `bin\Debug\net10.0-windows\TheGuideToTheNewEden.WPF.exe`
> 停在 2026-09-11 18:05，回退后的 `bin\Debug\net8.0-windows10.0.19041\TheGuideToTheNewEden.exe` 为 2026-09-14 09:31）。
> 当时未单独记录，导致 §3/阶段 6 的 TFM 表述长期滞后于 csproj，并在阶段 48 直接误导了对注册表 exe 名的判断。
> 本阶段把代码事实与文档表述一并对齐。

- **触发**：在**未更新的 Windows 10 21H1（19043.985）**上运行 WPF 版**启动即崩**——
  `0x80131506`（`STATUS_FAIL_FAST_EXCEPTION`），故障模块 `KERNELBASE.dll`，没有托管异常、也没有日志。
- **根因**：.NET 9 起的 WPF 与该系统补丁级别不兼容。同机对照：.NET Framework / .NET 6 / .NET 8 的 WPF 均正常，
  任何版本的控制台程序也正常，只有 .NET 9/10 的 WPF 进程在运行时初始化阶段 fail-fast。
  **该判断已由实机验证证实**（回退 net8 后同机能正常启动，见下「实机验证」）。
- **修法（`TheGuideToTheNewEden.WPF.csproj`）**：
  1. `TargetFramework`：`net10.0-windows10.0.19041` → **`net8.0-windows10.0.19041`**。
     **平台版本 `10.0.19041` 必须保留**——那是阶段 25 为 `SkiaSharp.Views.WPF` 资产加的约束，与 .NET 大版本无关。
  2. `AssemblyName`：`TheGuideToTheNewEden.WPF` → **`TheGuideToTheNewEden`**，产物为 `TheGuideToTheNewEden.exe`。
  3. `H.NotifyIcon.Wpf`：2.4.1 → **2.3.0**（2.4.1 只有 `net462`/`net10.0-windows7.0` 资产，net8 下会 `NU1701` 回退到 .NET Framework；
     **2.3.x（2.3.0 / 2.3.1）都带 `net8.0-windows7.0` 资产**，本项目取 2.3.0，`obj/project.assets.json` 已确认命中 `lib/net8.0-windows7.0/`）。
- **连带影响（均已同步）**：
  - Core（`netstandard2.1`）与 WinUI 版（`net9.0-windows10.0.19041.0`）不受影响；net8 目标由 .NET 10 SDK 10.0.401 正常编译。
  - **协议注册**：程序集名变化后 `AuthHelper` 里硬编码的 `TheGuideToTheNewEden.WPF.exe` 随即失效（阶段 48 P0-1 的根因），
    现已改为 `Environment.ProcessPath`。
  - **两版输出同名 exe**：WinUI 版的 `AssemblyName` 一直是 `TheGuideToTheNewEden`，WPF 版改为同名后两版产物都是
    `TheGuideToTheNewEden.exe`——**不要放进同一目录**，也不要靠文件名区分版本（见 §8「环境/协作注意」）。
- **文档订正范围**（本阶段一并完成）：§2 架构决策表；§3「最终 csproj 要点」/包引用表/「环境变更」；阶段 6 的 superseded 标注；
  阶段 25 的 TFM 结论注；阶段 45 的产物名注；§8 环境注意；§9 第 9/19 条。
- **验证**：`dotnet build -t:Rebuild -p:OutDir=<临时目录>` → **0 错误**、35 个警告与改动前逐条一致（阶段 48 记录）；
  回退后的产物为 `net8.0-windows10.0.19041\TheGuideToTheNewEden.exe`（2026-09-14 09:31，现行）。
- **实机验证（已闭环）**：回退后的 `TheGuideToTheNewEden.exe` 在触发问题的那台**未更新的 Win10 21H1（19043.985）**上
  **可以正常运行**，不再启动即崩 → 确认「.NET 9/10 的 WPF 与该系统补丁级别不兼容」这一根因成立，net8 是可行的落地方案。
  至此本阶段无遗留未覆盖项。

---

### 阶段 50：授权回调的"转发进程"改为在创建 WPF Application 之前退出

> **注（阶段 51 起前提已变）**：授权回调已改走**本地回环**，不再经由注册表协议拉起第二个进程，
> 因此本阶段开头"授权回调由 Windows 按注册表启动第二个客户端进程"的前提**不再是现行路径**。
> 但本阶段的修法**依然有效且必要**——"程序已在运行时用户又双击一次图标"仍然会走到单实例转发，
> 而那条路径上的 `settings.json` 清空风险与 WPF 程序集白加载问题完全一样。历史记录保持原样。

**背景**：欧服授权回调由 Windows 按注册表启动**第二个客户端进程**（协议激活在机制上只能"运行一条命令行"，
没有任何办法把 URL 直接投递给已运行的进程），该进程的唯一任务是把命令行转交主实例然后退出。
阶段 48 已把单实例判定提到 `CoreInitializer.Init()` 之前，但**判定点仍在 `App.OnStartup` 内**——
于是转发进程依然要构造 `Application`、解析 `App.xaml` 里的全部资源字典（WPF-UI 的
`ThemesDictionary`/`ControlsDictionary` + 3 个项目字典 + `zh-CN.xaml`），最后再走一遍退出清理。

**实测（独立 WPF 探针，net8；一次性产物已清理）**：

1. **在 `Startup` 里调用 `Shutdown()` 依然会触发 `Exit`**——实测时序 `MAIN → STARTUP → EXIT → RUN returned`。
   于是 `App.OnExit` 会照跑，其中 `SettingsService.Save()` 在**从未调用过 `Initialize()`** 的进程里
   `Values` 还是空字典，会把与 WinUI 共用的 `Configs/settings.json` **覆盖成 `{}`**。
   这是阶段 48 把单实例判定提前（因而不再 `Initialize()` 设置）所引入的**回归**；
   实测当时磁盘上的 settings.json 仍完好，是因为主实例随后会用自己的内存值重新写回——
   但"回调后主实例被强杀/崩溃"就会真的丢配置。
2. **只要 `Main` 的方法体不引用 `App`（WPF 派生类），进程内 `PresentationFramework` / `System.Xaml` /
   `WindowsBase` 一个都不加载**；一旦引用 `App` 的静态成员，其基类 `System.Windows.Application`
   会被连带加载，三个程序集全部进来。
3. 而读取**普通静态类**（如 `SettingsService.DataPath`，其方法签名里虽有 WPF 的 `Color`）
   不会加载任何 WPF 程序集——所以转发进程可以安心复用 `SettingsService.DataPath` 定位数据目录。

**改法**：

1. 新增 `Program.cs` 作为入口，csproj 加 `<StartupObject>TheGuideToTheNewEden.WPF.Program</StartupObject>`：
   先 `RegisterSingleInstance`，非首实例**直接 `return 0`**——不构造 `Application`、不解析 XAML、不触发 `Exit`。
2. **单实例状态从 `App` 迁到 `Program`**（`InstanceName` / `InstanceTempFile` / `Program.SingleInstance`）：
   `App` 继承 `Application`，碰它的静态成员就会把 WPF 栈拉进来；`AuthHelper.WaitForCallbackAsync`
   改读 `Program.SingleInstance`。
3. `App.OnStartup` 只保留"订阅 `Activated` + Core 初始化 + 建主窗"，删掉 `Shutdown()` 分支；
   `App.OnExit` 不变（现在只有真正的主实例会走到）。
4. `Program.Main` 给单实例注册加 try/catch：失败只记 Warn 并**退回独立实例启动**，
   不让"命名事件被占用/权限异常"这类意外变成启动即崩（此前异常只是被 WPF 的未处理异常处理器接住）。

**效果**：转发进程从"完整启动一个 WPF 应用再退出"变成"起运行时 → 转发 → 退出"，
不加载 WPF 程序集、不解析任何 XAML、不碰设置/数据库/日志。

**验证**：`dotnet build -t:Rebuild -p:OutDir=<临时目录>` → **0 错误**、35 个警告（与改动前逐条一致），exit 0。
**未覆盖**：真实浏览器回调的端到端走通与转发进程的耗时对比（需一次真实授权）。

---

### 阶段 51：欧服授权回调改为本地回环（协议/注册表通道保留但停用）

**触发**：用户确认 EVE 开发者后台**不允许为同一个 SSO 应用登记两个 Callback URL**，
所以"自定义协议（注册表）"与"本地回环"只能二选一。决定改用回环，注册表那套保留代码但不再走。

**为什么回环能省掉一整个进程**：协议激活在机制上只能"运行一条命令行"——
浏览器跳到 `eveauth-…://` 时 Windows 必然新起一个客户端进程（阶段 50 已把它的代价压到最低，
但**进程本身依然存在**）。而 `http://localhost:<port>/…` 是浏览器**直接发起的一次 HTTP 请求**，
打在主实例自己监听的端口上：不需要中间进程、不需要注册表、也不需要
`App.OnSingleInstanceActivated` 参与。CCP 官方文档也把 loopback 列为非浏览器应用的两条标准路径之一
（明确允许 `http://localhost:$port/`，但**不支持通配端口**）。

**实现**（新增 `Helpers/LoopbackAuthServer.cs`）：

- **端口/路径不写死**，从 `Config.ESICallback`（= `Configs/ESILicense.txt` 第 2 行）解析——
  它就是发给 CCP 的 `redirect_uri`，天然与后台登记值一致。
  建议值 `http://localhost:38471/callback/` 只用于界面提示与测试页。
  **端口号本身没有技术含义**，选在 1024–49151 之间是为了：① 不落在特权段（<1024，非管理员绑不上）；
  ② 避开 Windows 默认动态端口段（49152–65535，系统会派给其它程序的出站连接，落在里面会偶发"端口被占用"）；
  ③ 避开 80/443/3000/8080 一类常用端口。改端口只需同步改上面两处，`DefaultCallbackPort` 不必动。
- **只绑 `127.0.0.1` 与 `::1`**：不暴露到局域网、不触发 Windows 防火墙提示，
  也不用 `HttpListener`（它非管理员会 `AccessDenied`，因为要先做 URL ACL）。
  **IPv6 回环是必需的补充**——浏览器可能把 `localhost` 解析成 `::1`，只绑 IPv4 会表现为"跳过去了但回调收不到"。
- 手写极简 HTTP：读请求行 → 路径比对 → 解析查询串 → 回一页自带 `prefers-color-scheme` 的 HTML，
  `Connection: close`。
- **结束等待的唯一条件**是"路径匹配且带 `code`（或 `error`）"；favicon、`/`、其它路径一律 404 并**继续等待**，
  用户等待期间随手点开链接不会把登录打断。
- 拿到回调后**立刻 `StopListening()`**，用户紧接着重试不会撞端口占用；
  首次绑定失败时带 `SO_REUSEADDR` 重试一次（覆盖上次连接留下的 TIME_WAIT），并记 Debug。
- 起始失败（端口占用/地址非法）不静默吞：`LoginAsync` 把原因交给界面
  （新增 `CharacterAuthService.LastFailure`），而不是只显示一句"登录失败"。
- `AuthHelper.TryGetLoopbackEndpoint` 对配置做五道校验（缺值 / 无法解析 / 非 http / 非回环 / 无端口 / 带查询串），
  失败时给出"把开发者后台与 `ESILicense.txt` 第 2 行都设为 …"的可执行指引，**并且不打开授权页**——
  `redirect_uri` 不匹配时 CCP 只会在浏览器里给一个英文报错页，比提前失败难排查得多。

**停用的部分**（保留代码，见 `AuthHelper` 的 `#region 自定义 URL 协议 / 注册表（保留，授权流程已不使用）`）：

- `ProtocolName` / `ReadProtocol` / `WriteProtocol` / `DeleteProtocol` / `WaitForProtocolCallbackAsync`
  （末者由 `WaitForCallbackAsync` **改名**，以便与回环通道区分）；`LoginAsync` 不再调用 `WriteProtocol()`。
- 设置 → 测试页的 **HKCR 卡片保持可用**（旧环境排查仍需要）。
- `App.OnSingleInstanceActivated` 仍识别 `eveauth` 参数，避免把旧链接的唤起当成"用户又开了一次程序"。
- 安装包 `TheGuideToTheNewEden.nsi` 的 `Protocol` 段不动（无副作用）。

> **以上"保留"的部分已在阶段 52 全部删除**（回环方案实机跑通后清理，见下）。

**实测**（一次性探针：把 `LoopbackAuthServer.cs` **链进**控制台工程 + 一个 `Core.Log` 桩，
不引用 WPF 程序集也不碰真实日志；6 组 26 项断言 **全 PASS**，产物已清理）：

| 场景 | 结果 |
|---|---|
| 正常回调 `?code=…&state=…` | `200` + 成功页；结果 URI 完整保留查询串且指向 `127.0.0.1:端口/callback/` |
| `/favicon.ico`、`/`、`/other/?code=WRONG` | 均 `404`，且**未打断等待**，最后仍由真回调解除 |
| `?error=access_denied&error_description=user%20said%20no` | `Denied`，原因含 error 与 description；失败页不含成功标题 |
| 1 秒超时 | `Timeout`，**1010ms** 返回（未永久挂起） |
| 向 `::1` 发请求 | 连通并解除等待（证明 IPv6 回环补充有效） |
| 超时后立刻复用同一端口 | 两次绑定都成功（模拟"用户马上重试"） |

**验证**：`dotnet build -p:OutDir=<临时目录>` → **0 错误**、14 个警告（既有告警，无一条指向改动文件），exit 0。

**实机验证（已闭环）**：真实 CCP 授权页**一次完整跳转跑通**——
浏览器 → 授权页 → 同意 → 回环回调页 → 主实例拿到 `code` → `ESIService.VerifyAuthorization` 换码 →
`CharacterStore.Add()` 落盘 `Auth.json`，**「添加角色」全流程一切正常**；
全程**未再起第二个客户端进程**（回调由浏览器直接打进主实例监听的端口），
`settings.json` 也未被覆写（阶段 50 的回归在回环通道下不再有可能触发）。

**一次性配置**（已按下表值生效；两处必须完全一致，否则授权页会直接报 `redirect_uri` 不匹配）：

| 位置 | 值 |
|---|---|
| EVE 开发者后台 → 该 SSO 应用的 Callback URL | `http://localhost:38471/callback/` |
| `%LocalAppData%\TheGuideToTheNewEden\Configs\ESILicense.txt` 第 2 行 | 同上 |

**配套的界面/资源改动**：
- 新增语言键 `AuthCallback.SuccessTitle/SuccessHint/FailureTitle/RetryHint`（结果页）与
  `TestSettingPage_Loopback*`（测试页）。
- 设置 → 测试页新增「**回环回调**」卡片：**检测**（校验配置 + 真的占一次端口）/ **复制**
  （把当前或建议地址送进剪贴板，便于贴到开发者后台）。
- 原 48 阶段加的"超时后弹提示"仍然保留，只是内容从"登录失败"变成具体原因。

---

### 阶段 52：删除协议/注册表通道的全部残留代码

**触发**：阶段 51 的回环方案已**实机跑通**（真实授权页一次完整跳转，「添加角色」全流程正常），
用户要求把自定义协议（注册表）那套"保留但停用"的代码彻底删除，**包括单实例激活里对 `eveauth` 的识别**。

**删除清单**：

| 文件 | 删除内容 |
|---|---|
| `Helpers/AuthHelper.cs` | 整个 `#region 自定义 URL 协议 / 注册表`：`ProtocolName` / `MachineRoot` / `UserRoot` / `BuildCommand()` / `TryWrite()` / `ReadProtocol()` / `WriteProtocol()` / `DeleteProtocol()` / `WaitForProtocolCallbackAsync()`；顺带去掉只被它们使用的 `using System.IO;` 与 `using Microsoft.Win32;` |
| `App.xaml.cs` | `OnSingleInstanceActivated` 里"命令行带 `eveauth` 就直接 return"的分支（**窗口置前逻辑保留**） |
| `Views/Pages/Settings/TestSettingPage.xaml` | 「HKCR 协议」卡片（读/写/删三个按钮）与显示协议值的 `ProtocolValueText` 卡片 |
| `Views/Pages/Settings/TestSettingPage.xaml.cs` | 三个按钮的事件挂接、`RunProtocolAction()` |
| `Resources/Languages/{zh-CN,en-US}.xaml` | `TestSettingPage_HKCRProtocol(_Desc)` / `_ReadProtocol(_Success)` / `_RegistyProtocol(_Success)` / `_DeleteProtocol(_Success)` 共 8 键 ×2；另删掉同样无人引用的 `CharacterPage_RegistyProtocol`（"注册授权服务失败，请使用管理员模式运行"——注册表时代的话术） |
| `TheGuideToTheNewEden.nsi` | 安装时的 `Section "Protocol"`（不再写注册表） |

**刻意没删的两处，以及理由**：

- **`Core/Helpers/SingleInstanceHelper.cs` 与 `Program.Main` 的单实例判定保留**——回环消灭的是
  "授权"这一条第二进程来源，但"程序已在运行时又双击一次"依然会产生第二进程，而阶段 50 已证实
  这种进程若走到 `App.OnExit` 会用空字典覆盖 `settings.json`。所以判定必须继续留在
  `Application` 创建之前；它现在只服务于"重复启动置前窗口"，与授权无关。
- **卸载段的 `DeleteRegKey HKCR "eveauth-qedsd-neweden3"` 保留**——它是老版本残留项的清理动作。
  删掉它，升级用户注册表里那条陈旧关联就永远留在机器上了。

**不涉及**：WinUI 项目的 `Helpers/AuthHelper.cs` 与语言文件里同名键（旧版仍走协议通道，未动）。

**验证**：`dotnet build -t:Rebuild -p:OutDir=<临时目录>` → **0 错误、35 个警告**（与改动前逐条一致：
`CS0169`/`CS8604`/`CS0414`/`CS0162` 等既有告警，无一条指向本次改动的文件），exit 0。临时目录已删。

**未覆盖**：删除不影响运行时行为（被删的都是授权流程已不再调用的死代码与界面），
但**本页的界面改动（测试页少了两张卡片）未做截图核验**——按约定改界面不需要截图，由使用者自行查看。

---

### 阶段 53：Zkillboard（KB）模块迁移 —— 服务层重建 + 主页面 / 击杀流 / 实体统计 / KB 详情

- 目标：把 WinUI 的 ZKB 模块（`Views/KB/**`、`ViewModels/KB/**`、`Services/KBNavigationService`、`Helpers/ZKBHelper`）迁到 WPF 并按本项目分层重建。此前 WPF 的 ZKB 页只是**导航占位**，角色工作区 ZKB 卡片的按钮跳到该占位页（见原 §8）。
- **新增服务层**（`Services/KB/`，本项目此前完全没有 ZKB 数据层）：
  - `ZkbMapping`：`IdName.CategoryEnum ↔ ZKB EntityType ↔ ParamModifier ↔ EntityStatisticType` 的**唯一映射入口** + zkillboard 网页地址。WinUI 把同一套映射分散写在 `KBNavigationService`（两处 switch）、`ZKBHomePage`、各 `Statist*ViewModel` 里；其 `type[..^2]`（去 "ID" 后缀）拼 URL 会把 `ShipTypeID` 拼成 `/shipType/`、`SolarSystemID` 拼成 `/solarSystem/`，这里按 zkillboard 实际路由段名映射。
  - `ZkbQueryService`：实体统计（2 分钟内存缓存 + `forceRefresh`）、击杀列表分页、单条/批量 killmail、实体搜索、实体基础信息与各统计页富化。**全部 async + CancellationToken，失败返回 null 并 `Log.Error`**（与其它 WPF 服务同约定）。
  - `ZkbStreamFilter`：把 `ZKBStreamConfig` 的 6 组黑白名单编译成**不可变匹配快照**；未配置"星域"过滤时**跳过** system→region 的 SQLite 查询（WinUI 对每条流消息都查一次库）。
  - `ZkbKillStreamHub`：进程内**唯一**订阅者（引用计数，最后一个退出才断开）。用有界 `Channel` + `WaitToReadAsync` 取代 WinUI 的 `ConcurrentQueue` + `Thread.Sleep(100)` 忙等轮询（队列满丢最旧，永不积压）；**配置变更自动重建过滤器**，修复 WinUI"过滤条件在连接期间修改不生效、必须断开重连"的问题。
- **新增界面**：
  - `Views/Pages/ZKBPage.xaml(.cs)`（替换占位页；类名与 `MainWindow` 注册均不变）：多标签宿主 + 顶部实体搜索。首个标签固定"击杀流"，其余为实体统计标签与 KB 详情标签（可关闭、实例常驻）。标签内容统一用 `Frame` 承载；标签标题绑定到页面 VM 的 `Title`，实体名解析完成后自动更新。
  - `Views/Pages/KB/KillStreamPage.xaml(.cs)`：击杀流页 —— "筛选 KB / 已过滤 KB"两个列表 + 设置面板（连接、通知、最小价值、最大条数、排序方式、含三组黑白名单的过滤页签）。
  - `Views/Pages/KB/EntityStatistPage.xaml(.cs)`：实体统计页 —— 左侧信息卡（头像/归属/成员/安全等级 + 危险系数与抱团概率条（`RatioBar`）+ 击杀损失）+ 右侧 **6 个子页签**（KB 列表 / 最贵击杀 / 最高击杀 / 分类统计 / 月份统计 / 超期击杀），**子页签懒加载**。
  - `Views/Pages/KB/KbDetailPage.xaml(.cs)`：KB 详情 —— 受害者/价值面板/攻击者表/货柜，可复制链接或用浏览器打开。
  - 可复用控件（`Views/UserControls/KB/`）：`KillListControl`（KB 列表，`ui:DataGrid` + 阶段 22 滚动方案）、`KillRankCard`（排名卡片）、`IdNameSearchBox`（实体搜索框）、`KbFilterPairControl`（一组"排除项/包含项"编辑器）。
  - 转换器与工具：`CategoryEnumToStringConverter`、`IdNameImageConverter`、`UrlToImageConverter`、`IskConverter`、`DateTimeToLocalConverter`、`StringToVisibilityConverter`；`GameImageHelper` 扩展角色头像/军团徽标/联盟徽标与"按类别分派"的统一入口；`IskFormatHelper`（按**数值阈值**而不是 WinUI `ISKNormalizeConverter` 那种"按字符串长度 + 小数点切分"判断量级）。
  - `Services/KbNavigation`：让任意界面（角色工作区卡片、击杀列表、统计页里的实体名…）都能"跳到 ZKB 页并打开某实体/某 KB"，与 `Navigation.NavigateToMarket` 同思路（含页面未创建时的待处理请求，两条路径都 `Drain`，不会重复打开）。
- **顺带修掉的 WinUI 缺陷**（本次未沿用其写法）：
  1. "最近 7 天最高价值"误用了历史数据源 `TopIskKills`（应为 `TopIskKills7d`）；
  2. 过滤类型下拉用 `(TypeModifier)(KBModifierIndex - 1)` 强转、与 ComboBox 项顺序强绑定 → 改为显式数组映射；
  3. 实体页构造函数一次性 `new` 出 5 个子页面（各自挂 `Loaded` 并取数）→ 改懒加载；
  4. 批量取 killmail 是串行 `foreach + await`（WinUI 里并行实现被整段注释）→ 改为有界并发（信号量，默认 6，上限 16）；
  5. `KBNavigationService` 直接持有 `TabView`/`TabViewItem`/`ToolWindow`（作者自注"懒得改成 IOC 了"）→ 本模块服务层与 UI 完全解耦。
- **接线**：角色工作区的 ZKB 卡片按钮由"跳到占位页"改为"跳转并打开该角色的实体统计标签"。
- **本地化**：中英各补约 120 个键（`CategoryEnum_*`、`KBListControl_*`、`ZKBHomePage_*`、`EntityStatistPage_*`、`Statist*Page_*`、`KB_*`、`ZKBPage_*`、`MapIntelTool_*`），键值沿用 WinUI 原文；已做重键检查（两文件均 1282 键、0 重复）。
- **校验**：`dotnet build` **0 错误 0 新警告**（仅剩 6 类既有告警，均不指向本次新增文件）。另对 WPF-UI 4.3.0 程序集逐个核对了所用**图标名**（`Play24`/`RecordStop24`/`Eraser24`/`Settings24`/`Dismiss24`/`Filter24`/`ArrowSync24`/`Copy24`/`Open24`/`Info24`）与**主题画刷键**是否存在（图标名非法是运行时 `XamlParseException`，见 §9 第 16 条同类问题）。
- **未覆盖**：本次**未做实机核验**（真实账号 + 真实 ZKB 服务逐页点击）。新增页面一律未在真机上跑过，首次使用请留意 §8 新增的 ZKB 待办项。
- **修复（用户实机反馈，2 处）**：打开 ZKB 页即抛 `XamlParseException`——"无法找到名为 `StringToVis` 的资源"（`KbFilterPairControl.xaml` 行 50）。根因：该控件只在 `UserControl.Resources` 里注册了 `CategoryEnum`，却在 `Tip` 的显隐绑定上引用了 `StringToVis`。**`StaticResource` 解析失败是运行时异常**（编译与 BAML 生成都不报），属 §9 第 16 条同类（与阶段 40 那个 `HasResultCard` 完全同型）。修复：补上 `<converters:StringToVisibilityConverter x:Key="StringToVis" />`。
  - 顺带做了一次**全量 `StaticResource` 键审计**：对本次新增的 8 个 XAML 逐个提取 `{StaticResource X}`，与"本文件 `x:Key` 定义 + App 级三个字典（`GlobalControlStyles`/`CardControlStyles`/`SettingCardStyles`）的键"比对 → 45 处引用全部解析（并打印了每个文件的引用清单以确认不是"正则没匹配到"的假通过）。
- **修复（自查发现的静默绑定失败）**：`KillRankCard.xaml` 的 7 处绑定写成了 `{Binding No/Name/ImageUrl/SubTitle/Kills/ValueText, RelativeSource={RelativeSource AncestorType=UserControl}}`——`AncestorType=UserControl` 的源是**控件自身**，而这些属性在 `KillCardItem` 上，控件上没有 → 绑定静默失败，"最贵击杀 / 最高击杀 / 超期击杀"的卡片会**名称/击杀数/估价/图片全空**（不报错、不崩溃）。修复：改为经 DP 走一层 `Item.*`。
  - 同类自查：把所有"以控件自身为源"的绑定路径全部列出核对 → 只剩 `KbFilterPairControl` 的 `Tip/Categories/Exclusions/Inclusions`、`KillListControl` 的 `ItemsSource`、`KillRankCard` 的 `Item.*`，均为该控件上的真实 DP；另确认无 `ElementName=` 残留。
- **同时修复**：`KillStreamViewModel` 的 `IsConnected` / `IsSettingVisible` 只通知了 `IsDisconnected`（漏了 `IsContentVisible` 与 `ShowDisconnectedHint`）→ **点「连接」后列表不会出现、未连接提示和连接按钮不会隐藏**。属 §9 第 20 条"绑定到不通知的属性"同类，已补齐成对通知。
- **修复（用户实机反馈，第二轮）**：`System.NullReferenceException` → `Core/Services/DB/LocalDbService.cs:266`（`TranMapSolarSystem(MapSolarSystem item)`）。**这是 Core 里的既有缺陷，WinUI 同样存在**：
  - **根因**：`MapSolarSystemService.Query(int id, bool local = true)` 在**主库（SDE）里查不到该 id** 时得到 `null`，却仍把它交给 `LocalDbService.TranMapSolarSystem(null)`，而该方法第一行就取 `item.SolarSystemID` → NRE。**同一文件里 `TranInvType(InvType)` / `TranInvTypeAsync` 本来就写了 `if (invType != null)` 守卫，而 region / solar system / station / market group / group 这 5 组（sync + async 共 10 个方法）漏了**——写法不一致导致的缺口。
  - **修复①（治本）**：上述 10 个"item 变体"翻译器一律补 `item == null` 守卫（与 `TranInvType` 的既有约定一致）；`MapSolarSystemService` 的 4 个 `Query*`（2 个同步 + 2 个异步）改为"**查不到行就不调翻译器**"，顺带省掉一次本地库查询。
  - **修复②（不再瞎报）**：`App.OnDispatcherUnhandledException` 此前**只弹框、不记日志**，于是出现"界面报了一个异常，但日志从那时起完全静默、无法定位调用方"——本次排障的首要障碍就是它。现在改为 `Core.Log.Error(e.Exception)` + 弹框显示 `e.Exception.ToString()`（含**完整堆栈**）；并补挂 `AppDomain.CurrentDomain.UnhandledException` 与 `TaskScheduler.UnobservedTaskException`，让**后台线程/未观察 Task** 的异常也落盘（ZKB 的流消费、分页取数都在后台线程上）。
  - **修复③（加固）**：`ZkbKillStreamHub` 的消费循环里 `_filter.Pass(...)` 与事件回调此前在 try/catch **之外**，单条异常会冲出内层循环、结束整个消费者任务——表现为"界面仍显示已连接、却再也收不到任何数据"。已改为逐条走 `ProcessOne` 隔离，坏一条只丢一条。
  - **定位受限说明（如实记录）**：因修复②之前不记录日志，本次**无法从日志确认具体调用方**。静态排查把该崩溃方法的调用方收敛为全仓库仅两处——`MapSolarSystemService.Query(int, bool)`（第 64 行）与 `Query(string)`（第 37 行）；后者的调用方在仓库内已不存在，前者的 WPF 侧可达路径只有 Core `KBHelpers.cs:96`（在本次新增的 try/catch 内、且 catch 会记日志，与"日志静默"矛盾）与 `ZkbStreamFilter.ResolveRegion`（自带 try/catch）。因此**不排除是调试器把一条已被捕获的首发异常提示给了使用者**。修复①之后该异常已不可能再发生；修复②之后若出现新异常，日志里会有完整堆栈。
- **修复（用户实机反馈，第三轮："KB列表与最贵击杀都没数据，最高击杀等其他Tab有数据"）**——这一轮用**独立探针工程**（引用 Core + ZKB.NET + WPF，按 `CoreInitializer` 同参数初始化，`%TEMP%` 下一次性产物、核对后已删）把数据链逐跳跑通，找到了**真正的根因**：
  - **事实链**（探针实测）：① zkillboard `/kills/` 接口现在**直接返回完整 killmail**（`attackers`/`victim`/`killmail_time`/`solar_system_id` + `zkb`），URL 形状（`characterID`/`systemID`/`killID`/`page/1/`）全部 200 条正常；② Core 富化 `KBHelpers.CreateKBItemInfo(List<ZKillmaill>)` 走的是"拿 `killmail_id + zkb.hash` **逐条去 ESI 换完整 killmail**，再 `kmInfo.DepthClone<SKBDetail>()` 跨模型拷贝"；③ **`DepthClone` 是 JSON 往返，而 EVEStandard 模型靠 snake_case 命名策略反序列化（属性上没有 `[JsonProperty]`），`SKBDetail` 靠显式 `[JsonProperty("killmail_id")]`** —— 序列化出 PascalCase、反序列化对不上 → **`SKBDetail` 全是默认值**（`KillmailId=0`、`KillmailTime=default`、`Victim.ShipTypeId=0`、`SolarSystemId=0`）。
  - **由此同时解释三个症状**：KB列表 50 行全是空壳（开本地化时 `MapSolarSystemService.Query(0)` → null → 正是上一轮那个 NRE）；「最贵击杀」`byId.TryGetValue(真实id)` 因克隆出 `KillmailId=0` **永远匹配不上** → 0 条且无日志（`logErrors:false`）；而最高击杀/分类/月份只用 statistics 聚合数据，不经过 killmail → 正常。
  - **修复①（主路径改造）**：`ZKB.NET` 新增 `ZKB.GetKillmailDetailsAsync(...)` —— **直接把 `/kills/` 响应反序列化成 `List<SKBDetail>`**（完整数据本来就在响应里），`ZkbQueryService` 的列表/单条/最贵击杀全部改走它，再交给 `KBHelpers.CreateKBItemInfo(SKBDetail)` 做本地库富化。**省掉每页最多 200 次 ESI 往返**，数据也不再是空壳。旧的 ESI 路径保留为兜底（响应形状变化时自动回退），并对其产出的空壳（`KillmailId<=0`）过滤。
  - **修复②（并发改串行）**：批量富化原计划 6 并发，实测 50 条丢 16 条 —— Core 的名称/星系查询共用同一 SQLite 连接且非并发安全（`KBHelpers` 注释原文："使用一个线程来执行查询KB具体信息，避免ESI查名字时数据库冲突"）。改回**串行**后 50/50 全部成功。
  - **修复③（不再静默）**：直取失败**重试一次**并记 Warn（两次都失败/为空也记）；「最贵击杀」取回数少于 killID 数时记 Warn；击杀列表每页记一条 Info（抓到 N 条/本页富化 M 条）。
  - **探针验证**（直取路径）：KB列表 `items=50 hasNext=True`；「最贵击杀」`items=1`（旧路径 0）；单条详情正常。**注意**：复测时 `killID` 单查一度返回空——是本机连续探测触发 zkillboard 限流（同一 URL 直取/重试/兜底三连发），非代码问题；正常使用频率不会触发。
  - **遗留（Core/WinUI 侧）**：`KBHelpers.CreateKBItemInfo(List<ZKillmaill>)` 的 ESI+DepthClone 空壳问题**在 WinUI 版同样存在**，本轮未动 Core（WPF 已完全绕开该路径）；若日后要修 WinUI，可让它也改走 `GetKillmailDetailsAsync`。
- **修复（用户实机反馈，第四轮："加载等待不要用全局等待效果，实体可并发操作"）**——实体统计页此前复用全局 `PageNotifyService.ShowWaiting/HideWaiting`，但实体标签**可以同时打开多个并发加载**，全局遮罩会误遮其他实体标签、且多页并发时互相干扰（先结束的页会把别的页的遮罩藏掉）。改造为**每页独立等待态**：
  - `EntityStatistViewModel` 新增 `IsBusy`（**可重入计数**：同页内并发触发的多个操作全部结束才隐藏）与 `BusyText`；`BeginBusy/EndBusy` 由页面驱动。
  - `EntityStatistPage.RunAsync` 由 static 改实例方法：不再碰 `PageNotifyService.ShowWaiting`，只置/清本页 busy 态；**错误提示仍走右下角非阻塞通知**。首次取数用 `ZKBPage_LoadingStatistic`，其余用 `ZKBPage_Loading`。
  - `EntityStatistPage.xaml` 新增**页面局部等待遮罩**（`Grid.ColumnSpan=2`，只盖本标签并拦截点击）：半透明背景 + 卡片 + `SpinnerIcon` 自绘旋转图标（视觉与全局 `WaitingOverlay` 一致；不用 `ProgressRing`，见 §9 第 17 条）。
  - `KillStreamViewModel.ConnectAsync` 同步改掉：连接等待由 `IsConnecting` 驱动 `KillStreamPage` 页内"连接中"指示（`SpinnerIcon` + `ZKBHomePage_ConnectingToWSS`），不再弹全局遮罩；`ShowDisconnectedHint` 增加 `!_isConnecting` 条件（连接中显示指示而非"未连接"提示）；顺带补了 `catch`——此前 `StartAsync` 抛出的异常会沿 async void 调用链冲到全局异常处理器弹框（finally 只复位 `IsConnecting`，不吞异常）。
  - 全局等待遮罩保留给"应用级单实例操作"（倒货取数/估价等），ZKB 内已无 `ShowWaiting` 调用点。
- **UI 对齐（用户要求："实体页面左侧概况卡片和 WinUI3 保持一致"）**——左侧列由"单张带标题的卡片"重构为 **WinUI `EntityStatistPage` 同款两张卡**（`ScrollView` 内纵向堆叠）：
  - **卡 1 实体信息**：100×100 **圆形头像**（`CornerRadius=999`，无头像隐藏）→ 名称（18px Bold，可换行）+ **"浏览器查看"按钮**（`Open24` 图标，打开 `ZkbMapping.BuildEntityWebUrl`，对应 WinUI 的 `OpenInBrowerCommand`）→ 信息行（**标签宽 80 细体 + 值**，同 WinUI `StackPanel_ListInfo` 行组）。信息行带**实体链接**：`EntityInfoRow` 增加 `Link`（`IdName`），非空时值渲染为强调色透明按钮、点击 `KbNavigation.OpenEntity` 跳转（同 WinUI 各 `Button_*_Click`）。行集对齐 WinUI：军团/联盟/执行军团/星系/星域/舰船/类别/安全等级/成员（**WinUI 本就不展示 CEO 行，此处同样移除**）；军团/联盟补**成员数**兜底取 ZKB `statistic.Info.MemberCount`（联盟的 ESI 信息不含成员数，WinUI 即如此）。
  - **卡 2 统计概览**：危险系数/抱团概率两行改为 WinUI 同款形态——**标签 + 2px 细条（绿轨 `SystemFillColorSuccessBrush` / 红条 `SystemFillColorCriticalBrush`，两者同色，替代原"危险红/抱团橙"）+ 数值 + %**；下方为 WinUI 同款 **3 列统计块**：击杀数/价值/点数（绿）与 损失数/价值/点数（红），外加**单挑击杀/单挑损失**行（`SoloKills`/`SoloLosses`，新增 VM 属性；WinUI 把这些计数误过一遍 `ISKNormalizeConverter` 且两组不一致，WPF 直接显示原始计数）。
  - 移除旧卡独有的"拥有超期"文字（WinUI 无此元素，超期页签的出现本身已表达该信息；`HasSupers` 属性保留用于页签可见性）。`CategoryLabel` 不再展示（WinUI 无）。
  - 本地化 **0 新增键**（`EntityStatistPage_Ship`、`StatistMonthPage_Points*/Solo*` 等阶段 53 已带入）。`dotnet build` 0 错误、无新增告警；未实机核验。
- **修复（用户实机反馈，第五轮："实体头像还是矩形；KB 列表双击弹详情改单击"）**：
  - **头像圆形裁剪**：上一轮用 `<Border CornerRadius="999"><Image/></Border>` 仍是矩形——WPF 的 `Border.CornerRadius` 只圆化 Border **自身**的背景/边框，**不裁剪子元素**（与 UWP/WinUI 不同）。改为 `Image.Clip` 椭圆几何：100×100 定尺寸 + `<EllipseGeometry Center="50,50" RadiusX="50" RadiusY="50"/>`，外层换回 `Grid` 只承担 `AvatarUrl` 可见性绑定（见 §9 第 45 条）。
  - **KB 行单击开详情**：`KillListControl` 行事件由 `MouseDoubleClick`（`OnRowDoubleClick`）改为 `MouseLeftButtonUp`（`OnRowClick`），对齐 WinUI `KBListControl` 的 `IsItemClickEnabled` 单击行为。安全性：行内的 `KbLinkButton` 实体链接（舰船/类别/星系/星域/受害者/最后一击）是 `ButtonBase`，其 `OnMouseLeftButtonUp` 触发 Click 后把事件标记为 Handled，**不会**冒泡到行处理器造成"点实体名却开了 KB 详情"；该控件为共享控件，实体统计/月统计/主页等所有宿主一并生效（`OpenKillmail` 订阅方无需改动）。
- **修复（用户实机反馈，第五轮补遗 09-15："系统通知 KB 时点击通知没法导航到 KB详情"）**——即 §8 第 46 条的落地：
  - `NotificationService.Show` 增加可选 `onClick` 参数（**单槽位** `volatile` 字段，"最后一次 Show 获胜"——Win32 气泡点击回调不带通知身份，只能近似路由）；`TrayBalloonTipClicked` 先触发既有全局 `NotificationClicked`（频道预警"点击停声"不受影响）再执行槽位动作并清空。
  - `ZkbKillStreamHub.TryNotify` 传入 `() => KbNavigation.OpenKillmail(detail.KillmailId)`——复用既有导航链路：`Navigation.Navigate(ZKBPage)` → ZKB 页 `Drain()` 取 killmailId 开详情标签 → `Navigation.Activate()` 前置/恢复主窗口（最小化到托盘也能拉回前台）。
  - 边界：同一时刻只有"最近一条"通知可点开；击杀流高频时旧通知点击会开到最新的 kill（Win32 无 per-balloon 身份，无解，已在 §8 第 46 条记录）。构建 0 错误无新告警；未实机核验。
- **UI 重构（用户要求，09-15："KB 主页应该使用卡片将连接、设置等按钮放到 footer，设置界面使用弹窗弹出"）**：
  - `KillStreamPage` 整页改为一张 `controls:CardControl`：**Header** = 标题（`ZKBHomePage_KillStream`）+ 连接状态点（绿 `SystemFillColorSuccessBrush` / 灰 `TextFillColorSecondaryBrush`，随 `IsConnected`/`IsDisconnected` 切换）+ 右侧"匹配 / 总数"计数；**Footer** = 连接（Primary）/断开/清空/设置四个按钮；内容区只剩"未连接提示 / 连接中指示 / 筛选-已过滤两个列表"。本地化 0 新增键。
  - 设置改为**弹窗**：原内嵌设置面板抽成 `Views/UserControls/KB/KillStreamSettingView`（构造传入 `KillStreamViewModel`，绑定同一份 Config），页面以 `ToolWindow` 单实例承载（`Owner=主窗口`、重复点击仅激活、680×640，与 MarketPage / 频道翻译弹窗同款模式）；页面 `Unloaded`（页签切换/关闭）时关闭弹窗并 `Dispose` VM——设置视图绑定的是页 VM，页签切走即退订流事件，留着弹窗只会展示陈旧状态。
  - VM 顺带删掉过时的 `IsSettingVisible`（设置不再遮挡内容区，`IsContentVisible`/`ShowDisconnectedHint` 只看连接状态）。构建 0 错误无新告警；未实机核验。
- **UI 调整（用户要求，09-15："搜索可以 flyout 显示吗"）**——ZKB 实体搜索收进 Flyout：
  - `ZKBPage` 顶部由"常驻搜索框 + 左侧结果卡"改为**右上角一个搜索按钮**（`Search24`，沿用既有键 `ZKBPage_SearchTip`，0 新增本地化键）；搜索框、搜索状态、结果列表整体移入 `<ui:Flyout Placement="Bottom">`（内容宽 380、结果 MaxHeight 360），标签宿主改为全宽。选中结果后 `SearchFlyout.Hide()` 并清空（跳实体统计标签，行为不变）。
  - **WPF-UI 4.x 的 Flyout 用法**（3.x 的 `FlyoutService` 附加属性已移除；4.3.0 API 以读 dll 字符串表 + GitHub 源码双重确认）：`Wpf.Ui.Controls.Flyout : ContentControl`，DP `IsOpen`/`Placement`（`System.Windows.Controls.Primitives.PlacementMode`），方法 `Show()/Hide()`，路由事件 `Opened`/`Closed`（`TypedEventHandler<Flyout, RoutedEventArgs>`）。**模板内的 `PART_Popup` 不设 PlacementTarget → 锚点 = Flyout 元素自身在布局中的位置**：把 Flyout 与锚定按钮放进同一个 Grid 并 `HorizontalAlignment/VerticalAlignment=Stretch` 覆盖同一矩形，`Placement="Bottom"` 即"从按钮下方弹出"；`Popup.StaysOpen=False`（点外部自动关闭）。展开后聚焦搜索框挂在 `Opened` 事件上（此时 Popup 已呈现，可安全 `Focus()`）。
  - 构建 0 错误无新告警；未实机核验。
- **UI 调整二（用户要求，09-15："搜索按钮放到 TabControl 的 Header 区域末尾，与 KB 流、各实体页 TabItem 的 Header 同行，末尾留一段宽度给按钮"）**：
  - `ZKBPage.xaml` 新增 `ControlTemplate x:Key="KbTabsTemplate"`（TargetType TabControl）**逐项镜像 WPF-UI 4.3.0 默认模板**（来源 GitHub tag 4.3.0 `TabControl.xaml`：外层 Grid 两行、`TabPanel` 头行 `Panel.ZIndex=1` 压住内容区顶边线、内容 Border `BorderThickness="0,1,0,0"` + `CornerRadius="0,4,4,4"` + `PART_SelectedContentHost` `ContentSource="SelectedContent"`），唯一差异：头行由单个 TabPanel 改为 Grid 两列 `*,40`——TabPanel 占第 0 列，**右端 40px 空列留给搜索按钮**；标签多了在预留位前换行，永远不会滑到按钮底下。
  - 搜索按钮 + Flyout 移入页面级**覆盖层** `Grid Width=40 Height=36 HorizontalAlignment=Right VerticalAlignment=Top`（40 = 预留列宽、36 = WPF-UI TabItem MinHeight，按钮与 TabItem Header 精确同行）；Flyout 锚定沿用上一轮"同 Grid Stretch 覆盖同矩形 + `Placement=Bottom`"惯用法，仍从按钮下方弹出。
  - **关键取舍（模板命名空间）**：`ControlTemplate` 内的 `x:Name` 处于模板命名空间，页面 code-behind **拿不到**对应字段——模板只放非交互骨架（TabPanel/Border/ContentPresenter），交互元素（SearchButton/SearchFlyout/SearchBox/SearchResultList）全留在页面层覆盖层上，事件照常挂（见 §9 第 46 条）。
  - 构建 0 错误、14 既有警告；未实机核验。
- **UI 调整三（用户实机反馈，09-15："搜索 Flyout 会飞到窗口外面，往左显示；Header 放不下会分行，要像 WinUI3 那样按钮切换、永远一行"）**：
  - **Flyout 向左展开**：`Placement=Bottom` 的弹出层与**锚点左缘对齐**向下展开，而锚点 = Flyout 元素自身布局矩形（此前只有 40px 宽）→ 380px 内容向右伸出窗口。修复：页面级覆盖层从 40px 加宽到 **420px**（380 内容 + 40 按钮列，按钮仍右对齐其中），锚点矩形随之加宽——弹出后右缘落在按钮下方附近、整体向左展开，不再出窗。通用规律：Bottom 模式要"右对齐弹出"，就把锚点元素加宽到"内容宽 + 右缘余量"。
  - **头行单行化（WinUI3 TabView 式）**：`KbTabsTemplate` 头行由 `TabPanel`（放不下即换行）改为 `DockPanel`（左/右两个 `RepeatButton` 卷动钮 + 中间 `ScrollViewer`（滚动条 Hidden）包 `Orientation=Horizontal` 的 `StackPanel IsItemsHost="True"`）——标签**永不换行**，溢出经卷动钮/滚轮横滚；`ScrollableWidth=0`（无溢出）时卷动钮经 DataTrigger **折叠**（对齐 WinUI3"溢出才出按钮"），到达两端时命令 `CanExecute=false` 自动置灰（Opacity 0.35）。
  - **模板内元素取用按 §9 第 46 条实战**：`Tabs.Loaded` 里 `Tabs.Template.FindName("HeaderScroll", Tabs)` 取滚动宿主（挂 `ScrollChanged → CommandManager.InvalidateRequerySuggested` 刷新按钮状态）；卷动钮不挂 Click 而用页面级静态 `RoutedCommand`（`HeaderScrollLeft/RightCommand` + `CommandBinding`，路由命令从模板冒泡到页面，RepeatButton 按住可连发）；`SelectionChanged → BringIntoView()` 让新开/搜索跳转/关闭后顺移的标签自动滚入可视区；`PreviewMouseWheel` 把滚轮转成横滚。
  - 卷动钮为自绘轻量模板（26×26 圆角块 + `ChevronLeft24/ChevronRight24`，悬停 `SubtleFillColorSecondaryBrush` / 按下 `SubtleFillColorTertiaryBrush`，两键项目已多处使用）。
  - 构建 0 错误、14 既有警告；未实机核验。
- **修复（用户实机反馈，09-15："点击系统通知的 KB 会显示查询失败"）**——根因是**击杀广播先于 API 可查**（见 §9 第 47 条）：
  - 日志实锤（`Log/20260915.txt`）：11:38 点通知 → `[ZKB] kills 直取两次均为空（KillID:138452560）` → 兜底也无数据 → null →「查询失败」；09:34 同模式还炸出 ESI `Ensure all IDs are valid before resolving`（空壳数据带无效 ID 去解析）。zkillboard 的 websocket 流把击杀推出来的那一刻，`/kills/killID/` 还要过几秒才查得到——而"点通知"恰恰是击杀广播后的最早时刻，按 ID 重查必然扑空。
  - 修复：**携带现成数据直开，不重查**。`KbNavigation.OpenKillmail` 新增 `KBItemInfo` 重载（内部随请求携带 `_pendingKillmailInfo`，`Drain()` 改为返回三元组 Entity/KillmailId/Info）；通知回调（`TryNotify` 闭包里本就有富化好的 info）、击杀流行点击（`KillStreamPage`）、实体统计页行点击（`EntityStatistPage`）全部直传 info；`ZKBPage.OpenKillmailAsync` 有现成数据就**零网络**建页，仅无数据时才按 ID 查询（"最高击杀"卡片只有 ID，但都是历史击杀、API 必可查，走 ID 路径）。
  - 收益：点通知秒开详情（省一次网络往返），"最新击杀查不到"的竞态从根上消除；附带修复了击杀流行点击"刚出的 kill"同样会查询失败的隐患。构建 0 错误、14 既有警告；未实机核验。
- **性能修复（用户实机反馈，09-15："加载 KB 时击杀人数过多会卡一会儿；从实体 KB 列表点击时也会卡住一会儿"）**——两个独立瓶颈，都是"UI 线程被同步工作占住"：
  - **① 富化逐条查询 → 批量去重**（`ZkbQueryService.EnrichDetailsAsync`）：原实现对 slice 里每条 killmail 各调一次 `KBHelpers.CreateKBItemInfo`，每条都做 `IDNameService.GetByIds`（SQLite 查询，未命中还会发 ESI `/universe/names`）；而 `DefaultConcurrency=1`（Core SQLite 非并发安全，见 §9 第 37 条）使其**完全串行**——50 条 = 最多 50 次查询。但一页击杀涉及的**角色/军团/联盟/星系/船型高度重叠**（同一战场同一批人），重复查询纯属浪费。改为：先汇总全页所有 ID **一次性解析**（`ResolveNamesAsync` + `MapSolarSystemService.Query` + `InvTypeService.QueryTypes` 各一次），再用字典**纯内存组装**（新增 `BuildFromResolved`），查询次数与页内条数**解耦**。单条组装失败只丢该条。
  - **② 图片在 UI 线程同步下载**（新增 `Controls/AsyncImage` + `Controls/AsyncImageCache`）：`TypeImageConverter` / `UrlToImageConverter` 走 `BitmapImage.UriSource`，那是 WIC 的**按需下载**路径——首次 `EndInit()` 在调用线程（图像绑定都在 UI 线程求值）**同步下载**。击杀列表每行 1 张舰船图标、KB 详情页每行 1 张头像，行一多就是"整页卡一会儿"；且转换器一旦返回 null **不会自动重算**，做不了"先占位后填充"。新增附加属性 `ctl:AsyncImage.Source/IdName/TypeId` (+`Size`)：后台抓字节 → 同线程解码 → `Freeze()` → 回 UI 线程直写 `Image.Source`（不依赖绑定刷新），按 URL 进程内缓存（`null` 也缓存，取不到的地址不再重试），并发同 URL 合并为一次下载；下载完成时校验容器地址，避免列表虚拟化复用造成串图。
  - **改造范围**：`KillListControl`（舰船图标）、`KbDetailPage`（受害者舰船图 + 攻击者头像）、`EntityStatistPage`（实体圆头像）、`KillRankCard`（排名卡图片）。`TypeImageConverter`/`UrlToImageConverter` 保留（频道市场窗等非列表场景仍在用），`IdNameImageConverter` 改为走 `AsyncImageCache.TryGet` 并注明"列表请用附加属性"。
  - 构建 0 错误、14 既有警告；未实机核验（效果需实机确认：击杀者上百的 KB 详情页与首页列表滚动应明显顺滑）。
- **性能修复二（用户实机反馈，09-15："从实体 KB 列表点 KB 还是卡一会，像是先把数据加载完才跳详情页"）**——卡的不是数据，是**渲染**：
  - **主因：外层 `ScrollViewer` 废掉了 `DataGrid` 的行虚拟化**（见 §9 第 49 条）。`KbDetailPage` 两个表格原本套着外层 `ScrollViewer`（+`MinWidth` 绑视口宽）——外层以**无限高度**测量表格，表格自己的滚动视口等于全部内容，于是**一次性实例化所有行**（攻击者表每行 1 张头像 + 6 列模板）。击杀者上百的 KB 点开就卡一下，且卡在标签首次布局时，观感就是"先加载完数据才跳页"。改为：去掉外层 `ScrollViewer`，表格自滚（`Vertical/HorizontalScrollBarVisibility=Auto`、`CanContentScroll=True`，仅在样式里把横向滚动条从 `Disabled` 改成 `Auto` 以保留窄窗可横向滚动），删掉指向外层视口的 `MinWidth` 绑定 → 只实例化可见行，页面秒出。
  - **页内加载态**（用户要求"在详细页加载数据并显示加载效果"）：数据本来就是携带现成 `KBItemInfo` 直开（无网络），真正耗时的是页内富化（名称/星系/船型）。现在标签先出现、富化在页内异步补齐，期间显示**局部加载遮罩**（`SmokeFillColorDefaultBrush` 半透明底 + 卡片 + `controls:SpinnerIcon`，文案沿用既有键 `ZKBPage_Loading`，**0 新增本地化键**），视觉与实体统计页的局部等待一致。**坑**：`KbDetailViewModel.IsLoading` 原本是普通自动属性（`{ get; private set; }`），**不发通知** → 遮罩绑上去永远不显示（属静默失效）；已改为带 `OnPropertyChanged` 的属性。
  - **顺带修一个"人多才触发"的隐患**：`ResolveNamesAsync` 原来一次性把全部 ID 交给 ESI `/universe/names`，而单条 killmail 的攻击者按 4 个 ID/人收集，**攻击者 ≥250 人时就会超过 ESI 的 1000 ID 上限**（也在逼近 SQLite 的 IN 变量数上限），整批会被拒绝、名字全空。改为按 500 分批解析再合并（单批失败不影响其余）。
  - 构建 0 错误、14 既有警告；未实机核验（重点复测：击杀者上百的 KB 点开后是否立刻出页 + 显示加载卡片，玩家名是否齐全）。
- **UI 调整四（用户要求，09-15："实体 KB 列表的类型下拉框、刷新挪到底部与切页按钮同一行，靠左显示"）**：
  - `EntityStatistPage` 的「KB 列表」页签：顶部工具栏整行取消，`类型`标签 + `ComboBox`(宽 140) + 刷新按钮(ArrowSync24) 移到底部行，与 `controls:PagerControl` 同处一个 Grid——筛选区 `HorizontalAlignment="Left"`，分页保持 `Right`；列表因此多出一行高度（Grid 由三行减为两行）。本地化 **0 新增键**（沿用 `StatistKBListPage_KBModifier`、`General_Refresh`）。
  - 验证：XAML/C# 编译通过（`dotnet build` 仅在**拷贝输出**阶段报 `MSB3027`/`MSB3021`——exe 被运行中的程序占用；关闭程序后重新构建即可生效，见 §9 第 50 条）。
- **UI 调整五（用户要求，09-15："KB 列表的受害者、最后一击只显示玩家名，WinUI3 里还有势力归属与头像"）**——对齐 WinUI 的 `KBListCharacterControl`：
  - **WinUI 原样**：每列 = 身份图（`Victim`/`FinalBlow` 的 `IdName`，按类别分派角色头像 / 军团、联盟徽标）+ 势力徽标（有联盟显联盟、否则显军团）+ 两行链接（名称、势力名，各自可点击跳实体统计）；**最后一击列的名称后附 `( 击杀者数 )`**（WinUI 把攻击者数放在这一列）。
  - WPF 改造：`KillListControl` 的受害者 / 最后一击列由"名称 + 类别文字"改为上述三列 Grid（两张 32×32 `ctl:AsyncImage`，`Size=64` 取更清晰的源图；两行 `KbLinkButton`），列宽 176 → 210；点击分派新增 `victimfaction` / `finalblowfaction` 两个 Tag（`OnEntityClick` 里分别取 `VictimFctionName` / `FinalBlowFctionName`）。
  - 数据侧：`VictimFctionName` 已有，**`FinalBlowFctionName` 是本次新增**（Core `KBItemInfo`，与前者对应），并在 `ZkbQueryService.BuildFromResolved`（批量富化主路径）与 `KBHelpers.CreateKBItemInfo`/`CreateKBItemInfoAsync`（旧/兜底路径）三处赋值，保证两条路径都有势力名。Core 属性为纯新增，WinUI 侧不受影响。
  - 构建：编译 0 错误、无新增告警（55 条唯一告警全在既有文件，见 §9 第 50 条关于"仅拷贝阶段失败"的说明）。
- **修复（用户实机反馈，09-15："位置列只有星系、没有星域"）**——**上一轮批量富化改写的回归**（见 §9 第 51 条）：
  - 为性能把逐条 `KBHelpers.CreateKBItemInfo` 换成自写的批量组装后，只补了名称 / 星系 / 船型，**漏了 `Region` 与 `Group`**——这两者需要二次关联（`regionId` 藏在星系里、`groupId` 藏在船型里）。结果：列表"星域"第二行静默空白、详情页副标题也少了星域。编译、告警、日志全无提示。
  - 修复：`EnrichDetailsAsync` 在解析完星系/船型后再收集一轮 `RegionID` / `GroupID`，用 `MapRegionService.Query(ids)` / `InvGroupService.QueryGroups(ids)` **各批量查一次**（仍然与页内条数解耦）；`BuildFromResolved` 补 `info.Region` / `info.Group` 赋值，行为与原 `KBHelpers` 完全对齐。
- **UI 调整六（用户要求，09-15："估价去掉粗体；位置列星系第一行、星域第二行"）**：
  - 列表估价列去掉 `FontWeight="SemiBold"`（用户对本控件的 `Width="Auto"` 微调一并保留）。
  - 位置列**布局本就是"第一行星系（+安全等级）、第二行星域"**（两者都是可点链接），用户看到"没有星域"实为上述数据回归；修数据后即正常显示。本地化 0 新增键。
  - 验证：本次 app 已关闭，`dotnet build` **完整成功**——0 错误、35 警告（全量双项目基线），并已把新产物写入 `bin\Debug\net8.0-windows10.0.19041`（Core.dll 15:35:50 / 主程序集 15:36:12）。
- **UI 调整七（用户要求，09-15："KB 详情页里参与者的名字、势力、舰船要可点击跳转；受害者的名字、舰船、星系、星域也要"）**：
  - **统一链接控件**：`KbLinkButton` 样式从 `KillListControl` 的局部资源**提升为全局键控样式**（`Controls/GlobalControlStyles.xaml`）——与本项目既有的 `StretchListItem`/`TranslationFieldLabel` 同样处理（跨页面复用的样式必须放全局，否则 `StaticResource` 找不到会在页面构造时抛 `XamlParseException`）；列表与详情页共用同一份定义，避免两处漂移。
  - **点击契约**：新增 `KbDetailPage.OnEntityLinkClick`——各链接的 `Tag` 上直接挂 **`IdName` 对象**（`Tag="{Binding VictimEntity}"`），处理器只做 `Tag as IdName → KbNavigation.OpenEntity(idName)`，无需按字符串 Tag 分派；导航链路与列表内的实体链接完全一致（跳到 ZKB 页开该实体统计标签）。
  - **受害者侧**（`KbDetailViewModel` 新增 `IdName` 属性）：`VictimEntity`（角色/军团/联盟）、`VictimFactionEntity`（联盟优先、否则军团）、`ShipEntity`、`SystemEntity`、`RegionEntity`，另加 `SystemSecurityText`。头部由"标题 + 副标题字符串 + 尾随星系名"改为 **4 行离散链接**：受害者名（16px，保留原字重）/ 势力 / 舰船 · 星系(安全等级) · 星域 / 时间——原先 `SubTitle` 一行字符串与之重复，故不再展示（`BuildSubTitle` 保留未删），尾随的重复星系名一并去除。
  - **参与者侧**（`AttackerRow` 新增 `CharacterEntity` / `FactionEntity` / `ShipEntity`）：角色名与势力名由纯文本改为链接（势力名同时修正为**联盟优先、否则军团**，原先只绑 `AllianceName`，无联盟时该行为空）；舰船列由 `DataGridTextColumn` 换成模板列 + 链接按钮。
  - 本地化 0 新增键；构建编译通过（`dotnet build` 仍只在**拷贝阶段**失败：用户重启了程序，exe 被 `TheGuideToTheNewEden (32216)` 占用，见 §9 第 50 条）。
- **UI 调整八（用户要求，09-15："把 KB 详情里的受害者头像、势力头像也显示"）**——对齐 WinUI `Views/KB/KBDetailPage.xaml` 的头部布局：
  - 头部由 3 列改为 **4 列**：`受害者身份图(高 128) | 受害者舰船图(高 128) | 信息列 | 价值面板`。身份图走 `ctl:AsyncImage.IdName="{Binding VictimEntity}"`——按 `IdName` 类别自动分派角色头像 / 军团、联盟徽标（与 KB 列表同一套规则，受害者是军团或联盟时该图自动换成对应徽标）；舰船图仍用 `ShipImageUrl`，**源图本来就是 `size=128`**，此前只显示 72，现按 WinUI 用 128。
  - 势力改为 **军团与联盟各一枚 32px 徽标 + 各一条链接**（WinUI 同款），取代「UI 调整七」里那条二选一的合并链接；对应数据为空时徽标与链接**各自隐藏**（VM 新增 `VictimCorpEntity` / `VictimAllianceEntity` / `HasVictimCorp` / `HasVictimAlliance`，显隐用页面既有的 `BoolToVis` 转换器，未新增转换器）。
  - 本地化 0 新增键；构建 **0 错误、14 既有警告**（WPF 增量基线），产物已落地（`TheGuideToTheNewEden.dll` 16:06:41——本次构建时程序已关闭）。
- **UI 调整九（用户要求，09-15："搜索 Flyout 现在在左下方，改到左侧"）**：
  - `SearchFlyout` 的 `Placement` 由 `Bottom` 改为 **`Left`**：弹出层贴在搜索按钮**左侧**（WPF `PlacementMode.Left` 使弹出层右缘对齐锚点左缘），与按钮所在行齐平。
  - 同时把覆盖层锚点宽度由 420 **收回 40**（= 预留按钮列宽）：`Left` 模式下"锚点左缘 = 弹出层右缘"，锚点若仍是 420，弹出层会离按钮约 380px 远。**规律**：`Bottom` 想要"右对齐向左展开"就把锚点加宽；`Left`/`Right` 想要"紧贴按钮"就把锚点收窄到按钮本身。
  - 本地化 0 新增键；构建 0 错误、14 既有警告，产物已落地（`TheGuideToTheNewEden.dll` 16:15:04）。
- **UI 调整十（用户要求，09-15："搜索框固定在现在按钮处、不再 flyout、宽 200"）**：
  - `ZKBPage` 头行右端的**搜索按钮 + Flyout（含输入框）改为常驻搜索框**：`ui:TextBox`（宽 200）直接摆在按钮原位置（右对齐），只有**结果列表**仍用 `<ui:Flyout>` 弹出。相应地删掉 `SearchButton`、`OnSearchToggleClick`、`SearchFlyout.Opened → OnSearchFlyoutOpened`（按钮不存在了，聚焦逻辑失去意义）；`OnSearchTextChanged` 改为**输入即展开结果下拉、清空即收起**（仍保留 350ms 防抖）。
  - 模板头行右侧预留列 **40 → 240**：200 给搜索框本体，另 40 是给结果弹出层的余量——`Placement=Bottom` 按"锚点左缘对齐"展开，弹出层实际宽 = 内容 200 + 内边距（≈226），锚点 240 才保证右缘不出窗口（同「调整三/九」的锚点宽度规律）。
  - 本地化 0 新增键；构建 0 错误、14 既有警告，产物已落地（`TheGuideToTheNewEden.dll` 16:26:47）。
- **UI 调整十一（用户要求，09-15："把 KB 流的标签头也固定，实体页多到要滚动切换时不影响它"）**——**钉住"击杀流"标签头**（见 §9 第 52 条）：
  - **首个 TabItem 的头部收敛为零宽**：`Header = null` + `Margin/Padding = 0` + `MinWidth = 0` + `Width = 0`——它照常承载击杀流页面内容与选中态，但不在标签行里占位置（否则会出现第二个"击杀流"）；零宽不影响程序化选中（`SelectedIndex = 0`）与 `BringIntoView`。
  - **页面级覆盖层重绘头部**（`PinnedStreamHeaderHost` + `PinnedStreamHeader`，固定 `HorizontalAlignment=Left`、高 36 与标签行同高）：点击 `SelectedIndex = 0` 切回击杀流；选中态用 `DataTrigger`（`{Binding SelectedIndex, ElementName=Tabs}` == 0）切换主色文字 + 半粗 + 强调色下划线，视觉对齐 WPF-UI 的选中标签。
  - **标签容器运行时让位**：模板里的标签容器 `DockPanel` 命名为 `HeaderTabsPanel`，代码在 `Tabs.Loaded` 与钉住头部 `SizeChanged` 时把它的 `Margin.Left` 设为钉住头部实际宽度——于是实体/详情标签**始终钉住头部右侧滚动**，且标题随语言变宽变窄会自动跟随（不写死宽度）。
  - 本地化 0 新增键；构建 0 错误、14 既有警告，产物已落地（`TheGuideToTheNewEden.dll` 16:39:25）。
- **修复（用户实机反馈，09-15："没打开任何实体/KB 时头部区域变小，KB 流与搜索框压到内容区上了"）**——钉住标签头的连带回归（见 §9 第 53 条）：
  - 根因：首个 `TabItem` 头部收敛为零宽后，**它的高度也跟着塌了**；标签行是 `Auto` 行，于是整行高度变 0 → 内容区从页面顶端开始，而两个固定高度、Top 对齐的页面级覆盖层（钉住头部、搜索框）就压在了击杀流内容上（截图即此现象）。
  - 修复：**给该 TabItem 显式 `Height = 36`**（= 标签行高度），**并给模板里的标签行 Grid 写死 `Height="36"`**（双保险：即使将来标签内容再变，行高也不会塌）。构建 0 错误、14 既有警告，产物已落地（`TheGuideToTheNewEden.dll` 16:46:12）。
- **UI 调整十二（用户要求，09-15："加粗显示左右切换按钮"）**——标签行的左右卷动钮改为**实心字形**：
  - `ui:SymbolIcon` 加 **`Filled="True"`**（走 Fluent 的 `_filled` 变体，比描边版明显更粗），文字色由 `TextFillColorSecondaryBrush` 提到 `TextFillColorPrimaryBrush` 增强对比；按钮尺寸/悬停/按下态不变。
  - 探测方式（项目既有手法）：`Wpf.Ui.dll` 拷到临时目录后扫字符串表，确认 `SymbolIcon` 有 `FilledProperty` / `get_Filled`、字体为 `FluentSystemIcons-Filled`，且 `ic_fluent_chevron_left_24_filled` / `_right_24_filled` 字形都在（避免出现空白方框）。
  - 构建 0 错误、14 既有警告，产物已落地（`TheGuideToTheNewEden.dll` 16:54:41）。
- **过滤机制复核与处置（用户要求，09-15："检查过滤机制、分析是否存在 bug、明确各过滤条件间互斥关系"）**——完整结论见 §8 第 48–50 条，本轮处置两项：
  - **删除旧入口的失效过滤字段**（§8 第 48 条处置）：`ZKBSettingPage` 的 6 个"逗号分隔 ID"文本框（类型 / 星系 / 星域 / 角色 / 军团 / 联盟）连同其读写代码与 `Join`/`Parse` 辅助一并移除，该页只保留 **连接 / 通知 / 列表上限 / 通知阈值**；过滤条件统一在击杀流页的设置弹窗里维护（6 组黑白名单）。核心理由：那 6 个字段（`ZKBStreamConfig.Types / Systems / Regions / Characters / Corps / Alliances`）**没有任何代码读取**，留在界面上只会让用户以为"配了过滤"却全放行。`ZKBStreamConfig` 中对应字段保留（WinUI 侧可能仍在使用）。
  - **修掉"空攻击者跳过"**（§8 第 49 条处置）：`ZkbStreamFilter.Pass` 去掉 `if (detail.Attackers is { Count: > 0 })` 守卫，改为**始终执行**攻击者三项检查——空集合 + 包含项非空 → 判为不通过，与 WinUI 对齐（原先会放行）。
  - 未动的两条已知代价（见 §8 第 50 条末段）：过滤发生在**富化之后**（被过滤的击杀也要付完整富化代价，属"已过滤列表要展示行"的设计取舍）；`HookConfig` 订阅的是**集合实例**（若将来整体替换 `CommonExclusions` 等集合，脏标记会失效）。
  - 构建 0 错误、14 既有警告，产物已落地（`TheGuideToTheNewEden.dll` 17:21:01）。
- **设置收敛 + 过滤说明（用户要求，09-15："全局设置界面的剩余 zkb 设置全部迁移到 zkb 页面的设置弹窗；给过滤设置界面增加各个过滤间与或关系说明"）**：
  - **收敛为单一入口**：`SettingsPage` 移除"设置 → Zkillboard"分类，`ZKBSettingPage.xaml/.cs` 一并删除（它是 §8 第 48 条那组"无代码读取"旧字段的宿主）。核对结果：实时流的全部设置在 `KillStreamSettingView` 里本就是旧页的**超集**（旧页没有"列表排序"），因此无需搬运、只需删入口；`SettingsPage.BuildCategories` 留注释说明"为何这里没有 ZKB 分类"。
  - **过滤界面加"组合规则"说明**：`KillStreamSettingView` 的「过滤」页签顶部新增信息块（`Info24` + 6 条规则），把 §8 第 50 条对外表述清楚：排除优先 / 包含项按类别分别判空 / 组内 AND / 三组之间 AND / 攻击者是"任一"判定 / 留空=不限制。文案为新键 `ZKBHomePage_Setting_Filter_Semantics`（zh-CN、en-US 各一条；已按项目脚本查重：两侧各 1283 键、**0 重复**、新键两侧都在）。XAML 注释里写明"改过滤逻辑时必须同步维护该文案"。
  - 构建 0 错误、14 既有警告，产物已落地（`TheGuideToTheNewEden.dll` 17:28:57）。
  - **补充修复（同日实机截图："过滤说明提示显示不全、不会自动换行"）**：根因见 §9 第 54 条——**横向 `StackPanel` 会给子元素无限宽度，`TextWrapping="Wrap"` 因此完全失效**（文字被父 Border 裁切）；文案里原本用 `&#10;` 写的换行也未生效。改法：① 文案写成**不依赖换行**的整段（编号之间用"；"分隔，随宽度自然折行）；② 承载块由横向 `StackPanel` 改为 **`Grid`（图标 Auto 列 + 文字 `*` 列）**以限宽。`KbFilterPairControl` 的 `Tip` 说明块是同款写法、有同样隐患，一并改为 `Grid`。

---

## 5. 角色功能分层设计

```
授权层   CharacterAuthService ── CharacterStore ── AuthHelper / LoopbackAuthServer / SerenityAuthHelper
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
| 35 | WPF 版在**未更新的 Windows 10 21H1（19043.985）**上**启动即崩**（`0x80131506`，故障模块 `KERNELBASE.dll`，无托管异常、无日志） | .NET 9 起的 WPF 与该系统补丁级别不兼容——同机 .NET Framework / 6 / 8 的 WPF 与任何控制台程序都正常 | TFM 由 `net10.0-windows10.0.19041` 回退为 **`net8.0-windows10.0.19041`**（LTS），平台版本 `10.0.19041` 保留；连带 `AssemblyName` → `TheGuideToTheNewEden`、`H.NotifyIcon.Wpf` → 2.3.0。见阶段 49。**已实机验证**：net8 产物在该机上正常运行 |
| 36 | **授权回调的转发进程会清空共享的 `settings.json`**（每次欧服授权回调，以及"程序已在运行时又双击一次图标"都会触发） | 阶段 48 把单实例判定提前到 `CoreInitializer.Init()` 之前，转发进程不再 `Initialize()` 设置 → `App.OnExit` 里的 `SettingsService.Save()` 用**空字典**覆盖 `Configs/settings.json`。且实测在 `Startup` 里 `Shutdown()` **仍会触发 `Exit`**，所以这段清理必然执行 | 入口改为自定义 `Program.Main`（`<StartupObject>`）：非首实例**在创建 `Application` 之前**就转交并 `return`，`Exit` 不再触发；单实例状态从 `App` 迁到 `Program`（碰 `App` 的静态成员会连带加载 WPF 栈）。见阶段 50 |
| 37 | 欧服授权回调**必然要多起一个客户端进程**（协议激活只能"运行一条命令行"，无法把 URL 投递给已运行的进程） | 自定义 URL 协议是 Windows 上唯一由注册表驱动的机制，它只能表达"运行某个命令"；而同一个 SSO 应用不允许登记第二个 Callback URL，所以两种通道只能二选一（阶段 51 用户确认） | 改用**本地回环**：浏览器把回调直接打进主实例监听的 `http://localhost:<port>/callback/`，零第二进程、零注册表依赖。注册表那套的代码与界面已在阶段 52 **全部删除**。见阶段 51 / 52 |

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
| 欧服授权回调（**本地回环**，阶段 51） | **真实账号 + 真实 CCP 授权页** | 通过：浏览器 → 授权 → 回环回调 → 换码 → 落盘 `Auth.json`，**「添加角色」全流程一切正常**；未起第二进程，`settings.json` 未被覆写 |
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

### 阶段 54：多开预览浮窗的"不透明度"真正生效（画面透出后面的窗口）

- **现象**（用户反馈："多开功能预览浮窗透明度没生效"）：多开页把选中项的**不透明度**从 65% 一路调到 100%、10%，预览浮窗外观基本不变，被它挡住的游戏客户端完全看不见。
- **根因 ①（实现口径错）**：阶段 43 把"不透明度"实现成 **DWM 缩略图自身的 opacity**，而缩略图是按 alpha 合成到**本窗口自己的内容**上的——
  画面区的底色当时写死成纯黑（`ThumbnailArea.Background = Black`），于是"调低不透明度"只是把画面**向黑色变暗**，
  窗口本身始终完全不透明。对比 WinUI 版的实现（`SetLayeredWindowAttributes(LWA_ALPHA)` 整窗 alpha），
  用户要的是"能看见被预览窗挡住的那个客户端"，也就是**降不透明度=透出后面的窗口**，而不是"画面变暗"。
  - 实机数据（EVE 客户端在跑，预览窗 476×286 物理像素，取窗口区域平均亮度）：不透明度 65% → **47.05**，
    100% → **57.16**（画面确实在按 alpha 变暗）；调到 10% 时画面几乎全黑，但**窗口依旧不透明**、后面的窗口一点透不出来。
- **根因 ②（第一次修复为什么没成：缺了 DWM 那一步，症状是"只是泛白"）**：把画面区改成"洞"（底色透明）之后用户反馈
  「并没有，只是泛白」——实测确认：画面区**既不透明也不透出后面的窗口**，而是被一层实色填成了白味。
  - 判据（本阶段的关键手段）：在预览窗后面放一个**红/蓝/黑/绿四色测试窗**，看颜色能不能从画面区透出来。
    只有模糊区域那次（`DwmEnableBlurBehindWindow` 空区域 + `Window.Background=Transparent`）**四色一点都透不出来**。
  - 三件事缺一不可，补上之后就通了：
    1. **`DwmExtendFrameIntoClientArea(hwnd, 四个 -1)`**——把 DWM 玻璃框铺满客户区，**这一步才是"客户区可以透出桌面"的开关**；
       只调 `DwmEnableBlurBehindWindow` 完全没有效果（实测 HRESULT 都是 0，看起来"成功"）。
    2. **窗口背景必须真是透明**：WPF-UI 的 `FluentWindow` 会把 `Background` 设成主题底色（浅色主题=白），
       XAML 里的 `Background="Transparent"` 会被它覆盖；诊断日志实测 `bg=#00FFFFFF`（代码按回来的结果）vs
       `hostBg=#FFFAFAFA`（标题栏实色底）。所以 `EnsureTransparentClientArea()` 里每次重新赋值。
    3. **`CompositionTarget.BackgroundColor` 也要透明**（WPF 合成目标自带的底色，否则窗口没画到的地方会被它填掉）。
- **修法（最终）**：把"画面区"做成真正的**洞**（透明），让带不透明度的缩略图合成到"窗口后面的桌面/游戏窗口"上。
  1. `NativeMethods.EnableTransparentClientArea()`：`DwmExtendFrameIntoClientArea(-1)` + `DwmEnableBlurBehindWindow`（**空模糊区域**：只要透明不要模糊）。
     为什么必须走 Win32：预览窗口是 `ui:FluentWindow`，与 `AllowsTransparency` 冲突（阶段 43 实测抛 `InvalidOperationException`），
     拿不到 WPF 的逐像素透明。调用点：`OnSourceInitialized`、`Start`、`ShowWindow`（WPF-UI 显示窗口时会再调一次外观，需要补；调用幂等且很轻）。
     顺带修掉一处隐患：`OnSourceInitialized` 里原来在 `EnsureTransparentClientArea` **之后**才给 `_hwnd` 赋值，
     而 `EnsureHandle()` 会在内部触发本方法——首次调用拿到的还是 `IntPtr.Zero`。改为进方法先 `EnsureHandle()` 取一次。
  2. `GamePreviewWindow.xaml`：窗口 `Background="Transparent"`；标题栏外面包一层**实色** `Border`
     （`TitleBarHost`，底色 `ApplicationBackgroundBrush`），并在 `ApplyVisuals` 里与 `TitleBar` 一起显示/隐藏——
     客户区整体透明后，凡是需要实色的区域都得自己画底色。
  3. `ApplyPreviewPlaceholder()`：**有画面时**画面区底色 = `Transparent`（即那个洞）；没有画面时（源窗口最小化/已退出）
     仍是实色底（高亮色或主题底色）+ 提示文字，因此不会留下空洞。
  4. 高亮边框由"`Background` 填色 + `Padding`"改为 **`BorderBrush` + `BorderThickness`**：画面区透明后，
     外框只要填底色就会从洞里透出来、把整幅画面染成高亮色（**中间版本实测就是这个现象**：整幅画面泛绿）。
     留白尺寸仍由同一个 `GetHighlightThickness()` 导出，比例校正用的 `GetHighlightPadding()` 与画面区域几何完全不变。
- **最终行为/取舍**：只有**画面**是半透明的，桌面/游戏窗口从画面里透出来；标题栏（样式 0）、
  角色名叠加窗（样式 1，本来就是独立窗口）、高亮边框都保持**实色**——既保住了阶段 43 "名称/边框不跟着变灰"的取向，
  又让不透明度真正可用。缩略图 opacity 仍是唯一的淡化手段：100% 时画面完全不透明，越低越透。
- **验证**（实机：重建后在装有运行中 EVE 客户端的机器上跑，截图 + 像素统计 + 四色测试窗）：
  - 四色测试窗放在预览窗后面：画面区**逐像素**透出后面的内容——同一窗口里，压在游戏上的那半透出游戏界面、
    压在应用窗口上的那半透出应用的导航项（不是"整体泛白"）。
  - 样式 0（带标题栏）：标题栏保持实色（白底黑字 + 关闭键照常），只有画面透；样式 1（无标题栏）：
    切换后窗口高度正好少一个标题栏、画面占满窗口、角色名叠加窗照常出现（`TitleBarHost` 的联动隐藏生效）。
  - 高亮边框：样式 0/1 都仍是实色边框（点击预览窗激活源客户端后边框变绿、叠加窗底色切到高亮色）。
  - 交互：点击半透明的画面区仍能正常激活源客户端（实测前台窗口从本应用变成 `EVE - QEDSD`）——透明只影响合成，不影响命中测试。
  - 未覆盖：源窗口最小化/已退出时的占位分支（实色底 + 提示文字）本轮没有实机触发，那条路径给的是实色底、不会留洞。
- **构建状态**：**0 错误**（仅既有警告）。

---

### 阶段 55：预览浮窗不再限制最小尺寸

- **现象**（用户反馈："浮窗最小尺寸被限制了，不应该限制"）：预览浮窗拖到某一尺寸后再也拖不小。
- **两层限制**：① 我们自己的 **200×120**（XAML 的 `MinWidth/MinHeight` + `GamePreviewWindow` 的 `MinWindowWidth/MinWindowHeight`，
  后者还出现在比例校正、`SetSize`、滚轮缩放、默认尺寸等处）；② 系统的**默认最小跟踪尺寸**
  （`SM_CXMINTRACK/SM_CYMINTRACK`，约 112×27 物理像素）。
- **第一轮（只删前两层）没效果**：去掉 XAML 的 `Min*`、把常量降到 1 之后，窗口**仍然卡在当前尺寸**：
  `SetWindowPos` 与鼠标拖边框都被拒（`SetWindowPos` 返回 True 但矩形不变），拖动表现为"只能变大、不能变小"。
- **根因**：窗口对 `WM_GETMINMAXINFO` 的答复恒为"启动时那个尺寸"（实测 575×400）——**不是我们写的**。
  我们在 `HwndSource` 钩子里把 `ptMinTrackSize` 改成 1×1 之后**没有把它标记为已处理**，WPF / WPF-UI
  （`FluentWindow` + `WindowChrome` 自己也会处理这条消息）随后又把最小跟踪尺寸按回了窗口当前尺寸。
  诊断方式：钩子里加日志 + **从外部 `SendMessage(hwnd, WM_GETMINMAXINFO, 0, 缓冲区)` 读回窗口的答复**——
  加上 `handled = true` 前后，同一窗口的答复从 575×400 变成 1×1。
- **修法**（三处）：
  1. `GamePreviewWindow.xaml` 去掉 `MinWidth="200" MinHeight="120"`；
  2. `MinWindowWidth/MinWindowHeight` 由 200/120 降到 **1**（只兜住"算出 0/负数"的退化尺寸，不再构成可用意义上的限制），
     比例校正 / `SetSize` / 滚轮缩放 / 无历史尺寸时的默认值等处自动跟着放开；
  3. `OnWindowMessage` 里**接管** `WM_GETMINMAXINFO`：`ptMinTrackSize = 1×1` 并 **`handled = true`**
     （不再往后传、避免被 WPF/WPF-UI 覆盖；其余字段保持系统预填值）——**这一步才是真正让"拖小"生效的关键**。
- **验证**（实机，EVE 客户端在跑，预览窗为样式 1）：
  - 程序化尺寸：575×303 → `SetWindowPos(300,200)` → 稳定在 **300×158**（低于旧的 200×120 下限）；
    再放大到 800×560 → 自动按游戏比例校正为 **800×422**（比例锁仍然有效）。
  - 真实拖动：按住右下角边框往内拖 → 575×303 → **175×101**（远低于旧下限），此后还能继续拖到 22×47，没有人为下限；
    不再出现"拖不动"或"只能变大"。
  - 构建 **0 错误**。
- **取舍说明**：比例锁（"画面区域恒等于游戏客户区比例"，阶段 44）没有取消——放开的是**尺寸下限**，窗口仍按游戏比例缩放；
  把窗口拖得比标题栏还矮时画面区会被压到最小 1px（用户明确要求不设下限）。

---

### 阶段 56：预览浮窗的画面区稳定贴合游戏比例（"缩放后上下出现透明背景"）

- **现象**（用户反馈："缩放有时候会上下出现透明背景，像是窗口没有按照画面比例调整"）：缩放/拖动后画面区与游戏客户区比例不一致，
  缩略图按比例居中留边，而画面区现在是**透明的洞**（阶段 54），那圈留边就直接透出桌面——看起来就是"多出一条透明背景"。
- **根因（三处叠加，都让"比例校正"没能把画面区修到位）**：
  1. **校正里做了"同尺寸去重"**：`_lastCorrectedSize` 让"算出的目标尺寸与上次下发过的相同"时直接跳过。
     窗口完全可能在两次校正之间被别的路径改偏几像素（拖动收尾、恢复位置、源比例变化……），
     这时目标尺寸与上次相同、窗口却已经不对了，一去重就**再也不修**。
     实测：窗口 485×261（画面区 1.858）而游戏客户区 1.8949 → 上下各留 2.5px；把它强改到别的尺寸再回来，仍然停在 485×261。
  2. **拖动期间的比例锁用了拖动前的客户区**：`LockClientAspect` 反算"另一条边"时读 `GetClientRect`（旧值），
     于是拖动过程中另一条边不动、比例跑偏，要等拖动结束后 80ms 的防抖校正才补回来——期间就是透明留边。
  3. **缩略图摆放与比例校正用了两套口径**：缩略图目标矩形原来取 WPF 布局（`TransformToAncestor` + `ActualWidth/Height`），
     比例校正取 Win32（`GetWindowRect/GetClientRect`）。窗口是用 `SetWindowPos` 改尺寸的，
     **WPF 布局会滞后于实际窗口**——诊断日志实测抓到过"WPF 布局 460×320 DIP、实际窗口 485×110"的瞬间，
     那一刻缩略图按错误的容器摆放、画面区就多出透明边。
- **修法**：
  1. `CorrectWindowSizeToAspect` 去掉 `_lastCorrectedSize` 去重（防自循环交给原有的 2px 容差判断：
     下发后窗口就等于目标尺寸，下一轮必落在容差内）。
  2. `LockClientAspect` 反算另一条边时用**用户这次给出的新值**（`proposedWidth/Height`）而不是旧客户区。
  3. `UpdateThumbnailDestination` 的画面区域改成与比例校正**同一套 Win32 口径**：
     客户区（`GetClientRect`）+ 标题栏元素高度（`GetChromeHeight`）+ 高亮留白（`GetHighlightThickness`），
     两边由同一组函数导出，不再各算一套。`GetChromeHeight` / `GetHighlightPadding` 因此重新成为活代码（阶段 44 的实现）。
  4. 顺带把"源窗口客户区尺寸变化"也接上：300ms 的轮询原来只盯 `IsIconic`，现在同时比较源客户区尺寸，
     变了就重算比例（游戏改分辨率/切窗口/拉伸窗口后，预览窗跟着走）。阶段 44 把它列为"未做/后续"，本轮补上。
- **验证**（实机，EVE 客户端客户区 2560×1351 = 1.8949，预览窗样式 1 + 高亮开=四周 4DIP 留白）：
  - 比例贴合：485×261 → 画面区 475×251（1.8924，差 **0.62px**）；强改到 520×420 → 自动收敛到 520×279
    （画面区 510×269 = 1.8959，差 **0.27px**）。都在 1px 以内 → `FitAspect` 走"比例一致、铺满"的短路，不再留边。
  - 拖动（模拟真实拖右下角）：485×261 → 245×134，画面区 235×124 = 1.8952（差 0.04px），**拖动结束即比例正确**，
    不必再等防抖校正。
  - 截图核对：画面被高亮边框完整包住，上下没有透明缝。
  - 未覆盖：游戏侧改分辨率触发轮询重算这条分支只做了代码检查（没实际去改游戏分辨率）。
- **构建状态**：**0 错误**（仅既有警告）。

---

### 阶段 57：高亮边框只画出左/上两条 + 偶发白边（WPF-UI 的最小尺寸卡住 WPF 布局）

- **现象**（用户反馈："有时候有白边，高亮时只有左跟上两个边框"）。
- **根因**：**WPF-UI 的 `FluentWindow` 样式自带一个最小尺寸 = 460×320 DIP**
  （实测日志：`构造后 MinWidth=460 MinHeight=320 localMinW=unset(来自样式/主题)`——
  不是本地值，是样式给的；项目自己的 XAML 里没有 Window 级 `MinWidth/MinHeight`）。
  它会**卡住 WPF 的布局尺寸**：
  - 窗口是用 Win32（`SetWindowPos`）缩小的，而 WPF 的 `Width`/`Height` 会被这个 Min 抬回 460×320，
    于是布局（Root / `ThumbnailFrame`）一直停在 ≥460×320 DIP，**跟实际窗口脱节**；
  - 高亮边框是 WPF 画的（`ThumbnailFrame.BorderBrush` + `BorderThickness`），**按"大尺寸"绘制** →
    右侧/下侧的边框线落到窗口之外 → 看起来"高亮时只有左跟上两个边框"；
  - 布局多出来的那圈正是画面区的"洞"（阶段 54 起画面区是透明的），于是透出后面的窗口
    （后面是浅色窗口/页面时就是"白边"）。
  - **回溯阶段 55**：那轮我量到的"最小跟踪尺寸 = 启动时的尺寸 575×400 物理像素"其实就是这个值
    （460×320 DIP × 1.25 = 575×400）——不是"当前尺寸"，而是 WPF-UI 样式的固定最小值，
    恰好与当时的窗口尺寸一致才被误读。当时接管 `WM_GETMINMAXINFO` 只解决了**拖动**这条路径，
    WPF 布局这一层的最小尺寸仍在，于是"能拖小、但画面/边框按大尺寸画"。
- **修法**：
  1. 显式 `MinWidth = 0; MinHeight = 0;`（本地值才能压过样式）：在 `EnsureTransparentClientArea`（启动/显示时）
     设一次，并在 300ms 兜底对账里再清一次（WPF-UI 显示窗口时会重新套样式）。预览浮窗按用户要求不限制最小尺寸。
  2. 兜底对账 `SyncLayoutSizeIfStale()`（并进原有的 300ms 源窗口轮询）：比对 WPF 窗口尺寸
     （`ActualWidth/ActualHeight` × DPI）与 Win32 客户区，不一致时清 Min、把 `Width`/`Height` 按实际尺寸设回，
     并**补发一条 `WM_SIZE`**（新增 `NativeMethods.NotifyClientSize`）——只设 `Width`/`Height` 有时会被 WPF
     判成"没变化"而跳过重排（窗口本身已是目标尺寸），补 `WM_SIZE` 才会真正按真实客户区重新布局。
  3. `WM_ENTERSIZEMOVE`/`WM_EXITSIZEMOVE` 期间跳过对账（`_inSizeMove`），拖动结束再对一次账并安排比例校正。
- **验证**（实机，EVE 客户端，预览窗样式 1 + 高亮开）：每条边内侧 3px 处取中点像素：
  - 修复前：左 `(0,128,0)` 绿、上 `(0,128,0)` 绿、**右 `(243,243,243)` 白**、下暗色（与用户描述一致）。
  - 修复后：**四条边全部 `(0,128,0)`**；正常改尺寸（560×400 → 收敛 560×300）与
    **故意用 `SWP_NOSENDCHANGING` 制造 WPF 布局滞后**（660×470 → 660×353）之后仍四边全绿、
    画面区比例误差 0.05–0.48px。
  - 本轮日志里"WPF 尺寸与实际窗口不一致"的兜底告警出现 **0 次**——Min 清零后布局本来就跟着窗口走，不需要兜底。
- **构建状态**：**0 错误**（仅既有警告）。

---

### 阶段 58：带标题栏的样式不再有透明边框（缩略图摆放里标题栏高度被重复乘了一次 DPI）

- **现象**（用户反馈："有标题栏的窗口带有透明边框"）：样式 0（带标题栏、高亮关）的预览窗，画面没有铺满窗口——
  标题栏下面以及左右各有一条缝，透出后面的窗口（后面的窗口偏浅时就很显眼）。
- **根因**：阶段 56 把缩略图摆放改成 Win32 口径时，公式写成了
  `top = Math.Round((chromeHeight + thickness.Top) * dpiY)`，
  而 `GetChromeHeight()` **返回的已经是物理像素**（它内部已经乘过 DPI）——等于把标题栏高度又乘了一遍：
  1.25 缩放下 35px 变成 44px，画面区少了 9px，比例也跟着偏；`FitAspect` 于是按比例居中留边，
  而留边处是画面区的"洞"（阶段 54 起），看起来就是"一圈透明边框"。
  （样式 1 的 `chromeHeight` 恒为 0，`0 × 1.25` 还是 0，所以只有带标题栏的样式中招。）
- **修法**：`top = chromeHeight + (int)Math.Round(thickness.Top * dpiY)`——**统一单位**：
  `GetChromeHeight()` 是物理像素、`GetHighlightThickness()` 是 DIP（要乘 DPI），并在代码里写明，
  免得下次又把两者混在一起。
- **验证**（实机，预览窗样式 0、高亮关、870×494）：把一个**纯色测试窗**（纯红/蓝/绿/洋红四块）压在预览窗后面，
  然后扫描预览窗客户区内的全部像素（步长 2px）匹配"纯图案色"：
  **上/下/左/右/中间命中数全部为 0** → 画面把客户区铺满了，没有任何透出后面窗口的缝。
  对照点：预览窗左侧 40px 处的屏幕像素是 `(254,0,0)`（纯红）→ 说明测试窗确实在预览窗后面、判据有效。
- **构建状态**：**0 错误**（仅既有警告）。

---

### 阶段 59：滚轮缩放不再"放大又被还原"（缩放基准由"窗口"改为"画面区域"）

- **现象**（用户反馈："会出现缩放放大又自动还原……鼠标滚轮放大缩小都会还原"，并且"放大不了，可以缩小"）。
- **根因**：`OnPreviewMouseWheel` 原来按**窗口尺寸**做等比缩放
  （`ScalePreservingAspect(rect.Width, rect.Height, sourceWidth, sourceHeight, …)`），等于把"窗口比例"当成了"画面比例"。
  样式 0（带标题栏）的窗口比画面多出标题栏（约 35px），窗口比例 ≠ 画面比例：
  滚轮刚把窗口放大，80ms 后的比例校正（按"画面区域"算，见阶段 44）就把它算回去 → "放大又被还原"；
  缩小时两边同样各算一套（都倾向更窄），用户感觉"只能缩小、放大不了"。
- **修法**：滚轮改为缩放**画面区域**（客户区 − 标题栏/名称条 − 高亮留白，与 `CorrectSizeToAspect` **同一口径**），
  再换算回窗口尺寸，并沿用 `ClampToWorkArea` 夹进工作区。由于两边由同一组量导出，滚轮的结果天然满足比例校正，不会再被拉回。
- **验证**（实机，样式 0、高亮关）：光标停在画面区中间，用 `mouse_event(MOUSEEVENTF_WHEEL)` 模拟滚轮：
  - 连续上滚 3 次：459×277 → **531×315**（= 459 × 1.05³ ✓），**1.5s 后仍是 531×315**（没有被还原）；
  - 再下滚 2 次：→ **482×289**（= 531 ÷ 1.05² ✓），1.5s 后不变；
  - 两次的画面区比例 1.896 / 1.898（游戏客户区 1.8949）→ 比例锁依然成立。
- **构建状态**：**0 错误**（仅既有警告）。

---

### 阶段 60：进程列表的"角色名"列不刷新（转换器绑了整个对象）

- **现象**（用户反馈）：EVE 客户端刚启动时窗口标题只有 `EVE`，选中角色后才变成 `EVE - 角色名`；
  进程列表的**名字列一直显示 `EVE`**，等多久都不变，点"刷新列表"也不变（标题列倒是会跟着变）→
  "不会识别到角色名称"。
- **根因**（纯 WPF 绑定语义，与刷新逻辑无关）：名字列的绑定是
  `Text="{Binding Converter={StaticResource ProcessName}}"`——**绑的是整个 `ProcessInfo` 对象**（路径 `.`）。
  WPF 对"路径为 `.`"的绑定只在源对象发出**空名/`null` 名**的 `PropertyChanged` 时才重新求值，
  而 `ProcessInfo` 改的是具体属性名（`WindowTitle`、`Setting`），于是**转换器再也不会重跑**：
  名字列就停在"进程刚被发现那一刻"的值（那时标题还是 `EVE`）。
  标题列是 `{Binding WindowTitle}`（具体路径）所以会刷新——这正是"标题变了、名字没变"的原因。
  （实测：把 EVE 窗口标题改回 `EVE` 再改回 `EVE - QEDSD`，标题列两次都跟着变，名字列始终显示旧值。）
- **修法**：`ProcessDisplayNameConverter` 由 `IValueConverter` 改为 **`IMultiValueConverter`**，
  改用 `MultiBinding` 把两个**会变的输入**分别绑上：`{Binding WindowTitle}` + `{Binding Setting.Name}`——
  任一变化都会重新求值。解析规则不变（配置里的角色名优先 → 否则取标题第一个 `-` 之后的部分 → 否则回退标题原文），
  且解析口径与 Core `ProcessInfo.GetCharacterName()` 保持一致。
- **验证**（实机复现用户场景）：把 EVE 客户端窗口标题临时改成 `EVE`（模拟"刚启动、未选角色"）→
  启动应用 → 列表为 **`EVE / EVE`**（名字列 = 回退到标题，与用户描述一致）→ 把标题改回 `EVE - QEDSD` →
  3.5s 后列表变成 **`QEDSD / EVE - QEDSD`**（名字列已跟着更新）。修复前同样操作名字列会一直停在 `EVE`。
- **构建状态**：**0 错误**（仅既有警告）。

---

### 阶段 61：选中角色、出现角色名后没有自动开始预览

- **现象**（用户反馈）："选完角色出现角色名称后没有自动开始预览"。
- **根因**：`AutoStartNewProcess`（"自动开始新出现的进程"）**只在新发现进程的那一轮生效**——
  `RefreshAsync` 里只有遍历 `byHandle.Values`（本轮新发现、尚未入列表的进程）时才会"能解析出角色名就 `StartProcess`"。
  EVE 刚启动时窗口标题只有 `EVE`、解析不出角色名 → 不开始（这一步是对的）；
  等选中角色、标题变成 `EVE - 角色名` 时，这个进程**早就在列表里了**（不属于"新发现"），
  而改名那条分支只对"已在前台运行且带配置"的进程做换绑（`OnCharacterSwitched` 开头就 `return` 了），
  于是**再也没有人调用 `StartProcess`**。
- **修法**：在"改名后刚解析出角色名"这一刻补一次自动开始：
  `!hadCharacter && !existing.Running && AutoStartNewProcess && 现在能解析出角色名` → `StartProcess(existing)`。
  用"**改名前是否已经有角色名**"（`hadCharacter`）把**角色切换**排除在外——那种情况归
  "同进程切换角色后沿用设置"与 `OnCharacterSwitched` 管，不该被"自动开始"逻辑重新拉起来。
- **验证**（实机，先复现再修）：把 EVE 客户端窗口标题临时改成 `EVE`（模拟"刚启动、未选角色"）→ 启动应用 → 进入多开页 →
  列表里有进程但**没有预览窗**（正确）；把标题改回 `EVE - QEDSD` → **修复后 500ms 内自动出现预览窗**
  （窗口标题 `QEDSD` + 角色名叠加窗 `PreviewName`）✓；**修复前**同样操作 **6s 后仍无预览窗**（已复现该缺陷）。
- **构建状态**：**0 错误**（仅既有警告）。

---

### 阶段 62：星图（Map）模块迁移 —— SkiaSharp 全新渲染 + 页面化情报/导航（重设计，非逐行照搬）

- **目标**：替换 `Views/Pages/MapPage.cs` 占位页，迁移 WinUI 星图核心能力；按用户要求**不照搬 WinUI 的框架与 UI**，
  重新设计星图展示方式（深空科幻 HUD 风格），必要时用开源库做渲染效果。
- **渲染技术选型（与 WinUI 的最大分歧）**：WinUI 用 **Win2D `CanvasControl`**（改数据坐标 + Invalidate 全量重绘，三层画布分离）；
  WPF 没有 Win2D，若沿用 WPF 视觉树（8000+ Ellipse/Line）会退化严重。选 **SkiaSharp**（`SkiaSharp.Views.WPF` 的 `SKElement`）：
  - LiveCharts 2.0.5 **已经传递带入** SkiaSharp 3.119.0（阶段 25 为其补过 TPV ≥ 10.0.19041），csproj 显式声明同版本（防传递漂移），**零新增重量级依赖**；
  - Skia 光栅化 8000 星系 + 10000 星门连线单帧毫秒级，且能做径向渐变发光、深空渐变、视差星尘等"科幻感"效果；
  - 交互自绘（滚轮以鼠标为锚缩放、拖拽平移、命中测试线性扫描 8k 节点 ~0.1ms，无需空间索引）。
- **新增文件**：
  - `Views/UserControls/Map/StarMapCanvas.cs` —— 核心画布（继承 `SKElement`）：
    - **LOD 分级**：连线透明度随缩放淡入（贴图缩放下不画）；`zmult≥5.5` 画星系名；`≥13` 画节点内安等数字；`>30` 画白色内核；
    - **着色**：安等（高安青绿 `#2EE6A8` / 低安橙→青绿过渡 / 00 红 `#FF4D6A`）、击杀/通行热度（对数刻度 蓝→橙→红）；
    - **覆盖层**：情报红圈脉冲（半径随威胁权重）、角色头像标记（圆形裁剪 + 定位光环 + 名字）、航线发光折线 + 流动光点 + 航点编号徽标、悬停高亮相邻星门、选中双环、定位涟漪高亮（`ToSystem`）；
    - **性能（实测踩坑后重构，见下）**：**底图缓存**——背景渐变/星尘/连线/节点标签渲染进 `SKBitmap`，键 = (zoom, offset, size, dpi, dataVersion, colorMode)；视图未变时动画帧只"贴底图 + 画覆盖层"；节点外发光用**精灵缓存**（颜色量化 5bit/通道，上限 96 张，避免每帧每节点创建渐变着色器）；
    - **动画驱动**：`CompositionTarget.Rendering` 仅在 `HasActiveAnimation`（飞行动画/情报圈/航线/定位高亮）时 `InvalidateVisual`，静止零开销；**选中环有意做成静态**（选中态不再触发连续重绘）。
  - `Services/Map/MapSettingService.cs` —— `Configs/MapSettings.json`（Core `MapConfig`），**与 WinUI 同路径同格式**，情报配置互通；
  - `Services/Map/ChannelIntelManager.cs` —— 复刻 WinUI 同名单例：聚合运行中的 `ChannelIntelSession`，`ListenChannelIntel` 把会话观察者切 `IgnoreJumps=true`（"无视跳数"旁路）、`UnListenChannelIntel` 还原；聚合 `OnIgnoreJumpsIntelUpdate`。`ChannelIntelSession.Start/Stop` 挂钩 Register/Unregister（会话停止时自动摘除其监听）；
  - `Services/Map/CharacterLocationService.cs` —— "显示角色"数据源：轮询全部授权角色 ESI `Location.GetCharacterLocation`（30s，令牌失效自动刷新），失败角色跳过；
  - `ViewModels/Map/MapPageViewModel.cs` —— 数据装载（`MapSolarSystemService.QueryAll` + `MapSolarSystemJumpService.QueryAll` + 区域名，Y 轴翻转口径与 `SolarSystemPosHelper` 一致）、ESI 击杀/通行统计、搜索、情报流（关键词过滤 + `ChannelDuration` 过期清理 + 按星系聚合权重）、导航（航点/规避/星门或旗舰跳/MaxLY）；
  - `Views/Pages/MapPage.xaml(.cs)`（替换占位页，类名/命名空间不变，`MainWindow` 注册无需改动）：
    - 顶栏：星系搜索（Popup 建议列表）、**星域定位**下拉（按星域包围盒适配视图）、着色模式、角色/情报开关、导航弹窗、重置视图；
    - 画布区永远是深空（不随主题变）；HUD 浮层（顶中悬停提示 / 右上情报流 / 右下选中星系信息卡）用固定深色玻璃面板 + 青色描边，不随主题；
    - 导航是**页内 Popup 面板**而非 WinUI 的 ToolWindow：选中星系后"选中加入"航点（首个为起点）/规避，结果列表点击定位、整条航线画在图上，可选角色"在游戏中设置"（ESI `UserInterface.SetAutopilotWaypoint`）。
- **与 WinUI 的有意差异**：情报工具不再是独立 ToolWindow（页签 + 设置页），改为页面右侧常驻情报流面板 + 顶栏开关；过滤改为排除/包含关键词（空格分隔，落盘到共用 `MapIntelConfig.Exclusions/Inclusions.Name`）；导航无燃料列（不做旗舰型号/技能换算）；星图永远深色（不做浅色主题适配）；连续动画只用"底图缓存 + 覆盖层"方案（WinUI 无此层）。
- **实机验证（CUA 自动化点击 + UIA 断言）**：构建 0 错误；启动 → 导航星图页 → **悬停命中测试命中 `西玛特尔 埃维斯贝尔 0.8`**（HUD 文案）→ **点击选中成功**（信息卡显示 星系名/安等/区域）→ **ESI 统计载入**（通行 153）→ 星门邻接/搜索/着色控件齐全；日志无异常。
- **实测踩坑（重要）**：首版"选中星系脉冲环"导致 `CompositionTarget.Rendering` 连续 60fps **全量重绘**（8k 节点 + 10k 连线 + 每节点创建渐变着色器），实测 **CPU 105%**。修复：① 选中环改静态；② 引入**底图缓存** + 发光精灵缓存。复测**空闲 CPU 1.1%**（10s 窗口），动画帧只剩位图 blit + 少量覆盖层绘制。
- **构建状态**：**0 错误**（仅既有警告）。

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
24. **翻译只覆盖"本地数据库里有的专有名词"**（阶段 46）：物品 / 星域 / 星系 / 空间站可中英互译；**通用文本**（句子、军团/角色名）与**结构（建筑）名**（不在 SDE 里）都译不了——后者要靠 `StructureService` 的 ESI 解析，前者要等在线/本地模型翻译源接入（`ITranslationProvider` 已预留）。
25. **翻译没有批量模式**（阶段 46 遗留）：一次只查一个名词；"粘贴一份物品清单逐行出译文"未做。
26. **频道翻译（ChannelTranslationPage）仍是导航占位**：它对聊天正文做通用文本翻译，需要与第 24 条同一套能力，届时可复用 `ITranslationProvider` 与语言判定，并复用频道日志观察者（`ChannelIntelObserver` 那一套）。
27. **`Converters/TypeImageConverter` 在受限环境下会抛异常**（阶段 46 核验时暴露）：它走 `BitmapImage.UriSource` 的 WPF URI 下载路径，会经 `MS.Win32.WinInet.get_InternetCacheFolder()`；该调用失败时抛 `COMException 0x80072EE4` 并记一条 ERROR（图标空白，功能不受影响）。翻译页已改走 `HttpClient` + 同线程解码 + `Freeze()`（估价页同一做法）绕开该路径；**共用转换器本身未改**，使用它的 `ChannelMarketWindow` 若在同类环境出现该日志，可照同一方式替换。
28. **AI 翻译（阶段 47）的边界**：只在**本地 mock 服务**上做过完整核验——真实服务商（OpenAI/DeepSeek/通义/GLM/Kimi/Azure/Claude/Gemini）需要用户自己的 Key，本轮未实测任何一家；协议层的请求形状与响应/流式解析已按各家文档逐字段核对并有断言覆盖，但**首次接入请先用设置页的「测试连接」**。
29. **术语库只覆盖 SDE 里的名词**（阶段 47）：物品/舰船/装备/星系/星域/空间站可约束（实测 57,010 条）；**结构（建筑）名不在 SDE 里**（要走 `StructureService` 的 ESI 解析），玩家/军团/联盟名也不在——这些只能靠模型自己或后续的"用户术语表"。
30. **AI 翻译的 Key 是明文**（阶段 47）：存在共用 `settings.json` 的 `TranslationPage.Ai.ApiKey`，与 `ESILicense.txt` 同一坦诚程度；要更强保护可后续换 DPAPI（`ProtectedData`）。日志里不会输出 Key（异常信息只带 HTTP 状态与服务端 message）。
31. **AI 翻译暂无并发与批处理**（阶段 47）：一次只发一个请求，没有按行批量、没有速率限制器；频道翻译（S4）已把多角色多频道的消息收敛到**一条队列串行翻译**（按次计费 + 易限流下最稳），但"一次请求翻多行"的批量还没做，频道刷屏时 token 消耗与延迟仍然线性增长。
32. **术语库每进程重新抽取**（阶段 47）：实测 57,010 条 241ms、索引 234ms，约 0.5s 一次，只在首次使用 AI 源或打开设置页时发生；未做落盘缓存（避免与 SDE 更新脱节），如嫌慢可加"版本 + 数据库时间戳"校验的缓存文件。
33. **语言字典重复键 = 应用启动即崩，且没有任何日志**（阶段 47 实测踩到）：`Resources/Languages/*.xaml` 里同名键会让 `ResourceDictionary` 加载抛 `ArgumentException: Item has already been added`；异常发生在 `App.xaml` 合并字典阶段，**`dotnet build` 0 错误、应用日志 0 字节**，进程以 `0xE0434352` 退出，现场只剩"窗口没出现"。**改语言文件后请查重键**：`(Select-String -Path zh-CN.xaml -Pattern 'x:Key="([^"]+)"' -AllMatches)` 分组看 count>1；或用本项目那次用的 `PageProbe` 思路（加载应用级资源字典并直接构造页面），它能把完整异常链打出来。
34. **频道翻译（S4）的验证边界**：预清洗/跳过规则/队列/失败去重/设置落盘都是**离线探针**（20/20 断言，注入替身翻译器；追加的 `StripProbe` 15/15 用用户真实 MOTD 覆盖清洗链路，`ViewProbe` 12/12 覆盖列表渲染与可选中复制）验证的；实机只验证到"页面渲染 + 未配置提示"（真实聊天日志 + 真实模型未跑，需要用户 Key 与游戏内频道）。另外频道翻译的**触发关键词依赖 Core 观察者的 `Important` 语义**（不填关键词 = 全部消息都翻译）。
35. **"AI 新词候选"是启发式，会有误报**（阶段 47 S5）：规则是"大写开头 + 术语库没有 + 译文里没原样出现"，因此句子里的普通英文词（如 `Warp`、`Hold`）也可能进候选——所以候选**只排队、由人确认**，并提供"忽略"（持久化到忽略名单）。若误报过多，可把规则收紧成"多词短语"或"含数字/连字符"，或只对 ≥2 次出现的词入队。
36. **用户术语表的生效时机**（阶段 47 S5）：改动会立即反映到术语匹配与 AI 缓存键（签名变化 → 缓存不命中），**不需要重启**；但已经在跑的那条频道翻译请求不受影响（下一条生效）。若发现新术语"没生效"，先确认 `Configs/UserGlossary.json` 里确实写入了（设置页列表与文件都会显示）。
37. **"没有译文输出"先看这两条**（阶段 47 追加）：① 若提示"模型没有返回译文（输出被「最大输出 token」截断）"→ 去设置页把**最大输出 token** 调大（或设 0 交给服务端默认）；如果用的是**推理模型**（响应里只有思维链），请换普通对话模型。② MOTD/长文本请确认已在渠道页点过「开始」——「频道置顶信息」是加入频道时写进日志的，应用只会在**会话启动时补翻日志尾部 20 行**，更早的历史消息不会被翻（这是有意的成本控制）。
38. **翻译页输入框已改为多行**（阶段 47 追加）：回车=查询、**Shift+回车=换行**（`PreviewKeyDown` 实现，因为多行 `TextBox` 会在 `KeyDown` 类处理器里吞掉回车）。粘贴整段 MOTD/邮件正文不会再被折平。
39. **AI 翻译的对话历史是本地明文**（阶段 47 追加）：存在 `Configs/AiTranslationHistory.json`，上限 40 个对话 / 每对话 200 条记录（超出丢最久未更新的），**没有加密**（与 `settings.json`/`UserGlossary.json` 同等坦诚）；内容就是用户翻过的原文与译文，介意的话可以直接删这个文件（不会影响设置）。另外落盘粒度是"按对话整体 upsert"：页面与弹窗各开一个实例没问题，但在两个实例里同时编辑**同一个**对话属于异常用法（没有逐条合并）。
40. **上下文只作用于 AI 翻译页，而且是"对话级、不限条数"**（阶段 47 追加）：频道翻译是逐条独立翻译（每条消息自成一句，混入前一条反而容易串味），不消费对话上下文；`TranslationRequest.Context` 这条通路目前只有 AI 翻译页在用。对话级开关打开后会把此前**所有**成功记录都带上，**没有条数或 token 上限**——长对话（几十条、每条又是整段 MOTD）会让提示词迅速膨胀，遇到"上下文超限"一类的报错时，新建一个对话即可重置。另外"并发翻译同一对话"时，上下文只包含**已经成功**的更早记录。
41. **AI 翻译页可以并发请求**（阶段 47 追加）：等待期间允许继续发送，每条一个请求、各自一个取消令牌，没有队列与并发上限（点「停止」会取消**全部**在跑的请求）。代价是"连发 5 条"会同时产生 5 个请求——按次计费的服务商请自行留意；相册式的顺序由本地记录顺序保证，与返回先后无关。
42. **长文本翻译的两个真实上限**（阶段 47 追加，已按 DeepSeek 官方参数重做）：**输入上限（上下文窗口）与输出上限是两件事**。以 `deepseek-flash` 为例，上下文 1M token、最大输出 384K token —— 所以"几万字的原文"本身根本不是问题，瓶颈在**输出**（一次能写多少 token）。本项目的处理：分片阈值由设置决定（`TranslationPage.Ai.MaxChunkChars`，默认 12000 字符，0 = 不分片），`finish_reason=length` 时**自动从断点续写**（最多 3 次），续不完会明确提示"译文可能不完整"。默认 12000 字符的意义是"首段结果更快出现、单次失败损失更小"，并不是模型限制；真嫌慢/嫌请求多都可以在设置页调。
43. **思考模式默认关闭（仅 DeepSeek 系生效）**（阶段 47 追加）：`deepseek-flash` 的思考模式默认开启且 effort=high，翻译前会先写一大段思维链——又慢又贵，而且**思考模式下 `temperature` 无效**。所以设置页新增「思考模式」，默认 `off`；由于 `thinking`/`reasoning_effort` 是 DeepSeek 专有字段，本项目只在"模型名或地址含 deepseek"时才下发（`AiTranslationSettings.ResolveThinkingMode`），别的服务商保持 `auto`（不下发），避免它们不认识该参数直接 400。
44. **ZKB 模块整体未做实机核验**（阶段 53）：主页面 / 击杀流 / 实体统计（6 子页签）/ KB 详情均只完成编译与静态校验（图标名、主题画刷键、占位符替换），**没有在真实账号 + 真实 ZKB 服务上逐页点击**。首次使用请优先验证：① 切到 ZKB 页时首个"击杀流"标签是否正常渲染；② 点"连接"能否连上并收到 KB（`ZKBStreamConfig.json` 里的 `AutoConnect` 控制是否自动连）；③ 搜索一个角色名能否打开实体统计并出数。
45. **ZKB 设置已收敛为"单一入口"**（阶段 53 起，09-15 完成）：实时流的**全部**设置（自动连接 / 通知 / 通知阈值 / 列表上限 / 列表排序 + 6 组黑白名单）都在**击杀流页的设置弹窗**（`KillStreamSettingView`）里维护；原先"设置 → Zkillboard"子页（`ZKBSettingPage`）维护的是 `ZKBStreamConfig.Types / Systems / Regions / Characters / Corps / Alliances` 这组**没有任何代码读取**的旧字段（详见第 48 条），已连同该子页与 `SettingsPage` 里的分类项一并删除。`SettingsPage.BuildCategories` 留有注释说明"这里为什么没有 ZKB 分类"。
46. **托盘气泡没有"每条通知"的身份，点击路由是"最后一次 Show 获胜"**（阶段 53 第五轮补，原"通知点击不跳 KB"已修）：Win32 气泡/Win10+ 操作中心 toast 的点击回调（`TrayBalloonTipClicked`）不带"是哪条通知"的信息，因此 `NotificationService.Show` 提供可选 `onClick` **单槽位**——每次 Show 覆盖上一次，点击气泡时（UI 线程）触发。ZKB 击杀通知传入"打开对应 KB详情"（`KbNavigation.OpenKillmail`：导航 ZKB 页 → 页面 Drain 开详情标签 → `Navigation.Activate()` 前置/恢复主窗口）；频道预警的"点击停止报警"仍走全局 `NotificationClicked` 事件（点任何气泡都会顺带停声，无声音时无害）。已知边界：多条通知堆在操作中心时，点旧的那条执行的也是"最新一条"的动作——ZKB 击杀流高频时点开的是最近一次通知对应的 KB。
47. **ZKB 击杀流/统计的排序与筛选口径以 WinUI 为准**（阶段 53）：流列表排序沿用 `ZKBStreamConfig.SortWay`（上传时间/发生时间），实体统计的"危险系数/抱团概率"等数值直接取 ZKB 服务端返回，本地不做二次计算；"分类统计"按击杀数降序（WinUI 也是），"最高击杀"按 ZKB 返回顺序重排为击杀数降序。未与 WinUI 逐项对拍。
48. **"设置 → Zkillboard" 旧入口的 6 组过滤字段对击杀流完全无效**（阶段 53 过滤机制复核，实锤）：`ZKBSettingPage` 维护的 `ZKBStreamConfig.Types / Systems / Regions / Characters / Corps / Alliances`（逗号 ID 文本框）**没有任何代码读取**——`ZkbStreamFilter.FromConfig` 只读 6 个黑白名单集合（Common / Victim / Attacker × Exclusions / Inclusions），`EnsureRoleFiltersInitialized` 也只补这 6 个集合、不碰那 6 个 `HashSet`。后果：① 在旧入口填的过滤条件不生效（流照旧全放行）；② 旧入口也清不掉新入口配置的过滤。**第 45 条"两处入口互相覆盖"的说法需按此修正为"旧入口整体失效"**。**处置（阶段 53）**：已删除旧入口的 6 个过滤文本框及其读写代码（含 `Join`/`Parse` 辅助），该页只留 连接 / 通知 / 列表上限 / 通知阈值；`ZKBStreamConfig` 里那 6 个 `HashSet` 字段保留（WinUI 侧可能仍在用）。
49. **攻击者过滤在 `Attackers` 为空时被整组跳过**（阶段 53 复核；与 WinUI 的小分歧）：`ZkbStreamFilter.Pass` 中 `if (detail.Attackers is { Count: > 0 })` 才做攻击者三项检查，于是"攻击者列表为空 + 配置了攻击者包含项"的消息会被**放行**；WinUI 对空集合仍执行检查（包含项非空 → 不命中 → 丢弃）。实际 killmail 至少有一名攻击者，影响面极小，**处置（阶段 53）**：已去掉 `Attackers is { Count: > 0 }` 守卫、改为始终执行三项检查（空集合 + 包含项非空 → 判为不通过，与 WinUI 对齐）。
50. **ZKB 过滤机制语义与"互斥关系"备忘**（阶段 53 复核；实现与 WinUI 逐条对齐，差异仅第 48/49 条）：
    - **单个子条件（一组 × 一个类别）**：**排除优先**——同一 ID 同时在排除与包含列表时按排除处理；包含项非空则必须命中，为空则不限；排除项为空则不否决。
    - **组内三个类别之间是 AND**（通用：星系 / 舰船类型 / 星域；受害者：角色 / 军团 / 联盟），短路求值——任一子条件不通过即丢弃整条。特别注意**包含项是"按类别分别判空"**：只往通用包含项里放"舰船"时，星系/星域不受限制（不是"整个通用列表当成一个白名单"），因此"星系 A + 舰船 B 同时放进包含项"= 要求两者**同时**命中。
    - **三个组之间也是 AND**（通用 × 受害者 × 攻击者）。
    - **攻击者组是集合级判定**：任一攻击者命中排除即整条否决；包含项非空时要求**至少一个**攻击者命中（不是"每个攻击者都要命中"）。受害者组是**单值级**判定。
    - **`id <= 0`**（受害者无联盟、NPC 击杀、星系未被本地库收录）：在排除里永不命中（`FilterSet` 只收正整数）；在包含项非空时判为不通过。**这是 WPF 相对 WinUI 的有意修正**——WinUI 的 `Contains(category, id)` 先 `if (id <= 0) return false;`，导致"受害者无联盟"的击杀在包含项为空时也被一并丢弃。
    - **组与组之间没有互斥**：同一个 ID 出现在不同组的列表里互不影响（某角色可以是这条的攻击者、另一条的受害者）；UI 已按类别限制可选范围（通用限星系/星域/舰船，角色组限角色/军团/联盟），因此正常路径不会出现"类别放错列表"——但若手工改 JSON 放错，`AddByCat` / `AddRoles` 会**静默忽略**该项。
    - 取消/重建时机：过滤快照在配置变更时按脏标记重建（`ZkbStreamFilter` 不可变，中枢 `EnsureFilter` 每次唤醒重建），因此改过滤**无需断开重连**；隐患是 `HookConfig` 订阅的是集合**实例**，若将来有代码整体替换 `CommonExclusions` 等集合（公开 setter 允许），脏标记将不再触发。
51. **星图（阶段 62）相对 WinUI 的功能缺口**（均为有意暂缓，框架已预留挂点）：
    - **无 SOV（主权）着色 / SOV 分组**（WinUI 的 `SetDataToSOV` + `SOVGroup.json`）——需 ESI `Sovereignty.ListSovereigntyOfSystems` + 联盟名解析；
    - **无行星资源热力 / 行星资源清单页**（WinUI `SetDataToPlanetResourc` / `PlanetResourcListPage`）——数据在本地库（`SolarSystemResourcesService`），待接入着色模式与列表工具；
    - **无星系详情页**（WinUI `MapSystemDetailPage` 五页签：统计/设施升级/行星资源/天体/邻接）——WPF 只有信息卡的 击杀/通行/邻接；
    - **无一跳覆盖工具**（WinUI `OneJumpCover`：`CalOneJumpCover` + 圈叠加）——Core 算法现成，缺 UI；
    - **无跳桥（JumpBridge）层**：`JumpBridgeSetting.json` 读写与虚线绘制、导航走桥边权（`CalStargatePath` 的 `bridge` 参数已支持，传 `null` 即可）都未接；
    - **情报红圈没有舰船图标**（WinUI `IntelDrawer` 在节点旁画攻击者舰船图 + 计数）——当前只有红圈权重 + 情报流文字；接入需图片下载 + `SKBitmap` 解码缓存 + 空白探测摆放；
    - **ZKB 击杀不上图**（WinUI IntelTool 订阅 `ZKBStreamService` 叠加击杀）——可复用 WPF 的 `ZkbKillStreamHub`，尚未接；
    - **导航结果无燃料列**（不做旗舰型号/技能换算），"省钱优先"模式（`CalCapitalJumpPath` mode=1）未暴露；
    - **星域筛选**（按区域/安等批量 `Enable`）未做，只有"星域定位"与安等着色。
52. **星图 intel 的会话依赖**：情报模式开关要求**先在频道预警页启动至少一个角色的预警**（`ChannelIntelManager` 才有会话可切 `IgnoreJumps`）；无会话时开关仍可点但不起作用（面板显示提示文案）。另外情报**排除/包含关键词**在"开始情报"时从共用 `MapSettings.json` 读入、失焦时写回——WinUI 与 WPF 共用该文件，字段语义（`IdName.Name` 当关键词）为 WPF 侧约定。
53. **星图性能边界**：底图缓存键按 (zoom, offset) 精确匹配，**拖拽/滚轮/飞行动画期间每帧都重建底图**（与 WinUI 同为全量重绘，只是把静态场景隔离开了）；实测拖拽流畅。窗口铺满 4K + 高 DPI 时位图缓存 ~几十 MB，Unloaded 时释放。

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
- ~~ZKB 页本身仍是导航占位（工作区 ZKB 卡的"ZKB"按钮跳到该占位页）~~ → **阶段 53 已完成**：ZKB 主页面、击杀流、实体统计（6 子页签）、KB 详情全部落地，工作区卡片改为直接打开该角色的实体统计标签。
- 软件更新页"安装"仍交外部 Updater。
- **预览浮窗不跟随源窗口实时改变宽高比**（阶段 44）：窗口尺寸被锁定为游戏客户区比例，但只在启动/缩放/统一尺寸/恢复位置/布局变化时校正；游戏侧切全屏或改分辨率后，要等下一次这类时机才跟着变（未监听源窗口 `WM_SIZE`）。

### 环境/协作注意
- **代码调整不需要截图验证**：改动完成、构建通过后直接说明结果即可，由使用者自行查看界面效果。
- 设置文件与 WinUI 版**共用** `Configs/settings.json`：两版同时运行会互相覆盖，迁移完成后建议只保留 WPF 版。
- **目标框架现为 net8（LTS）**：`net8.0-windows10.0.19041`、`AssemblyName=TheGuideToTheNewEden`。
  原因见阶段 49（.NET 9/10 的 WPF 在未更新的 Win10 21H1 上启动即 fail-fast，**已实机验证回退后可正常运行**）。
  构建用 .NET 10 SDK 即可（不需要 8.x SDK），但目标机需要 8.0.x 桌面运行时。**别再把 TFM 写回 net10**。
- **两版产物同名 exe**：WinUI 与 WPF 的 `AssemblyName` 都是 `TheGuideToTheNewEden`，产物都是 `TheGuideToTheNewEden.exe`——
  不要放进同一目录；凡是"按 exe 路径拉起自己"的逻辑（协议注册、开机自启等）一律取**当前进程路径**，不要拼文件名（阶段 48）。
- **入口点是自定义的 `Program.Main`（`<StartupObject>`），不要删**：单实例判定/命令行转发必须在**创建 `Application` 之前**完成，
  否则转发进程会白加载一整套 WPF 资源字典，并在退出时触发 `App.OnExit` 的清理（含 `SettingsService.Save()` 用空字典覆盖 settings.json）。
  同理，**单实例状态挂在 `Program` 而不是 `App` 上**——`App` 继承 `Application`，访问它的静态成员会把
  `PresentationFramework`/`System.Xaml`/`WindowsBase` 一起加载（阶段 50 实测）。
- **欧服授权走本地回环，不是注册表协议**（阶段 51）：回调地址取自 `Configs/ESILicense.txt` 第 2 行，
  必须与 **EVE 开发者后台登记的 Callback URL 完全一致**（CCP 不支持通配端口），当前约定值
  `http://localhost:38471/callback/`。改任一处都要同步改另一处，否则授权页会直接报错。
  设置 → 测试 → 「回环回调」卡片可一键检测（校验配置 + 真占一次端口）与复制该地址。
- **不要再引入自定义 URL 协议/注册表**（阶段 52）：旧通道的代码、测试页「HKCR 协议」卡片、语言键、
  安装包的 `Protocol` 段**已全部删除**，没有任何"保留但停用"的入口可以接回去。
  单实例机制本身**没有删**（见上一条）：它现在只服务于"用户重复启动"，与授权无关。
- 运行时资源以链接方式引用 WinUI 项目的 `Resources/*`：**若删除 WinUI 项目，需改为复制或迁移资源**。
- **构建前必须先退出应用**：应用运行时锁定输出目录的 `*.dll`/`*.exe`（以及 `Resources/Database/*.db`），`Rebuild` 会以 `MSB3061` 警告跳过复制，导致"改了代码但运行的是旧程序集"（本次排查名称解析时踩到）。改动 Core 后若行为未变，先核对 `bin\...\TheGuideToTheNewEden.Core.dll` 的时间戳。
- **应用正在运行时想校验"能不能编译"**：用 `dotnet build … -p:OutDir=<临时目录>\` 把输出重定向出去即可（被锁的 `bin` 不参与），核验完删掉临时目录；**正式出包仍必须先退出应用**（阶段 46 追加改动实测：VS 调试会话在跑时普通 `build` 报 `MSB3027/MSB3021`）。
- 本机显示器为 2560x1440 @125%：未声明 DPI 感知的进程（如默认的 PowerShell）拿到的窗口坐标是按 1.25 缩放后的 **DIP**，直接当物理像素用会抓错区域或"看起来右侧被裁"；需要截图时先开启 PerMonitorV2 DPI 感知。
- **structure id 请走 `StructureService`**：`Core/Services/IDNameService` 的 ID 是 `int`，结构（structure）ID 约 1e12 会被**静默截断**并解析出错误名称。详见文末「结构（structure）ID 解析约定」。
- **改完 XAML 界面没变 → 先怀疑 BAML 陈旧**：并行构建/中断过的构建会让 `obj` 里的 `.baml` 落后于 `.xaml`，程序集里嵌旧标记。删 `obj` 重建即可（详见 §9 第 16 条）。
- **不要使用 `ProgressBar`**：WPF-UI 隐式样式下会栈溢出，用 `Controls/RatioBar.cs`（详见 §9 第 17 条）。
- **发布必须走 `dotnet publish` 才能验出来**：`dotnet build` **不校验**重复的发布输出文件，`dotnet publish` 会（`NETSDK1152`）。改动了"链接/复制到输出"的内容项后，除 build 外请至少跑一次 publish（详见 §9 第 25 条与阶段 45）。

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
9. **NuGet 目标框架陷阱**：包若只提供 `net10.0`/`net462` 这一档资产、而项目目标框架低于它，会**静默回退到 .NET Framework**（`NU1701`，可能运行期异常）。本项目实例：TFM 为 net8 时 `H.NotifyIcon.Wpf 2.4.1`（只有 `net462`/`net10.0-windows7.0`）就会回退，故锁 **2.3.x**（2.3.0 / 2.3.1 均带 `net8.0-windows7.0` 资产；本项目取 2.3.0，见阶段 49）。选包前先核对包内 `lib/` 的目标框架。
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

19. **LiveCharts / SkiaSharp 要求 TFM 带平台版本，且图表配色不会自动跟随主题**：`LiveChartsCore.SkiaSharpView.WPF 2.0.5` 依赖 `SkiaSharp.Views.WPF 3.119.0`，后者的资产只有 `net462` / `net8.0-windows10.0.19041`；项目若写 `net10.0-windows`（隐含 `TargetPlatformVersion=7.0`）就会回退到 .NET Framework 资产（`NU1701`）或报 `NU1202`，把 TFM 写成 **`net8.0-windows10.0.19041`**（现行）即可——阶段 25 先补上平台版本，阶段 49 把 .NET 大版本落到 net8，**这条结论与 .NET 大版本无关**。另外两点：
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

23. **`WM_SIZING` 可以改尺寸，`WM_WINDOWPOSCHANGING` 不行**：阶段 44 实测——在 `WM_WINDOWPOSCHANGING` 钩子里改写 `cx/cy`，`SetWindowPos` 会**同步**再发一次同样的消息，钩子又改一次，消息泵永远回不到上层，UI 表现为**完全卡死**（因为卡在尺寸事务里，连缩略图更新都会停住，看起来像是在 DWM 调用上挂掉，容易误判）。正确做法是改用 **`WM_SIZING`**：它只在用户拖边框时由系统发出，**只改写消息载荷、不调用 `SetWindowPos`**，因此没有重入路径。程序化改尺寸（启动、统一尺寸、恢复位置）则用**布局回调之后的一次性防抖定时器**，并加两道保护：① 重入标志挡住"校正 → 布局回调 → 再校正"的同栈重入；② 记录最近下发过的尺寸，**同一个尺寸绝不重复下发**。
    - 配套教训：**数值探针只能证明算术收敛，证明不了消息层可重入**。本次先用探针验证了"比例校正一轮到位"，仍然在真实窗口上死循环——算得对 ≠ 改得对。

24. **WPF-UI 的 `FluentWindow` 把标题栏画在客户区内部**：`GetClientRect` 返回的**就是整个窗口**，`窗口高 − 客户区高` **恒为 0**（实测 `窗口=1595x842 客户区=1595x842 非客户区高=0`）。任何"客户区里再扣掉标题栏"的计算都会漏扣约 40px；本次因此把预览窗口算宽约 77px，表现为**恒定的左右黑边**（与窗口大小无关，只由标题栏高度决定，最难察觉）。
    - 正确口径：**客户区尺寸用 Win32 `GetClientRect`**（布局值带 DIP↔像素取整误差，反算尺寸会每轮累积），**标题栏高度用元素的渲染高度**（`ui:TitleBar` / `StripBorder` 的 `ActualHeight × DPI`）。
    - 另：WPF-UI 用 `WindowChrome` 提供 `ResizeBorderThickness`，**`ResizeMode="NoResize"` 拦不住边框的调节箭头**（实测"设了 NoResize 仍能拖"）；要禁拖只能从 `WindowChrome` 下手。

25. **`dotnet publish` 的 `NETSDK1152`（重复输出文件）与"发布链"上的清理钩子**（阶段 45 实测）：
    - **`build` 与 `publish` 的校验不同**：`dotnet build` 允许两个内容项落到同一个目标相对路径（两份都拷，留下哪一份取决于复制顺序，**内容不确定**），只有 `dotnet publish` 会按 `ResolvedFileToPublish` 的 `RelativePath` 查重并报 `NETSDK1152`。**只跑 build 的开发流程发现不了这类问题。**
    - **`dotnet publish` 是两次 MSBuild 调用**（Build + Publish），publish 那次会**重新**计算 `ResolvedFileToPublish` 等列表。因此挂在 build 链上的 `AfterTargets`（如 `GetCopyToPublishDirectoryItems`、`GetCopyToOutputDirectoryItems`）**对发布不生效**；发布侧的清理必须挂在发布链上——在 `AfterTargets="ComputeResolvedFilesToPublishList"` 里改 `ResolvedFileToPublish`（该 target 之后、`_HandleFileConflictsForPublish` 之前），实测有效。
    - **项目引用传递来的内容项不在 `ContentWithTargetPath` 里**：它是 `GetCopyToPublishDirectoryItems` 内部经子工程 MSBuild 调用直接并进 `ResolvedFileToPublish` 的；想"从本项目内容项里删掉"对发布侧无效。
    - **MSBuild 路径比较要先规范化**：`$(MSBuildThisFileDirectory)..\X` 含**字面量 `..\`**，与项元数据 `FullPath`（规范化过的绝对路径）直接 `==` 恒为 false，症状是"target 确实执行了、项却没被删"。用 `$([System.IO.Path]::GetFullPath('...'))` 归一化后再比。

26. **WPF-UI `TitleBar` 的三个坑**（阶段 46 追加；数据来自一个直接实例化真实 `ToolWindow` 的独立探针）：
    - **`TrailingContent` 会拉伸到整条标题栏**：`TitleBarButton`（最小化/最大化/关闭）固定 **44×30 且 `VerticalAlignment=Top`**，而 `TrailingContent` 的 `ContentPresenter` 没设垂直对齐（默认 `Stretch`）→ 塞进去的自定义按钮被撑到标题栏高度（默认 48），图标居中后比三个系统按钮**低约 11 DIP**（探针实测 11.0，用户一眼就能看出"错位"）。修法：自定义按钮显式 `Width="44" Height="30" VerticalAlignment="Top"`（实测高度差 0.0）。
    - **标题栏里的自定义按钮本来就能收到点击**：`TitleBar` 自带 `WM_NCHITTEST` 钩子——鼠标落在 `Header` / `CenterContent` / `TrailingContent`（按元素矩形判断 `IsMouseOverElement`）范围内时**不返回 `HTCAPTION`**，交给正常命中测试（探针实测：置顶按钮处 `HTCLIENT(1)`、关闭按钮处 `HTCLOSE(20)`）。所以 `WindowChrome.IsHitTestVisibleInChrome` 对它是多余的；本项目仍补上作双保险，不影响行为。
    - **"被拥有的窗口"看起来永远置顶**：`Owner = 主窗口` 的窗口按 Windows 规则**恒在宿主之上**，于是"取消置顶"和"置顶"在应用内看起来一模一样（用户实测反馈："置顶按钮没有生效，一直都是置顶"——而 `Topmost` 与 `WS_EX_TOPMOST` 其实都正常翻转）。需要真正的置顶语义时**不要设 `Owner`**（本项目翻译弹窗改为不拥有 + 手动居中到宿主），或明确把"置顶"解释为"压在其他程序之上"。

27. **`TextBox.Text` 默认是 `TwoWay` 绑定，绑到只读属性会在渲染时抛异常**（阶段 47 追加）：为了让"结果显示的所有文字都能选中复制"（WPF 的 `TextBlock` 不能选文本），把频道译文列表的原文/译文换成了只读 `TextBox`，其中的 `OriginalPreview` 是**只读计算属性**（`=> …`）——`TextBox.Text` 的 `BindsTwoWayByDefault` 为 true，于是行模板一被实例化就抛 `XamlParseException: 无法对"…OriginalPreview"类型的只读属性进行 TwoWay 或 OneWayToSource 绑定`，列表直接渲染不出来。**`dotnet build` 依旧 0 错误**（绑定模式是运行时解析的），只有把控件真正渲染出来才会暴露——本次能抓到它，靠的正是那个把结果视图挂进离屏窗口渲染的 `ViewProbe`。
    - 修法：这类只读文本一律写 **`Text="{Binding X, Mode=OneWay}"`**（本项目 `TranslationPanelView` 早就这么写，新加的频道结果行漏了）。
    - 排查手段：全文扫"`TextBox` 的 `Text` 绑定里没写 `Mode=`"的地方——本项目实测 54 处 `TextBox.Text` 绑定里只有 4 处没写，而那 4 处（`InputText`/`SearchText` 这类输入框）绑的都是可写属性、属正常；**只读属性必须显式声明 `Mode=OneWay`**。

28. **`TabControl` 只把"当前页签"的内容放进可视树**（阶段 47 拆页签时踩到，两条后果都要记住）：
    - **子控件找不到不等于没写对**：探针第一次跑就在 `FindFirst<LocalTranslationPanelView>(view)` 上返回 `null`（那是没被选中的页签），必须先 `SelectedIndex = 1` 再查——排查界面结构时别被这一点误导。**`x:Name` 的元素实例是存在的**（XAML 里已构造），只是不在可视树里。
    - **`Loaded`/`Unloaded` 正好等价于"进入/离开这个页签"**：本项目据此把两个页签的 VM 生命周期挂在自己的 `Loaded → Init()` / `Unloaded → Deactivate()` 上，面板整体离开页面时再由宿主统一 `Dispose()`——**`Deactivate` 只退订语言事件、不取消在跑的请求**（切走再切回来还能看到结果），只有整页离开才取消（`Unloaded` 早于宿主 `Unloaded`，两者是"先轻后重"的关系）。
    - （该页签方案后来改成了左侧导航的两个子菜单，页签本身没有了；但上面两条结论对任何 `TabControl` 都成立。现在两个翻译页各自把 VM 的生命周期挂在 `Loaded → Init()` / `Unloaded → Dispose()` 上：它们是独立页面，离开即收尾、不需要"轻/重"两档。）

29. **`StreamReader.ReadLineAsync(token)` 在 HTTP 响应流上不会因为令牌取消而返回**（阶段 47 追加，当时的测量环境为 .NET 10 + `HttpClient`；现行 TFM 已回退 net8，**修复代码保留、照旧适用**）：流式读取里哪怕超时令牌已经触发，只要服务端"卡住不出字"，`ReadLineAsync(timeout.Token)` 就一直挂着——**超时设置形同虚设**（探针实测：2 秒超时 + 8 秒卡顿，最后 14.2 秒后"成功"返回）。正确写法是把读取包一层托管等待：
    ```csharp
    line = await reader.ReadLineAsync(CancellationToken.None).AsTask()
        .WaitAsync(TimeSpan.FromSeconds(idleSeconds), timeout.Token);
    ```
    `WaitAsync` 立刻响应超时/取消（超时抛 `TimeoutException`，取消抛 `OperationCanceledException`），被放弃的那次读在随后释放 response/stream 时自然作废。另一个配套结论：**流式超时应该是"空闲超时"**（每收到一段增量就 `CancelAfter` 重置），否则长文本生成（几分钟）会被"总时长超时"砍掉。
30. **视图 `Unloaded` 里不要取消在跑的请求**（阶段 47 追加，用户反馈"翻译显示已停止"的根因）：`UserControl.Unloaded` 在**切换导航、页面被 Frame 重新承载**等场景都会触发，而本项目的页面是 `NavigationCacheMode="Required"` 常驻缓存的——在 `Unloaded → VM.Dispose()` 里 `Cancel()` 会让"用户只是切了个页面"变成"正在跑的请求被静默取消"，界面上留下的就是一句"已停止"。**只退订事件、别动请求**；要中止请走界面上的显式「停止」，并在日志里记一行原因，避免下次还要靠猜。
31. **三类"编译期不报、只在运行时炸"的错误，写完界面必须逐一自查**（阶段 53 实机踩全了）：
    1. **`StaticResource` 键未定义 → 运行时 `XamlParseException`**（内部异常 `无法找到名为 "Xxx" 的资源。资源名称区分大小写。`）。典型来源：控件在 `UserControl.Resources` 里只注册了一部分转换器，别处却引用了另一个键；或批量替换"改了引用、没改定义"。**BAML 生成阶段不校验**。自查（列引用 vs 本文件 `x:Key` + App 级三字典）：
       ```powershell
       $t = Get-Content $f -Raw
       $local = [regex]::Matches($t,'x:Key="([^"]+)"') | % { $_.Groups[1].Value }
       [regex]::Matches($t,'StaticResource\s+([A-Za-z_]\w*)') | % { $_.Groups[1].Value }   # 逐个对照
       ```
    2. **图标名（`SymbolRegular`）非法 → 运行时 `XamlParseException`**。用了新图标名就要先验证（见第 32 条的做法）。
    3. **绑定路径写错或写"看起来对但其实不对的源" → 静默失败**（不报错、不崩溃、界面空白）。本项目最阴的一种是 **`RelativeSource AncestorType=UserControl` 的源是控件自身**，所以路径必须是**该控件上的 DP**；写成数据项上的属性名（如 `{Binding Name, RelativeSource=...UserControl}`）会全部落空——正确写法是经 DP 走一层：`{Binding Item.Name, RelativeSource=...}`。
    自查：把"以控件自身为源"的绑定路径列出来，确认每一个都是该控件声明的 DP：
    `[regex]::Matches($t, '\{Binding\s+([^,}]+),\s*RelativeSource=\{RelativeSource\s+AncestorType=UserControl\}')`
    第三类的同族问题：**VM 里漏发 `PropertyChanged`**。当一个 `Visibility` 绑在"由两个字段组合出来"的计算属性上（如 `IsContentVisible = 连接中 && !设置面板打开`），两个字段的 setter **都**要通知它，否则会出现"点了连接但列表不出现、提示不消失"。
32. **校验 WPF-UI 的图标名 / 资源键：别用反射（`.Assembly.LoadFrom` 会被安全策略拦），直接读 dll 字符串表**：
    ```powershell
    $p = "...\bin\Debug\net8.0-windows10.0.19041\Wpf.Ui.dll"
    $b = [IO.File]::ReadAllBytes($p)
    ([Text.Encoding]::ASCII.GetString($b)).Contains("Filter24")     # 图标名与多数资源键在 ASCII 串表
    ([Text.Encoding]::Unicode.GetString($b)).Contains("TextFillColorPrimaryBrush")  # 少数在 UTF-16 串表
    ```
    两种编码都试一遍即可（某一种返回 False 不代表存在）。
33. **共享 Core 的"本地化翻译器"不做 null 守卫，是这一类 NRE 的总源头**（阶段 53 第二轮）：`LocalDbService` 里 `TranXxx(Xxx item)` 这种"把翻译结果写回传入对象"的方法，参数**可能**是"主库查不到 id 时返回的 null"（调用方 `XxxService.Query(id)` 就是这么传的）。本文件里 `TranInvType(InvType)` / `TranInvTypeAsync` 有 `if (invType != null)` 守卫，而 `MapRegion` / `MapSolarSystem` / `StaStation` / `InvMarketGroup` / `InvGroup` 这 5 组漏了 → 只要有一个 id 在主库里不存在，取参数属性即 `NullReferenceException`。**两条约定**：① 写这类"回写参数对象"的方法，第一个判断必须是 `item == null` 就返回；② 服务层的 `Query(id)` 在行不存在时**不要**把 null 递给翻译器（既避免异常，也省一次本地库往返）。
34. **全局异常处理器只弹框不记日志 = 自己把眼睛蒙上**（阶段 53 第二轮）：`App.OnDispatcherUnhandledException` 原先只有 `MessageBox.Show(e.Exception.Message)`，于是"用户看得到异常、日志里什么都没有"，而弹框只给 `Message`（**不含堆栈**），排查时等于没有信息。正确做法：`Core.Log.Error(e.Exception)` **加上**弹框显示 `e.Exception.ToString()`（含类型与完整堆栈）；另外补挂 `AppDomain.CurrentDomain.UnhandledException` 与 `TaskScheduler.UnobservedTaskException` 覆盖后台线程（本项目大量工作在后台线程：ZKB 流消费、分页取数、频道日志轮询）。
35. **"界面显示已连接但永远收不到数据"要先查消费循环的异常边界**（阶段 53 第二轮）：`ZkbKillStreamHub` 的消费者是 `while (await WaitToReadAsync) { while (TryRead) { ... } }`，若每条的处理逻辑（富化/过滤/回调）不在**内层** try 里，单条异常会冲出内层循环 → 被外层 catch 接住 → **消费者任务直接结束**，而 `IsRunning` 仍是 true。凡"长驻消费循环"，**逐条隔离**（每条的 try/catch 只丢弃该条）比"整体包一层"更稳。
36. **`DepthClone<T>` 跨模型拷贝会"拷出空壳"——命名策略不同的两个模型之间不能这么抄**（阶段 53 第三轮，ZKB 数据全空的根因）：`DepthClone` 是 `SerializeObject(obj)` → `DeserializeObject<T>(json)` 的 JSON 往返，**两边必须用同一套命名规则**。`EVEStandard` 的模型靠序列化设置里的 **snake_case 命名策略**解析（属性无 `[JsonProperty]`），而 `ZKB.NET` 的模型用显式 `[JsonProperty("killmail_id")]` —— 从前者拷到后者，键名全对不上，得到一个**字段全为默认值**的对象（`KillmailId=0`/`KillmailTime=default`），而且**不报错、不抛异常**，下游只能靠"id 匹配不上/显示全空"这类间接症状发现。两条教训：① 跨命名空间的模型转换不要用 JSON 往返，老老实实手写映射或用统一命名策略；② "接口响应里已经带了完整数据时，就不要再逐个去另一服务换取再拷贝"——`/kills/` 现在直接返回完整 killmail，`ZKB.GetKillmailDetailsAsync` 直取后本地富化即可（每页省 200 次 ESI 往返）。
37. **Core 的本地库（SQLite）查询不是并发安全的，批量富化必须串行**（阶段 53 第三轮实测）：`KBHelpers` 注释原文"使用一个线程来执行查询KB具体信息，避免ESI查名字时数据库冲突"。实测同一批 50 条的富化，6 并发丢 16 条（单条异常/数据缺失被静默吞掉），串行 50/50。凡要并发调用 `IDNameService` / `MapSolarSystemService` / `InvTypeService` 等 Core DB 服务的，先确认线程安全性，默认**串行**。
38. **zkillboard 有 IP 限流，"同 URL 直取+重试+兜底三连发"会把自己打死**（阶段 53 第三轮）：排障时连续探测后发现 `/kills/killID/<id>/` 从 200 条变成空——是限流，不是接口变更。日常使用频率不会触发，但写代码时注意：**不要让重试与兜底打同一个 URL**（本项目兜底走的是另一条数据通路），排障时探测间隔拉长、或换 IP 验证。
39. **可多开并发的页面不要用全局等待遮罩，等待态必须页面私有**（阶段 53 第四轮）：实体统计页一类的"可同时打开 N 份、各自异步加载"的页面，若用 `PageNotifyService.ShowWaiting`（全局 `WaitingOverlay`），会误遮其他副本，且"先结束的页 `HideWaiting`"会把别的页还在转的遮罩藏掉（全局遮罩无计数）。模式：VM 暴露 `IsBusy`（**可重入计数**，同页并发多个操作全结束才隐藏）+ `BusyText`，页面内嵌局部遮罩（`SpinnerIcon` 自绘旋转图标 + 半透明背景，视觉对齐全局 `WaitingOverlay`，但只盖住本页并拦截点击）；错误提示仍走右下角非阻塞通知。全局遮罩留给"应用级单实例操作"（倒货取数、估价等）。另：局部旋转指示**不要用 `ui:ProgressRing`**（WPF-UI 隐式样式栈溢出史，见第 17/22 条），用 `Controls/SpinnerIcon`。40. **`ui:FluentWindow` 也能做真透明（客户区逐像素透出后面的窗口），但四件事缺一不可**（阶段 54）：① **`AllowsTransparency` 用不了**（与 FluentWindow 冲突，实测抛 `InvalidOperationException`），只能走 Win32；② **`DwmExtendFrameIntoClientArea(hwnd, 四个 -1)` 才是开关**——把 DWM 玻璃框铺满客户区，客户区里的 alpha 才会被尊重；**只调 `DwmEnableBlurBehindWindow`（空模糊区域）没有任何视觉效果**，而且它的 HRESULT 还是 0（看起来"成功"），极易误判；③ **窗口背景必须真的透明**：WPF-UI 会把 `FluentWindow.Background` 设成主题底色（浅色主题=白），XAML 里的 `Background="Transparent"` 会被它覆盖，必须在 `OnSourceInitialized`/`Start`/`ShowWindow` 里重新按回来，`CompositionTarget.BackgroundColor` 也要一并置透明；④ **每块要实色的区域都得自己画底色**，而且**透明的子元素会透出父元素的底色**——给"洞"外面套一个填了高亮色的父元素，整幅画面会被染成高亮色（实测画面泛绿），所以边框要用 `BorderBrush`/`BorderThickness` 画，不能用"背景色 + Padding"。
41. **"透明/半透明"类问题的验收必须用高对比背景做判据，不能只看"画面是不是变淡了"**（阶段 54）：本次"透明度没生效"的第一版修复看起来对（画面变淡、数值能存），实际画面区被一层实色填成了"泛白"——**透出白色窗口与透出任何东西长得都一样**，肉眼分不出"透明"与"实色填充"。可靠判据是**在目标窗口后面放一个红/蓝/黑/绿四色测试窗**，看颜色能不能逐像素透出来（本次正是靠它定位到缺 `DwmExtendFrameIntoClientArea`）。同族教训：涉及"透明度/遮罩/置顶叠加"的需求，先确认它要透出的是"窗口自己的底色"还是"窗口后面的内容"——前者调 opacity 就够，后者必须真的开洞。42. **在 WPF / WPF-UI 窗口上"放开最小尺寸"，光删 `MinWidth/MinHeight` 不够**（阶段 55）：`WM_GETMINMAXINFO` 会被 WPF / WPF-UI 的后续处理按回"窗口当前尺寸"——实测删掉 `MinWidth/MinHeight` 后窗口仍卡在当前尺寸，`SetWindowPos` 返回 True 但矩形不变、拖边框只能变大；在 `HwndSource` 钩子里写入 1×1 也会被覆盖。正确做法是**在这条消息上自己接管**：改写 `ptMinTrackSize` 后设 `handled = true`（其余字段保持系统预填），此后 1×1 才会被真正采纳（实测可把窗口拖到 175×101 甚至更小）。排查这类"设了没生效"的 Win32 消息问题，最有效的手段是**从另一个进程 `SendMessage(hwnd, WM_GETMINMAXINFO, 0, 缓冲区)` 读回窗口的答复**——它能直接指出"谁在把值按回去"；同族的还有 `GetWindowRect`/`IsWindowVisible` 之类的旁证查询。43. **"比例/尺寸校正"里不要做"同尺寸去重"，同一几何量也只用一套口径**（阶段 56）：
  ① 去重（"算出的目标 == 上次下发的就跳过"）看着像防自循环，实际会把**真实需要修的情况一起挡掉**——
  窗口完全可能在两次校正之间被别的路径改偏几像素（拖动收尾、恢复位置、源比例变化），此时目标与上次相同、窗口已经不对，
  一去重就永远不修（表现为"画面区一直留一条透明边"）。防自循环用"目标与当前尺寸的容差判断"就够了：下发后窗口等于目标，下一轮必落在容差内。
  ② 同一个几何量（这里是"画面区"）**只能有一套口径**：缩略图摆放原来取 WPF 布局（`TransformToAncestor`/`ActualWidth`），
  比例校正取 Win32（`GetWindowRect/GetClientRect`）——窗口是用 `SetWindowPos` 改尺寸的，实测 **WPF 布局会滞后于实际窗口**
  （诊断日志抓到过"布局 460×320 DIP、实际 485×110"），两套口径一旦不同步就各算一套、画面留边。
  现在统一成"客户区用 Win32 + 标题栏高度用元素 + 留白用同源函数导出"，并把**画面区域算在哪、缩略图就摆在哪**钉在同一组函数上。44. **WPF-UI 的 `FluentWindow` 样式自带最小尺寸（实测 460×320 DIP），它会卡住 WPF 的布局**（阶段 57）：
  窗口用 Win32 缩小时，WPF 的 `Width`/`Height` 被这个 Min 抬回去，布局（以及 WPF 画的高亮边框）就一直停在 ≥ 该值——
  表现为"高亮边框右侧/下侧画到窗口外（看着只有左跟上两个边框）"与"布局多出来的那圈是透明的洞（浅色背景下就是白边）"。
  要让窗口能自由缩小，必须显式 `MinWidth = 0; MinHeight = 0;`（**本地值才能压过样式**；`ReadLocalValue` 返回 Unset 就是样式给的）。
  另一条同族经验：**布局尺寸与 Win32 尺寸脱节时，只设 `Width`/`Height` 可能无效**——窗口本身已是目标尺寸，
  WPF 会判成"没变化"而跳过重排，必须补发一条 `WM_SIZE`（`SendMessage(hwnd, WM_SIZE, 0, MAKELPARAM(w,h))`）
  才能逼它按真实客户区重新布局。诊断这类问题的有效手段：把 `MinWidth/MinHeight`、`ActualWidth/ActualHeight`、
  `Root/子元素` 的渲染尺寸、以及 Win32 客户区**一起打进日志**，两边一比就能看出是"谁比谁大"。45. **WPF 的 `Border.CornerRadius` 不裁剪子元素，圆头像必须用 `Image.Clip`**（阶段 53 第五轮）：
  与 UWP/WinUI 不同，WPF `Border` 的 `CornerRadius` 只影响**自身**背景/边框的绘制，里面的 `Image`/`Grid` 仍是矩形——
  `<Border CornerRadius="999"><Image/></Border>` 表面上"该圆了"，实际头像依旧是方块（编译期无任何提示）。
  两条正路：① `Image.Clip` + `EllipseGeometry`（需要定尺寸，中心/半径不能随尺寸自适应，写死即可）；② `Ellipse` + `ImageBrush`。
  `Grid` 想整体裁圆则用 `Clip` 绑定 `SizeChanged` 重算几何，或外包 `Border` + `OpacityMask`——别指望 `CornerRadius` 代劳。
46. **`ControlTemplate` 里的 `x:Name` 在模板命名空间，页面 code-behind 拿不到对应字段**（阶段 53 第五轮，TabControl 头行预留列）：自定义 `TabControl` 头行模板（左标签区 + 右端 40px 预留列给搜索按钮）时，模板内的 `x:Name` **编译照过**但页面类不生成对应字段——模板实例化属于另一个 namescope，`InitializeComponent` 只为页面级 XAML 生成字段。做法：模板只放非交互骨架（`TabPanel`/`Border`/`PART_SelectedContentHost`），要挂事件/程序化访问的元素（按钮、Flyout、输入框）放**页面级**（模板之外）叠加覆盖；确需取模板内元素用 `template.FindName(name, templatedParent)`。镜像库默认模板时逐项对照来源（WPF-UI 4.3.0 `TabControl.xaml`：`TabPanel` `Panel.ZIndex=1` 压住内容区顶边线、内容 `Border` `BorderThickness="0,1,0,0"` + `CornerRadius="0,4,4,4"`、`PART_SelectedContentHost` `ContentSource="SelectedContent"`；TabItem `MinHeight=36` 可用来对齐覆盖层高度）。
47. **zkillboard 的 websocket 击杀流先于 API 可查**（阶段 53 修复"点通知查询失败"时日志实锤）：流里刚广播出来的击杀，`/kills/killID/<id>/` 直取（含 500ms 重试）在最初几秒内是空，ESI 兜底同样空、还可能因空壳数据带无效 ID 炸出 `Ensure all IDs are valid before resolving`。**任何"手里已有完整富化数据"的入口（通知点击、流列表行点击、统计页行点击）都应携带现成 `KBItemInfo` 直开**（`KbNavigation.OpenKillmail(KBItemInfo)`），只把"按 ID 查询"留给手里没有数据的入口（如"最高击杀"卡片——均为历史击杀，API 必可查）。
48. **`BitmapImage.UriSource` 会同步下载，列表里每行一张图就是"卡一会儿"**（阶段 53 性能修复实锤）：图像绑定都在 **UI 线程**求值，而 `UriSource` 走 WIC 的**按需下载**路径——首次 `EndInit()` 当场同步下载。`CacheOption=OnLoad` 配 `UriSource` 同样是同步下载（只是时机提前），所以"改 OnLoad 就不卡了"是**错的**（正确做法是连字节一起自己抓，见阶段 11 的同族教训）。更要命的是**转换器返回 null 后绑定不会自动重算**，"先返回 null、下载完再给图"这条路走不通。正解：`Image` 用附加属性（本项目 `ctl:AsyncImage.Source/IdName/TypeId`），后台线程抓字节 → 同线程解码 + `Freeze()` → 回 UI 线程**直写 `Image.Source`**（绕开绑定刷新限制），并按 URL 做进程内缓存（失败的 URL 也要缓存 null，否则每次重试都打网络）；下载完成时务必校验容器当前地址（列表虚拟化会复用容器，不校验会串图）。
49. **外层 `ScrollViewer` 会废掉 `DataGrid` 的行虚拟化，表现为"点开先卡一下才出页面"**（阶段 53 性能修复二实锤）：把表格套进 `ScrollViewer`（本意是"列固定宽 + 表头随表滚"），外层会以**无限高度**测量表格 → 表格自身的滚动视口等于全部内容 → 虚拟化失效、**一次性实例化所有行**（每行还有头像与多列 `DataTemplate`）。击杀者上百的 KB 详情页一打开就卡，且卡在标签首次布局时，观感像"先把数据加载完才跳页"。两条要点：① 要虚拟化就别套外层 `ScrollViewer`，让表格自滚（`ScrollViewer.Vertical/HorizontalScrollBarVisibility=Auto` + `CanContentScroll=True`），列宽总量大于视口时把横向滚动交给表格自身；② **页面级 `ScrollViewer.CanContentScroll="False"` 是另一件事**（那只是让 WPF-UI 别给整个页面套滚动壳、页面自管滚动），别把两者混为一谈。同族：页内加载态要绑 `IsLoading` 之类的标志，而**该属性必须发通知**——普通自动属性 `{ get; private set; }` 绑上去永远不显示（静默失效，极易误判成"遮罩没写对"）。
50. **构建报 `MSB3027` / `MSB3021` 不是编译错误，是程序正在运行锁住了 exe**（阶段 53 实机验收到）：`dotnet build` 最后一步把 apphost → `TheGuideToTheNewEden.exe` 拷进输出目录，若 app 还在运行（能查到同名进程），这步会重试 10 次后失败——**此时编译（含 XAML 标记编译）其实已经通过**，不要回头去改代码。判别方法：错误里只有 `MSB3027`/`MSB3021`、没有任何 `CS`/XAML 错误；只验证编译可用 `dotnet build -t:Compile`（不拷贝输出）。**另外务必核对产物时间戳**：若 `bin\Debug\<tfm>\TheGuideToTheNewEden.dll` 早于源码改动时间，说明新代码**没进 bin**，必须关掉程序重新构建，否则用户测的还是旧版本。被锁的文件可能是 `Core.dll`/`Core.pdb`（Visual Studio + 运行中的 app 都会持有），关程序（VS 启动的则停止调试）即可。
51. **把"逐条富化"改写成"批量富化"时，必须逐字段对齐原实现**（阶段 53 实机回归，代价是一次用户可见的"数据消失"）：为性能把 `KBHelpers.CreateKBItemInfo` 换成自写的批量组装后，只补了名称/星系/船型，**漏掉 `Region` 与 `Group`**——这两者要二次关联（`regionId` 藏在星系里、`groupId` 藏在船型里），于是列表星域列与详情副标题静默少了星域，**编译、告警、日志全无信号**。三条纪律：① 改写前把原方法体逐行列出、逐项打勾（尤其"顺手补的关联字段"）；② 需要二次关联的字段要在批量阶段**再收集一轮 ID** 批次查询，不能因为"麻烦"省略；③ 这类缺失没有任何自动化提示，只能靠界面比对，改完必须实机看一遍每列每行。
52. **`TabControl` 只有一个 items host，"钉住某个标签"要绕道做**（阶段 53 钉住"击杀流"标签头）：想让首个标签固定在左端、其余标签在它右侧滚动，不能靠"把标签分到两个 `IsItemsHost` 面板"（`TabControl` 只认一个 items host）。可行做法是三件套：① 把该标签的头部**收敛为零宽**（`Header=null` + `Margin/Padding=0` + `MinWidth=0` + `Width=0`）——它照常承载内容与选中态，只是不在标签行占位，零宽也不影响 `SelectedIndex=0` 选中与 `BringIntoView`；② 用**页面级覆盖层**重绘一个"标签头"（高度与标签行一致、`HorizontalAlignment=Left`），点击时 `SelectedIndex = 0`；③ 代码在 `Loaded` 与该覆盖层 `SizeChanged` 时把模板里的标签容器 `Margin.Left` 设为覆盖层实际宽度，使其让位（**不要写死宽度**，标题会随语言变宽变窄）。
    两个易踩的细节：**选中态绑定 `{Binding SelectedIndex, ElementName=Tabs}` 只能写在 `Style.Triggers` 里**——写在 `ControlTemplate.Triggers` 里会按模板命名空间解析、找不到页面级 `Tabs`（静默失效）；同理 `Template.FindName` 取模板元素时优先取 `FrameworkElement`（如 `DockPanel`），对 `ColumnDefinition` 这类非 `FrameworkElement` 的命名元素不可靠。
53. **"零宽标签"会把标签行的高度一起塌掉**（阶段 53 钉住标签头的实机回归，用户截图实证）：把某个 `TabItem` 做成 `Header=null` + `Margin/Padding=0` + `MinWidth=0` + `Width=0` 后，**它的高度也会跟着塌**——标签行若是 `Auto` 行，整行高度变 0，内容区便从页面顶端开始；此时任何"固定高度 + `VerticalAlignment=Top`"的页面级覆盖层（钉住头部、搜索框）都会**压到内容上**（现象就是"头部区域变小、控件跑到内容里"）。修法：给该 TabItem **显式 `Height`（= 标签行高度，本项目 36）**，并给模板里承载标签行的 Grid **写死 `Height="36"`** 双保险。排查提示：这类"覆盖层压内容"的问题先量一下承载行的实际高度（`ActualHeight`），别急着调覆盖层的定位。
54. **横向 `StackPanel` 里的 `TextWrapping="Wrap"` 完全无效**（阶段 53 实机截图实证："过滤说明显示不全、不会自动换行"）：横向 `StackPanel` 以**无限宽度**测量子元素，内部 `TextBlock` 便按单行布局，超出部分被父容器（如 `Border`）裁切——`Wrap` 设置了也没用。要让说明文字换行，承载块用 **`Grid`（图标 `Auto` 列 + 文字 `*` 列）** 或 `DockPanel` 限宽。同族坑：本地化资源串（`sys:String`）里用 `&#10;` 写的换行**不保证生效**，长文案应写成不依赖换行的整段（编号/条目之间用"；"分隔），由宽度决定折行。45. **`{Binding Converter=…}`（绑整个对象）不会随对象属性的变化刷新**（阶段 60）：WPF 对"路径为 `.`"的绑定
  只在源对象发出**空名/`null` 名**的 `PropertyChanged` 时才重新求值；我们改的是具体属性名（如 `WindowTitle`、`Setting`），
  于是**转换器再也不会重跑**——表现为"某一列永远停在初始值"（本次是进程列表的角色名列停在 `EVE`，
  而绑具体路径的标题列正常刷新，很容易误判成"列表不刷新"）。
  要让它跟着变，就把**会变的输入**分别绑上（`MultiBinding`）或绑到具体路径（`{Binding WindowTitle, Converter=…}`），
  不要用"整对象 + 转换器"。


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

### 多开模块（阶段 43；预览窗口用 DWM 缩略图，无 IPC）
```
Services/GamePreview/GameClientService.cs        客户端发现 + 前台激活（3 种策略）
Services/GamePreview/ForegroundWatcher.cs        前台窗口轮询（UI 线程）
Services/GamePreview/GlobalHotkeyService.cs      全局快捷键（RegisterHotKey + WM_HOTKEY）
Services/GamePreview/PreviewWindowManager.cs     预览窗口创建/销毁/批量操作 + 每项快捷键（统一尺寸按首个窗口的源比例定高度）
Services/GamePreview/PreviewLayoutCalculator.cs  自动排列纯计算
Services/GamePreview/PreviewGeometry.cs          画面几何：FitAspect（按源比例居中留边，比例一致则铺满）/ TryGetSourceSize / 缩放尺寸
Services/GamePreview/SelectionPreview.cs         页面内选中进程实时画面（DWM）
Services/GamePreview/PreviewColorHelper.cs       Color ↔ 画刷 / #RRGGBB
Services/GamePreview/IPreviewWindow.cs           预览载体契约（窗口型 / 无窗口型）
Services/GamePreview/HeadlessPreviewWindow.cs    不显示窗口时的空实现（仅热键激活）
Helpers/Interop/DwmThumbnail.cs                  DWM 缩略图（注册/目标矩形/不透明度/可见性/源尺寸/换绑）
Helpers/Interop/NativeMethods.cs                 Win32 互操作（窗口位置尺寸、扩展样式、可透明客户区，阶段 54）
Services/Settings/GamePreviewSettingService.cs   Configs/GamePreviewSetting.json
ViewModels/GamePreview/GamePreviewViewModel.cs   进程列表/排序/分组/快捷键分发
Views/Windows/GamePreviewWindow.xaml(.cs)        预览窗口（两种样式共用一个窗口类；尺寸锁定游戏客户区比例，阶段 44；
                                                 客户区"可透明"、画面为洞、边框/标题栏实色，阶段 54）
Views/Windows/PreviewNameOverlayWindow.xaml(.cs) 无标题栏样式下左上角的角色名叠加窗（独立窗口，DWM 缩略图盖不住它）
Views/Pages/GamePreviewPage.xaml(.cs)            三列页面（进程列表 / 设置 / 预览）
Converters/{ColorHex,ColorToBrush,ProcessDisplayName,StringSet}Converter.cs  多开用转换器
```

### 星图模块（阶段 62：SkiaSharp 重设计渲染，占位页已替换）
```
Views/UserControls/Map/StarMapCanvas.cs   核心画布（SKElement）：深空背景/星尘/星门连线/发光节点/LOD 标签、
                                          底图缓存 + 发光精灵缓存、缩放平移/命中/悬停/选中、情报红圈、
                                          角色标记、航线折线、定位飞行与涟漪高亮；MapColorMode/MapSystemNode/覆盖层模型同文件
Services/Map/MapSettingService.cs         Configs/MapSettings.json（Core MapConfig，与 WinUI 共用）
Services/Map/ChannelIntelManager.cs       运行中预警会话聚合 + IgnoreJumps 开关 + 情报事件聚合（对齐 WinUI 同名单例）
Services/Map/CharacterLocationService.cs  授权角色 ESI 位置轮询（"显示角色"）
ViewModels/Map/MapPageViewModel.cs        数据装载/统计/搜索/情报流/导航编排
Views/Pages/MapPage.xaml(.cs)             星图页（顶栏搜索/星域/着色/角色/情报/导航 + HUD 浮层；占位页已替换）
Converters/NullToVisibilityConverter.cs   null → Collapsed（信息卡显隐）
```

### 主窗口
```
Views/MainWindow.xaml(.cs)   左菜单 + 内容区、标题栏图标、窗口位置持久化、托盘
Helpers/AppVersion.cs        版本号单一来源（主窗口标题 + 软件更新页共用）
```

### 翻译模块（阶段 46：本地 SDE 数据库专有名词互译，**不用有道 API**）
```
Core/Services/DB/TranslationDbService.cs        本地库翻译：原文库模糊匹配 + 译文库批量为对照（每类名词 2 次查询，完全匹配优先，译文缺失返回 null）
Core/Services/DB/DBService.cs                  新增 MainDbReady / LocalDbReady（外部判断数据库是否载入，避免 NullReferenceException）
Services/Translation/ITranslationProvider.cs   翻译源契约 + 方向(源/目标语言)/请求/结果模型（结果复用 Core TranslationItem）
Services/Translation/TextCollapse.cs          文本行数预算与"前 N 行预览"（AI 气泡与频道列表共用）
Services/Translation/ChatTranslationOptions.cs 频道翻译的按角色开关快照（随消息入队，避免多角色互相覆盖）
Services/Translation/TranslationLanguages.cs    语言代码表（auto + 中英日韩俄德法西）与中英成对判定
Services/Translation/TranslationLanguageHelper.cs  按正文脚本判定源语言 / 方向解析 / 语言代码→本地化键
Services/Translation/LocalDbTranslationProvider.cs  本地数据库源（自动方向，查不到再试另一侧）
Services/Translation/TranslationService.cs     已注册翻译源 + 按 Key 取源（接入在线翻译只需在此多注册一个）
Services/Settings/TranslationSettingService.cs     翻译设置（AI/本地各一套 From-To + AI 全套参数，共用 settings.json）
ViewModels/Translation/LocalTranslationViewModel.cs  「本地词库」页 VM：输入即查（350ms 防抖）/方向/结果/复制/语言切换重建
ViewModels/Translation/AiChatTranslationViewModel.cs 「AI 翻译」页 VM：配置门控 / 多对话 / 对话记录 / 对话级上下文 / 并发翻译与逐条取消
ViewModels/Translation/AiChatSessionViewModel.cs     一个翻译对话（标题/预览/记录集合/新建与清空/**对话级上下文开关**）
ViewModels/Translation/AiChatTurnViewModel.cs        对话里的一条记录（原文气泡三行折叠+展开、译文状态、模型信息）
ViewModels/Translation/TranslationMatchViewModel.cs  本地库一条结果（本地化类型与语言标签 + 描述 + 异步物品图标）
Services/Translation/AiTranslationHistoryService.cs  AI 对话历史落盘（Configs/AiTranslationHistory.json，按对话 upsert，40×200 上限）
Controls/RichTextPresenter.cs                        只读富文本展示（Text → FlowDocument，可任意拖选复制；对话气泡与本地库详情共用）
Controls/SpinnerIcon.xaml(.cs)                       行内加载指示（旋转图标，IsActive=false 时自身隐藏；供"某条译文正在翻"用）
Views/UserControls/AiTranslationChatView.xaml(.cs)   AI 页：左对话列表 + 右对话记录（原文/译文气泡 + 每条"加入上下文"）+ 底部输入
Views/UserControls/LocalTranslationPanelView.xaml(.cs) 本地词库页：左匹配列表右译文详情
Views/UserControls/TranslationPopup.cs               两个翻译页的「弹窗」宿主（各一个单例 ToolWindow，弹窗内视图独立实例）
Views/Pages/AiTranslationPage.xaml(.cs)              AI 翻译页（导航「翻译 → AI 翻译」，只承载对话视图）
Views/Pages/LocalTranslationPage.xaml(.cs)           本地词库页（导航「翻译 → 本地词库」，只承载面板）
Core/Services/DB/TranslationDbService.cs            阶段 47 追加 ExtractGlossary()：抽全量中英术语对（57,010 条 / 241ms）
Services/Translation/Llm/ChatModels.cs              协议层模型：消息/端点/请求/结果/用量/流式状态/ChatException
Services/Translation/Llm/IChatProtocol.cs           协议适配契约（BuildRequest/ParseResponse/ParseStreamLine/DescribeError）
Services/Translation/Llm/OpenAiChatProtocol.cs      OpenAI 兼容（含地址归一化）+ AzureOpenAiChatProtocol（部署名/api-key/api-version）
Services/Translation/Llm/AnthropicChatProtocol.cs   Anthropic Messages（顶层 system / max_tokens 必填 / 事件流）
Services/Translation/Llm/GeminiChatProtocol.cs      Google Gemini（systemInstruction / contents / alt=sse）
Services/Translation/Llm/ChatProtocolFactory.cs     协议注册表（openai / azure-openai / anthropic / gemini）
Services/Translation/Llm/ThinkingModes.cs           思考模式取值（auto/off/low/high；DeepSeek 专有，默认关闭）
Services/Translation/Llm/ChatClient.cs              HTTP 调用：重试退避、每请求超时、流式读取、错误归一化、测试连接
Services/Translation/GlossaryService.cs             术语库：抽取+索引+命中（英文词起点子串 / 中文 CJK 滑窗）+打分去重+注入块
Services/Translation/AiTranslationProvider.cs       语义层：提示词+术语注入+精确短路+缓存+清理+术语后校验
Services/Translation/TranslationCache.cs            结果缓存（一键一文件，键含术语库签名与提示词）
Services/Translation/AiTranslationSettings.cs       AI 设置模型（协议/地址/Key/模型/温度/token/超时/术语/上下文条数/流式/缓存/提示词）
Views/Pages/Settings/AiTranslationSettingPage.xaml(.cs)  设置子页「AI 翻译」（含测试连接、重新载入术语库、清空缓存）
Services/Translation/ChatMarkupProtector.cs         聊天标记预清洗（<br>→换行、其余 <...> 标签删除；ToPlainText/ToSingleLine）
Services/Translation/ChatTranslationEngine.cs        频道翻译引擎：单条队列串行、预清洗、跳过规则、失败去重（可注入替身）
Services/Settings/ChannelTranslationSettingService.cs 每角色的频道翻译设置（Configs/ChannelTranslationSettings.json，与 WinUI 同格式）
Services/ChannelIntel/ChannelTranslationSession.cs   每角色会话：勾选频道 → Core 观察者 → 入队翻译
ViewModels/Channel/ChannelTranslationViewModel.cs    频道翻译页 VM（角色/频道/参数/上下文/方向/未配置门控/实时译文集合）
ViewModels/Channel/ChannelTranslationItemViewModel.cs 频道译文一条（纯文本预览、译文 5 行折叠、meta 与术语展示）
Views/UserControls/ChannelTranslationResultView.xaml(.cs) 实时译文视图（页面与弹窗共用；右键复制原文/译文）
Views/Pages/ChannelTranslationPage.xaml(.cs)         频道翻译页（原占位页 ChannelTranslationPage.cs 已删除；MainWindow 注册不变）
Services/Translation/UserGlossaryService.cs         用户术语表（Configs/UserGlossary.json）：注入提示词优先级最高 / 覆盖本地库源 / 参与缓存签名
Services/Translation/GlossaryCandidateService.cs    AI 新词候选（Configs/GlossaryCandidates.json）：启发式排队 + 人工确认 + 忽略名单
Views/Pages/Settings/GlossarySettingPage.xaml(.cs)  设置子页「术语表」：用户术语增删 + 候选确认 + 术语库状态
Services/ChannelIntel/ChannelChatLogReader.cs       聊天日志尾部补翻（会话启动时把 MOTD 与最近 20 行一起翻译）
```

**主窗口标题带版本号**（阶段 44 附带）：
- 标题 = `AppDisplayName` + 版本号，同时写 `Window.Title`（任务栏/Alt+Tab）与自绘 `ui:TitleBar.Title`。
- 版本号取 `AssemblyInformationalVersion`（csproj 的 `<Version>3.0.1</Version>` 会生成 `3.0.1+<commit>`），
  **只保留 `+` 之前的部分**；`AppVersion` 同时被"软件更新"页复用（原来那段反射逻辑重复写在页面里）。
- 语言切换后会重新套用一次标题：XAML 里标题绑的是 `{DynamicResource AppDisplayName}`，切语言会把它刷回"只有名称"。
- 实测（探针读程序集）：`InformationalVersion = 3.0.1+2cf7896…` → 标题显示 `新伊甸漫游指南 3.0.1`。

### 验证产物说明
```
核验截图与抓图脚本（capture_window.ps1）均为一次性产物，核对后已清理，不随仓库保留。
需要再次抓图时的要点：EnumWindows + PrintWindow，且抓图进程必须先开启 PerMonitorV2 DPI 感知。
```

### 本次修改的 Core 文件（与 WinUI 共用，注意回归）
```
Core/Services/IDNameService.cs   GetByIds(List<long>) 无限递归 → 转发到 List<int>；空待查列表跳过 ESI
Core/DBModels/IdName.cs          ESI 小写类别字符串 → 按 [EnumMember] 显式映射
Core/Services/DB/TranslationDbService.cs  新增：本地库专有名词翻译（阶段 46）
Core/Services/DB/DBService.cs    新增公开只读属性 MainDbReady / LocalDbReady（阶段 46）
Core/Models/TranslationItem.cs   仅类注释更新（阶段 46）
Core/Services/YDTranslationService.cs  仅类注释标注"WPF 不再使用，为 WinUI 保留"（阶段 46，代码未动）
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
