# WPF 比价（CompareMarket）设计方案

> 状态：**设计稿，待确认后实施**（本文不含已实现内容）。
> 前置阅读：`REFACTORING.md` §10「商业模块（阶段 25 起：市场 / 订单；阶段 28：倒货；阶段 34：估价）」。
> 定位：商业分组下的**第 5 个功能页**（市场 / 订单 / 倒货 / **比价** / 估价），与「倒货」同源但目标不同（见 §2）。
> 命名：中文「比价」，英文「Price Compare」，导航键 `Nav.CompareMarket`，页面类 `CompareMarketPage`，
> 代号统一用 `CompareMarket*`（不叫 `ComparePage`，避免与未来"角色对比"之类混淆）。

---

## 1. 目标与需求映射

用户原话（逐条拆解，后文规格按序号对应）：

| # | 需求 | 本设计的落点 |
|---|---|---|
| R1 | 批量查询价格，匹配最佳市场 | 物品 × 市场 矩阵 + 每行「最优买入市场 / 最优卖出市场」两列（§5.4） |
| R2 | 可选择多个市场：星系、星域、建筑 | 复用 `MarketLocationSelectorView`，市场列表可增删（§6.2） |
| R3 | 可选择、粘贴多个物品 | 复用 `MarketSelecteTreeView`（选择）+ `AppraisalTextParser`（粘贴）（§6.2） |
| R4 | 按卖单/买单找出各个市场物品价格 | 每个市场每物品算**两个**参考价：卖单侧价 / 买单侧价（§5.1） |
| R5 | 表格显示各个市场价格 | 每个市场**两列**（`市场名 卖` / `市场名 买`），动态构造（§6.5） |
| R6 | 显示最优分布：哪些物品在哪个市场买入/卖出最佳 | 「最优买入市场」「最优卖出市场」列 + 最优单价 + 较次优优势%（§5.4） |
| R7 | 显示总支出/收入 | 汇总条：总收入 / 总支出 / 净额 / 总体积（§5.5） |
| R8 | 两类物品可同时参与计算：买入物品、卖出物品，可互换 | 物品分两组 + 一键/单行互换（§6.2、§6.4） |
| R9 | 方便找到手上物品的最优出货地点，以及各地点间差异 | 每市场两列铺开 + 「较次优优势%」列（§5.4） |
| R10 | 判断是否值得将货物放到不同地方卖（差距不大就一地卖） | **集中度分析**：给出"分地卖 vs 集中一地卖"的差额与差率，并按阈值给建议文案（§5.6） |
| R11 | 买入同理（差距不大就一地买） | 集中度分析的买入侧对称实现（§5.6） |
| R12 | 类似倒货，快速比较多个贸易点间存在利润差的物品 | **利差机会**页签：任意「A 市场买 → B 市场卖」组合按利差排序（§5.7） |

---

## 2. 与倒货 / 估价的边界（为什么是新页面而不是扩展现有页）

| 维度 | 倒货（`ScalperPage`） | 估价（`AppraisalPage`） | **比价（本设计）** |
|---|---|---|---|
| 市场数量 | 固定 2（源 → 目的） | 固定 1 | **N 个（1..12）** |
| 物品数量 | 多选（无数量概念） | 粘贴（名称 + 数量） | **选择 + 粘贴（名称 + 数量，且数量可编辑）** |
| 取数范围 | **整星域全量**（不知会选什么物品） | 按物品逐个取单 | **按物品逐个取单**（已知物品集合） |
| 价格口径 | 订单 + 历史（10 种） | 订单 + 历史（10 种） | **只做订单口径（6 种）**，理由见 §10 |
| 输出 | 18 列推荐表（推荐度/回报率/销量…） | 明细表 + 买卖总额 | **价格矩阵 + 最优分布 + 集中度建议 + 利差机会** |
| 核心问题 | "这两地之间倒什么最赚" | "这堆货值多少" | **"这批货在哪个地方出手最划算、值不值得分开跑"** |

三者共用 `MarketOrderService`（取数/缓存/富化）、`MarketLocationSelectorView`（选市场）、
`ScalperSetting.PriceType`（价格口径枚举），互不侵入。

---

## 3. 可以直接复用的资产（不新造）

| 能力 | 现成资产 | 比价用途 |
|---|---|---|
| 市场位置选择（星域/星系/建筑 + 搜索） | `Views/UserControls/MarketLocationSelectorView`（`SelectedItem` DP） | 市场列表每行一个选择器 |
| 物品多选（分组三态树 + 搜索） | `Views/UserControls/MarketSelecteTreeView`（`SelectedItems: List<int>`） | 物品挑选器（两个实例：卖出 / 买入） |
| 粘贴文本解析 | `Services/Business/AppraisalTextParser`（Tab / `x数量` / `数量x` / 行尾数字 / 兜底） | 粘贴多个物品 |
| 名称 → 类型 ID | 估价页的 4 级解析（主库精确 → 本地化库精确 → 主库模糊 → 本地化库模糊），**本轮抽成公共类**（§8） | 粘贴物品定位 TypeId |
| 订单取数/缓存/富化 | `Services/Business/MarketOrderService`（`GetRegionOrdersAsync` / `GetStructureOrdersAsync` / `GetHistoryBatchAsync`；`RegionOrders`、`StructureOrders`、`HistoryOrders` 缓存） | 全部取价 |
| 价格口径与算价 | `ScalperSetting.PriceType` + `ScalperCalculator.CalPrice*` 的既定口径（前 5% 均价 / 逐档吃单 / 最优价） | 卖单侧 / 买单侧参考价 |
| 全局等待遮罩 + 右下角通知 | `PageNotifyService`（`ShowWaiting` / `UpdateWaiting` / `Cancel` 回调 / `Success` / `Error`） | 取数进度（应用级单实例操作，与倒货/估价一致） |
| 卡片外壳 | `Controls/CardControl` | 汇总条、建议条 |
| 表格 | `ui:DataGrid` + 三个表格页既有的 `*CellStyle` 写法 | 矩阵表 / 利差表 |
| 设置持久化 | `ScalperSettingService` / `AppraisalSettingService` 的写法（`Configs/*.json`） | `Configs/CompareMarketSetting.json` |

---

## 4. 数据流与取数策略

### 4.1 总流程

```
[用户] 选 N 个市场 → 挑选/粘贴物品 → 分成 卖出组 / 买入组（数量可编辑）
   │
   ├─(点「开始比价」)→ 校验 → 保存设置
   │
   ▼
[CompareMarketService.RunAsync]
   1. 名称 → TypeId（仅粘贴来的物品需要；树选择的已是 TypeId）
   2. 逐市场取单（市场串行、物品并发）→ 每个市场得到 typeId → 订单列表
   3. 每个 (市场, 物品) 算两侧参考价 → 填入矩阵 Cell
   4. 每行算：最优买入市场 / 最优卖出市场 / 优势% / 买卖价差
   5. 汇总：总收入 / 总支出 / 净额 / 总体积 / 无报价计数
   6. 集中度分析（卖出侧、买入侧）
   7. 利差机会（每物品保留利差率 Top 3 组合）
   │
   ▼
[CompareMarketPageViewModel] → 结果表（动态列）+ 汇总条 + 建议条 + 利差页签
```

