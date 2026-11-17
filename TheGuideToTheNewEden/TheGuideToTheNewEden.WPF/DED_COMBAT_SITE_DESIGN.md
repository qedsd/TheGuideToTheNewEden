# 死亡远征（DED / 战斗站点）攻略补全设计

> **本文档回答三件事**：① EVE University Wiki「Combat site」现在到底有哪些站点（全量清单与概况）；
> ② 与本项目现有 DED 数据/攻略的差异（缺什么、错什么、旧什么）；③ 补齐与改造的落地计划。
>
> - 现状依据：`REFACTORING.md` 阶段 79（DED / 任务页迁移）与阶段 306 的代码状态（本次逐项复核过）。
> - 参考基准：<https://wiki.eveuniversity.org/Combat_site>，**revision 230825，最后编辑 2026-05-15**（CC BY-SA 4.0）。
> - 附表（本次抓取并解析的快照）：
>   - `Docs/wiki-combat-site-inventory.md` —— 380 条全量清单（人读）
>   - `Docs/wiki-combat-site-inventory.json` —— 同一份数据的机器可读版（供后续导入工具用）

---

## 0. 结论摘要

1. **现有 192 条数据的"核心三类"覆盖得比预期好**：DED 评级复合体 49/50、无评级复合体 74/74、远征 69/76。
   真正缺的只有 **8 个条目位**（1 条 DED + 7 条远征续篇）。
2. **真正的缺口在"类别"上**：wiki 一共 380 条站点，本项目只覆盖其中 3 类共 192 条；
   **战斗异常（126）、化学实验室（34）、战斗遗迹（7）、三神裔（9）等 6 类共 180 条，本地一条都没有**。
3. **补数据之前必须先改数据模型**：现有 `DED` 表只有 `Type / Level / TitleCN / TitleEN / Content` 五列，
   用 `Level=0` 把"无评级复合体"和"远征"混成一档。不加 `Category / Rating / Tier / Security / ShipLimit` 这些列，
   180 条新数据无处安放。
   > **口径澄清（用户指出）**：5 条 `<Faction> Fleet Staging Point` 记 `Level = 9` **不是缺陷**。
   > CONCORD 体系里确实没有 9/10 这一档（wiki 也这么写），但玩家一直按 9/10 称呼舰队集结点，
   > 这是**刻意保留的游戏习惯叫法**。新表里它们仍然是 `Rating = 9`（归在「无评级复合体」分类下），界面与 AI 照旧显示 `9/10`。
4. **本地中文攻略与 wiki 是互补关系，不是替代关系**：本地 192 条共 **151,907 字**中文实战内容
   （148 条带"数量 / 吨位 / 备注"怪表、134 条带掉落、172 条带触发来源），这是 wiki 没有的；
   wiki 强在**结构化元数据**（评级 / 船型限制 / 安等 / 每层 NPC 波次 / 可升级目标 / 战利品估值）。
   → 方案定为**双轨正文**（人工中文攻略 + 自动生成的 wiki 结构化数据），**绝不整体覆盖**。

---

## 1. 现状盘点

### 1.1 代码链路（本次逐项核对）

| 环节 | 实现 | 备注 |
|---|---|---|
| 表模型 | `Core/DBModels/Static/DED.cs` → `[SugarTable("DED")]`，字段 `Id / Type / Level / TitleCN / TitleEN / Content` | `Type` 0–5 = 无人机/天蛇/血袭者/萨沙/天使/古斯塔斯；`Level` 1–10 = `n/10`，0 = 未定级 |
| 数据访问 | `Core/Services/DB/DEDService.cs` → `QueryAllAsync(type)` / `SearchAsync(keyword)` | 只有"按势力"和"按标题模糊搜索"两个入口，**没有按等级/类别查询的方法** |
| 库 | 实际走 `DBService.StaticDb`（`Config.InitDb()` 里 `InitDEDDb(...)` 是**注释掉的**） | 所以数据在 **`Resources/Database/static.db`** |
| 死文件 | `Resources/Database/ded.db`（348 KB） | 与 `static.db` 的 DED 表**逐行完全相同**（192 条、名称与正文长度零差异），且不在代码路径上 |
| 页面 | `Views/Pages/DEDPage.xaml(.cs)` + `ViewModels/DEDPageViewModel.cs` | 左卡：搜索框 + 列表 + 势力下拉 + 状态行；右卡：标题 + `MarkdownPresenter` 正文 |
| 渲染 | `Helpers/MarkdownRenderer.cs` + `Controls/MarkdownPresenter.cs` | 已把 `\r\n` / `\r` 归一化成 `\n`（`MarkdownRenderer` 第 720–722 行），现有内容的 **CR-only 换行**能正常成段 |
| AI | `Services/Assistant/Tools/DedInfoTool.cs`（`ded_info`） | 关键词 / 势力检索 + 等级过滤，正文截断 180 字 |
| 手册 | `Resources/Manuals/zh-CN/guides/ded.md`（`id: guides/ded`） | 已完整，含"正文是离线快照"的限制说明 |

### 1.2 数据现状（`static.db` → `DED`，192 条）

