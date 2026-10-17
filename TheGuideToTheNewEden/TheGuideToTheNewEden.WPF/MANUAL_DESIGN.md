# 内置说明书（Markdown 手册）设计

> 目标：**用 Markdown 内置说明书取代现在的 PDF 手册**（`Resources/Manuals/Manual_zh.pdf` / `Manual_en.pdf`）。
> 一份内容同时服务两个消费者：**用户查阅**（在应用内阅读/搜索/从当前页一键打开）与 **AI 问答**（成为 AI 的"功能用法"权威，用户直接问助手即可）。
>
> 本文只做设计，不含实施代码。相关文档：`REFACTORING.md`（实施记录与踩坑）、`AI_ASSISTANT_DESIGN.md`（助手架构）、`HOME_DESIGN.md`（主页与功能目录）。

---

## 0. 一句话方案

把"每个功能一篇 Markdown"放进 `Resources/Manuals/<语言>/<功能id>.md`（**front-matter 元数据 + 固定章节 + 稳定 slug**），
应用内用**已有的 `MarkdownPresenter`** 渲染成手册窗口（主页「手册」按钮 / **F1** / 导航右键 / 设置分类），
并用**两个常驻 AI 工具**（`manual_search` / `manual_read`）把同一批文档喂给助手——手册内容**只经工具结果**进入上下文，绝不塞进固定前缀。

---

## 1. 现状与问题

### 1.1 现在的 PDF 手册

| 事实 | 位置 |
|---|---|
| 两个 PDF 由 csproj 以 `Content + Link` 从 WinUI 项目复制到输出 `Resources\Manuals\` | `TheGuideToTheNewEden.WPF.csproj` 第 95–98 行 |
| 打开方式：主页品牌条「手册」→ 按语言选 `Manual_zh.pdf` / `Manual_en.pdf` → 交给系统默认阅读器 | `Services/Home/HomeFeatureLauncher.cs` 第 166–180 行 |
| 文件缺失时给右下角警告（含完整路径） | 同上（`Home.Action.ManualMissing`） |

### 1.2 四个缺陷（为什么要换）

1. **AI 完全用不了**：PDF 是二进制，助手无法检索/引用；用户问"倒货怎么用"时，AI 只能靠工具描述猜（而工具描述被 140 字符的预算契约限制，写不下用法）。
2. **不可检索、不可深链**：用户只能"整本下载式阅读"，无法从"我正在看的这个页面"一键跳到对应章节。
3. **与代码/版本脱节**：PDF 是外部产物（在 WinUI 项目里），改功能时几乎不可能同步改它；也没有任何机械校验能发现"文档少了这个功能"。
4. **维护成本高**：改一处要重新出 PDF；而项目里已有的 `REFACTORING.md` 已经把"为什么这么写"记全了，PDF 里反而没有"怎么用"的结构化约定。

### 1.3 已有的三个可复用底座（本方案不新增任何依赖）

| 底座 | 现状 | 本方案怎么用 |
|---|---|---|
| Markdown 渲染 | `Helpers/MarkdownRenderer.cs`（标题/段落/粗斜删/行内与围栏代码/链接/图片降级/列表(可嵌套)/引用/水平线/**管道表格**/HTML 剥离与实体解码）+ `Controls/MarkdownPresenter.cs`（只读、可选中复制、主题色感知、内部滚动） | 手册正文直接渲染；**不引入 Markdig 等新包** |
| 功能目录 | `Services/Home/HomeFeatureCatalog.cs`：42 个条目 / 14 分组，`Id` 稳定（`characters` / `market` / `settings` / `tool-ime` / `action-manual`…），`PageType` 与 `Nav.*` 标题键齐备；`MainWindow` 启动时已做"目录 ↔ 导航"双向比对 | **手册 id 直接沿用这套 id**，覆盖率可机械校验（漏写一个功能会被探针抓住） |
| AI 工具层与 token 契约 | `Services/Assistant/ToolRegistry.cs`（45 个工具、字典序=缓存契约、`Categories`/`CoreToolNames`）、`ToolRouter`（固定套餐按需声明）、`probe/AssistantProbe`（`tokens`/`routing` 套件守住描述 ≤140 字符、参数 ≤24 字符、声明总量 ≤26,000、省 token 比例） | 手册以**两个核心常驻工具**接入；预算与顺序由既有断言守住 |

---

## 2. 设计原则（六条，先立规矩再动笔）

1. **手册只写"用户可见行为"，不写实现**。
   "为什么线程亲缘性会崩""为什么改成并发分页"归 `REFACTORING.md`（阶段号互相引用）；手册写"点了会怎样、报错长什么样、限制是什么"。
   否则手册会变成第二本 REFACTORING，维护成本翻倍且必然腐烂。
2. **手册永远不进固定前缀**。工具声明（每轮工具请求的固定前缀）里只放 ≤135 字符的 `manual_search` / `manual_read` 描述；
   正文一律走**工具结果**（`role: tool` 消息），随用随取、用完即出上下文。往系统提示词里塞手册 = 前缀不稳定 + 每轮重复付费。
3. **数字权威是工具，用法权威是手册**。助手规则明确：真实数字只能来自数据工具；功能怎么用/在哪设置/报错含义只能来自手册；
   两者冲突时以工具数据为准并提示"手册可能过期"。
4. **id 与 slug 用英文、标题用中文**。中文文件名与中文参数会在检索与模型调用里引入不确定性；slug 必须与语言无关（英文提问也能命中）。
5. **一页一册、就地可开**。用户在任何页面按 **F1** 就能拿到"这个页面的说明书"，不需要先知道手册在哪、再自己找章节。
6. **结构与口径靠机械断言守，事实靠人工评审**。探针能证明"每个功能都有手册、每节的 slug 合法、工具名存在、语言键存在"；
   但"这段话与代码行为是否一致"只能靠写完后逐页对界面核对（见 §8.3）。

---

## 3. 内容模型

### 3.1 文件布局（源 = 真理）

```
TheGuideToTheNewEden.WPF/
  Resources/
    Manuals/                    ← 新增（应用自有资源，不再放 WinUI 项目）
      zh-CN/
        getting-started.md
        home.md
        characters.md           ← 壳页：标签、添加/移除角色、国服向导
        characters/
          overview.md  skills.md  clone.md  wallet.md  mail.md  contract.md  industry.md
        market.md  order.md  scalper.md  compare-market.md  appraisal.md
        game-preview.md  assistant.md
        channel/  intel.md  monitor.md  scan.md  market.md  log-monitor.md
        translation/  ai.md  channel.md  local.md
        guides/  ded.md  mission.md
        wormhole.md  links.md  map.md  zkb.md  database.md  eve-settings-sync.md
        tools/  ime.md  server-ping.md  keyboard-span.md  taskbar.md
        settings.md
        settings/  general.md home.md game-log.md market.md structures.md esi-scope.md
                   ai.md glossary.md keyboard-list.md test.md update.md
        concepts.md  troubleshooting.md
      en-US/                    ← 可后补；缺失时回退 zh-CN 并提示（见 §4.5）
        （同构）
      Manual_zh.pdf  Manual_en.pdf   ← 过渡期保留，M4 退役（见 §10）
