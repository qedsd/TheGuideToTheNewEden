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
TargetFramework      net10.0-windows
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
- 产物：截图存于 `TheGuideToTheNewEden.WPF\VerificationScreenshots\`；抓图脚本 `VerificationTools\capture_window.ps1`。

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

## 5. 角色功能分层设计

```
授权层   CharacterAuthService ── CharacterStore ── AuthHelper / SerenityAuthHelper
              │                        │
              │                        └─ Auth.json / Auth_Serenity.json（按服务器分文件）
### 阶段 24：邮件详情弹窗白色背景、不跟随主题
- 现象：邮件详情弹窗背景是白色的，与主窗口（深色/主题色）不一致。
- 根因：`MailDetailWindow` 是**普通 `Window`**——普通窗口用的是系统原生标题栏与背景，不跟随应用主题（Windows 不会为该进程自动套用深色标题栏）；主窗口用的是 `ui:FluentWindow` + Mica 背景 + 自绘 `ui:TitleBar`，两者观感自然不一致。
- 修复：把两个弹窗（`MailDetailWindow` 与 `SerenityAuthWindow`）都改成 `ui:FluentWindow`：`ExtendsContentIntoTitleBar="True"` + `WindowBackdropType="Mica"` + 自绘 `ui:TitleBar`（与 MainWindow 完全同一套），内容顶部留 48px 避开标题栏；代码后置的基类同步改为 `Wpf.Ui.Controls.FluentWindow`。另外把正文 `HtmlPanel` 的 `Background` 显式设为 `Transparent`，避免 HTML 渲染器自身涂白底。
- 核验：编译通过（应用在运行，主输出目录被占用，改用重定向输出路径校验）；全项目已无普通 `Window`（仅剩 `ui:FluentWindow`）。

---

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

构建状态：**0 错误**，剩 3 个既有警告（`NU1903`：Core 传递依赖 `SQLitePCLRaw.lib.e_sqlite3 2.1.10` 的漏洞通告，与本次改造无关）。

---

## 8. 已知限制与待办

### 功能降级（为保证可编译而暂缓，补起来各需数分钟）
1. **邮件正文链接不可点击** —— `HtmlPanel.LinkClicked` 委托签名与本项目用法不匹配（阶段 8 起遗留）。
2. **合同详情窗口未实现**（WinUI 点击合同行会打开详情；WPF 侧只有列表 + 分页）。
3. **Syncfusion 的表格能力未平替**：WinUI 的 `SfDataGrid` 支持分组拖放区、列筛选、**分组合计行**（钱包"分组合计 ISK"），WPF 标准 `DataGrid` 只保留排序/列宽/列重排。
4. **等待遮罩未复刻**：WinUI 的 `ShowWaiting()` 全屏等待态在 WPF 侧一律是静默异步加载（各页首次进入可能短暂空列表）。
5. **结构名称的 ESI 路径**：地点名走 `LocationNameResolver`，结构 ID 只查本地 `StructureService` 列表，解析不到时回退显示原始 ID（见 §8 计划内未做）。
6. **ZKB 卡数据可能为 0**：ZKB 是第三方服务，同一角色两次启动取到的统计会不同（服务端按周期归零/波动），非本地缺陷。
7. 技能组"组内技能个数"口径略窄：WinUI 显示该组**全部**技能数，WPF 显示的是本地库+账号都命中的技能数（服务 DTO 限制）。
8. 技能名 ToolTip（WinUI 用 `InvType.Description`）未实现，等级/技能点提示已有。