| 势力 | Level 0（未定级） | Level 1–10 | 小计 |
|---|---|---|---|
| 0 无人机 Rogue Drones | 18 | 4（2/3/5/10） | 22 |
| 1 天蛇 Serpentis | 24 | 10 | 34 |
| 2 血袭者 Blood Raider | 24 | 10 | 34 |
| 3 萨沙 Sansha | 24 | 10 | 34 |
| 4 天使 Angel | 24 | 10 | 34 |
| 5 古斯塔斯 Guristas | 24 | 10 | 34 |
| **合计** | **138** | **54** | **192** |

拆开看，138 条 `Level = 0` 实际是两类东西混在一起：**无评级复合体 69 条 + 远征 69 条**；
54 条 `Level ≥ 1` 实际是**DED 评级复合体 49 条 + 舰队集结点（FSP）5 条**（FSP 按游戏习惯记在 `Level = 9`）。

内容规模：总 **151,907 字**（均 791 字，最长 4,605 字）；148 条含"数量 / 吨位 / 备注"怪表，134 条含掉落，172 条含触发来源。

### 1.3 UI 现状与其局限

- 只有一个**势力下拉**（6 项）+ 搜索框。`REFACTORING.md` 阶段 79 写的"等级（1–5）筛选"**在 DED 页并不存在**（那是任务页的），属文档滞后。
- 列表项显示：英文名（主题色）/ 中文名 / 势力 / 等级（`DEDLevelConverter`：`Level<=0` → "未定级"，否则 `n/10`）。
- 因为 `Level` 承担了"评级 / 未定级 / 分类"三种语义，界面上：
  - **无评级复合体与远征长得一模一样**（都显示"未定级"），玩家无法区分"自己扫出来打"和"只能从 Agency 进"；
  - **舰队集结点显示成 `9/10`**：这是**玩家习惯叫法**（有意保留），而 wiki 明确写着 *"There are no 9/10-rated DED complexes"*；
  - AI 工具 `ded_info` 直接拼字符串 `【{Level}/10】`，于是把"未定级"打印成 **`【0/10】`**（这才是真缺陷）；`9/10` 属习惯叫法，保留。

### 1.4 已确认的缺陷清单（本次逐条核实）

| # | 缺陷 | 证据 | 影响 |
|---|---|---|---|
| 1 | 缺少 **Drone Infested Mine**（无人机 4/10 DED） | wiki 评级表有，本地无 | 少一个 4/10 站点 |
| 2 | 缺少 **7 条远征续篇**（见 §3.2） | wiki 远征表有，本地无 | 玩家打到第二段时查不到 |
| 3 | 缺 6 大类共 **180 条**站点（异常 126 / 化学实验室 34 / 遗迹 7 / 三神裔 9 / 神庙 2 / 被围困 1） | wiki 全量 380 条 vs 本地 192 条 | "刚出远征"与"异常升级"这条主链路断在异常一端 |
| 4 | 数据模型无"类别"概念，`Level` 三义混用 | §1.3 | 无法容纳新类别，且现有条目已误显示 |
| 5 | ~~`Level = 9` 的 FSP 显示成 `9/10`~~ | **不是缺陷**：玩家习惯叫法，刻意保留（见 §0 口径澄清） | — |
| 6 | **5 条中文标题为空** | Id 99 `Colony Under Fire`、119 `Religious Fury`、160 `Salvation Angel's Shipment`、179 `Gurista Productions Shipment`、193 `Loose Ends` | 列表第二行空白；且 `SearchAsync` 用 `TitleCN.Contains`，**中文搜不到这 5 条** |
| 7 | **重名**：`Outgrowth Rogue Drone Hive` 出现两次（Id 3 = 5/10、Id 4 = 10/10） | wiki 用 `(5/10)` / `(10/10)` 区分 | 列表里两条一模一样，无法分辨哪个是 10/10 |
| 8 | 标题多余空格：Id 183 `"Guristas Powergrid "` | 尾随空格 | 搜索 `Powergrid` 能命中，但显示与排序受影响 |
| 9 | 命名变体不统一：`Gurista Watch` / `Gurista Vigil`（wiki 作 `Guristas…`）、`Pulverize the Pioneers`（wiki 作 `The`） | 大小写 / 单复数 | 名称搜索命中率下降 |
| 10 | **24 条正文过简（< 300 字）**，其中 **7 条是明确的占位文案** | 如 Id 187 `Hired Gun` 89 字、Id 183 `Guristas Powergrid` 62 字"请参考天使能量网"、Id 121 `Dubious Assignment` 106 字"未找到该远征的详细信息" | 右侧基本等于空白 |
| 11 | `ded.db` 与 `static.db` 的 DED 表完全重复，且 `InitDEDDb` 已注释 | §1.1 | 白占 348 KB 发布体积 + 两份数据源漂移风险 |
| 12 | 代码注释写错了数据来源 | `DEDPageViewModel.cs` 第 21–23 行、`DEDPage.xaml` 第 14 行、`DEDPage.xaml.cs` 第 9 行都写"来自 `Resources/Database/ded.db` / `Config.DEDDBPath`" | 后来人按注释改 `ded.db` 会完全不生效 |
| 13 | `REFACTORING.md` 阶段 79 写 DED 页有"等级（1–5）筛选" | 实际没有 | 文档与代码不一致 |
| 14 | 正文无任何"更新时间 / 来源"字段 | `DED` 表无 `UpdatedAt` | 无法判断某条攻略是哪一年的，也就无法做增量刷新 |