```

**id 规则**：`id` = 相对语言目录的路径（`/` 分隔、去掉 `.md`）。

- 顶层 id **必须等于** `HomeFeatureCatalog` 里的 id（`market` / `characters` / `tool-ime` / `settings` / `assistant` …）——这是"每个功能都有手册"可校验的基础；
- 子页面 id 用 `父id/子slug`（`characters/mail`、`settings/ai`、`tools/ime`、`channel/intel`）；
- 横切文档 id 走白名单（`getting-started`、`concepts`、`troubleshooting`、`manual`），在探针里显式列出，防止"随便加一篇没人发现的文档"。

### 3.2 front-matter（极简 YAML 子集，不引依赖）

文件必须以 `---` 开头、`---` 结束的头部块开始。只支持**标量**与**行内数组**两种形态（自写 ~80 行解析，不引 YamlDotNet）：

```markdown
---
id: market
title: 商业 · 市场
group: Nav.Business          # 必须命中语言文件里的真实键（Nav.* / Home.Group.*）
page: Views/Pages/MarketPage.xaml
tools: [market_orders, market_history, market_navigate, market_trend, market_favorite]
aliases: [市场, 行情, 价格, 卖单, 买单, market, price, orderbook]
children: [market/star-history]
updated: 2026-10-20
---
```

| 键 | 必填 | 校验（探针） |
|---|---|---|
| `id` | ✅ | == 相对路径；顶层必须存在于 `HomeFeatureCatalog` |
| `title` | ✅ | 非空；建议与 `Nav.*` 文案一致（不强制，允许"商业 · 市场"这种加工） |
| `group` | ✅ | 是 `Nav.*` / `Home.Group.*`，且在 zh/en 两份语言文件里都存在 |
| `page` | ⬜ | 相对项目根；文件必须存在（防重命名后文档指向空气） |
| `tools` | ⬜ | 每个名字都在 `ToolRegistry` 里；**反向覆盖**：每个产品工具至少被一篇手册提到 |
| `aliases` | ✅ | ≥4 项、中英混排（≥1 个纯 ASCII、≥1 个含中文）；用于检索与英文提问命中 |
| `children` | ⬜ | 每个子 id 的手册都存在；父页声明子页（F1 从子页能回到父页目录） |
| `updated` | ✅ | `yyyy-MM-dd`；**只在文件里给人和评审看，不进任何 AI 声明** |

### 3.3 章节规范（固定 slug，机器可读）

每个 H2 **必须**在标题末尾带 slug：`## 5. 字段与口径 {#fields}`。
加载器在渲染前把 `{#slug}` 剥掉（标题照旧显示"5. 字段与口径"），索引器按 slug 建章节表。
这样 **AI 的 `section` 参数与语言无关**（中文问、英文文档也稳定），也不会因为改标题文字而断链。

| slug | 名称 | 必填 | 写什么 |
|---|---|---|---|
| `tldr` | 一句话 | ✅ | 这个功能解决什么问题、典型使用场景（≤120 字） |
| `where` | 在哪打开 | ✅ | 导航路径（商业 › 市场）、快捷键、窗口形态；**不写"左上角第三个按钮"这类空间指代**，写控件名/文案 |
| `prereq` | 前置条件 | ⬜ | 需要登录角色/权限 scope/设置项/游戏日志目录/数据缓存；缺了会看到什么 |
| `steps` | 操作步骤 | ✅ | 分场景编号步骤；每个场景一个小标题（H3），含"看到什么算成功" |
| `fields` | 字段与口径 | ⬜ | 管道表格：列名 → 含义 → 单位/口径 → 默认值；口径必须与代码一致（如"5% 均价=前 5% 订单的算术平均"） |
| `ai` | AI 能替你做什么 | ✅ | 关联工具名 + 3–5 条推荐问法 + **明确写出 AI 做不到的部分**（订阅型/写设置/交互密集） |
| `errors` | 常见失败与处置 | ⬜ | **报错原文照抄** → 原因 → 处置（用户拿报错问 AI 时能对上） |
| `limits` | 限制与已知问题 | ✅ | 逐条对应 `REFACTORING.md` §8 的编号；写"表现 + 规避方式"，不写内部原因 |
| `related` | 相关功能 | ⬜ | 手册 id 链接（`[邮件](characters/mail)`），供用户与 AI 顺着走 |

顺序固定为 `tldr → where → prereq → steps → fields → ai → errors → limits → related`；可跳过可选节，**不允许加顺序外的 H2**（加新节 = 先改本设计 + 改探针）。

### 3.4 写作纪律（面向 AI 检索的硬约束）

1. **每节第一句就是结论**（AI 摘取时第一句往往就是它的回答素材）。
2. **枚举化**：字段、口径、失败分支一律用管道表格或列表，不用长段落。
3. **能力边界必须显式写"不做什么"**：AI 的常见幻觉是"承诺软件不具备的功能"（如"我帮你开启频道预警"）；`ai` 节要写"AI 只能告诉你步骤，不能替你启动监控"。
4. **报错原文照抄**（含中文标点），因为用户是复制报错来问的。
5. **示例数字标注"示例"**：`例如 1,300,000（示例，实际以工具返回为准）`——防止 AI 把手册示例当行情回答。
6. **术语与 UI 文案、语言键一致**；关键名词给中英对照（`卖单出售价（lowest sell）`），提升英文提问命中率。
7. **不写个人数据、不写会变的第三方状态**（不要写"某建筑订单约 7 MB"这类具体实测值，写"首次较慢，按 TTL 缓存"）。
8. **外部 URL 写成纯文本**（渲染器点开走系统浏览器，AI 无法验证链接有效性）；站内引用一律用 `id#slug`。
9. **体量预算**：单篇 ≤ 9,000 字符，单节 ≤ 1,600 字符（`steps` ≤ 2,500）。超了就拆子页（`parent/child`），不要写超长一节——`manual_read` 分块读取的体验直接取决于此。