### 4.2 取价策略（**逐个物品取单**，不整星域全量）

比价与倒货最大的取数差异：**物品集合在取数前就已知**，所以按物品调单物品接口，数据量小两三个数量级。

| 市场类型 | 取数调用 | 说明 |
|---|---|---|
| 星域 | `GetRegionOrdersAsync(typeId, regionId)` | 单物品星域订单（星域接口本身已含空间站与建筑卖单）；PLEX 由服务内部 `NormalizeRegion` 自动切到全球星域 19000001 |
| 星系 | 同上 → `.Where(p => p.SystemId == location.SolarSystemId)` | 与估价页完全同款（先按物品取星域、再按星系过滤） |
| 建筑 | `GetStructureOrdersAsync(structureId)` **整建筑全量一次**，再按 `TypeId` 分组 | 结构接口没有"按物品"参数；有 TTL 缓存（`Configs/StructureOrders/{id}.json`，TTL 同市场设置的订单有效期），首次可能较慢（7 MB 级）；需角色授权（服务内部自动选角色） |

**并发模型：市场串行、物品并发。**

- 理由：跨市场并发会让总并发 = 市场数 × 物品数，容易触发 ESI/Cloudflare 限流；而单个市场内部按物品并发（线程数取 `MarketOrderSettingService.ThreadValue`，与倒货/估价一致）已足够快。
- 同一星域下选了多个星系市场时会重复取单（罕见）；第一版不做跨市场去重，列入 §10。

### 4.3 进度与容错

- 进度结构：`CompareProgress { int MarketIndex; int MarketCount; string MarketName; int Done; int Total; }`，
  VM 拼成本地化文案（`市场 2/4：吉他 — 12/30 物品`）后交给 `PageNotifyService.UpdateWaiting`。
  服务层不依赖 UI（沿用估价页 `Action<int,int> progress` 的做法，只是带上市场维度）。
- **单个市场取数失败不中断整体**：记录 `CompareMarketFailure { MarketIndex, MarketName, Reason }`，
  该市场列照常生成（全格显"取数失败"），完成后在通知/警告条里汇总报告（如"2 个市场取数失败：建筑 A（无权限）、建筑 B（超时）"）。
  这条与估价页对建筑失败的处理同源（`StructureOrdersFailed`）。
- 取消：整条链路带 `CancellationToken`，遮罩上给「取消」（与倒货页一致）；取消后不产生半成品结果。

---

## 5. 价格口径与公式（精确定义）

### 5.1 每个 (市场, 物品) 的两个参考价

| 字段 | 含义 | 默认口径 | 取值规则 |
|---|---|---|---|
| `SellPrice` | **卖单侧价**：该市场现有卖单的参考价（"我挂卖单要跟这些人竞争"） | `SellTop5` | 该市场该物品**卖单**按价升序，取最低价侧 |
| `BuyPrice` | **买单侧价**：该市场现有买单的参考价（"我砸给买家能拿到的价"） | `BuyTop5` | 该市场该物品**买单**按价降序，取最高价侧 |

口径可选（只列订单类，复用既有键文案）：

| 枚举 | 文案键（已存在） | 算法（与 `ScalperCalculator` 一致） |
|---|---|---|
| `SellTop5` / `BuyTop5` | `BusinessPage_SellTop5` / `BusinessPage_BuyTop5` | 最优价侧订单的前 5% 均价；不足 2 条取首条 |
| `SellTop` / `BuyTop` | `BusinessPage_SellTop` / `BusinessPage_BuyTop` | 排序后首条（最低卖价 / 最高买价） |
| `SellAvailable` / `BuyAvailable` | `BusinessPage_SellAvailable` / `BusinessPage_BuyAvailable` | 按**该物品的数量**逐档吃单的加权均价 |

同时记录 `SellOrderCount` / `SellVolumeRemain` / `BuyOrderCount` / `BuyVolumeRemain`，用于表格 ToolTip 与"该市场有没有量"的判断。

### 5.2 出货 / 进货方式（决定用哪一列）

| 设置 | 取值 | 说明 |
|---|---|---|
| 卖出方式 `SellMode` | `MakeOrder`（挂卖单，默认）/ `TakeOrder`（直接卖给买单） | 挂卖单 → 用 `SellPrice`；卖买单 → 用 `BuyPrice` |
| 买入方式 `BuyMode` | `TakeOrder`（从卖单买，默认）/ `MakeOrder`（挂买单等成交） | 从卖单买 → 用 `SellPrice`；挂买单 → 用 `BuyPrice` |

- `EffectiveUnitPrice`（该物品在其角色下的实际单价）：
  - 卖出组：`SellMode == MakeOrder ? SellPrice[bestSellMarket] : BuyPrice[bestSellMarket]`
  - 买入组：`BuyMode == TakeOrder ? SellPrice[bestBuyMarket] : BuyPrice[bestBuyMarket]`
- 缺省组合（挂卖单卖出 + 从卖单买）语义直观、也是估价页的缺省口径，因此作为默认。

### 5.3 税费

- `SellTaxPercent`（默认 **0**，即有税不计）：**只作用于卖出侧**，`到手价 = 参考价 × (1 − SellTaxPercent / 100)`。
- 买入侧不扣（买家的经纪费体现在挂单成交后，第一版不建账）。
- 设置项说明文案要写清"这是销售税 + 经纪费的合计，按自己技能填（如 3.6 / 5.0）"，避免用户以为系统已自动算税。

### 5.4 最优市场与"较次优优势"

对每个物品（**与它在哪一组无关，两列都算**）：

- **最优卖出市场** `SellMarketIndex` = 按 `SellMode` 选取的价格最高的市场；
- **最优买入市场** `BuyMarketIndex` = 按 `BuyMode` 选取的价格最低的市场；
- `SellUnitPrice` / `BuyUnitPrice` = 上述两个市场的对应单价；
- **`AdvantagePercent`（较次优优势%）** = 相对**次优市场**的优势：
  - 卖出组：`(最优 − 次优) / 最优 × 100`
  - 买入组：`(次优 − 最优) / 最优 × 100`
  - 语义统一为"最优点比次优点好多少个百分点"。市场数 < 2 或次优无价 → `null`（显示 `—`）。
