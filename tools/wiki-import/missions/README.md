# 任务攻略导入管线

把 [EVE University Wiki 的 Missions 体系](https://wiki.eveuniversity.org/Missions) 抓下来，
渲染成中文 Markdown，写进 `Resources/Database/static.db` 的 `Mission` 表。

产出的表结构与「死亡远征」(`CombatSite`) 同构，页面也同构（左列表 + 三筛选，右正文），
用户切功能不用换脑子。

## 管线

```bash
# 1+2. 抓取
python fetch_missions.py            # 按 10 个 wiki 分类先抓一批（增量；--force 全量重抓）
python parse_missions.py            # 先跑一遍，产出 includes_missing.json（转写页清单）
python fetch_includes.py            # 补抓「章节页里 {{:任务名}} 转写」的任务页
python fetch_by_template.py         # ★ 权威口径：谁转写了 Template:Missiondetails 谁就是任务页
                                    #   （1741 页；分类只用 prop=categories 批量取来归类）
# 3. 解析
python parse_missions.py            # wikitext → parsed.json（单任务 / 同页多变体 / 章节枢纽都兼容）
# 4. 译文
python export_translations.py       # → to_translate/batch_XX.json（只导短文本）
#   人工或模型逐批译，产出 to_translate/zh_XX.json（键必须一一对应）
python merge_translations.py        # 校验并写入 translations.json
# 5. 任务名对齐（旧库社区名）
#    旧版工具有 405 条社区中文名（玩家真正在用的叫法）。对齐到英文标题后写进
#    align_result*.json，合并成 align_merged.json —— 构建时**优先用它**，
#    否则直译名搜不到（例：Enemies Abound 直译「敌人环伺」，玩家叫「腹背受敌」）。
# 6. **官方译名校准**（SDE）
#    SDE 的 missions.jsonl 有 name.zh（2887 条官方译名），npcCharacters.jsonl 有 NPC 中文名
#    （史诗任务线不少页面是用代理人名命名的）。这一步把 parsed.json 每页补上 sdeZh / sdeBriefing。
python match_sde_missions.py            # 只统计命中率
python match_sde_missions.py --apply    # 写回 parsed.json
# 7. 剩下对不上的逐条核对（对齐 / 保留 / 剔除），结论写回 parsed.json
python sde_candidates.py                # 先看每条最像的 SDE 候选
python sde_review.py                    # 只打印结论
python sde_review.py --apply            # 写回：map→改名，exclude→标记（build 时跳过）
# 8. 组装 + 落库
python build_missions.py            # 只生成 missions.json 便于检查
python build_missions.py --write-db # 建表写库（会 DROP 老 Mission 与 MissionContent）
# 7. 对比旧库
python compare_old_new.py           # 需要 %TEMP%\old_static.db（git show HEAD:…static.db 导出的旧库）
```

> 为什么枚举口径不用分类：`Category:Security missions` 翻页会漏（实际 1000+，按 500 翻只拿到 717），
> 而真正的史诗任务在 `Category:Epic arc missions`（不是 `Category:Epic Arcs`，那个只是章节枢纽）。
> 用模板转写枚举才是完整的：**1230 页 → 1793 页，任务 1313 → 1771 条**。

## 数据来源与结构

| 项目 | 说明 |
|---|---|
| 页面骨架 | `{{Missiondetails\|Level=\|Type=\|Objective=\|Faction=\|WebPoint=\|EWAR=\|ShipSizeLimit=\|ShipSuggestion=\|Rewards=\|StandingLoss=\|Extra=}}` |
| NPC 表 | `{{NPCTableHead\|标题}}` + `{{NPCTableRow\|舰型\|数量\|名称\|ewar=\|trigger=\|note=}}` |
| wiki HTML 表 | 正文里的 `{\|` … `\|}` 表格会转成 Markdown（采矿产出、毒性波次等） |
| 分类 | 安全 1168 / 史诗 205 / 运输 155 / 故事线 119 / 职业代理人 45 / 采矿 30 / Circle 30 / 异常 13 / 研发 6 = **1771 条** |
| 势力 | 0–5 海盗（无人机/天蛇/血袭者/萨沙/天使/古斯塔斯）、**6–9 四大帝国**（艾玛/加达里/盖伦特/米玛塔尔） |

## 舰型列

NPC 表的"舰型"是闭集（49 种取值、7800+ 行），用 `build_missions.py` 里的 `CLASS_CN` 词典直接映射，
不走翻译批次：`Frigate→护卫舰`、`Elite Frigate→精英护卫舰`、`Structure→建筑`、`Drone→无人机`、
`Sentry→哨塔`、`Acceleration Gate→加速门`……

## 译文范围（重要）

- **已译**：任务名、小节标题、NPC 表标题、`Objective / Rewards / Extra / Ewar / 船型` 等字段值（5066 条片段）。
- **任务名特殊**：优先用 `align_merged.json` 里的**社区通用名**（旧库 405 条对齐而来），其次才是 wiki 直译。
- **未译**：wiki 上大段的 walkthrough 叙述（约 100 万字符英文原文，直接展示）。
- `translations.json` 的 key 是 `sha1(英文原文)[:16]`，改原文会自动失效（回退英文），不会静默错位。

## 文件

| 文件 | 作用 |
|---|---|
| `index.json` | 抓取清单（标题 / 分类 / 文件名 / 字符数） |
| `raw/*.txt` | 原始 wikitext（1793 页，约 4 MB） |
| `parsed.json` | 解析结果（infobox + 分节 + NPC 表 + 转写关系） |
| `translations.json` | 译文缓存（key → 中文） |
| `align_merged.json` | 旧库社区中文名 → 英文标题（构建时优先使用） |
| `missions.json` | 组装结果（每次 build 重新生成，可不入库） |
| `to_translate/` | 翻译批次（`batch_XX.json` 原文、`zh_XX.json` 译文） |