### 3.5 模板（复制即用）

```markdown
---
id: characters/mail
title: 角色 · 邮件
group: Nav.Characters
page: Views/Pages/Characters/MailPage.xaml
tools: [character_mail, character_navigate]
aliases: [邮件, 收件箱, 未读, 已读, 标签, mail, inbox, unread, label]
updated: 2026-10-20
---

# 角色 · 邮件

## 一句话 {#tldr}
按标签浏览角色邮件、读正文、标记已读；适合清邮件与找合同/通知类邮件。

## 在哪打开 {#where}
左侧导航「角色」→ 选中角色标签 → 工作区子页签「邮件」；也可在主页快捷输入里问助手（会给出跳转）。

## 前置条件 {#prereq}
- 已添加该角色且令牌有效（授权范围含 `esi-mail.read_mail.v1`）；未授权时列表为空并提示重新授权。

## 操作步骤 {#steps}
### 按标签查看
1. 左侧标签列表点「全部邮件」或具体标签（未读数以徽标显示）。
2. 右侧列表点任一行 → 打开邮件详情窗口（打开即标记已读，列表徽标同步减一）。
### 只找未读
1. …

## 字段与口径 {#fields}
| 列 | 含义 | 口径 |
|---|---|---|
| 发件人 | 角色名（本地库/ESI 解析） | 解析不到时显示原始 ID |

## AI 能替你做什么 {#ai}
- 工具：`character_mail`（列标签/邮件头/正文）、`character_navigate`（跳到邮件页）。
- 推荐问法：「我有哪些未读邮件」「读一下最近那封关于合同的邮件」。
- **做不到**：AI 不能替你发送邮件、不能删除邮件（该功能未开放为工具）。

## 常见失败与处置 {#errors}
| 界面提示 | 原因 | 处置 |
|---|---|---|
| 授权已过期 | 令牌失效 | 设置 → 权限选择重新授权 |

## 限制与已知问题 {#limits}
- 邮件正文里的链接不可点击（REFACTORING §8-1）；需要访问请复制链接。
- 正文中的图片被剥离（离线/隐私取向，REFACTORING 阶段 8）。

## 相关功能 {#related}
- [角色 · 总览](characters/overview)　[角色 · 合同](characters/contract)
```

---

## 4. 运行时服务（WPF 侧，纯本地、零依赖）

新增 `Services/Manual/`：

| 文件 | 职责 |
|---|---|
| `ManualFrontMatter.cs` | 极简 YAML 子集解析（标量 + 行内数组）+ `{#slug}` 抽取 + H2→章节切分；解析失败给"文件 + 行号"明确异常 |
| `ManualModels.cs` | `ManualDoc`（Id/Lang/Title/Group/Page/Tools/Aliases/Children/Updated/Sections/RawMarkdown）、`ManualSection`（Slug/Title/Text/Chars）、`ManualHit`（Id/Title/SectionSlug/Snippet/Score） |
| `ManualService.cs` | 惰性扫描 + 索引 + 检索 + 读取 + 页面映射 + 语言回退 + 打开窗口（对 UI 的唯一出口） |
| `ManualToolMap.cs` | 工具名 ↔ 手册 id 的集中映射表（正向给失败回填、反向给"AI 能力"核对） |

### 4.1 扫描与索引

- 根目录：`Path.Combine(AppContext.BaseDirectory, "Resources", "Manuals", <lang>)`（与现有 PDF 同目录，资源为 `Content` 复制，**可热修**：用户改文件即刻生效，不用重编译）。
- 启动不扫；首次使用（打开手册窗口 / 首次调用手册工具）时扫描并缓存；按 `id` 字典序（**确定性顺序**，与工具声明的缓存契约同思路）。
- 索引内容：id、title、group、page、tools、aliases、章节 slug 列表 + 每节字符数、全文字符数。
- 扫描结果打一条 INFO：`手册索引：52 篇 / 18.4 万字符 / zh-CN`（这行日志是"覆盖率"的现场证据）。

### 4.2 检索（`Search(query, limit)`）

**纯函数、无依赖、可探针断言**，打分（可调，写进探针基线）：

| 命中位置 | 分值 |
|---|---|
| `title` / `id` 精确或全词 | 10 |
| `aliases` 命中 | 8 |
| 章节标题命中 | 4 |
| 正文命中 | 1（同一文档最多累计 3 次，防长文靠数量淹没短文档） |
| 空的 query | 返回功能目录（id + title 一行一条） |

返回 `ManualHit` 列表（≤ limit，默认 5），每条附带**命中章节的 slug + ≤120 字符摘要**。

### 4.3 读取（`Read(id, section?, part?)`）

- `section` 为空 → 返回 `tldr` 全文 + 目录（每行 `slug · 标题 · 字符数`），让模型**两步读取**、按需取节（省 token 的关键）。
- `section` 指定 → 返回该节正文（`{#slug}` 已剥离），上限 `MaxSectionChars = 1800`，超出按段切块返回 `part N/M` 并提示续读。
- 未知 id/section → 返回明确错误文本 + 最接近的 id 列表（供模型自我纠正），**不抛异常**（服务层约定：try/catch → 明确文本）。

### 4.4 页面映射（F1 用）

```
ManualService.ResolveForPage(Type pageType, object? pageInstance)
  ① pageInstance is IManualAware aware && aware.ManualId 非空 → 用它（子页/页内标签的精确手册）
  ② 否则 HomeFeatureCatalog.FindByPageType(pageType)?.Id
  ③ 都没有 → getting-started（并写一条 WARN 日志，暴露"新页面忘了配手册"）
```

新增极简接口 `Views/Pages/IManualAware.cs`：`string? ManualId { get; }`。
需要实现的只有**含子页/页内模式的 6 个壳页**：`CharactersShellPage`（当前标签 → `characters/mail` 等）、`ScalperPage`（倒货/购物车/记录 → 子 id）、`MapPage`（主图/情报面板）、`SettingsPage`（当前分类 → `settings/ai` 等）、`ZKBPage`（击杀流/实体统计）、`HomePage`（返回 `home`）。

### 4.5 语言与回退

- 取 `LanguageService.Value`（`zh-CN` / `en-US`）；先找 `<lang>/<id>.md`，缺失时回退 `zh-CN/<id>.md` 并在窗口顶部显示一条提示（新增键 `Manual_FallbackToZh`），**不静默**。
- 订阅 `LanguageService.LanguageChanged` → 重建索引 + 重载当前文档（手册窗口天然跟随语言切换，符合阶段 70 的"切换即时生效"约定）。
- 助手侧：`manual_search` 的 `lang` 参数默认跟随界面语言，但**回退 zh**（模型读中文文档无障碍；不为了省事让工具失效）。