- **`SpreadUnit` / `SpreadPercent`（买卖价差）** = `SellUnitPrice − BuyUnitPrice` 与其占买价的比例 —— 一行即可看出"这件物品在当前市场集合里有没有倒货空间"。
- **`Subtotal`** = `EffectiveUnitPrice × Amount`（卖出组 = 预期收入，买入组 = 预期支出）。
- 有价判定：该市场该物品两侧都无价 → `HasQuote = false`（表格显示 `—`，且不计入汇总）。

### 5.5 汇总（R7 / R9）

| 汇总项 | 定义 |
|---|---|
| 总收入 | `Σ 卖出组 Subtotal`（只统计有报价的行） |
| 总支出 | `Σ 买入组 Subtotal`（只统计有报价的行） |
| 净额 | 总收入 − 总支出 |
| 总体积 | `Σ (单位打包体积 × 数量)`，两组都算（判断货柜/运费；单位体积用 `PackagedVolume > 0 ? PackagedVolume : Volume`，与估价页同口径） |
| 无报价 | `NoQuoteCount` 与具体物品名列表（**必须显式提示**，否则汇总会静默漏算） |

### 5.6 集中度分析（R10 / R11，本功能的核心差异化）

同一个数字——"这批货一共能卖多少"——在"分散到各自最优"与"全放在某一个市场"下是不同的。**两者的差额就是"值不值得分开跑"的答案。**

**卖出侧：**

- 各自最优收入 `BestIncome = Σ_i ( q_i × max_j p_ij )`（只统计 `max_j p_ij > 0` 的物品，即"可卖物品"集合 `S_all`）
- 对每个市场 j，**只用"在 j 有报价的物品"子集 `S_j` 做自洽比较**：
  - 单市场收入 `Income_j = Σ_{i ∈ S_j} ( q_i × p_ij )`
  - 该子集的各自最优收入 `BestIncome_j = Σ_{i ∈ S_j} ( q_i × max_m p_im )`
  - 集中损失 `Loss_j = BestIncome_j − Income_j`，损失率 `LossPercent_j = Loss_j / BestIncome_j × 100`
- `BestSingleMarket` = `LossPercent_j` 最小的市场；`CoversAll = (S_j == S_all)`；
  覆盖不全时**必须提示**："该市场只覆盖 n/N 件可卖物品，未覆盖部分未计入"。
- **建议文案（阈值 `SplittingThresholdPercent`，默认 2%）**：
  - `LossPercent ≤ 阈值` → "差距仅 X%，**不值得分市场出货**，在「Y」一地卖即可"
  - 否则 → "分市场出货多赚 `Loss` ISK（+X%），**建议分开卖**"

**买入侧（对称）：**

- `BestExpense = Σ_i ( q_i × min_j c_ij )`；市场 j 的 `Expense_j`、`Loss_j = Expense_j − BestExpense_j`、`LossPercent_j`
- `BestSingleMarket` = 损失率最小者；文案换成"不值得分市场采购 / 建议分开买"。
- `q_i` 取该行当前数量（数量可编辑，见 §6.4）。

### 5.7 利差机会（R12，"倒货式"视角）

对**矩阵里的全部物品**（与分组无关），穷举 `买入市场 a × 卖出市场 b (a ≠ b)`：

| 口径 `SpreadPriceMode` | 买价 | 卖价 | 语义 |
|---|---|---|---|
| `MakeOrder`（挂单，默认） | 市场 a 的 `BuyPrice`（挂买单收） | 市场 b 的 `SellPrice`（挂卖单卖） | 利差最大，两端都要等成交（倒货常识口径） |
| `TakeOrder`（吃单） | 市场 a 的 `SellPrice`（直接吃掉卖单） | 市场 b 的 `BuyPrice`（直接砸给买单） | 即时成交，利差最小 |

- `UnitSpread = 卖价 − 买价`；`SpreadPercent = UnitSpread / 买价 × 100`；`TotalSpread = UnitSpread × q_i`
- 过滤：`UnitSpread > 0` 且 `q_i > 0`
- **每个物品只保留 `SpreadPercent` 最高的 3 个组合**（避免 `物品数 × M × (M−1)` 爆炸；20 市场 × 100 物品 = 38000 组合），
  全局按 `SpreadPercent` 降序，最多显示 500 行。
- 页面提供口径切换下拉（挂单 / 吃单），切换后**纯内存重算**（无需重新取数）。

---

## 6. UI 设计

### 6.1 整体布局

沿用商业模块既有的「左设置 + 右结果」两栏（左 360，右 `*`）；**页面内不显示大标题**（§UI 约定）。

```
┌──────────────────────────────┬───────────────────────────────────────────────────────────┐
│ 左栏 360                     │ 右栏 *                                                     │
│ ┌──────────────────────────┐ │ ┌─ CardControl 汇总条 ────────────────────────────────────┐ │
│ │ TabControl               │ │ │ 总收入 1,234,567.89 │ 总支出 456,789.00 │ 净额 +777,778 │ │
│ │ ┌ 市场 ────────────────┐ │ │ │ 总体积 12,345 m³   │ 无报价 3 件                        │ │
│ │ │ [选择器] 吉他   [✕]  │ │ │ └───────────────────────────────────────────────────────┘ │
│ │ │ [选择器] 皮尔米特 [✕]│ │ │ ┌─ 建议条（集中度）──────────────────────────────────────┐ │
│ │ │ [选择器] 建筑A   [✕] │ │ │ │ ⓘ 分市场出货可多赚 89,000 ISK（+7.2%），建议分开卖      │ │
│ │ │ [+ 添加市场]         │ │ │ │   买入：集中在「吉他」即可（差距 0.8%，未超 2% 阈值）   │ │
│ │ └──────────────────────┘ │ │ └───────────────────────────────────────────────────────┘ │
│ │ ┌ 物品 ────────────────┐ │ │ ┌─ 结果区（TabControl）──────────────────────────────────┐ │
│ │ │ 粘贴区（多行）       │ │ │ │ [最优分布] [利差机会]                                  │ │
│ │ │ 导入到: 卖出▾ [导入] │ │ │ │ ┌ DataGrid（物品×市场 矩阵）─────────────────────────┐ │ │
│ │ │ [⇄ 两组互换]         │ │ │ │ │ 物品 | 组 | 数量 | 体积 | 最优买入 | 最优卖出 |…   │ │ │
│ │ │ ┌ 卖出物品 …树… ────┐│ │ │ │ │ 吉他 卖 | 吉他 买 | 皮尔米特 卖 | 皮尔米特 买 | …  │ │ │
│ │ │ │                  ││ │ │ │ │ ...                                                │ │ │
│ │ │ └──────────────────┘│ │ │ │ └────────────────────────────────────────────────────┘ │ │
│ │ │ [↙ 加入卖出清单]    │ │ │ └───────────────────────────────────────────────────────┘ │
│ │ │ ┌ 买入物品 …树… ────┐│ │                                                             │
│ │ │ └──────────────────┘│ │                                                             │
│ │ │ [↗ 加入买入清单]    │ │                                                             │
│ │ └──────────────────────┘ │                                                             │
│ │ ┌ 口径 ────────────────┐ │                                                             │
│ │ │ 卖单侧口径 [SellTop5▾]│ │                                                             │
│ │ │ 买单侧口径 [BuyTop5▾] │ │                                                             │
│ │ │ 卖出方式 [挂卖单▾]    │ │                                                             │
│ │ │ 买入方式 [从卖单买▾]  │ │                                                             │
│ │ │ 卖出税费 [0] %        │ │                                                             │
│ │ │ 分地阈值 [2] %        │ │                                                             │
│ │ │ 利差口径 [挂单▾]      │ │                                                             │
│ │ └──────────────────────┘ │                                                             │
│ └──────────────────────────┘ │                                                             │
│ [开始比价] [取消]            │                                                             │
└──────────────────────────────┴───────────────────────────────────────────────────────────┘
```