### 本次核验结论（阶段 9）
- 克隆 / 邮件（含详情窗 HTML 渲染）/ 合同 / 工业 **已完成逐页实机截图核验**，结论见 §7。
- 抓图方式：`VerificationTools\capture_window.ps1`（`EnumWindows` + `PrintWindow`，脚本内先开启 PerMonitorV2 DPI 感知）；产物在 `TheGuideToTheNewEden.WPF\VerificationScreenshots\`（`final_*` 为修复后的最终核验图）。
- 合同 / 工业两页在该账号下**列表为空属正常空态**（账号无对应数据），空态下不触发名称解析，因此这两页的"名称解析"路径未被覆盖。

### 计划内未做
- 角色卡片**拖拽排序**（WinUI 用 `GridView` 的 `CanReorderItems`；WPF 的 `ItemsControl`+`WrapPanel` 需自行实现拖放）——排序数据层 `CharacterStore.Move` 已具备。
- 合同**详情窗口**；钱包 `SfDataGrid` 的分组/筛选/分组合计（见上「功能降级」3）。
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
- 本机显示器为 2560x1440 @125%：部分工具（如未声明 DPI 感知的 PowerShell）拿到的窗口坐标会被按 1.25 缩放，截图会"看起来右侧被裁"——抓图脚本已内置 DPI 感知修正。
- **structure id 请走 `StructureService`**：`Core/Services/IDNameService` 的 ID 是 `int`，结构（structure）ID 约 1e12 会被**静默截断**并解析出错误名称。详见文末「结构（structure）ID 解析约定」。
- **改完 XAML 界面没变 → 先怀疑 BAML 陈旧**：并行构建/中断过的构建会让 `obj` 里的 `.baml` 落后于 `.xaml`，程序集里嵌旧标记。删 `obj` 重建即可（详见 §9 第 16 条）。
- **不要使用 `ProgressBar`**：WPF-UI 隐式样式下会栈溢出，用 `Controls/RatioBar.cs`（详见 §9 第 17 条）。

---

## 9. WPF / WPF-UI 踩坑备忘

1. **`Page` 的父级限制**：`Page` 只能由 `Window` / `Frame`（或 `NavigationWindow`）承载。放进 `ContentControl` / `TabItem.Content` 会抛"Page 只能具有 window 或 frame 父级"。→ 统一用 `Frame` 托管（`NavigationUIVisibility=Hidden`）。
2. **`ScrollViewer.CanContentScroll` 决定页面能否自滚**：WPF-UI 导航完成后按该附加属性决定是否给页面套 `DynamicScrollViewer`；**默认 true** 会让页面被无限高度测量、内部滚动条失效。自管滚动的页面必须显式设为 `False`。
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
17. **`ProgressBar` 在 WPF-UI 隐式样式下会栈溢出**：本项目实测——卡片模板里出现标准 `ProgressBar`（带 `Height`/`Background`/`Foreground`/`Value` 绑定），切到该页进程即以 `0xC00000FD` 退出，故障模块是 `dwrite.dll`（栈耗尽发生在文本/渲染栈里），移除后立即恢复。WPF-UI 会为 `ProgressBar` 提供隐式样式，本项目此前从未用过该控件，属首次暴露。**细进度条建议自绘**（见 `Controls/RatioBar.cs`：`OnRender` 画两条圆角矩形，无模板无样式，行为可控）。
18. **"颜色不跟随主题"有五种来源**，改动界面时对照排查：
    - 用**标准控件**而非 WPF-UI 版本：WPF-UI 的主题样式只作用于自己的子类（`ui:DataGrid`、`ui:Button`…）。标准 `DataGrid`/`ComboBox` 取的是**系统主题色**，与应用主题无关。
    - **在代码里抓资源**：`TryFindResource("...Brush") as Brush` 拿到的是当前主题的**实例**，主题切换后 WPF-UI 会换掉资源字典里的对象，缓存的实例不会更新。要么在 XAML 里用 `{DynamicResource}`，要么用 `DataTrigger` + `DynamicResource` 组合。
    - **字面量颜色**（`#RRGGBB`、`OrangeRed`、`White`…）在两种主题下恒定；语义色请用 `SystemFillColorSuccessBrush` / `SystemFillColorCautionBrush` / `SystemFillColorCriticalBrush` / `TextOnAccentFillColorPrimaryBrush`。
    - **`static readonly Brush` 字段**（如 VM 里的 `Brushes.SeaGreen`）同样是恒定色；改用布尔语义标记，把颜色交给 XAML 的 `DataTrigger`。
    - **显式 `Style` 会整体替换隐式样式**：给控件套自定义样式（带自己的 `Template`，如 `SettingsRowButton`，或只是覆盖几个属性，如三个表格页的 `*GridStyle`）时，WPF-UI 隐式样式里的 `Foreground`、模板一并失效 → 文字退回**控件默认的系统色**（深色下变黑）、控件退回**原生模板**（表格变原生外观）。两条出路：样式里自己补 `Foreground`/模板，或 `BasedOn` 隐式样式。`BasedOn` 的写法要求 **`TargetType` 必须与基样式一致**：WPF-UI 的子类控件（`ui:DataGrid`）用 `TargetType="{x:Type ui:DataGrid}" BasedOn="{StaticResource {x:Type ui:DataGrid}}"`；而 `{x:Type ListBoxItem}` 这类**未验证存在**的隐式样式不要写（找不到会在页面构造时抛 `XamlParseException`），改为在行/单元格模板里显式给颜色。
    另：VM 里**缓存本地化字符串**（`TryFindResource(key) as string`）有同类问题——切语言后不会更新，页面需在 `LanguageChanged` 后重建/刷新这些文本。

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
Services/StructureService.cs           市场结构列表
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

### 主窗口
```
Views/MainWindow.xaml(.cs)   左菜单 + 内容区、标题栏图标、窗口位置持久化、托盘
```

### 验证工具与产物（阶段 9）
```
VerificationTools/capture_window.ps1                   DPI 感知的窗口枚举 + PrintWindow 抓图脚本
TheGuideToTheNewEden.WPF/VerificationScreenshots/      实机核验截图
    dpi_*.png        修复名称解析前的 4 页全尺寸截图
    fixed_mail_1_*.png  邮件列表日期列修复后的截图
    final_*.png      最终核验图（克隆/邮件/邮件详情）
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
- 因此 **结构名称一律走 `StructureService`，不要用 `IDNameService`**：WinUI 版 `Services/StructureService.cs` 已具备该职责 —— `QueryStructureAsync(long id, long characterID)` 先查本地 `Configs/Structures.json` 缓存，未命中再用该角色的授权调 `Universe.GetStructureInfoAsync` 并回写缓存；WPF 版 `Services/StructureService.cs` 目前只有本地列表与 `GetStructure(long)`（`Configs/MarketStructures.json` / `Structures.json`），**按 ID 查询结构的 ESI 路径还没移植**（见 §8「计划内未做」），需要时把 WinUI 的 `QueryStructureAsync` 按 `CharacterContext` 风格补进来即可（授权层已在阶段 7 打通，只差这一步）。
- 同理，任何"可能是建筑/结构 ID"的字段（如 `IndustryJob.FacilityId`、合同 `StartLocationId/EndLocationId`、`LocationId` 等）在解析成名称前，都要先判断是否落在地点/结构域，是则走 `StructureService` 而非 `IDNameService`。
- 新增调用点时自查：这一列 ID 有可能 ≥ 1e12 吗？会 → `StructureService`；不会 → `IDNameService`。