---

## 5. 用户侧接入

### 5.1 手册窗口 `Views/Windows/ManualWindow.xaml(.cs)`

- 形态：`ui:FluentWindow`（照 `ToolWindow` 那套：Mica + 自绘 `ui:TitleBar` + 左上角 logo），**单实例**（重复打开只激活），可调整大小、记忆尺寸位置、可选置顶。
- 内容：`Views/UserControls/ManualBrowserView.xaml(.cs)`（便于将来嵌进页面或助手弹窗）：

```
┌──────────────────────────────────────────────────────────────┐
│ [🔍 搜索功能/字段…]                 [在助手中提问] [复制] [打开文件] │
├────────────────────┬─────────────────────────────────────────┤
│ 分组目录（= 导航分组） │ 面包屑：商业 · 市场 › 字段与口径   [返回全文] │
│  ▸ 商业            │                                         │
│    市场  ◀         │  <MarkdownPresenter 渲染当前文档/章节>    │
│    订单            │                                         │
│  ▸ 角色            │                                         │
│  ▸ 设置            │                                         │
│ ─ 章节（当前文档）  │                                         │
│    1. 一句话        │                                         │
│    5. 字段与口径 ◀  │                                         │
└────────────────────┴─────────────────────────────────────────┘
```

- 左栏两段：**分组目录**（分组标题取 `Nav.*` 语言键 → 与左侧导航永远一致；条目取手册索引，缺手册的功能**也列出来但灰化 + 提示"暂无说明"**，这本身就是给维护者的可见清单）+ **当前文档章节树**。
- 右栏：默认渲染**整篇**（`MarkdownPresenter` 自带滚动与选中复制）；点章节 → 只渲染该节 + 面包屑 + 「返回全文」（一次字符串切片，无需改渲染器）。
- 工具条：`在助手中提问` → 复用**阶段 110 的 handoff**（`Navigation.NavigateToAssistant("《商业 · 市场》怎么用？")`），打开助手页并自动发送；`复制` 复制当前展示的 Markdown 原文；`打开文件` 用 `ShellHelper` 在资源管理器里定位该 md（便于用户自己改/扩展）。
- 渲染注意：手册里**不要用 `ProgressBar`/未验证控件**（§9-17 栈溢出）；只用 Markdown 语法。

### 5.2 入口矩阵

| # | 入口 | 行为 | 改动量 |
|---|---|---|---|
| 1 | 主页品牌条「手册」 | 打开 `ManualWindow`（默认 `getting-started`，或上次浏览位置） | `HomeFeatureLauncher.OpenManual()` 改一行 |
| 2 | **F1**（全局，仅应用有焦点时） | 打开**当前页面**的手册（`ResolveForPage`）；手册窗口已开着则切到对应文档 | `MainWindow.PreviewKeyDown` + 记当前页（`NavigationView.Navigated` / `NavigationStack`），约 30 行 |
| 3 | 左侧导航项**右键菜单** | 加一项「查看说明」（现有菜单已是"打开/固定/取消固定/管理"四条，加第五条） | `MainWindow.BuildHomeShortcutMenu` + 2 个语言键 |
| 4 | 设置分类行 | `SettingCard` 右侧操作区加一个小问号按钮 → 打开 `settings/<分类>` | 1 个控件 + 分类表加 id 字段 |
| 5 | 工具窗口（4 个）/ 弹窗（简介、买入、助手弹窗、翻译弹窗） | 各自的 `PreviewKeyDown` F1 → `tools/<id>` / 对应手册 | 每个窗口 3–5 行 |
| 6 | 关于窗口 | 「使用手册」链接 → `ManualWindow`（替代现在的 PDF 项） | 1 行 |
| 7 | 助手回答里的 `manual://market#errors` 链接 | 点开手册窗并定位章节 | `MarkdownRenderer.OnRequestNavigate` 里加 `manual:` 分支（约 6 行）+ 引擎提示词允许输出该链接 |
| 8 | AI 助手页顶部「功能说明」按钮 | 打开当前对话最后引用过的手册（没有则打开 `assistant`） | 可选，M5 |

> F1 只在应用内处理（`PreviewKeyDown`），**不注册全局热键**——避免与系统/其它程序的 F1 抢键。

### 5.3 PDF 退役

- M4 起：csproj 删除 `Resources\Manuals\**\*.*` 的 PDF 链接，删除 `Home.Action.ManualMissing`（改由手册服务给"找不到该文档 + 路径"提示），主页按钮只指向手册窗口。
- 过渡期（M1–M3）：PDF 保留，主页同时给「使用手册（Markdown）」与「旧版 PDF」两个入口？**不建议**——双入口会让用户与文档都分裂。建议过渡期直接切到 md，PDF 只留在仓库里备查（不出包），M4 彻底删文件。

---

## 6. AI 侧接入（"给 AI 看"是重点）

### 6.1 两个工具（核心常驻）

| 工具 | 参数（JSON Schema，参数描述 ≤24 字符） | 返回（紧凑，控 token） |
|---|---|---|
| `manual_search` | `query`（问题/关键词）、`limit`（默认 5，≤10）、`lang`（可选，默认界面语言） | ≤5 条：`id · title` + 命中章节 slug + ≤120 字符摘要；空 query → 全部功能目录（id + title 一行一条） |
| `manual_read` | `id`、`section`（slug，可空）、`part`（默认 1） | 无 `section`：`tldr` + 章节目录（`slug · 标题 · 字符数`）；有 `section`：该节正文（≤1800 字符，超出给 `part N/M`） |

- 挂在 `ToolRegistry.CoreToolNames`（与 `search_sde` / `translate` 同级）：**任何路由都声明**，保证"用户随便问怎么用，AI 一定有手册可查"。
- 描述文案遵守既有文体契约（100–135 字符、不复述参数表、不放易变值），例如：
  - `manual_search`：`Search the built-in user manual for a feature or setting; returns manual ids and matching section slugs. Use before answering any "how do I use / where is / why can't I" question.`
  - `manual_read`：`Read a manual section by id and slug from manual_search. Omit section to get the summary and section list; sections are returned in chunks with part numbers.`
- 走 `ToolRouter` 的字典序与"核心常驻"机制，不需要专用关键词；`tokens` / `routing` 套件会自动把新工具纳入断言（见 §8.1 的阈值回归）。