### 6.2 左栏细节

**「市场」页签**

- `ItemsControl` 绑定 `Markets`（`ObservableCollection<MarketLocation>`），每行 = `MarketLocationSelectorView` + 「✕」删除按钮。
- 「＋ 添加市场」在最底部追加一个**未选择**的占位行（`SelectedItem = null`，选择器显示"未选择市场"）；用户在弹层里选中即成为真实市场。
  - 添加时**去重**（同 `Type` + `Id` 已存在则提示"该市场已在列表中"，不追加）。
  - 上限 **12** 个（超出给提示，避免误操作与取数时长失控）。
- 市场数量 < 2 时允许比价，但建议条提示"只选了 1 个市场，无法比较地点差异"。

**「物品」页签**

- **粘贴区**：多行 `ui:TextBox`（`AcceptsReturn`，`TextWrapping=Wrap`，`UpdateSourceTrigger=PropertyChanged`，与估价页输入区同款）；
  旁边「导入到：[卖出组 ▾]」下拉（记忆上次选择，落设置）+ 「导入」按钮。
  - 导入流程：`AppraisalTextParser.Parse` → 逐行过滤空行 → **标记待解析的名称**（名称→TypeId 在取数时统一解析，导入当下只做"能立刻解析的就填名字"）。
    为让用户在导入后**立刻看到物品名**，导入时同步做一次本地解析（`MarketTypeNameResolver`，纯本地库、无网络，几十条毫秒级）；
    解析不到的进入 `UnresolvedNames`，导入后在提示条列出（不静默丢弃）。
  - 重复物品：同 TypeId 已在**任一组**时，按"数量累加 + 归属以本次导入目标组为准（从原组移走）"处理 —— **同一物品只允许出现在一组**（见 §10）。
- **两个物品树**：`MarketSelecteTreeView` × 2（卖出组 / 买入组），各自带搜索框。
  - **树只做"挑选器"**，不直接等于清单：勾选若干 → 点「↙ 加入卖出清单」/「↗ 加入买入清单」→ 加入清单（数量默认 1）→ 树勾选清空。
  - 为什么这样设计（重要，实施时别改回去）：`MarketSelecteTreeView.SelectedItems` 是**就地增删**的 `List<int>`，
    控件改列表时**不会发通知**（倒货页靠 `SelectedItemsCount` 这个 TwoWay DP 只刷新"数量"显示，VM 拿不到"具体哪个被加了/删了"）。
    若把树当事实来源，就无法把"清单行的数量编辑 / 移组 / 删除"回写到树。因此**清单（右表行）是唯一事实来源，树只是入口**。
  - 清空树：把 `SelectedItems` 换成新的 `List<int>()`（DP 已实现 `ApplySelectedItems`，会清掉所有勾选）。
- **「⇄ 两组互换」**：交换两组的全部物品（`Group` 枚举逐行翻转），数量不变；树勾选清空（避免残留状态）。
- 每行还可**单行移组**（右表行上的按钮/右键菜单），见 §6.4。

**「口径」页签**

- 7 个设置项（§5.2/5.3/5.6/5.7 + 两个口径下拉），全部即时保存（切换即 `Save()`，与倒货页"改设置就 `SaveSetting`"的习惯一致）。

**底部按钮**

- 「开始比价」（`Appearance=Primary`）+ 「取消」（仅在 `IsBusy` 时可见/可用）。

### 6.3 右栏细节

1. **汇总条**（`CardControl`）：总收入 / 总支出 / 净额 / 总体积 / 无报价件数。
2. **建议条**（`CardControl`，两行）：
   - 卖出建议（含"不值得分地卖"或"建议分开卖"，及具体差额与差率）；
   - 买入建议（对称）；
   - 附加提示：市场取数失败数、无报价物品名、未识别名称。
   - 颜色语义：`LossPercent ≤ 阈值` → 次要色（`TextFillColorSecondaryBrush`）；超过 → 成功色（`SystemFillColorSuccessBrush`）；失败/无报价 → 警告色（`SystemFillColorCautionBrush`）。
3. **结果区 = `TabControl`（两个页签）**：
   - **「最优分布」**：矩阵 `DataGrid`（§6.5）；
   - **「利差机会」**：利差表（物品 / 买市场 / 买价 / 卖市场 / 卖价 / 单位利差 / 利差率 / 数量 / 总利差），顶部一个口径下拉。

### 6.4 交互清单（右表行）

| 交互 | 行为 |
|---|---|
| 数量列直接编辑 | 行 `Amount` 变更 → 该行 `Subtotal`/`SpreadUnit`/`SpreadPercent` 与汇总、集中度、利差总额**即时重算**（纯内存） |
| 右键「移到买入组 / 移到卖出组」 | 翻转该行 `Group` + 重算汇总 |
| 右键「从清单移除」 | 删除该行（不影响树勾选；想再添加就再勾一次） |
| 右键「复制物品名」 | 剪贴板（便于回游戏里搜） |
| 双击行 | 第一版**不做**详情窗（§12）；预留：双击 = 跳市场页（`Navigation`，但市场页尚不支持带物品选中，见 §10） |
| 列头点击 | 由 `DataGrid` 自带排序承担（数值列按数值排序） |

**"数量已变更需重新比价"的提示**：若卖/买口径含 `*Available`（按数量逐档吃单），数量变更后价格本应变化，
但即时重算只会改金额、不会重取订单 → 显示 `NeedsRecompare` 提示条："数量已变化，当前口径按数量取价，请重新比价以刷新价格"。

### 6.5 表格列构造（技术要点，实施时按此写）

**固定列（11 列）**：