> 补充说明（**不是缺陷**）：现有正文 191/192 条用 **CR-only（`\r`）** 换行，是历史编辑器留下的；
> `MarkdownRenderer` 已在 `NormalizeNewlines` 里统一处理，渲染正常。但后续导入工具应统一输出 `\n`，避免新旧混用。

---

## 2. 参考基准：wiki「Combat site」的完整站点体系

### 2.1 八类站点（+ 两类未逐条列举）

| 类别 | 说明 | 条目数 |
|---|---|---|
| **战斗异常** Combat Anomalies | 进星系后自动出现，不需要扫描；单层无门；有概率"升级"成 DED 评级复合体 | **126** |
| **无评级复合体** Unrated Complexes | 扫描出，多层加速门，有概率触发**远征**；含 `Fleet Staging Point`（9/10 的等价物） | **74** |
| **DED 评级复合体** DED Rated Complexes | 扫描出或由异常升级而来；评级 1/10–8/10、10/10（**没有 9/10**）；评级决定**最大可进船型** | **50** |
| **远征** Expeditions | 由无评级复合体触发，只能从 **Agency（星际代理处）** 进入；多为 4 段链 | **76** |
| **化学实验室** Chemical Labs | 探测窗口归类为"gas site"，实为战斗站点；部分需 Data Analyzer 开箱 | **34** |
| **战斗遗迹** Combat Relic Sites | 极稀有，只出现在特定 00 区域，外观像遗迹但探针显示为战斗 | **7** |
| **三神裔入侵** Triglavian（Pochven） | 波赫文星域的异常，双方阵营各 5 / 4 个等级 | **9** |
| **特殊站点** | ISHAEKA 战术响应总部 1、血袭者神庙复合体 2、被围困的隐蔽研究设施 1 | **4** |
| **合计** | | **380** |
| （未逐条列举） | 势力战争复合体（FW complexes）、Homefront Operations、COSMOS 站点（8 个区域） | 页面只作文字说明 |

### 2.2 三条必须照搬的规则

**① DED 评级 → 最大可进船型**（升评级 = 升一个船型，5/10 封顶）

| 评级 | 最大船型 |
|---|---|
| 1/10 | 护卫 |
| 2/10 | 驱逐（含 T3 驱逐） |
| 3/10 | 巡洋（T3 巡洋除外） |
| 4/10 | 战巡（T3 巡洋除外） |
| 5/10 – 10/10 | 战列 / T3 巡洋 |

**② 战斗异常的"10 档 × 变体"命名规律**（除无人机外，六个势力完全一致）

| 档 | 变体 | 出现于 | 无人机对应 |
|---|---|---|---|
| 1 | `Hideaway` / `Hidden` / `Forsaken` / `Forlorn` | 高安、低安 | Drone Cluster |
| 2 | `Burrow` | 高安 | Drone Collection |
| 3 | `Refuge` | 高安、低安 | Drone Assembly |
| 4 | `Den` / `Hidden` / `Forsaken` / `Forlorn` | 高安+低安（L1）/ 低安+00（L2–L4） | Drone Gathering |
| 5 | `Yard` | 低安 | Drone Surveillance |
| 6 | `Rally Point` / `Hidden` / `Forsaken` / `Forlorn` | 低安、00 | Drone Menagerie |
| 7 | `Port` | 低安、00 | Drone Herd |
| 8 | `Hub` / `Hidden Hub` / `Forsaken Hub` / `Forlorn Hub` | 低安、00 | Drone Squad |
| 9 | `Haven` | 00 | Drone Patrol |
| 10 | `Sanctum` / `Forsaken Sanctum` | 00 | Drone Horde / Teeming Drone Horde |

每个档位在 wiki 表里还标了 **"可升级为哪个 DED"**（例：`Angel Refuge` → `5/10 Angel's Red Light District`）。
**这张"异常 → DED"的映射表本身就是玩家最需要的东西**，而本地一条都没有。

**③ 远征链的形状**

- 触发概率约 5%；**每段续接概率约 50%**（四段全通的概率约 12.5%）；
- 下一段要求**真实安等更低**的星系，找不到就静默结束（不会有"升级失败"提示）；
- 因此"远征"与"触发它的无评级复合体"是**成对**的：`<Faction> Hideout → Blue Pill` 这类关系必须在数据里体现。

### 2.3 全量清单

380 条的逐条明细见 **`Docs/wiki-combat-site-inventory.md`**（含每个异常的难度档与档内等级、可升级目标、高/低/00 三档分布；
每个远征的触发来源；化学实验室的区域）。机器可读版 `Docs/wiki-combat-site-inventory.json` 供导入工具直接消费。

### 2.4 为什么这件事可以自动化（关键发现）

wiki 的**每个站点子页**都是模板化结构，抓 `action=raw` 即可稳定解析：

```
{{CMBSiteInfo |name= |type= |rating= |location= |ship limit= |faction= |signature strength= }}
{{NPCTableHead|Wave 1}}
{{NPCTableRow|Elite Frigate|2|Strain Splinter Alvi|ewar= Web|point= yes}}
{{NPCTableRow|Battleship|3|Alvus Queen}}
|}
```

