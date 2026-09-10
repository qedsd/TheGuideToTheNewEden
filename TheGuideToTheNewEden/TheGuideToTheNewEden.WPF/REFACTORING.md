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
         ↑ 统一入口先 EnsureTokenValidAsync()，各接口独立 try/catch（缺 scope 不致整页失败）
VM 层    CharactersViewModel / CharacterCardViewModel；各页以 CharacterContext 构造
UI 层    CharactersShellPage(Tab) ─ CharacterCardsPage
         └ CharacterWorkspacePage（左信息栏 + 右子页 Tab，Frame 承载）
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

构建状态：**0 错误**，剩 3 个既有警告（`NU1903`：Core 传递依赖 `SQLitePCLRaw.lib.e_sqlite3 2.1.10` 的漏洞通告，与本次改造无关）。

---

## 8. 已知限制与待办

### 功能降级（为保证可编译而暂缓，补起来各需数分钟）
1. **邮件详情收件人**显示为原始 ID（**发件人已随 #17 修复为名称**）—— `CharacterMailService` 未对 `MailContent.Recipients` 的元素做名称解析。
2. **打开邮件自动标已读**未实现 —— `UpdateMetadataAboutMailAsync` 第三参数类型与推测不符。
3. **邮件正文链接不可点击** —— `HtmlPanel.LinkClicked` 委托签名与本项目用法不匹配。
4. **合同详情窗口未实现**（仅列表 + 分页）。

### 本次核验结论（阶段 9）
- 克隆 / 邮件（含详情窗 HTML 渲染）/ 合同 / 工业 **已完成逐页实机截图核验**，结论见 §7。
- 抓图方式：`VerificationTools\capture_window.ps1`（`EnumWindows` + `PrintWindow`，脚本内先开启 PerMonitorV2 DPI 感知）；产物在 `TheGuideToTheNewEden.WPF\VerificationScreenshots\`（`final_*` 为修复后的最终核验图）。
- 合同 / 工业两页在该账号下**列表为空属正常空态**（账号无对应数据），空态下不触发名称解析，因此这两页的"名称解析"路径未被覆盖。

### 计划内未做
- 角色卡片**拖拽排序**、工作区 **ZKB 概览卡**。
- 国服粘贴 code 的简易输入窗 → 换成 Fluent `ContentDialog`。
- 玩家建筑页的"按角色搜索"（依赖 OAuth 授权链路打通后的 ESI 结构查询）。
- 软件更新页"安装"仍交外部 Updater。

### 环境/协作注意
- 设置文件与 WinUI 版**共用** `Configs/settings.json`：两版同时运行会互相覆盖，迁移完成后建议只保留 WPF 版。
- 运行时资源以链接方式引用 WinUI 项目的 `Resources/*`：**若删除 WinUI 项目，需改为复制或迁移资源**。
- **构建前必须先退出应用**：应用运行时锁定输出目录的 `*.dll`/`*.exe`（以及 `Resources/Database/*.db`），`Rebuild` 会以 `MSB3061` 警告跳过复制，导致"改了代码但运行的是旧程序集"（本次排查名称解析时踩到）。改动 Core 后若行为未变，先核对 `bin\...\TheGuideToTheNewEden.Core.dll` 的时间戳。
- 本机显示器为 2560x1440 @125%：部分工具（如未声明 DPI 感知的 PowerShell）拿到的窗口坐标会被按 1.25 缩放，截图会"看起来右侧被裁"——抓图脚本已内置 DPI 感知修正。

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
ViewModels/Characters/CharactersViewModel.cs
ViewModels/Characters/CharacterCardViewModel.cs
Views/Pages/Characters/CharactersShellPage.xaml(.cs)    壳（Tab）
Views/Pages/Characters/CharacterCardsPage.xaml(.cs)     卡片页
Views/Pages/Characters/CharacterWorkspacePage.xaml(.cs) 工作区
Views/Pages/Characters/{Overview,Skill,Clone,Wallet,Mail,Contract,Industry}Page.xaml(.cs)
Views/Windows/MailDetailWindow.xaml(.cs)               邮件详情（HtmlPanel）
Controls/PagerControl.xaml(.cs)                        分页
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