| 列 | 绑定 | 宽度 |
|---|---|---|
| 物品 | `Name` | `*`（`MinWidth=160`，用于铺满视口） |
| 组 | `Group`（经 `CompareItemGroupConverter` → 本地化"卖出/买入"） | 70 |
| 数量 | `Amount`（`N0`，**可编辑**） | 80 |
| 体积 | `Volume`（`N2`） | 90 |
| 最优买入市场 | `BestBuyMarketText`（如 `吉他 · 卖`） | 150 |
| 最优卖出市场 | `BestSellMarketText`（如 `皮尔米特 · 卖`） | 150 |
| 最优单价 | `EffectiveUnitPrice`（`N2`） | 110 |
| 较次优优势% | `AdvantagePercent`（`PercentOrDash` 转换器） | 100 |
| 买卖价差 | `SpreadUnit`（`N2`） | 100 |
| 价差率 | `SpreadPercent`（`PercentOrDash`） | 90 |
| 小计 | `Subtotal`（`N2`） | 130 |

**动态列（每个市场 2 列）**：

- 列头 = 两行文本：第一行市场名（超长省略 + `ToolTip` 全名与类型），第二行 `卖` / `买`（本地化键后缀）；
- 绑定 = `Cells[{i}].SellPrice` / `Cells[{i}].BuyPrice`，`StringFormat={}{0:N2}`，无价显示 `—`（`PriceOrDash` 转换器或 `HasQuote` 判定）；
- `ElementStyle` 复用页面级 `CompareNumberCellStyle`（与倒货/估价同款：主前景色、右对齐、`Margin`）。

**关键实现约束（有踩坑史，勿简化）**：

1. **不要用 `DataGridTemplateColumn` + `XamlReader` 动态模板**，也**不要动态构造 `Style`/`DataTrigger`** —— 都用固定列 + 纯 `DataGridTextColumn`（动态构造的只是 `Binding` 对象，零 XAML 解析风险）。
2. **不要给 `DataGrid` 套外层 `ScrollViewer`**（§9 第 49 条：外层无限高度测量会废掉行虚拟化，几百行 × 二十几列会一次性实例化上万元素而卡死）。
   列铺满由「物品列 `Width="*"`」承担，横向滚动交给表格自身（`ScrollViewer.HorizontalScrollBarVisibility="Auto"` + `ScrollViewer.CanContentScroll="True"`）。
   这与倒货/估价页的 `ScrollViewer + MinWidth=ViewportWidth` 写法**有意不同**（那两页列少、行少，这里列多）。
3. **列在"比价完成"与"语言切换"两个时点重建**：`RebuildColumns()`（先 `Clear()` 再依次 `Add`）。
   列头里的 `卖`/`买` 是**求值时拼好的字符串**，不会随语言自动变（§9 第 63 条），所以换语言必须重建列；
   VM 订阅语言变化 → 抛 `ColumnsInvalidated` 事件 → 页面 `RebuildColumns()`。
4. 有价/无价、最优市场的文字（`BestBuyMarketText` 等）由 **VM 求值属性**提供（`=> ...FindString(...)` 或纯数值拼接），
   依赖 `LanguageService.RefreshAllBindings` 的重算机制刷新；**不要在 VM 里缓存本地化字符串**（§9 第 18 条末段、第 63 条）。

---

## 7. 模型定义（字段级）

### 7.1 设置（WPF 侧，`Services/Business/CompareMarketSettingService.cs` 内定义，与估价页同放法）

```csharp
public sealed class CompareMarketSetting
{
    public List<MarketLocation> Markets { get; set; } = new();          // 复用 Core 的 MarketLocation
    public List<CompareMarketItemSetting> Items { get; set; } = new();  // 清单（含数量与分组）
    public ScalperSetting.PriceType SellPriceType { get; set; } = ScalperSetting.PriceType.SellTop5;
    public ScalperSetting.PriceType BuyPriceType  { get; set; } = ScalperSetting.PriceType.BuyTop5;
    public CompareTradeMode  SellMode { get; set; } = CompareTradeMode.MakeOrder;   // 挂卖单
    public CompareTradeMode  BuyMode  { get; set; } = CompareTradeMode.TakeOrder;   // 从卖单买
    public double SellTaxPercent { get; set; }                                      // 默认 0
    public double SplittingThresholdPercent { get; set; } = 2;                      // 分地阈值 %
    public CompareSpreadPriceMode SpreadPriceMode { get; set; } = CompareSpreadPriceMode.MakeOrder;
    public CompareItemGroup LastPasteTarget { get; set; } = CompareItemGroup.Sell;  // 记忆粘贴目标
}

public sealed class CompareMarketItemSetting
{
    public int TypeId { get; set; }
    public double Amount { get; set; } = 1;
    public CompareItemGroup Group { get; set; }
}

public enum CompareItemGroup { Sell, Buy }
public enum CompareTradeMode { MakeOrder, TakeOrder }   // 挂单 / 吃单
public enum CompareSpreadPriceMode { MakeOrder, TakeOrder }
```

- 持久化 `Configs/CompareMarketSetting.json`（WPF 专有；不碰 WinUI 与 Core）。
- `Load()` 要做的**防御性修补**（沿用倒货/估价的做法）：
  - `Markets` 里 `SolarSystemId == 0` 的位置补齐（星域取首个星系、建筑取结构所在星系）；
  - 树/清单的 `TypeId` 若本地库查不到 → 保留但标记（实施时读到时跳过并记日志）；
  - 历史口径枚举值（若用户手改过 JSON）→ 回落默认订单口径。

### 7.2 运行时模型（`ViewModels/Business/CompareMarketModels.cs`）