- `CMBSiteInfo` → 评级 / 船型限制 / 安等 / 势力 / 信号强度（**直接就是我们要的元数据列**）；
- `NPCTableHead` + `NPCTableRow` → 每层的"波次 / 吨位 / 数量 / 怪名 / EWAR / 反跳 / 掉落"，可无损转成本项目既有的
  `| 数量 | 吨位 | 备注 |` 表；
- 页面底部普遍带 `Exploration logs`（带日期的战利品与估值）与 `{{DED Complexes|势力}}` 导航模板。

**实测存在性**：把 380 个条目标题批量丢给 `action=query&titles=…`，**365 个页面存在、15 个为红链/标题归一化差异**
（红链如 `Sansha Chemical Lab`、`Crumbling Ruins`、`* Forsaken Sanctum`、`Shadow Serpentis Fleet Staging Point 2/3`）。
→ 实际需抓取的页面约 **360–370 个**，规模可控（单页 2–7 KB，全量约 1.5–2 MB 原始 wikitext）。

---

## 3. 差异汇总

### 3.1 总账

| 类别 | wiki | 本地 | 差异 |
|---|---|---|---|
| 战斗异常 | 126 | 0 | **−126** |
| 化学实验室 | 34 | 0 | **−34** |
| 战斗遗迹 | 7 | 0 | **−7** |
| 三神裔入侵 | 9 | 0 | **−9** |
| 神庙复合体 / ISHAEKA / 被围困设施 | 4 | 0 | **−4** |
| DED 评级复合体 | 50 | 49 | **−1** |
| 无评级复合体（含 FSP） | 74 | 74 | 0（FSP 的 9/10 是习惯叫法，非缺陷） |
| 远征 | 76 | 69 | **−7** |
| **合计** | **380** | **192** | **−188** |

（本地 192 = 49 评级 + 5 FSP + 69 无评级 + 69 远征；wiki 的 74 无评级里含那 5 条 FSP，故两项完全对齐。）

### 3.2 缺失的 8 个条目位（逐条）

| # | 站点 | 势力 | 类别 | wiki 页 |
|---|---|---|---|---|
| 1 | **Drone Infested Mine** | 无人机 | DED **4/10**（wiki 记 `location= ?, ?, Null`，正文写"`?` 与 00"；进入后 8 小时窗口） | 存在 |
| 2 | **Angel Mineral Acquisition Outpost, 2nd Part** | 天使 | 远征（6/10 续篇） | 存在 |
| 3 | **Serpentis Logistical Outpost Part 2** | 天蛇 | 远征（6/10 续篇） | 存在 |
| 4 | **Angel Domination Fleet Staging Point 2** / **3** | 天使 | 远征（FSP 续篇，2 页） | 存在 ×2 |
| 5 | **Dark Blood Fleet Staging Point 2** / **3** | 血袭者 | 远征（FSP 续篇，2 页） | 存在 ×2 |
| 6 | **Dread Guristas Fleet Staging Point 2** / **3** | 古斯塔斯 | 远征（FSP 续篇，2 页） | 存在 ×2 |
| 7 | **True Sansha Fleet Staging Point 2** / **3** | 萨沙 | 远征（FSP 续篇，2 页） | 存在 ×2 |
| 8 | **Shadow Serpentis Fleet Staging Point 2** / **3** | 天蛇 | 远征（FSP 续篇，2 页） | **红链（wiki 也没有）** |

> 即：**表格口径 8 条，实际 wiki 页面 11 个**（4 势力 × FSP 续篇 2 页 = 8，+ 3 条独立站点）。
> Shadow Serpentis 的续篇 wiki 自己也没有，只能留占位并注明"wiki 无此页"。

### 3.3 整类缺失的 180 条

| 类别 | 条目数 | 本地情况 |
|---|---|---|
| 战斗异常 | 126 | 0，**且连带丢掉 126 条"异常 → DED"升级映射** |
| 化学实验室 | 34 | 0 |
| 战斗遗迹 | 7 | 0 |
| 三神裔入侵 | 9 | 0 |
| 血袭者神庙复合体（含 Inner Sanctum） | 2 | 0 |
| ISHAEKA 战术响应总部 | 1 | 0 |
| 被围困的隐蔽研究设施 | 1 | 0 |
| **合计** | **180** | **0** |

### 3.4 数据模型差异（补齐前必须先解决）

| 维度 | wiki 有 | 现有 `DED` 表 | 结论 |
|---|---|---|---|
| 类别 | 8 类 | 无（靠 `Level` 猜） | **必须加 `Category`** |
| 评级 | `1/10`–`10/10`（无 9） | `Level` 1–10，其中 9 是 FSP 的习惯叫法 | **`Rating` 与 `Level` 分开，且 FSP 保留 `Rating = 9`** |
| 异常难度档 | 档 1–10；T1/T4/T6/T8 再分 1–4 级、T10 分 1–2 级 | 无 | 需 `Tier` + `Level` |
| 安等分布 | 高安 / 低安 / 00 | 无（散落在正文里） | 需 `Security` 位掩码 |
| 最大船型 | `ship limit` 字段 | 无（正文里偶有"最高可进 T1 巡洋"） | 需 `ShipLimit` |
| 升级/触发关系 | `Escalation` / 触发来源 | 只在正文自然语言里 | 需 `Escalation` + `Source` |
| 更新时间 | 页面 revision | 无 | 需 `UpdatedAt` |
| wiki 对应页 | 页面标题 | 无 | 需 `WikiTitle`（增量同步 + "看原文"） |