### 6.2 token 账（先说清楚再做）

| 项 | 估算 |
|---|---|
| 两条工具声明（名字 + 描述 + schema + 骨架） | ≈ +500 字符（≈ +0.2k token）/ 每个工具轮 |
| 一次 `manual_search` 结果 | ≤ 1,200 字符 |
| 一次 `manual_read`（单节） | ≤ 1,800 字符（超出分块，模型按需续读） |
| 一次"这功能怎么用"问答（search + read 一节 + 回答） | ≈ 4.5k–6k 字符输入增量（≈ 1.5k–2.5k token） |

对照：全量工具声明 23,358 字符；加两条后 `tokens` 套件的"总量 ≤26,000"仍成立；`routing` 的"单套餐 ≤55% / 双套餐 ≤80%"仍成立（分母分子同增，比例基本不变）。**实施后必须重跑这两个套件确认**（§8.1）。

### 6.3 系统提示词增补（规则 14–16）

1. **14**：用户问"这个软件怎么用 / 在哪设置 / 为什么没这个功能 / 这条报错什么意思"时，**必须先 `manual_search`**（必要时 `manual_read`），按手册回答；手册是**功能用法**的权威，工具返回值是**数字**的权威；冲突时以工具为准并提示手册可能过期。
2. **15**：引用手册时说明出处（功能名 + 章节名），**不要整段照抄**；回答里可以给出 `manual://<id>#<slug>` 链接（界面可点击打开手册）。
3. **16**：手册里的数字只是示例；真实价格/资产/订单数字**只能**来自数据工具，禁止引用手册里的示例数字当结果。

### 6.4 失败回填（把手册接到每个工具上）

`ManualToolMap` 给出 `工具名 → 手册 id` 的集中映射（**不改 45 个工具文件**，一处维护、探针校验两端都真实存在）。
`AiAssistantEngine` 在**工具执行失败**时，于回填给模型的 tool 文本尾部追加一行：

```
[说明书参考: manual://market#errors] 获取订单失败：建筑市场需要该角色有市场访问权…
```

作用：模型"自动知道去哪儿查怎么修"，且**只在失败时付费**（约 30 字符/次）。
另可选：工具文本里出现"需要授权/权限不足/未登录"这类关键词时同样附上（一句话启发式，写进探针基线）。

### 6.5 "功能目录 ↔ AI 能力"机械对齐

- 每篇手册的 `tools:` 声明它对应的工具；**反向断言**：每个产品工具至少出现在一篇手册里（否则 FAIL，暴露"新工具没写手册"）。
- 订阅型/不可代操作的功能（频道预警/监控/查价、日志监控、多开预览、配置同步）**没有工具**，靠手册 `ai` 节显式写"AI 不能替你启动/修改，只能告诉你怎么做"——这是防"AI 承诺做不到的事"的关键一栏。
- 与 `AI_ASSISTANT_DESIGN.md` §12（27 个导航目标开放度盘点）口径一致：手册 `ai` 节就是那张表在**用户可读层面**的落地；两者不一致时以代码为准，并同步改两处。

---

## 7. 覆盖清单（52 篇）

> 分批 = 写作顺序；每批写完即做一次探针 + 人工核对，不等全部写完再验。

### 批 1 · 骨架样板（3 篇，先定风格）
| id | 标题 | 页面/入口 |
|---|---|---|
| `market` | 商业 · 市场 | `Views/Pages/MarketPage.xaml` |
| `characters/mail` | 角色 · 邮件 | `Views/Pages/Characters/MailPage.xaml` |
| `assistant` | AI 助手 | `Views/Pages/AiAssistantPage.xaml` |

### 批 2 · 商业 + 角色（13 篇：批 1 的 2 篇 + 11 篇）
`order` 订单 ／ `scalper` 倒货（含 `scalper/cart`、`scalper/records` 子节，暂不拆文件）／ `compare-market` 比价 ／ `appraisal` 估价 ／
`characters` 角色（壳页：标签/添加/移除/国服向导）／ `characters/overview` 总览 ／ `characters/skills` 技能 ／ `characters/clone` 克隆 ／ `characters/wallet` 钱包 ／ `characters/contract` 合同 ／ `characters/industry` 工业

### 批 3 · 频道 + 翻译 + 多开（9 篇）
`channel/intel` 预警 ／ `channel/monitor` 监控 ／ `channel/scan` 统计 ／ `channel/market` 查价 ／ `channel/log-monitor` 日志监控 ／
`translation/ai` AI 翻译 ／ `translation/channel` 频道翻译 ／ `translation/local` 本地翻译 ／ `game-preview` 多开

### 批 4 · 星图 / KB / 资料 / 攻略（8 篇）
`map` 星图（含情报面板、跳桥、主权、资源、一跳覆盖 → H3 场景）／ `zkb` ZKB（击杀流/实体统计/详情）／ `wormhole` 虫洞 ／
`database` 数据库 ／ `links` 快速链接 ／ `guides/ded` DED ／ `guides/mission` 任务 ／ `eve-settings-sync` 配置同步

### 批 5 · 工具 + 设置 + 横切（22 篇）
`tools/ime` ／ `tools/server-ping` ／ `tools/keyboard-span` ／ `tools/taskbar`（各含迷你模式/透明度等）；
`settings`（壳页：分类怎么进）／ `settings/general` ／ `settings/home` ／ `settings/game-log` ／ `settings/market` ／ `settings/structures` ／
`settings/esi-scope` ／ `settings/ai`（模型与翻译/行为/ESI 全量访问/工具开关四节）／ `settings/glossary` ／ `settings/keyboard-list` ／ `settings/test` ／ `settings/update`；
`getting-started` 快速上手（首次启动/添加角色/语言/托盘/授权）／ `concepts` 术语与口径（ISK/SP/安等/跳数/订单有效期/5% 均价…）／ `troubleshooting` 疑难排查（日志/配置目录/托盘不可用/低完整性/网络与 ESI 报错对照）／ `home` 主页（品牌条/快捷方式/数据卡）

**素材来源**（写的时候按这个顺序取材，效率最高）：
`REFACTORING.md` 对应阶段（功能做了什么、怎么用）+ §8（限制，逐条变 `limits`）+ `AI_ASSISTANT_DESIGN.md` §12（`ai` 节）+ 页面 XAML（控件名与文案，`where`/`steps`）+ 语言文件（术语与报错原文）。

---

## 8. 质量闸门

### 8.1 新增探针套件 `manuals`（`probe/AssistantProbe/ManualSuite.cs`，约 25 条断言）

