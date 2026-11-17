# wiki-import —— 战斗站点（死亡远征）资料导入

把 [EVE University Wiki · Combat site](https://wiki.eveuniversity.org/Combat_site) 上的站点资料
抓下来、解析成结构、翻成中文，写进 `TheGuideToTheNewEden.WPF/Resources/Database/static.db` 的 `CombatSite` 表。

设计说明见 `../../TheGuideToTheNewEden/TheGuideToTheNewEden.WPF/DED_COMBAT_SITE_DESIGN.md`。

## 产物落点

| 文件 | 作用 |
|---|---|
| `index.json` | 索引：8 个分类各自的站点清单（势力 / 评级 / 难度档 / 安等 / 升级目标 / 触发来源 / wiki 页名） |
| `parsed.json` | 每个 wiki 页解析后的结构（元信息、简介、每层 NPC 波次、战利品、升级说明，保持章节顺序） |
| `translations.json` | 正文片段的译文（键 = 原文 sha1 前 12 位） |
| `translations_names.json` | 站点名的中文名 |
| `capital_sites.json` | 旗舰远征（12 条）：来自游戏 SDE `dungeons.jsonl` 的官方中英文名与描述 |
| `sites.json` | 组装后的全部记录（每次 `build_sites.py` 重新生成，已 gitignore） |

## 管线（6 步）

```bash
# 0. 旗舰远征：wiki 没有这些条目，从游戏 SDE 的 dungeons.jsonl 抽（含官方中英文名与描述）
python extract_sde_dungeons.py "D:\\QEDSD\\SDE\\eve-online-static-data-3632936-jsonl"
#    -> capital_sites.json（12 条）

# 1. 抓取：索引页 + 全部站点子页（约 370 页，限速 0.3s）
python fetch_wiki.py            # 增量；--force 全量重抓

# 2. 解析：wiki 模板 → 结构化 JSON
python parse_wiki.py

# 3. 抽译文单元：按三波（核心三类 / 战斗异常 / 长尾）分批
python extract_units.py         # 产出 batch_w{N}_XX.json 与 names_w{N}.json

# 4. 翻译：每批一个翻译代理，读 batch_wN_XX.json、写 zh_wN_XX.json（键集合必须一致）
#    人工/AI 均可；本项目用子代理批量译，术语表见当时的工作流脚本

# 5. 合并 + 组装 + 落库（build 会自动把 capital_sites.json 并进去）
python merge_translations.py    # 逐批校验键集合并合并
python build_sites.py           # 只生成 sites.json 便于检查
python build_sites.py --write-db # 建表并写入 static.db
```

> `capital_sites.json` 已经入库，**日常重跑不必再执行第 0 步**（除非游戏版本更新、SDE 里新增/改了站点）。

## 几处必须知道的实现细节

- **表格解析用 HTML 网格**，不是正则切单元格：`Combat_site` 的表格大量使用 `rowspan/colspan`
  （难点是把 `4/10` 这一行里"高安/低安/00"的空单元格与 12 个势力列对齐），
  所以 `fetch_wiki.py` 抓的是 `action=parse&prop=text` 的渲染结果，再用带 `rowspan/colspan` 展开的表格网格读数。
- **NPC / 建筑 / 战利品名优先走本地 SDE**：`Resources/Database/main.db` 的 `types` 取 typeID →
  `Resources/Database/Local/zh.db` 的 `types` 取中文名。共享前缀的写法（`Centior Abomination/Horror`）
  会先补前缀再查；查不到的保留英文。
- **文件名要做 Windows 转义**：wiki 页名里有 `?`（`Trap?`）、`&`、`:`，`safe_name()` 会替换成全角字符；
  解析时再用 `index.json` 反查回原标题。
- **页名可能与实际文件名不一致**：重定向页会存成目标名（`Blood_raider_prison_camp` → `Blood Raider Prison Camp`），
  甚至有拼写差异（`Pith_Robux_Asteroid_Mining_&_Co.` → `Pith-Robux Asteroid Mining Co.`）。
  `build_sites.py` 用"去掉全部非字母数字后的小写"做规范键来兜住这些情况。
- **长剧情播报不逐字翻**：`>200` 字符的 warp-in / 升级独白对攻略价值低却占近一半体量，
  `extract_units.py` 的 `MAX_MESSAGE_CHARS` 控制这个阈值。
- **分波是为了可回填**：任何一波没翻完，`build_sites.py` 会让缺失片段回退成英文原文，
  不会出现"翻一半就整体不可用"。

## 许可

抓取的正文来自 EVE University Wiki，许可为 **CC BY-SA 4.0**。
派生的 `WikiContent` 在界面上必须保留署名与许可声明（页内固定一行 + 设置 → 关于 + README），
并以相同协议共享。