```csharp
public sealed class CompareMarketCell
{
    public double SellPrice { get; init; }        // 卖单侧价（0 = 无）
    public double BuyPrice { get; init; }         // 买单侧价（0 = 无）
    public int SellOrderCount { get; init; }
    public long SellVolumeRemain { get; init; }
    public int BuyOrderCount { get; init; }
    public long BuyVolumeRemain { get; init; }
    public bool HasQuote => SellPrice > 0 || BuyPrice > 0;
}

public sealed class CompareMarketRow : INotifyPropertyChanged
{
    public int TypeId { get; init; }
    public string Name { get; init; }
    public double UnitVolume { get; init; }
    public CompareItemGroup Group { get; set; }            // 可改（移组）
    public double Amount { get; set; }                     // 可改（表格编辑）→ 触发 VM 重算
    public List<CompareMarketCell> Cells { get; init; }     // 与 Markets 同序
    public int BuyMarketIndex { get; init; }                // -1 = 无
    public int SellMarketIndex { get; init; }
    public double BuyUnitPrice { get; init; }
    public double SellUnitPrice { get; init; }
    public double EffectiveUnitPrice { get; init; }         // 按角色选出的价
    public double Subtotal => EffectiveUnitPrice * Amount;
    public double? AdvantagePercent { get; init; }          // null → 显示 —
    public double SpreadUnit => SellUnitPrice - BuyUnitPrice;
    public double? SpreadPercent { get; init; }
    public bool HasNoQuote { get; init; }
    public double Volume => UnitVolume * Amount;
}

public sealed class ConcentrationResult
{
    public int MarketIndex { get; init; }
    public string MarketName { get; init; }
    public double PerItemTotal { get; init; }        // 各自最优的总额
    public double SingleMarketTotal { get; init; }   // 集中到该市场的总额
    public double Gain { get; init; }
    public double GainPercent { get; init; }
    public int CoveredCount { get; init; }
    public int TotalCount { get; init; }
    public bool CoversAll => CoveredCount >= TotalCount;
}

public sealed class CompareMarketFailure
{
    public string MarketName { get; init; }
    public string Reason { get; init; }
}

public sealed class CompareMarketResult
{
    public List<MarketLocation> Markets { get; init; }
    public List<CompareMarketRow> Rows { get; init; }
    public List<AppraisalParsedLine> UnresolvedLines { get; init; }
    public List<CompareMarketFailure> Failures { get; init; }
    public double TotalSellIncome { get; init; }
    public double TotalBuyExpense { get; init; }
    public double NetAmount => TotalSellIncome - TotalBuyExpense;
    public double TotalVolume { get; init; }
    public int NoQuoteCount { get; init; }
    public ConcentrationResult SellConcentration { get; init; }
    public ConcentrationResult BuyConcentration { get; init; }
}

public sealed class CompareSpreadOpportunity
{
    public string Name { get; init; }
    public double Amount { get; init; }
    public int BuyMarketIndex { get; init; }
    public int SellMarketIndex { get; init; }
    public double BuyPrice { get; init; }
    public double SellPrice { get; init; }
    public double UnitSpread => SellPrice - BuyPrice;
    public double SpreadPercent { get; init; }
    public double TotalSpread => UnitSpread * Amount;
}
```

> `Amount` 变更的传播：`CompareMarketPageViewModel` 订阅每一行的 `PropertyChanged`，收到 `Amount`/`Group` 变化即调用
> `Recalculate()`（重算汇总、集中度、利差表金额、`NeedsRecompare`），**不重取数据**。

---

## 8. 文件清单（新增 / 修改）

### 8.1 新增（WPF，共 8 个源文件）

| 文件 | 职责 |
|---|---|
| `Services/Business/MarketTypeNameResolver.cs` | 名称 → `InvType` 的 4 级解析（**从 `AppraisalService` 原样抽出**，供估价与比价共用） |
| `Services/Business/CompareMarketSettingService.cs` | `CompareMarketSetting` / `CompareMarketItemSetting` 模型 + `Configs/CompareMarketSetting.json` 的 `Load`/`Save` + 旧值修补 |
| `Services/Business/CompareMarketService.cs` | 取数编排（逐市场、逐物品）+ 双侧参考价 + 最优/优势/价差 + 汇总 + 集中度 + 利差机会；`RunAsync(setting, items, token, progress)` |
| `ViewModels/Business/CompareMarketModels.cs` | §7.2 的运行时模型（Cell / Row / Concentration / Failure / Result / Spread）+ 3 个枚举 |
| `ViewModels/Business/CompareMarketPageViewModel.cs` | 市场集合、清单集合（两组视图/计数）、粘贴导入、树→清单、互换、口径属性、汇总文案、进度与取消、语言事件、`ColumnsInvalidated` |
| `Views/UserControls/CompareMarketView.xaml(.cs)` | 主体视图（左右两栏 + 动态列构造 + 事件转发 VM） |
| `Views/Pages/CompareMarketPage.xaml(.cs)` | 薄壳页（承载 `CompareMarketView`，`Loaded → Init()` / `Unloaded → Dispose()`，与 `ScalperPage`/`AppraisalPage` 同构） |

### 8.2 修改（WPF，共 4 处；**Core 零改动**）

| 文件 | 改动 | 风险 |
|---|---|---|
| `Views/MainWindow.xaml` | 「商业」分组新增第 4 项（在 `Nav.Scalper` 之后、`Nav.Appraisal` 之前）：`Nav.CompareMarket` → `CompareMarketPage`，`NavigationCacheMode="Required"` | 无 |
| `Resources/Languages/zh-CN.xaml` / `en-US.xaml` | 新增 `Nav.CompareMarket` + `CompareMarketPage_*`（§9）；两侧键集一致、无重复键 | 低（§8 第 33 条：重复键会启动即崩，加完必须查重） |
| `Services/Business/AppraisalService.cs` | `ResolveTypes` 改为委托 `MarketTypeNameResolver`（**行为逐字不变**） | 中 → 必须回归估价页（本地名/英文名/模糊三种输入） |
| `Services/Home/HomeFeatureCatalog.cs` | （可选，P5）把 `CompareMarketPage` 加入主页功能目录，导航叶子 26 → 27 | 低 |

### 8.3 明确**不动**的文件

`Core`（全部）、`MarketOrderService`、`MarketLocationSelectorView`、`MarketSelecteTreeView`、`AppraisalTextParser`、
倒货/估价三个页面的既有视图与 VM、`MarketOrderSettingService`、`BusinessService`（比价不使用倒货的过滤清单与共享购物车）。

---

## 9. 本地化键清单

### 9.1 新增键（前缀 `CompareMarketPage_`，中英各约 46 个）