| # | 断言 |
|---|---|
| 1–3 | 每篇：front-matter 键齐全、`id` == 路径、`updated` 是合法日期 |
| 4 | 顶层 id 全部存在于 `HomeFeatureCatalog`（含 `ToolWindow` 与 `Action` 类） |
| 5 | `HomeFeatureCatalog` 里每个 `Page`/`ToolWindow` 条目都有对应手册（**否则列出缺口清单**；缺口语义见 §8.2） |
| 6 | `group` 键在 zh/en 两份语言字典里都存在 |
| 7 | `page` 指向的文件存在 |
| 8–9 | 每篇 slug 唯一、匹配 `^[a-z0-9][a-z0-9-]*$`、且都在固定节集合内 |
| 10 | 必填节 `tldr/where/steps/ai/limits` 齐全且顺序正确 |
| 11 | `aliases` ≥4、中英混排 |
| 12–13 | `tools` 里每个名字都在 `ToolRegistry`；**反向**：每个产品工具至少被一篇手册覆盖（排除显式白名单） |
| 14 | `children` 双向一致（父声明子、子存在） |
| 15–16 | 体量预算（单篇 ≤9,000、单节 ≤1,600 / `steps` ≤2,500 字符）|
| 17 | zh/en 两份**同 id 集合**（允许 en 缺文件 → 记录"待翻译"清单，不 FAIL）；同 id 的 slug 集合一致 |
| 18 | 检索纯函数：同输入同结果、空 query 返回目录、限流生效、分数排序稳定 |
| 19 | 读取分块：超长节返回 `part 1/N` 且拼接后 == 原文（去 `{#slug}` 后） |
| 20 | `section` 未知 → 明确错误文本 + 候选 id，不抛异常 |
| 21 | `{#slug}` 在渲染输入里已被剥离（避免界面出现 `{#tldr}`） |
| 22 | `manual_search` / `manual_read` 的描述长度与参数描述长度符合 `tokens` 契约 |
| 23 | `ToolRouter` 路由含核心常驻（新工具在单套餐与回退两种情况下都被声明） |
| 24 | `ManualToolMap` 双向有效（每个值都是真手册 id、每个键都是真工具名） |
| 25 | 语言字典完整性（沿用阶段 124 既有断言，确保新增的 12–14 个手册键中英一致、0 重复） |

**既有套件回归**：`tokens`（声明总量 ≤26,000、单条 ≤140）与 `routing`（单套餐 ≤55%、双套餐 ≤80%、"7 个典型问题 ≤6 种声明集合"）必须重跑，输出新比例存档。

### 8.2 覆盖率的"三层账"

1. **硬 FAIL**：顶层 id 不存在于功能目录、slug/节非法、工具名不存在、语言键缺失、`page` 文件不存在。
2. **缺口清单（WARN + 打印）**：功能目录里的条目暂时没有手册（M1–M3 期间是常态，随批次递减到 0；M4 起改为 FAIL）。
3. **待翻译清单（INFO）**：en 缺失的 id 列表（决定是否投入翻译）。

### 8.3 人工评审清单（探针管不到的部分，每篇过一遍）

- [ ] 每个"操作步骤"自己照着点一遍，能走通、结果与文档一致；
- [ ] `limits` 与 `REFACTORING.md` §8 逐条对得上（条号 + 表现一致）；
- [ ] 界面上真实出现的报错原文照抄，没有加工改写；
- [ ] `fields` 的口径与代码/工具返回一致（尤其**均值、百分比、去极值、TTL** 这类容易被想当然的口径）；
- [ ] `ai` 节的"做不到"是真的做不到（到 `ToolRegistry` 里核对有没有对应工具）；
- [ ] 术语与界面文案一致（用界面上的词，不用同义词）。

### 8.4 协作约定（建议并入 `REFACTORING.md` §8）

> **改功能即改手册**：任何改动用户可见行为（新增/改名/改口径/新增限制）的阶段，收尾必须同步更新对应 `Manuals/zh-CN/**.md`，
> 并在该章节的 REFACTORING 记录里注明"手册已更新（id）"。漏更新由 `manuals` 套件在结构层兜底（结构没变但口径变了，只能靠这条约定）。

---

## 9. 反模式（明令禁止）

| # | 反模式 | 后果 |
|---|---|---|
| 1 | 把手册正文塞进系统提示词 / 每轮预注入上下文 | 前缀逐字节变化 → 缓存全失效（阶段 122/123 的核心结论）；token 成本暴涨 |
| 2 | 用中文文件名或中文 slug | 检索与模型参数不稳定；语言切换时断链 |
| 3 | 一节写到 4000+ 字符 | `manual_read` 分块体验崩坏；模型抄一半就答 |
| 4 | 手册里写死行情/建筑订单量等实测数字 | 过期后被 AI 当事实回答；改为"量级 + 行为"描述 |
| 5 | 手册变第二本 REFACTORING（写实现细节/根因/踩坑） | 双份维护、必然腐烂；根因统一归 REFACTORING |
| 6 | 给 45 个工具的描述逐个加"见手册 xxx" | 破坏 140 字符文体与总量预算断言；改走 `ManualToolMap` 集中映射 + 失败回填 |
| 7 | 手册里用 HTML/`<img>`/复杂语法 | 渲染器图片降级为链接、HTML 被剥离；只允许纯 Markdown + 管道表格 |
| 8 | 靠"记得更新文档"而没有探针 | 漏写不会被发现；结构层全部机械断言化 |
| 9 | PDF 与 md 长期双轨 | 用户与维护者分裂；M4 明确退役 PDF |
| 10 | 让 AI 生成手册初稿后直接发布 | 幻觉数字/不存在的菜单项会被写进"权威文档"；AI 只可用于**初稿**，逐条人工核对（§8.3）|

---

## 10. 实施阶段