### 3.5 命名与字段瑕疵

见 §1.4 的第 6–9 条（5 条空中文名、1 组重名、1 处尾随空格、若干单复数/大小写变体）。

补充两个只在对照 wiki 时才看得出的差异：

| wiki | 本地 | 说明 |
|---|---|---|
| `Outgrowth Rogue Drone Hive (5/10)` / `(10/10)` | 两条同名 | 建议本地改为 `… Hive (5/10)` / `… Hive (10/10)` |
| `Frentex` | `Frentix` | wiki 页名是 `Frentex`（`Frentix` 重定向过去）；**游戏内是 `Frentix`**，本地写法更贴近游戏，建议保留并加别名 |

### 3.6 正文质量

| 指标 | 数量 |
|---|---|
| 正文 < 300 字 | 24 |
| 其中明确的占位/无内容文案（"无详细信息""未找到""请参考…"） | 7 |
| 正文无任何怪表（`|` 表） | 44 |
| 正文最短 | 62 字（`Guristas Powergrid`） |

这 24 条恰好是"小站点 / 低评级 / 冷门远征"，而 wiki 对**这些**反而写得最细（例：`Drone Infested Mine` 有 5 波完整 NPC 表 + 两条实测战利品记录）。
→ **补齐这 24 条是本轮性价比最高的部分**。

### 3.7 与 wiki 的关系：互补，不是替代

| 维度 | 本地中文攻略 | wiki |
|---|---|---|
| 语言 | 中文（含中英对照怪名） | 英文 |
| 强项 | 打法经验、翻车点、省事技巧（例："先跳日不完进去存轨道坐标"）、赏金总量 | 评级/船型/安等/信号强度、每层完整 NPC 波次、战利品估值、Exploration logs、导航模板 |
| 弱项 | 无元数据、无船型限制、24 条过简 | 纯英文、无中文打法、部分页面是 `{{Stub}}` |

**结论**：`Content`（人工中文）+ `WikiContent`（自动生成）双列并存，界面给两个页签；不做任何自动覆盖。

---

## 4. 修改计划

### 4.1 三条原则

1. **先扩模型，再灌数据**。否则 180 条新记录只能继续往 `Level` 里硬塞，把问题放大 3 倍。
2. **人工正文与 wiki 数据分列存放**，各自独立更新。wiki 自动生成的内容可整段重刷，人工内容一个字都不动。
3. **一切可离线复现**：抓取 + 解析 + 生成走一个独立离线条目（DevTools），产物落到 `static.db`；
   运行时**不联网**（保持现有"打开即查"的体验与手册承诺）。

### 4.2 P0 · 数据模型升级（不动 UI 也能先跑）

**新增表 `CombatSite`，保留 `DED` 不动**（`DED` 有 AI 工具与旧链路在引用，渐进迁移风险最低）：

```sql
CREATE TABLE CombatSite (
  Id            INTEGER PRIMARY KEY,
  Category      INTEGER NOT NULL,  -- 0 DED评级 1 无评级 2 远征 3 异常 4 化学实验室 5 遗迹 6 三神裔 7 特殊
  Faction       INTEGER,           -- 0..5，与 DED.Type 同义；特殊站点可空
  Rating        INTEGER,           -- DED 评级 1..10（FSP 按习惯记 9）；真正无评级为 0
  Tier          INTEGER,           -- 异常难度档 1..10；其它 0
  Level         INTEGER,           -- 异常档内等级 1..4；其它 0
  Security      INTEGER,           -- 位掩码 1=高安 2=低安 4=00
  ShipLimit     TEXT,              -- "护卫" / "驱逐" / … / "战列 / T3 巡洋"
  TitleCN       TEXT,
  TitleEN       TEXT,
  Escalation    TEXT,              -- 可升级到的 DED 站点（多个用逗号）
  Source        TEXT,              -- 远征的触发来源（无评级复合体名）
  WikiTitle     TEXT,              -- 对应 wiki 页名，用于增量同步与"看原文"
  Content       TEXT,              -- 人工中文攻略（迁移自 DED.Content）
  WikiContent   TEXT,              -- 自动生成的 wiki 结构化正文
  IsStub        INTEGER DEFAULT 0, -- 正文过简/占位标记
  ContentUpdatedAt TEXT, WikiUpdatedAt TEXT
);
```

**迁移规则（一次性，可脚本验证）**：

| 原 `DED` | 新 `Category` | 判定方式 |
|---|---|---|
| `Level` 1–8、10 | 0（DED 评级） | `Rating = Level` |
| `Level` = 9 | 1（无评级），**`Rating = 9` 原样保留** | 仅 5 条 FSP；9/10 是玩家习惯叫法，不改 |
| `Level` = 0 | 1 或 2 | **按名称与 wiki 清单精确匹配**（69 条无评级 / 69 条远征），匹配不上的人工确认；文本特征（`^由 **X** 触发` → 远征）只作交叉校验 |