| 键 | 中文 | 英文 |
|---|---|---|
| `Nav.CompareMarket` | 比价 | Price Compare |
| `CompareMarketPage_Markets` | 市场 | Markets |
| `CompareMarketPage_AddMarket` | 添加市场 | Add market |
| `CompareMarketPage_MarketDuplicate` | 该市场已在列表中 | Market already in the list |
| `CompareMarketPage_MarketLimit` | 最多可添加 {0} 个市场 | Up to {0} markets |
| `CompareMarketPage_RemoveMarket` | 移除该市场 | Remove market |
| `CompareMarketPage_Items` | 物品 | Items |
| `CompareMarketPage_PasteHint` | 粘贴物品清单（支持游戏内复制的多行文本） | Paste an item list (multi-line text copied from the game) |
| `CompareMarketPage_ImportTo` | 导入到 | Import to |
| `CompareMarketPage_Import` | 导入 | Import |
| `CompareMarketPage_ImportResult` | 导入 {0} 行，识别 {1} 件 | {0} lines imported, {1} items resolved |
| `CompareMarketPage_UnresolvedNames` | 未识别：{0} | Unresolved: {0} |
| `CompareMarketPage_SellGroup` | 卖出物品 | Items to sell |
| `CompareMarketPage_BuyGroup` | 买入物品 | Items to buy |
| `CompareMarketPage_AddToSellList` | 加入卖出清单 | Add to sell list |
| `CompareMarketPage_AddToBuyList` | 加入买入清单 | Add to buy list |
| `CompareMarketPage_SwapGroups` | 两组互换 | Swap groups |
| `CompareMarketPage_GroupSell` | 卖出 | Sell |
| `CompareMarketPage_GroupBuy` | 买入 | Buy |
| `CompareMarketPage_MoveToSell` | 移到卖出组 | Move to sell group |
| `CompareMarketPage_MoveToBuy` | 移到买入组 | Move to buy group |
| `CompareMarketPage_RemoveFromList` | 从清单移除 | Remove from list |
| `CompareMarketPage_CopyName` | 复制物品名 | Copy item name |
| `CompareMarketPage_PriceSetting` | 价格口径 | Price basis |
| `CompareMarketPage_SellPriceBasis` | 卖单侧口径 | Sell-side basis |
| `CompareMarketPage_BuyPriceBasis` | 买单侧口径 | Buy-side basis |
| `CompareMarketPage_SellMethod` | 卖出方式 | Sell method |
| `CompareMarketPage_SellByOrder` | 挂卖单 | Post a sell order |
| `CompareMarketPage_SellToBuyOrder` | 直接卖给买单 | Sell to buy orders |
| `CompareMarketPage_BuyMethod` | 买入方式 | Buy method |
| `CompareMarketPage_BuyFromSellOrder` | 从卖单买入 | Buy from sell orders |
| `CompareMarketPage_BuyByOrder` | 挂买单 | Post a buy order |
| `CompareMarketPage_SellTax` | 卖出税费 % | Sell tax % |
| `CompareMarketPage_SellTaxTip` | 销售税 + 经纪费合计，按自身技能填写（不计税填 0） | Sales tax + broker fee; enter your own rate (0 = ignore) |
| `CompareMarketPage_SplitThreshold` | 分地阈值 % | Split threshold % |
| `CompareMarketPage_Start` | 开始比价 | Compare |
| `CompareMarketPage_Running` | 正在比价 | Comparing |
| `CompareMarketPage_Progress` | 市场 {0}/{1}：{2} — {3}/{4} 物品 | Market {0}/{1}: {2} — {3}/{4} items |
| `CompareMarketPage_TotalIncome` | 总收入 | Total income |
| `CompareMarketPage_TotalExpense` | 总支出 | Total expense |
| `CompareMarketPage_NetAmount` | 净额 | Net |
| `CompareMarketPage_TotalVolume` | 总体积 | Total volume |
| `CompareMarketPage_NoQuoteCount` | 无报价 {0} 件 | {0} without quotes |
| `CompareMarketPage_NoQuoteItems` | 无报价物品：{0} | No quotes: {0} |
| `CompareMarketPage_FailedMarkets` | {0} 个市场取数失败：{1} | {0} markets failed: {1} |
| `CompareMarketPage_SplitWorth` | 分市场{0}可多{1} {2} ISK（+{3}%），建议分开{0} | Spreading {0} gains {2} ISK (+{3}%); spreading recommended |
| `CompareMarketPage_SplitNotWorth` | 差距仅 {0}%，不值得分市场{1}，在「{2}」一地{1}即可 | Only {0}% difference; just {1} at «{2}» |
| `CompareMarketPage_SplitCoverage` | 「{0}」仅覆盖 {1}/{2} 件，未覆盖部分未计入 | «{0}» covers only {1}/{2}; the rest is excluded |
| `CompareMarketPage_SingleMarketOnly` | 只选了 1 个市场，无法比较地点差异 | Only one market selected; no comparison possible |
| `CompareMarketPage_NeedsRecompare` | 数量已变化，当前口径按数量取价，请重新比价 | Amount changed; amounts affect the price basis, please compare again |
| `CompareMarketPage_BestDistribution` | 最优分布 | Best distribution |
| `CompareMarketPage_SpreadOpportunities` | 利差机会 | Spread opportunities |
| `CompareMarketPage_SpreadBasis` | 利差口径 | Spread basis |
| `CompareMarketPage_SpreadMakeOrder` | 挂单（买→买单 / 卖→卖单） | Order book (buy→buy orders, sell→sell orders) |
| `CompareMarketPage_SpreadTakeOrder` | 吃单（买→卖单 / 卖→买单） | Immediate (buy→sell orders, sell→buy orders) |
| `CompareMarketPage_ColItem` | 物品 | Item |
| `CompareMarketPage_ColGroup` | 组 | Group |
| `CompareMarketPage_ColAmount` | 数量 | Amount |
| `CompareMarketPage_ColVolume` | 体积 | Volume |
| `CompareMarketPage_ColBestBuy` | 最优买入市场 | Best buy market |
| `CompareMarketPage_ColBestSell` | 最优卖出市场 | Best sell market |
| `CompareMarketPage_ColUnitPrice` | 最优单价 | Best unit price |
| `CompareMarketPage_ColAdvantage` | 较次优优势% | vs. 2nd best |
| `CompareMarketPage_ColSpread` | 买卖价差 | Buy/sell spread |
| `CompareMarketPage_ColSpreadPercent` | 价差率 | Spread % |
| `CompareMarketPage_ColSubtotal` | 小计 | Subtotal |
| `CompareMarketPage_ColSell` | 卖 | Sell |
| `CompareMarketPage_ColBuy` | 买 | Buy |
| `CompareMarketPage_ColBuyMarket` | 买入市场 | Buy market |
| `CompareMarketPage_ColBuyPrice` | 买入价 | Buy price |
| `CompareMarketPage_ColSellMarket` | 卖出市场 | Sell market |
| `CompareMarketPage_ColSellPrice` | 卖出价 | Sell price |
| `CompareMarketPage_ColUnitSpread` | 单位利差 | Unit spread |
| `CompareMarketPage_ColTotalSpread` | 总利差 | Total spread |
| `CompareMarketPage_EmptyList` | 清单为空，请先挑选或粘贴物品 | The list is empty; pick or paste items first |
| `CompareMarketPage_EmptyMarkets` | 请先添加至少一个市场 | Add at least one market first |
| `CompareMarketPage_NoResult` | 没有可比较的物品（全部无报价） | Nothing to compare (no quotes) |
| `CompareMarketPage_Failed` | 取数失败 | Failed |

### 9.2 复用既有键（不新增）

`MarketPage_UnSelectedMarket`、`MarketPage_SearchRegion` / `SearchSolarSystem` / `SearchStructure`、
`General_Region` / `General_SolarSystem` / `General_Structure`、`StructuresSettingPage_EmptyTip`、
`BusinessPage_SellTop5` / `SellTop` / `SellAvailable` / `BuyTop5` / `BuyTop` / `BuyAvailable`、
`BusinessPage_PriceType`、`General_RemoveSelected`、`General_Canceld`、`AppraisalPage_Market`。