| 阶段 | 交付物 | 验收 | 工作量 |
|---|---|---|---|
| **M1 框架 + 样板** | `ManualFrontMatter`/`ManualModels`/`ManualService`；`ManualWindow` + `ManualBrowserView`；F1 + 主页入口 + 导航右键；`IManualAware`（6 个壳页）；`manuals` 套件骨架（断言 1–21）；批 1 的 3 篇 | 探针把"其余 49 篇"打成缺口清单；F1 在任意页面都能打开对应/回落文档；zh/en 切换即时生效 | 1.5 人日 |
| **M2 AI 接线** | `manual_search`/`manual_read` + 核心常驻 + 系统提示词 14–16 + `ManualToolMap` 失败回填 + 语言键 | 助手问"倒货怎么用"能引手册并给出正确步骤与限制；`tokens`/`routing`/`manuals` 三套件全绿 | 0.5 人日 |
| **M3 内容铺量** | 批 2–5 共 49 篇（可并行：子代理出初稿 → 我逐条核对口径与限制） | 缺口清单清零；每篇过 §8.3 评审；探针 25 条全绿 | 约 20–25 小时（可并行压缩） |
| **M4 PDF 退役** | csproj 去 PDF 链接、主页按钮只指手册、删 `Home.Action.ManualMissing`、删 PDF 文件 | `dotnet publish` 无重复输出（NETSDK1152 回归）；主页手册按钮正常 | 0.2 人日 |
| **M5 打磨（可选）** | 助手回答里的 `manual://` 可点击链接；助手页「功能说明」按钮；`en-US` 手册补齐；手册内"上次更新时间/适用版本"页脚 | 点击助手引用可定位章节 | 0.5 人日 |

---

## 11. 文件清单

**新增**
```
MANUAL_DESIGN.md                                   （本文）
Resources/Manuals/zh-CN/**/*.md                    （52 篇）
Resources/Manuals/en-US/**/*.md                    （可选，M5）
Services/Manual/ManualFrontMatter.cs
Services/Manual/ManualModels.cs
Services/Manual/ManualService.cs
Services/Manual/ManualToolMap.cs
Services/Assistant/Tools/ManualSearchTool.cs
Services/Assistant/Tools/ManualReadTool.cs
Views/Windows/ManualWindow.xaml(.cs)
Views/UserControls/ManualBrowserView.xaml(.cs)
Views/Pages/IManualAware.cs
probe/AssistantProbe/ManualSuite.cs
```

**修改**
```
TheGuideToTheNewEden.WPF.csproj      Content 复制 md（+ 退役 PDF 时删除原链接）
Views/MainWindow.xaml.cs             F1、记录当前页、导航右键「查看说明」
Services/Home/HomeFeatureLauncher.cs OpenManual() → ManualWindow
Services/Navigation.cs               可选：NavigateToAssistant 已有，无需新增
Services/Assistant/ToolRegistry.cs   注册 2 个工具 + CoreToolNames
Services/Assistant/AiAssistantEngine.cs  系统提示词 14–16 + 失败回填
Helpers/MarkdownRenderer.cs          manual: 链接分支（M5）
Views/Pages/Characters/CharactersShellPage.xaml.cs  等 6 个壳页  IManualAware
Resources/Languages/zh-CN.xaml / en-US.xaml          约 12–14 个新键（中英同步、0 重复）
probe/AssistantProbe/Program.cs      注册 manuals 套件
REFACTORING.md                       §8 增加"改功能即改手册"约定；实施后追加阶段记录
```

**新增语言键（草案，中英同步）**：`Manual_Title`（内置说明书）、`Manual_Search`（搜索功能、字段或报错…）、`Manual_NoResult`（没有找到相关内容）、`Manual_Empty`（该功能暂无说明）、`Manual_AskAssistant`（在助手中提问）、`Manual_ShowAll`（返回全文）、`Manual_Copy`（复制本文）、`Manual_OpenFile`（打开文件位置）、`Manual_FallbackToZh`（该功能暂无英文说明，已显示中文）、`Manual_NotFound`（找不到说明书：{0}）、`Nav.Manual`（查看说明）、`Manual_ChapterCount`（本节 {0} 字）、`Assistant_Chat_ManualHint`（参考说明书：{0}）。

---

## 12. 需要拍板的四点

1. **粒度**：按"页面/功能"约 52 篇（本方案），还是要把每个**设置项/字段**都单独成节（写作量 ×2–3）？
2. **英语版**：先只做 `zh-CN` + 回退提示（推荐，M5 再补），还是从批 1 就中英同步（每篇成本 +40%）？
3. **AI 形态**：`manual_search` + `manual_read` **核心常驻**（推荐：任何问法都能查手册，代价约 +500 字符/工具轮），
   还是放 `General` 套餐（更省，但"这个软件怎么用"若没命中意图词就要回退全量才有手册工具）？
4. **文件形态**：输出目录下的 md（**可热改、可打开文件位置**，推荐）还是嵌进程序集资源（用户改不坏，但改了要重编译）？

---

## 13. 落地进度（2026-10-16 起；阶段 131 收尾）