> 文本启发式实测结果：138 条里只有 62 条能匹配"`^由 **X** 触发`"、65 条匹配到"触发远征"字样、**11 条两者都不匹配**
> （例：`Guristas Annex` 写作"远征：古斯塔斯能量网"，`Radiance` / `Hierarchy` 正文里根本没提远征）。
> 所以**不能单靠文本**：以 wiki 名称表做权威判定，文本特征只作交叉校验。

**兼容层**：`DEDService` 增加 `CombatSite` 版本的重载（`QueryAsync(category, faction, rating, keyword)`），
旧的 `QueryAllAsync(int type)` 保留并转调新表，让页面/AI/手册先不炸。

### 4.3 P1 · 补齐与纠错（纯数据，立刻可见）

| 步骤 | 内容 | 量 |
|---|---|---|
| P1-1 | 补 `Drone Infested Mine`（4/10，含 5 波 NPC 表） | 1 |
| P1-2 | 补 7 条远征续篇（其中 Shadow Serpentis 2 条留"wiki 无页"占位） | 7 |
| P1-3 | 补 5 条空中文名：`Colony Under Fire` 殖民地交火 / `Religious Fury` 宗教狂热 / `Salvation Angel's Shipment` 救赎天使的货运 / `Gurista Productions Shipment` 古斯塔斯产品货运 / `Loose Ends` 扫尾 | 5 |
| P1-4 | 拆重名：`Outgrowth Rogue Drone Hive` → `(5/10)` / `(10/10)` | 2 |
| P1-5 | 修尾随空格、统一 `Guristas…` 单复数（保留 `Gurista` 作搜索别名） | ~6 |
| P1-6 | 修 7 条占位正文（`Hired Gun` / `Guristas Powergrid` / `Dubious Assignment` / `Consequences Smonsequences` / `Gurista Fortress` / `Sansha Powergrid` / `Angel Owned Station`）→ 用 wiki 正文生成 | 7 |
| P1-7 | 人工补写/精修 17 条"< 300 字但非占位"的冷门站点 | 17 |
| P1-8 | 删除死文件 `Resources/Database/ded.db`（与 `static.db` 完全重复、不在代码路径） | 1 |

**验收**：`Level = 0` 的条目 0 条空中文名、0 条重名；`SearchAsync` 中文命中率 100%；`DED` 相关探针全绿。

### 4.4 P2 · 新类别导入（180 条）

**做法**：写离线工具 `DevTools/CombatSiteImport`（或 WPF 里一个隐藏命令，二选一），四步流水线：

1. **抓取**：按 `WikiTitle` 列表拉 `action=raw`（约 360–370 页，限速 0.3 s/次，全量约 2–3 分钟）。
2. **解析**：
   - `{{CMBSiteInfo}}` → `Rating / ShipLimit / Security / Faction / SignatureStrength`；
   - `{{NPCTableHead}}` + `{{NPCTableRow}}` → 每层的 `| 数量 | 吨位 | 备注 |` 表（`ewar=` → 备注"带 ECM/网子"，`point= yes` → "反跳"，`drop=` → 掉落列）；
   - `== Loot ==` / `Exploration logs` → "掉落与实测估值"小节；
   - `[[Category:…]]` 与 `{{…Complexes|势力}}` → 交叉校验类别与势力；
   - `{{Stub}}` → `IsStub = 1`。
3. **转换**：输出本项目既有风格的 Markdown（`###第一层` + 表 + `&nbsp;` 分隔），复刻本地正文的版式习惯，保证两种来源在右卡里观感一致。
4. **落库**：写 `CombatSite`（`WikiContent` 列），`ContentUpdatedAt / WikiUpdatedAt` 记录时间；`WikiTitle` 回填。

**分批（建议按价值排序）**：

| 批次 | 内容 | 条数 | 理由 |
|---|---|---|---|
| B1 | 战斗异常 126（含"异常 → DED"升级映射） | 126 | 玩家日常刷的入口，且现有页面完全无法回答"这个异常能升级成什么" |
| B2 | 化学实验室 34 + 战斗遗迹 7 + 三神裔 9 + 特殊 4 | 54 | 长尾内容，一次补齐 |
| B3 | 现有 49 条评级 + 69 条无评级/远征的 `WikiContent` | 118 | 给已有条目补结构化怪表/船型/战利品 |

**验收**：`WHERE Category=3` 计数 = 126 且零空 `TitleEN`；抽 10 条与 wiki 原文逐层比对波次数一致。

### 4.5 P3 · UI 改造