> 键名与文案在实施时以两侧语言文件的实际写法为准（本节是规格，不是最终文件内容）；
> 加完必须跑既有查重脚本核对"中英各 N 键、0 重复、键集一致"。

---

## 10. 边界、限制与风险

| # | 事项 | 处置 / 说明 |
|---|---|---|
| 1 | **只做订单口径（现价），不做历史口径** | 历史会让取数量变 4 倍（每个市场每物品一次历史请求）、显著变慢，而"找地点"用现价即可。口径下拉只列 6 个订单项；读到历史枚举值回落默认 |
| 2 | **建筑市场是整建筑全量拉取** | 结构接口无"按物品"参数；首次可能 7 MB 级、较慢（有 TTL 缓存）。多个建筑市场会各拉一次 |
| 3 | **同一星域下多个星系市场会重复取单** | 第一版不按星域去重（正确去重需要"该星域所有物品的订单"合并，收益 < 复杂度）。物品种类多 + 同星域多市场时请求数偏多，日志里可见 |
| 4 | **请求量 = 物品数 × 市场数** | 50 物品 × 5 市场 = 250 次单物品请求（每次可能多页，并发走设置线程数）。UI 上有进度、可取消 |
| 5 | **`*Available` 口径的价格依赖数量** | 数量变更后价格本应变化，即时重算只改金额 → 用 `NeedsRecompare` 提示条（§6.4） |
| 6 | **同一物品只允许在一组** | 加入另一组时从原组移走（避免同一物品两行导致汇总重复），并给提示 |
| 7 | **总数不闭合时有显式提示** | 无报价物品、取数失败市场、未识别名称三类都在建议条里列出，**不允许静默漏算** |
| 8 | **集中度比较是"同集合自洽"口径** | 单市场收入只用"该市场有报价的物品子集"计算，避免把"不能在 J 卖"与"在 J 卖得便宜"混成一件事；覆盖不全时提示（§5.6） |
| 9 | **结构 ID 走 `StructureService`** | 建筑 ID 约 1e12，`IDNameService` 的 `int` 通路会静默截断（`REFACTORING.md` 文末约定）——比价所有建筑相关取值都经 `MarketOrderService`/`StructureService` |
| 10 | **PLEX 自动走全球星域** | `MarketOrderService.NormalizeRegion` 已处理，比价不额外分支 |
| 11 | **不做跨市场税费差异建模** | 统一一个 `SellTaxPercent`（默认 0），不区分建筑/空间站税率 |
| 12 | **页签常驻 + 语言切换** | 页面 `NavigationCacheMode="Required"`，切走再切回不重建；换语言需重建列与刷新求值型文案（§6.5 第 3/4 条） |
| 13 | **不做详情窗** | 复用倒货的 `ScalperItemDetailWindow` 是"源/目的"两市场语义，比价是多市场；第一版双击不弹窗（§12） |
| 14 | **构建前必须退出应用** | 输出目录被锁会 `MSB3027/MSB3021`；只验编译可用 `-p:OutDir=<临时目录>`（项目既有约定） |
| 15 | **本地化重复键会启动即崩** | 两文件加键后必须查重（§8 第 33 条） |

---

## 11. 分阶段实施计划

| 阶段 | 内容 | 产出 / 验收 |
|---|---|---|
| **P0** | 抽出 `MarketTypeNameResolver`（估价 `ResolveTypes` 原样搬家并委托）+ `CompareMarketSettingService` + `CompareMarketModels` | `dotnet build` 0 错误；**估价页名称解析回归**（英文名精确 / 中文名 / 模糊唯一命中） |
| **P1** | `CompareMarketService.RunAsync`：逐市场逐物品取数（含进度/失败容错）+ 双侧参考价 + 最优/优势/价差 + 汇总 + 集中度 + 利差 | 纯计算逻辑探针（人工构造订单，断言最优选择、集中度、利差 Top3 与手算一致） |
| **P2** | VM + `CompareMarketView` 骨架：市场列表增删、物品树→清单、粘贴导入、互换、口径设置、开始/取消 | 构建 0 错误；界面可点（用户实机看） |
| **P3** | 右栏：汇总条、建议条、动态列矩阵、利差页签、数量编辑即时重算、`NeedsRecompare` | 构建 0 错误；矩阵列与数值实机核对 |
| **P4** | 本地化键（中英）+ 导航注册 + 列头随语言重建 | 键查重通过；`MainWindow` 出现「商业 → 比价」 |
| **P5** | （可选）主页功能目录加一项；`REFACTORING.md` 补一节阶段记录 | 构建 0 错误 |

**验证纪律（沿用项目约定）**

- 每阶段 `dotnet build` **0 错误**（应用运行中就用临时 `OutDir`）；
- P1 的数学必须有一次性探针（构造订单断言），因为**金额算错不会抛异常、只会静默给出错数字**；
- **不做截图核验**：界面效果由使用者实机查看（§8「环境/协作注意」）；
- 收尾时清理探针与临时产物，并把本轮结论写进 `REFACTORING.md`（新增一节「阶段 NN：商业-比价」）。

---

## 12. 明确不做（第一版）

1. **历史口径**（历史最高/最低/平均/中位数）与历史波动、热力值、饱和度等倒货指标。
2. **推荐度打分**（倒货那套加权排名）：比价的结论是"哪个市场"，不需要再折算一个综合分。
3. **物品详情窗**（多市场版本）与图表（LiveCharts）。
4. **跨市场的倒货路线规划**（跳数、ISK/跳、燃料、货柜容量约束）：这些属于"运输成本"，第一版只给"体积"，
   不引入 `ShortestPathHelper` 的距离维度（若日后要，插在汇总条与优势列之间即可，不需要动取数链路）。
5. **行星/合同/资产等一键导入**（只支持"粘贴文本"与"树选择"两种入口）。
6. **结果的导出/落盘/历史记录**（购物车与购物记录是倒货专有，比价不接）。
7. **自动定时刷新**（比价是一次性分析动作，不常驻轮询）。
8. **跨星域去重取单优化**（§10 第 3 条）。

---

## 13. 与既有功能的回归关注点

| 关注点 | 为什么 | 怎么验 |
|---|---|---|
| 估价页名称解析 | `ResolveTypes` 被搬走并改为委托 | 用估价页粘贴同一份文本（英文名 + 中文名 + 拼写偏差），结果物品数与名称与改动前一致 |
| 市场选择器 | 被比价多实例复用（原只在一个页面用一两次） | 页面内 12 个选择器实例并存时，弹层、搜索、建筑列表重载都正常 |
| 物品树 | 被比价复用两个实例（原来一个页面一个） | 两棵树的勾选互不干扰；「加入清单」后清空只影响自己那棵 |
| 输出目录资源 | 无新增资源项 | `dotnet build` 与一次 `dotnet publish`（`NETSDK1152` 只在 publish 暴露） |