四点已按推荐项拍板并落地：**粒度**＝功能级（57 篇，含 `getting-started` / `concepts` / `troubleshooting` 三篇横切与 `home`）；
**语言**＝先 `zh-CN`（en-US 留 M5）；**AI 形态**＝`manual_search` / `manual_read` 核心常驻（CoreToolNames）；
**文件形态**＝输出目录 md（csproj Content 复制，运行时 `AppContext.BaseDirectory\Resources\Manuals\<lang>\`）。

### M1 框架 + 样板（已完成）

- 内容：`Resources\Manuals\zh-CN\` **57 篇**全部写完，经过"写作 → 对抗式校对（逐条回核语言键 / XAML / REFACTORING / 工具源码）→ 机械校验"三道工序。
- 服务层：`Services\Manual\ManualDocument.cs`（最小 YAML 解析 + H2 切节 + **`{#slug}` 的解析/渲染分离**）、
  `ManualService.cs`（懒加载索引 / 宽松 id 查找 / 关键词检索 / **概览 + 分节分片读取** / **工具反查** / **语言回退状态** / `DirectoryOverride` 探针挂载点）、
  `ManualToolMap.cs`（页面类型 → 手册 id、工具 → 手册 id、失败语义与 `/errors` 回填）。
- 界面：`Views\UserControls\ManualBrowserView.xaml(.cs)`（左搜索列表 + 右 `MarkdownPresenter`；支持 `id#节` 单节渲染 + 「返回全文」、
  语言回退提示、工具条的**在助手中提问 / 复制本文 / 打开文件位置**）、`Views\Windows\ManualWindow.cs`（工具窗口外壳、单实例、不设 Owner）。

### 入口矩阵（§5.2，已落地 7 项）

| # | 入口 | 状态 |
|---|---|---|
| 1 | 主页品牌条「手册」 | ✅ 打开 `ManualWindow`（默认 `getting-started`） |
| 2 | **F1** 打开当前页手册 | ✅ 先问页面自己的 `IManualAware`（壳页精确到子页签），再静态映射，最后回落「快速上手」 |
| 3 | 左侧导航右键「查看说明」 | ✅ 没有对应手册的项自动隐藏 |
| 4 | 设置分类行小问号 | ✅ 打开 `settings/<分类>`（`e.Handled` 阻止冒泡触发整行点击） |
| 5 | 工具窗口 F1 | ✅ `ToolWindow.ManualId` 由 `ToolWindowHost` 按页面类型注入（助手窗口 → `assistant`） |
| 6 | 关于窗口「使用手册」 | ✅ 新增按钮（原来这里是"打开 PDF"） |
| 7 | 正文内链 / AI 回答里的 `manual://id#节` | ✅ `MarkdownRenderer` 的 `manual:` 分支 + 窗口内单节定位 |
| 8 | 助手页「功能说明」按钮（可选） | ⛔ 未做（M5） |

`IManualAware` 的 7 个实现：`CharactersShellPage`、`CharacterWorkspacePage`、`ScalperPage`、`ZKBPage`、`SettingsPage`、`MapPage`、`HomePage`。

### M2 AI 接线（已完成）

- 两个核心常驻元工具：`manual_search`（空检索词 = 全部手册目录）、`manual_read`（不给节 = 一句话 + 章节目录；
  指定节时按 **`part N/M`** 分片，单片 ≤1,800 字符，**拼回等于原文**）。工具声明只放 ≤140 字符的描述，正文一律走工具结果。
- 系统提示词规则 **14–16**：14 用法类问题先查手册（与实时数据冲突以数据为准）、15 引用要给出处并可给 `manual://<id>#<slug>` 链接、16 手册里的数字只是示例。
- **工具失败回填**：异常退出或结果命中失败语义（`失败` / `需要授权` / `权限不足` / `没有权限` / `未登录` / `重新授权` / `已过期`）时，
  给模型的工具文本尾部追加 `[说明书参考: manual://<id>#errors] …`；只进工具结果、不进固定前缀。
- 工具 → 手册的映射**不手写第二份**：直接反查各篇 front-matter 的 `tools:` 声明（`ManualService.FindManualIdForTool`），
  漂移由 `manuals` 套件的双向覆盖断言兜住。本轮据此补上了 `character_location`（只在一篇正文里被提到、没进 `tools:` 声明）的缺口。

### M3 质量闸门（已完成：`manuals` 套件 58 断言）

`probe/AssistantProbe/ManualSuite.cs` 覆盖：

- 结构：id 合法 / front-matter 齐全且日期合法 / 每个 H2 有合法且唯一的 slug / 必填节齐全且顺序正确 /
  体量预算 / 别名 ≥4 且中英混排 / **渲染正文不含 `{#`** / `group` 键在 zh-en 字典里都存在 / `page` 指向的 XAML 存在 / `children` 真实存在。
- 覆盖：功能目录里每个**页面 / 工具窗**条目都能经 `ManualToolMap` 找到手册；设置壳页 11 个分类同理；
  **每个产品工具都至少被一篇手册讲到**；手册声明的工具名都真实存在；元工具在 `CoreToolNames`。
- 服务行为：检索确定性 / limit / 排序 / 命中 / `Find` 的宽松匹配（`characters_mail`、`manual://characters/mail#errors` 都能命中）/
  概览含 tldr 与全部节目录 / **502 个节的分片读取全部无损** / `part` 越界钳制 / 未知 slug 返回 null。
- AI 接线：两个元工具的描述与参数描述符合 token 文体契约；失败关键词基线；回填带 `manual://<id>#errors`。
- 界面构造：离屏构造 `ManualBrowserView`（XAML / 图标名 / 静态资源 / 工具条语言键这几类**只在运行时才炸**的问题），
  并验证"窗口还没 Show 就跳转"（F1 首次打开）与 `manual://<id>#节` 链接都能落到目标手册。

**与本文早期预算的偏差（按实测调整，探针注释里写明了理由）**：

| 项 | 设计值 | 实测 | 现取值 |
|---|---|---|---|
| 单篇字符 | ≤9,000 | 8,447 | ≤9,000 |
| `steps` 节 | ≤2,500 | 2,491 | ≤2,500 |
| 其它节 | ≤1,600 | `errors` 2,187 / `fields` 1,723 | **≤2,200**（超 1,800 的节由 `ReadSection` 分片，体验有兜底） |
| 系统提示词 | ≤1,100 | 1,317（加了规则 14–16） | **≤1,400**（`TokenSuite` 预算同步上调） |

### M4 PDF 退役（已完成）

- csproj 删除 WinUI `Resources\Manuals\**\*.*` 的 Content 链接 → **WPF 出包不再带 PDF**（WinUI 侧仍自用那两份，
  文件留在仓库备查；未从 WinUI 项目删除，避免影响旧版）。
- 删除已无引用的 `Home.Action.ManualMissing` 键；`Home.Feature.ManualDesc` 文案由"（PDF）"改为
  "内置使用手册（Markdown，可搜索、可按 F1 从当前页进入）"（中英同步）。
- 输出目录 `Resources\Manuals\` 只剩 `zh-CN\`（已核对）。

### M5 打磨（未做，按约定留待后续）

- `en-US` 内容（当前英文界面显示中文手册 + 窗口顶部回退提示 `Manual.FallbackToZh`）。
- 助手页「功能说明」按钮；左侧"按导航分组折叠 + 当前文档章节树"；手册页脚的"上次更新 / 适用版本"。

### 验证（阶段 131 收尾）

- `dotnet build`：**0 错误 0 新警告**。
- 探针逐个套件跑（`all` 在本机跑不完，原因见 `REFACTORING.md` §8 第 69 条）：
  `manuals` **58/0**、`tokens` **9/0**、`routing` **23/0**、`toolcall` 124/0、`engine` 62/0、`tools` 458/0、
  `quickroute` 29/0、`business` 13/0、`writes` 69/0、`esi` 163/0、`ui` 87/0。
- 本轮顺带修正三处**过期探针期望**（产品工具数 44→46、系统提示词预算 1,100→1,400、
  "未配置端点时提问留在输入框"的产品行为补齐），详见 `REFACTORING.md` 阶段 131 第七节。

### 维护约定（重要）

**改功能即改手册**：任何改动用户可见行为（新增 / 改名 / 改口径 / 新增或解除限制）的阶段，收尾必须同步更新
`Resources/Manuals/zh-CN/**.md`，并在该阶段的 REFACTORING 记录里注明"手册已更新（id）"。
结构层由 `manuals` 套件机械兜底；**口径是否过期探针管不到**，只能靠这条约定 + 人工评审（§8.3 清单）。