| 改动 | 细节 |
|---|---|
| 分类筛选 | 左卡底部第二个下拉：全部 / 评级复合体 / 无评级复合体 / 远征 / 战斗异常 / 化学实验室 / 战斗遗迹 / 三神裔 / 特殊 |
| 难度筛选 | 第三个下拉：全部 / 1–10 评级 / 未定级 / 异常档 1–10（按 `Category` 动态换内容，避免无效组合） |
| 列表项 | 加**类别徽标**（彩色小标签）与**安等点**（高/低/00）；重名问题随之消失 |
| 右卡元信息条 | 标题下一行：`类别 · 势力 · 评级/档位 · 最大船型 · 安等 · 可升级为 · 触发来源` + "在 wiki 打开原文"按钮 |
| 正文双页签 | `社区攻略`（`Content`）/ `Wiki 数据`（`WikiContent`）；只有一侧有内容时自动隐藏另一个页签，并记住上次选择 |
| 空态 | 保留现有"暂无攻略正文"，但改为"该条目暂无社区攻略，可查看 wiki 数据"并自动切到有内容的页签 |
| 搜索 | `SearchAsync` 扩到 `TitleCN / TitleEN / WikiTitle` 三列；对空 `TitleCN` 加 `COALESCE` 保护 |
| AI 工具 | `ded_info` 输出改为 `【6/10】` / `【异常 T8】` / `【远征】` 三态；新增 `category` 参数；正文摘要优先取 `Content`、缺失时回落 `WikiContent` |

**遵守既有约定**（`REFACTORING.md` §2.1）：等待态用**页面内等待**（`IsBusy` + 局部遮罩），不用全局遮罩；
主题色一律 `DynamicResource`，不写字面量颜色；`DataGrid`/`ListBox` 用既有 `StretchListItem` 等隐式样式，不新造显式样式顶掉 Fluent 模板。

### 4.6 P4 · 正文双轨与增量同步

- **人工正文不动**：`Content` 只由人改；导入工具**永不写** `Content` 列。
- **wiki 内容可整段重刷**：`WikiContent` 由工具按 `WikiTitle` 覆盖，并更新 `WikiUpdatedAt`。
- **增量同步命令**：`--diff-only` 模式先比对 wiki 页面 revision 与本地 `WikiUpdatedAt`，只重抓变化的页（日常几十页以内，秒级）。
- **中文化策略（建议）**：
  - **结构化怪表保持英文 NPC 名**（游戏里就是英文，本地正文本来也是中英对照，无需翻译）；
  - 说明性段落**按需翻译**：复用现成的 AI 翻译链路（阶段 46/47），在右卡给一个"翻译本篇"按钮，**不做全量预翻译**（省 token、避免 AI 幻觉改坏数据）；
  - 译文缓存进 `Configs/WikiTranslation/<WikiTitle>.md`，跟随语言开关。

### 4.7 P5 · 文档、许可与清收

| 事项 | 内容 |
|---|---|
| **许可（重要）** | E-UNI Wiki 为 **CC BY-SA 4.0**。派生内容必须①署名 EVE University Wiki 并附链接，②以同协议共享。落地：右卡底部固定一行"数据来源：EVE University Wiki（CC BY-SA 4.0）"；`设置 → 关于` 加"第三方内容与许可"条目；`README` 加一节 |
| `REFACTORING.md` | 新增"阶段 N：死亡远征攻略补全"完整记录（含本次的 380 条盘点与差异表）；修正阶段 79 的"等级筛选"表述 |
| 代码注释 | 修 `DEDPageViewModel.cs` / `DEDPage.xaml` / `DEDPage.xaml.cs` 里"来自 `ded.db`"的三处错误描述 |
| 手册 | `Resources/Manuals/zh-CN/guides/ded.md` 增补：分类筛选、双页签、"Wiki 数据"的含义与许可、`ded_info` 的新行为；`updated` 字段同步 |
| 依赖资源 | 若确定引入 `WikiContent`，评估 `static.db` 体积增量（预估 +2–4 MB，可接受）；`ded.db` 删除后净减 348 KB |

### 4.8 工作量估算

| 阶段 | 内容 | 预估 |
|---|---|---|
| P0 | 建表 + 迁移脚本 + 兼容层 + 探针 | 1.5 天 |
| P1 | 8 条补录 + 20 余条纠错 + 删 `ded.db` | 1 天（含人工/翻译确认） |
| P2 | 抓取解析工具 + 180 条导入（B1 先行） | 2.5 天 |
| P3 | UI 分类/难度/元信息条/双页签 | 2 天 |
| P4 | 增量同步 + 按需翻译 | 1 天 |
| P5 | 文档/许可/手册 | 0.5 天 |
| **合计** | | **约 8.5 人日**（不含 17 条冷门正文的人工精修） |

### 4.9 风险与对策

| 风险 | 对策 |
|---|---|
| wiki 内容本身有 `{{Stub}}` / 错误（如红链、过期的 `Ship limit`） | `IsStub` 标记；界面对 stub 加"wiki 数据不完整"提示；保留"在 wiki 打开原文" |
| 抓取把 wiki 拖慢或封 IP | 限速 0.3 s、带自定义 User-Agent、单会话抓完；产物入库，**运行时永不联网** |
| CC BY-SA 的"传染性"影响项目许可 | 只在 `WikiContent` 字段与其展示区标注；人工 `Content` 不混入 wiki 译文原文；关于页写清边界 |
| 180 条新数据把左栏列表撑爆 | 默认只显示"评级复合体 + 无评级 + 远征"（= 现状 192 条），新类别**要靠分类下拉主动切换**才出现 |
| 双页签让右卡变复杂 | 单侧有内容时不显示页签栏，直接铺满（多数条目只有一侧有内容） |
| 迁移判错类别（无评级 vs 远征） | 以 wiki 名称表为权威；迁移脚本输出"对不上的清单"要求人工确认后才允许 `--commit` |

---

## 5. 验收清单

- [ ] `CombatSite` 表存在且 `SELECT Category, COUNT(*)` 与 §3.1 的 wiki 口径一致（380 条）
- [ ] 原有 192 条全部迁移成功，`Content` 逐字节未变（diff 校验）
- [ ] FSP 仍显示 `9/10`（习惯叫法）；「未定级」不再在 AI 输出里打印成 `0/10`
- [ ] 5 条空中文名补齐；中文搜索可命中全部 192 + 180 条
- [ ] 无重名；无尾随空格
- [ ] `ded.db` 已删除，`DEDPageViewModel` 等三处注释已改正
- [ ] 战斗异常列表可见"可升级为 X/10"且点击可跳到该 DED 条目
- [ ] 右卡元信息条字段齐全（类别/势力/评级/船型/安等/升级/来源）
- [ ] 双页签：只有一侧有内容时不显示页签栏
- [ ] 深/浅主题下元信息条与徽标配色正确（全部 `DynamicResource`）
- [ ] `ded_info` 三态标签正确，`category` 参数可用
- [ ] CC BY-SA 署名出现在右卡底部 + 关于页 + README
- [ ] `dotnet build` 0 错误；`REFACTORING.md` / 手册 / 注释三处文档同步

---

## 附：本次核实的口径说明

- wiki 数据全部来自 `Combat_site` **revision 230825（2026-05-15）**的 `action=raw` 与 `action=parse&prop=text`，
  表格用 HTML 解析器（正确处理 `rowspan / colspan`）而非正则切分，故"126 / 74 / 50 / 76 / 34"这些数字是**解析后去重**的结果。
- 本地数据全部来自 `Resources/Database/static.db` 的 `DED` 表直读，并与 `Resources/Database/ded.db` 做过逐行比对（完全一致）。
- "缺失 8 条"是**按表元组逐条比对**得出，不是凭印象；`Shadow Serpentis` 两条在 wiki 上是红链，已单独标注。

---

## 实施状态（2026-11-17，对应 REFACTORING.md 阶段 351）

**已落地**：P0 数据模型（`CombatSite` 表 + `CombatSiteService`，`DED` 表原样保留）、P1 补齐与纠错、
P2 全量导入（379 条，正文全部中文化）、P2.5 **旗舰远征 12 条（来自游戏 SDE）**、P3 界面改造、
P5 文档/许可/手册；`ded.db` 已删除。
`Tools/wiki-import/` 是可重跑的离线管线（抓取 → 解析 → 分批翻译 → 合并 → 落库；旗舰远征走 `extract_sde_dungeons.py`）。

### 补充：旗舰远征（wiki 没有，改从 SDE 取）

- wiki 的 `Combat site` 页**不把** `<Faction> Capital Staging` / `Shielded Starbase` /
  `Drone Capital Hive` / `Drone Infested Starbase` 列成条目，只在个别异常的"可升级为"里提过，
  所以按 wiki 抓永远抓不到。正确数据源是游戏 SDE 的 **`dungeons.jsonl`**（1416 条，含 `name.en` / `name.zh` 与八语种描述）。
- 判定规则：`archetypeID == 31`（Escalation）**且**名字匹配 `Capital Staging` / `Capital Hive` /
  `Shielded Starbase` / `Infested Starbase`，排除 `Escalation Test 3/4`。
  **只按中文名搜会误收**（搜「旗舰」13 条里 7 条不是旗舰远征；搜「母星」会带上测试条目）。
- 新增第 9 个分类 `CapitalExpedition = 8`：12 条（5 势力 × 集结地 + 5 势力 × 母星，无人机另占两项）。
  正文 = 官方中文描述 + 触发关系（仅 3 条 wiki 有记载）+ 说明；`IsStub = 1`。
- **待定**：SDE 能覆盖现有 84.7% 的英文名，其中 **175 条中文名与官方不同**（官方名更贴近客户端），
  但整体替换会重新造出重名（`Sansha Military Complex` 与 `Sansha Military Operations Complex` 官方同名），
  本轮只把官方名用在新增 12 条上，切换与否留待决定。

**未做**：P4 的**增量同步**（日常只重抓 revision 变化的页）与"按需翻译"按钮——当前刷新一次就是全量重跑；
以及实机截图核验（按 §8 约定，观感由使用者确认）。

**与本文档原始设想的差异（如实记录）**：

1. 原计划删掉 `DED` 表并把页面完全迁到新表；实际**保留 `DED` 表与 `DEDService`**（WinUI 版仍在用），
   WPF 侧只新增 `CombatSite` 读路径，渐进迁移、回滚成本为零。
2. 原计划"FSP 的 9/10 是缺陷、要改成无评级"——**用户指出这是游戏习惯叫法，已按 `Rating = 9` 实现**（见 §0 口径澄清）。
3. 原文说"预告 24 条正文过简需要人工精修 17 条"——实际这 24 条里有 17 条 **wiki 侧资料齐全**，
   直接用抓取内容补齐即可，只有少数几条确实需要人工（本轮未开工单）。
4. 战斗遗迹的 `Crumbling Ruins` 在 wiki 上是红链，**没有建条目**（清单里 7 条 → 库里 6 条）。
