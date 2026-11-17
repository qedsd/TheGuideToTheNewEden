# EVE University Wiki · Combat site 全量清单（快照）

- 来源：<https://wiki.eveuniversity.org/Combat_site>
- 版本：revision **230825**，最后编辑 **2026-05-15T09:37:14Z**（抓取于 2026-11-17）
- 许可：**CC BY-SA 4.0**（引用/派生需署名 EVE University Wiki 并以同协议共享）
- 说明：本文件是 `DED_COMBAT_SITE_DESIGN.md` 的附表；机器可读版见 `wiki-combat-site-inventory.json`。
- 解析方式：抓 `action=parse&prop=text` 的 HTML，用**展开 rowspan / colspan 的表格网格解析器**读数（不用正则切单元格），
  故下面每个数字都是"去重后的站点条目数"。

## 汇总

| 类别 | 条目数 |
|---|---|
| 战斗异常 Combat Anomalies | 126 |
| 无评级复合体 Unrated Complexes | 74 |
| DED 评级复合体 DED Rated Complexes | 50 |
| 远征 Expeditions | 76 |
| 化学实验室 Chemical Labs | 34 |
| 战斗遗迹 Combat Relic Sites | 7 |
| 三神裔入侵 Triglavian（Pochven） | 9 |
| 特殊站点（神庙复合体 ×2 / ISHAEKA / 被围困设施） | 4 |
| **合计** | **380** |

## 1. 战斗异常（126 条）

### 1.1 难度档与出现空间（按 wiki 表格逐档读出）

| 档 | 档内等级 | 高安 | 低安 | 00 |
|---|---|---|---|---|
| T1 | 1–4 | ✔ | ✔ |  |
| T2 | — | ✔ |  |  |
| T3 | — | ✔ | ✔ |  |
| T4 | 1–4 | ✔ | ✔ | ✔ |
| T5 | — |  | ✔ |  |
| T6 | 1–4 |  | ✔ | ✔ |
| T7 | — |  | ✔ | ✔ |
| T8 | 1–4 |  | ✔ | ✔ |
| T9 | — |  |  | ✔ |
| T10 | 1–2 |  |  | ✔ |

> 逐档按等级细分时：T4 的 L1（`Den`）是高安+低安，L2–L4（`Hidden/Forsaken/Forlorn Den`）是低安+00；
> 其余档位各等级的空间分布一致。

### 1.2 变体命名规律

| 档 | 变体 | 无人机对应 |
|---|---|---|
| T1 | `Hideaway` / `Hidden` / `Forsaken` / `Forlorn` | Drone Cluster |
| T2 | `Burrow` | Drone Collection |
| T3 | `Refuge` | Drone Assembly |
| T4 | `Den` / `Hidden` / `Forsaken` / `Forlorn` | Drone Gathering |
| T5 | `Yard` | Drone Surveillance |
| T6 | `Rally Point` / `Hidden` / `Forsaken` / `Forlorn` | Drone Menagerie |
| T7 | `Port` | Drone Herd |
| T8 | `Hub` / `Hidden Hub` / `Forsaken Hub` / `Forlorn Hub` | Drone Squad |
| T9 | `Haven` | Drone Patrol |
| T10 | `Sanctum` / `Forsaken Sanctum` | Drone Horde / Teeming Drone Horde |

### 1.3 逐条清单（含"可升级为哪个 DED"）

| 势力 | 站点 | 档 | 档内等级 | 可升级为 | 高 | 低 | 00 |
|---|---|---|---|---|---|---|---|
| Angel Cartel | Angel Hideaway | T1 | 1 | — | ✔ | ✔ |  |
| Blood Raiders | Blood Hideaway | T1 | 1 | — | ✔ | ✔ |  |
| Guristas Pirates | Guristas Hideaway | T1 | 1 | 4/10 | ✔ | ✔ |  |
| Sansha's Nation | Sansha Hideaway | T1 | 1 | 3/10 | ✔ | ✔ |  |
| Serpentis Corporation | Serpentis Hideaway | T1 | 1 | 3/10 | ✔ | ✔ |  |
| Rogue Drones | Drone Cluster | T1 | 1–4 | — | ✔ | ✔ |  |
| Angel Cartel | Angel Hidden Hideaway | T1 | 2 | — | ✔ | ✔ |  |
| Blood Raiders | Blood Hidden Hideaway | T1 | 2 | — | ✔ | ✔ |  |
| Guristas Pirates | Guristas Hidden Hideaway | T1 | 2 | — | ✔ | ✔ |  |
| Sansha's Nation | Sansha Hidden Hideaway | T1 | 2 | — | ✔ | ✔ |  |
| Serpentis Corporation | Serpentis Hidden Hideaway | T1 | 2 | — | ✔ | ✔ |  |
| Angel Cartel | Angel Forsaken Hideaway | T1 | 3 | 3/10 | ✔ | ✔ |  |
| Blood Raiders | Blood Forsaken Hideaway | T1 | 3 | — | ✔ | ✔ |  |
| Guristas Pirates | Guristas Forsaken Hideaway | T1 | 3 | — | ✔ | ✔ |  |
| Sansha's Nation | Sansha Forsaken Hideaway | T1 | 3 | — | ✔ | ✔ |  |
| Serpentis Corporation | Serpentis Forsaken Hideaway | T1 | 3 | — | ✔ | ✔ |  |
| Angel Cartel | Angel Forlorn Hideaway | T1 | 4 | — | ✔ | ✔ |  |
| Blood Raiders | Blood Forlorn Hideaway | T1 | 4 | — | ✔ | ✔ |  |
| Guristas Pirates | Guristas Forlorn Hideaway | T1 | 4 | — | ✔ | ✔ |  |
| Sansha's Nation | Sansha Forlorn Hideaway | T1 | 4 | — | ✔ | ✔ |  |
| Serpentis Corporation | Serpentis Forlorn Hideaway | T1 | 4 | — | ✔ | ✔ |  |
| Angel Cartel | Angel Burrow | T2 | — | — | ✔ |  |  |
| Blood Raiders | Blood Burrow | T2 | — | — | ✔ |  |  |
| Guristas Pirates | Guristas Burrow | T2 | — | — | ✔ |  |  |
| Sansha's Nation | Sansha Burrow | T2 | — | — | ✔ |  |  |
| Serpentis Corporation | Serpentis Burrow | T2 | — | — | ✔ |  |  |
| Rogue Drones | Drone Collection | T2 | — | — | ✔ |  |  |
| Angel Cartel | Angel Refuge | T3 | — | 5/10 | ✔ | ✔ |  |
| Blood Raiders | Blood Refuge | T3 | — | 4/10 | ✔ | ✔ |  |
| Guristas Pirates | Guristas Refuge | T3 | — | 4/10 | ✔ | ✔ |  |
| Sansha's Nation | Sansha Refuge | T3 | — | 3/10 | ✔ | ✔ |  |
| Serpentis Corporation | Serpentis Refuge | T3 | — | 3/10 | ✔ | ✔ |  |
| Rogue Drones | Drone Assembly | T3 | — | 3/10 | ✔ | ✔ |  |
| Angel Cartel | Angel Den | T4 | 1 | 5/10 | ✔ | ✔ |  |
| Blood Raiders | Blood Den | T4 | 1 | 5/10 | ✔ | ✔ |  |
| Guristas Pirates | Guristas Den | T4 | 1 | 5/10 | ✔ | ✔ |  |
| Sansha's Nation | Sansha Den | T4 | 1 | 5/10 | ✔ | ✔ |  |
| Serpentis Corporation | Serpentis Den | T4 | 1 | 5/10 | ✔ | ✔ |  |
| Rogue Drones | Drone Gathering | T4 | 1–4 | 3/10 | ✔ | ✔ | ✔ |
| Angel Cartel | Angel Hidden Den | T4 | 2 | — |  | ✔ | ✔ |
| Blood Raiders | Blood Hidden Den | T4 | 2 | — |  | ✔ | ✔ |
| Guristas Pirates | Guristas Hidden Den | T4 | 2 | 6/10 |  | ✔ | ✔ |
| Sansha's Nation | Sansha Hidden Den | T4 | 2 | — |  | ✔ | ✔ |
| Serpentis Corporation | Serpentis Hidden Den | T4 | 2 | — |  | ✔ | ✔ |
| Angel Cartel | Angel Forsaken Den | T4 | 3 | 7/10 |  | ✔ | ✔ |
| Blood Raiders | Blood Forsaken Den | T4 | 3 | — |  | ✔ | ✔ |
| Guristas Pirates | Guristas Forsaken Den | T4 | 3 | 7/10 |  | ✔ | ✔ |
| Sansha's Nation | Sansha Forsaken Den | T4 | 3 | — |  | ✔ | ✔ |
| Serpentis Corporation | Serpentis Forsaken Den | T4 | 3 | 7/10 |  | ✔ | ✔ |
| Angel Cartel | Angel Forlorn Den | T4 | 4 | 7/10 |  | ✔ | ✔ |
| Blood Raiders | Blood Forlorn Den | T4 | 4 | — |  | ✔ | ✔ |
| Guristas Pirates | Guristas Forlorn Den | T4 | 4 | 7/10 |  | ✔ | ✔ |
| Sansha's Nation | Sansha Forlorn Den | T4 | 4 | — |  | ✔ | ✔ |
| Serpentis Corporation | Serpentis Forlorn Den | T4 | 4 | — |  | ✔ | ✔ |
| Angel Cartel | Angel Yard | T5 | — | 5/10 |  | ✔ |  |
| Blood Raiders | Blood Yard | T5 | — | 6/10 |  | ✔ |  |
| Guristas Pirates | Guristas Yard | T5 | — | 5/10 |  | ✔ |  |
| Sansha's Nation | Sansha Yard | T5 | — | 6/10 |  | ✔ |  |
| Serpentis Corporation | Serpentis Yard | T5 | — | 6/10 |  | ✔ |  |
| Rogue Drones | Drone Surveillance | T5 | — | 3/10 |  | ✔ |  |
| Angel Cartel | Angel Rally Point | T6 | 1 | 6/10 |  | ✔ | ✔ |
| Blood Raiders | Blood Rally Point | T6 | 1 | 6/10 |  | ✔ | ✔ |
| Guristas Pirates | Guristas Rally Point | T6 | 1 | 6/10 |  | ✔ | ✔ |
| Sansha's Nation | Sansha Rally Point | T6 | 1 | 6/10 |  | ✔ | ✔ |
| Serpentis Corporation | Serpentis Rally Point | T6 | 1 | 6/10 |  | ✔ | ✔ |
| Rogue Drones | Drone Menagerie | T6 | 1–4 | 5/10 |  | ✔ | ✔ |
| Angel Cartel | Angel Hidden Rally Point | T6 | 2 | 6/10 |  | ✔ | ✔ |
| Blood Raiders | Blood Hidden Rally Point | T6 | 2 | — |  | ✔ | ✔ |
| Guristas Pirates | Guristas Hidden Rally Point | T6 | 2 | 9/10 |  | ✔ | ✔ |
| Sansha's Nation | Sansha Hidden Rally Point | T6 | 2 | 9/10 |  | ✔ | ✔ |
| Serpentis Corporation | Serpentis Hidden Rally Point | T6 | 2 | Capital Staging |  | ✔ | ✔ |
| Angel Cartel | Angel Forsaken Rally Point | T6 | 3 | 8/10 |  | ✔ | ✔ |
| Blood Raiders | Blood Forsaken Rally Point | T6 | 3 | 8/10 |  | ✔ | ✔ |
| Guristas Pirates | Guristas Forsaken Rally Point | T6 | 3 | 8/10 |  | ✔ | ✔ |
| Sansha's Nation | Sansha Forsaken Rally Point | T6 | 3 | 8/10 |  | ✔ | ✔ |
| Serpentis Corporation | Serpentis Forsaken Rally Point | T6 | 3 | 8/10 |  | ✔ | ✔ |
| Angel Cartel | Angel Forlorn Rally Point | T6 | 4 | — |  | ✔ | ✔ |
| Blood Raiders | Blood Forlorn Rally Point | T6 | 4 | 8/10 |  | ✔ | ✔ |
| Guristas Pirates | Guristas Forlorn Rally Point | T6 | 4 | 8/10 |  | ✔ | ✔ |
| Sansha's Nation | Sansha Forlorn Rally Point | T6 | 4 | 8/10 |  | ✔ | ✔ |
| Serpentis Corporation | Serpentis Forlorn Rally Point | T6 | 4 | 8/10 |  | ✔ | ✔ |
| Angel Cartel | Angel Port | T7 | — | 7/10 |  | ✔ | ✔ |
| Blood Raiders | Blood Port | T7 | — | 7/10 |  | ✔ | ✔ |
| Guristas Pirates | Guristas Port | T7 | — | 7/10 |  | ✔ | ✔ |
| Sansha's Nation | Sansha Port | T7 | — | 7/10 |  | ✔ | ✔ |
| Serpentis Corporation | Serpentis Port | T7 | — | 7/10 |  | ✔ | ✔ |
| Rogue Drones | Drone Herd | T7 | — | 5/10 Infested Starbase |  | ✔ | ✔ |
| Angel Cartel | Angel Hub | T8 | 1 | 8/10 |  | ✔ | ✔ |
| Blood Raiders | Blood Hub | T8 | 1 | 8/10 |  | ✔ | ✔ |
| Guristas Pirates | Guristas Hub | T8 | 1 | 8/10 |  | ✔ | ✔ |
| Sansha's Nation | Sansha Hub | T8 | 1 | 8/10 |  | ✔ | ✔ |
| Serpentis Corporation | Serpentis Hub | T8 | 1 | 8/10 Shielded Starbase |  | ✔ | ✔ |
| Rogue Drones | Drone Squad | T8 | 1–4 | 10/10 |  | ✔ | ✔ |
| Angel Cartel | Angel Hidden Hub | T8 | 2 | 9/10 |  | ✔ | ✔ |
| Blood Raiders | Blood Hidden Hub | T8 | 2 | — |  | ✔ | ✔ |
| Guristas Pirates | Guristas Hidden Hub | T8 | 2 | 9/10 |  | ✔ | ✔ |
| Sansha's Nation | Sansha Hidden Hub | T8 | 2 | Unknown |  | ✔ | ✔ |
| Serpentis Corporation | Serpentis Hidden Hub | T8 | 2 | — |  | ✔ | ✔ |
| Angel Cartel | Angel Forsaken Hub | T8 | 3 | 9/10 |  | ✔ | ✔ |
| Blood Raiders | Blood Forsaken Hub | T8 | 3 | 9/10 |  | ✔ | ✔ |
| Guristas Pirates | Guristas Forsaken Hub | T8 | 3 | 9/10 |  | ✔ | ✔ |
| Sansha's Nation | Sansha Forsaken Hub | T8 | 3 | 9/10 |  | ✔ | ✔ |
| Serpentis Corporation | Serpentis Forsaken Hub | T8 | 3 | 9/10 |  | ✔ | ✔ |
| Angel Cartel | Angel Forlorn Hub | T8 | 4 | 9/10 |  | ✔ | ✔ |
| Blood Raiders | Blood Forlorn Hub | T8 | 4 | 9/10 |  | ✔ | ✔ |
| Guristas Pirates | Guristas Forlorn Hub | T8 | 4 | 9/10 |  | ✔ | ✔ |
| Sansha's Nation | Sansha Forlorn Hub | T8 | 4 | 9/10 |  | ✔ | ✔ |
| Serpentis Corporation | Serpentis Forlorn Hub | T8 | 4 | 9/10 |  | ✔ | ✔ |
| Angel Cartel | Angel Haven | T9 | — | 10/10 |  |  | ✔ |
| Blood Raiders | Blood Haven | T9 | — | 10/10 |  |  | ✔ |
| Guristas Pirates | Guristas Haven | T9 | — | 10/10 |  |  | ✔ |
| Sansha's Nation | Sansha Haven | T9 | — | 10/10 |  |  | ✔ |
| Serpentis Corporation | Serpentis Haven | T9 | — | 10/10 |  |  | ✔ |
| Rogue Drones | Drone Patrol | T9 | — | 10/10 |  |  | ✔ |
| Angel Cartel | Angel Sanctum | T10 | 1 | — |  |  | ✔ |
| Blood Raiders | Blood Sanctum | T10 | 1 | 10/10 |  |  | ✔ |
| Guristas Pirates | Guristas Sanctum | T10 | 1 | 10/10 |  |  | ✔ |
| Sansha's Nation | Sansha Sanctum | T10 | 1 | 10/10 |  |  | ✔ |
| Serpentis Corporation | Serpentis Sanctum | T10 | 1 | 10/10 |  |  | ✔ |
| Rogue Drones | Drone Horde | T10 | 1 | 10/10 |  |  | ✔ |
| Angel Cartel | Angel Forsaken Sanctum | T10 | 2 | ? |  |  | ✔ |
| Blood Raiders | Blood Forsaken Sanctum | T10 | 2 | ? |  |  | ✔ |
| Guristas Pirates | Guristas Forsaken Sanctum | T10 | 2 | ? |  |  | ✔ |
| Sansha's Nation | Sansha Forsaken Sanctum | T10 | 2 | ? |  |  | ✔ |
| Serpentis Corporation | Serpentis Forsaken Sanctum | T10 | 2 | 10/10 |  |  | ✔ |
| Rogue Drones | Teeming Drone Horde | T10 | 2 | ? |  |  | ✔ |

## 2. 无评级复合体（74 条）

| 势力 | 站点 | 高 | 低 | 00 |
|---|---|---|---|---|
| Angel Cartel | Angel Hideout | ✔ | ✔ |  |
| Blood Raiders | Blood Hideout | ✔ | ✔ |  |
| Guristas Pirates | Gurista Hideout | ✔ | ✔ |  |
| Sansha's Nation | Sansha Hideout | ✔ | ✔ |  |
| Serpentis Corporation | Serpentis Hideout | ✔ | ✔ |  |
| Rogue Drones | Haunted Yard | ✔ | ✔ |  |
| Angel Cartel | Angel Lookout | ✔ | ✔ |  |
| Blood Raiders | Blood Lookout | ✔ | ✔ |  |
| Guristas Pirates | Gurista Lookout | ✔ | ✔ |  |
| Sansha's Nation | Sansha Lookout | ✔ | ✔ |  |
| Serpentis Corporation | Serpentis Lookout | ✔ | ✔ |  |
| Rogue Drones | Desolate Site | ✔ | ✔ |  |
| Angel Cartel | Angel Watch | ✔ | ✔ |  |
| Blood Raiders | Blood Watch | ✔ | ✔ |  |
| Guristas Pirates | Guristas Watch | ✔ | ✔ |  |
| Sansha's Nation | Sansha Watch | ✔ | ✔ |  |
| Serpentis Corporation | Serpentis Watch | ✔ | ✔ |  |
| Rogue Drones | Chemical Yard | ✔ | ✔ |  |
| Angel Cartel | Angel Vigil | ✔ | ✔ |  |
| Blood Raiders | Blood Vigil | ✔ | ✔ |  |
| Guristas Pirates | Guristas Vigil | ✔ | ✔ |  |
| Sansha's Nation | Sansha Vigil | ✔ | ✔ |  |
| Serpentis Corporation | Serpentis Vigil | ✔ | ✔ |  |
| Angel Cartel | Provisional Angel Outpost |  | ✔ |  |
| Blood Raiders | Provisional Blood Outpost |  | ✔ |  |
| Guristas Pirates | Provisional Gurista Outpost |  | ✔ |  |
| Sansha's Nation | Provisional Sansha Outpost |  | ✔ |  |
| Serpentis Corporation | Provisional Serpentis Outpost |  | ✔ |  |
| Rogue Drones | Rogue Trial Yard |  | ✔ |  |
| Angel Cartel | Angel Outpost |  | ✔ |  |
| Blood Raiders | Blood Raider Outpost |  | ✔ |  |
| Guristas Pirates | Gurista Outpost |  | ✔ |  |
| Sansha's Nation | Sansha Outpost |  | ✔ |  |
| Serpentis Corporation | Serpentis Outpost |  | ✔ |  |
| Rogue Drones | Dirty Site |  | ✔ |  |
| Angel Cartel | Minor Angel Annex |  | ✔ |  |
| Blood Raiders | Minor Blood Annex |  | ✔ |  |
| Guristas Pirates | Minor Guristas Annex |  | ✔ |  |
| Sansha's Nation | Minor Sansha Annex |  | ✔ |  |
| Serpentis Corporation | Minor Serpentis Annex |  | ✔ |  |
| Rogue Drones | Ruins |  | ✔ |  |
| Angel Cartel | Angel Annex |  | ✔ |  |
| Blood Raiders | Blood Annex |  | ✔ |  |
| Guristas Pirates | Guristas Annex |  | ✔ |  |
| Sansha's Nation | Sansha Annex |  | ✔ |  |
| Serpentis Corporation | Serpentis Annex |  | ✔ |  |
| Angel Cartel | Angel Base |  |  | ✔ |
| Blood Raiders | Blood Raider Base |  |  | ✔ |
| Guristas Pirates | Gurista Base |  |  | ✔ |
| Sansha's Nation | Sansha Base |  |  | ✔ |
| Serpentis Corporation | Serpentis Base |  |  | ✔ |
| Rogue Drones | Independence |  |  | ✔ |
| Angel Cartel | Angel Fortress |  |  | ✔ |
| Blood Raiders | Blood Raider Fortress |  |  | ✔ |
| Guristas Pirates | Gurista Fortress |  |  | ✔ |
| Sansha's Nation | Sansha Fortress |  |  | ✔ |
| Serpentis Corporation | Serpentis Fortress |  |  | ✔ |
| Rogue Drones | Radiance |  |  | ✔ |
| Angel Cartel | Angel Military Complex |  |  | ✔ |
| Blood Raiders | Blood Military Complex |  |  | ✔ |
| Guristas Pirates | Gurista Military Complex |  |  | ✔ |
| Sansha's Nation | Sansha Military Complex |  |  | ✔ |
| Serpentis Corporation | Serpentis Military Complex |  |  | ✔ |
| Rogue Drones | Hierarchy |  |  | ✔ |
| Angel Cartel | Angel Provincial HQ |  |  | ✔ |
| Blood Raiders | Blood Provincial HQ |  |  | ✔ |
| Guristas Pirates | Gurista Provincial HQ |  |  | ✔ |
| Sansha's Nation | Sansha Provincial HQ |  |  | ✔ |
| Serpentis Corporation | Serpentis Provincial HQ |  |  | ✔ |
| Angel Cartel | Angel Domination Fleet Staging Point |  |  | ✔ |
| Blood Raiders | Dark Blood Fleet Staging Point |  |  | ✔ |
| Guristas Pirates | Dread Guristas Fleet Staging Point |  |  | ✔ |
| Sansha's Nation | True Sansha Fleet Staging Point |  |  | ✔ |
| Serpentis Corporation | Shadow Serpentis Fleet Staging Point |  |  | ✔ |

> 最后一行 `<Faction> Fleet Staging Point` 常被当作 **9/10 的等价物**，但它属于无评级复合体（wiki 明确没有 9/10 评级）。

## 3. DED 评级复合体（50 条）

| 评级 | 最大可进船型 |
|---|---|
| 1/10 | 护卫 |
| 2/10 | 驱逐（含 T3 驱逐） |
| 3/10 | 巡洋（T3 巡洋除外） |
| 4/10 | 战巡（T3 巡洋除外） |
| 5/10 – 10/10 | 战列 / T3 巡洋 |

| 评级 | 势力 | 站点 | 高 | 低 | 00 |
|---|---|---|---|---|---|
| 1/10 | Angel Cartel | Minmatar Contracted Bio-Farm | ✔ | ✔ |  |
| 1/10 | Blood Raiders | Old Meanie - Cultivation Center | ✔ | ✔ |  |
| 1/10 | Guristas Pirates | Pith Robux Asteroid Mining & Co. | ✔ | ✔ |  |
| 1/10 | Sansha's Nation | Sansha Military Outpost | ✔ | ✔ |  |
| 1/10 | Serpentis Corporation | Serpentis Drug Outlet | ✔ | ✔ |  |
| 2/10 | Angel Cartel | Angel Creo-Corp Mining | ✔ | ✔ |  |
| 2/10 | Blood Raiders | Blood Raider Human Farm | ✔ | ✔ |  |
| 2/10 | Guristas Pirates | Pith Merchant Depot | ✔ | ✔ |  |
| 2/10 | Sansha's Nation | Sansha Acclimatization Facility | ✔ | ✔ |  |
| 2/10 | Serpentis Corporation | Serpentis Live Cargo Distribution Facilities | ✔ | ✔ |  |
| 2/10 | Rogue Drones | Rogue Drone Infestation Sprout | ✔ | ✔ |  |
| 3/10 | Angel Cartel | Angel Repurposed Outpost | ✔ | ✔ |  |
| 3/10 | Blood Raiders | Blood Raider Intelligence Collection Point | ✔ | ✔ |  |
| 3/10 | Guristas Pirates | Guristas Guerilla Grounds | ✔ | ✔ |  |
| 3/10 | Sansha's Nation | Sansha's Command Relay Outpost | ✔ | ✔ |  |
| 3/10 | Serpentis Corporation | Serpentis Narcotic Warehouses | ✔ | ✔ |  |
| 3/10 | Rogue Drones | Rogue Drone Asteroid Infestation | ✔ | ✔ |  |
| 4/10 | Angel Cartel | Angel Cartel Occupied Mining Colony | ✔ | ✔ |  |
| 4/10 | Blood Raiders | Mul-Zatah Monastery | ✔ | ✔ |  |
| 4/10 | Guristas Pirates | Guristas Scout Outpost | ✔ | ✔ |  |
| 4/10 | Sansha's Nation | Sansha's Nation Occupied Mining Colony | ✔ | ✔ |  |
| 4/10 | Serpentis Corporation | Serpentis Phi-Outpost | ✔ | ✔ |  |
| 4/10 | Rogue Drones | Drone Infested Mine | ✔ | ✔ |  |
| 5/10 | Angel Cartel | Angel's Red Light District | ✔ | ✔ |  |
| 5/10 | Blood Raiders | Blood Raider Psychotropics Depot | ✔ | ✔ |  |
| 5/10 | Guristas Pirates | Guristas Hallucinogen Supply Waypoint | ✔ | ✔ |  |
| 5/10 | Sansha's Nation | Sansha's Nation Neural Paralytic Facility | ✔ | ✔ |  |
| 5/10 | Serpentis Corporation | Serpentis Corporation Hydroponics Site | ✔ | ✔ |  |
| 5/10 | Rogue Drones | Outgrowth Rogue Drone Hive (5/10) | ✔ | ✔ |  |
| 6/10 | Angel Cartel | Angel Mineral Acquisition Outpost |  | ✔ | ✔ |
| 6/10 | Blood Raiders | Crimson Hand Supply Depot |  | ✔ | ✔ |
| 6/10 | Guristas Pirates | Guristas Troop Reinvigoration Camp |  | ✔ | ✔ |
| 6/10 | Sansha's Nation | Sansha War Supply Complex |  | ✔ | ✔ |
| 6/10 | Serpentis Corporation | Serpentis Logistical Outpost |  | ✔ | ✔ |
| 7/10 | Angel Cartel | Angel Military Operations Complex |  |  | ✔ |
| 7/10 | Blood Raiders | Blood Raider Coordination Center |  |  | ✔ |
| 7/10 | Guristas Pirates | Gurista Military Operations Complex |  |  | ✔ |
| 7/10 | Sansha's Nation | Sansha Military Operations Complex |  |  | ✔ |
| 7/10 | Serpentis Corporation | Serpentis Paramilitary Complex |  |  | ✔ |
| 8/10 | Angel Cartel | Cartel Prisoner Retention |  |  | ✔ |
| 8/10 | Blood Raiders | Blood Raider Prison Camp |  |  | ✔ |
| 8/10 | Guristas Pirates | Pith's Penal Complex |  |  | ✔ |
| 8/10 | Sansha's Nation | Sansha Prison Camp |  |  | ✔ |
| 8/10 | Serpentis Corporation | Serpentis Prison Camp |  |  | ✔ |
| 10/10 | Angel Cartel | Angel Cartel Naval Shipyard |  |  | ✔ |
| 10/10 | Blood Raiders | Blood Raider Naval Shipyard |  |  | ✔ |
| 10/10 | Guristas Pirates | The Maze |  |  | ✔ |
| 10/10 | Sansha's Nation | Centus Assembly T.P. Co. |  |  | ✔ |
| 10/10 | Serpentis Corporation | Serpentis Fleet Shipyard |  |  | ✔ |
| 10/10 | Rogue Drones | Outgrowth Rogue Drone Hive (10/10) |  |  | ✔ |

> 评级与出现空间：1/10–5/10 为高安 + 低安（其中 5/10 无法在高安自然刷新，只能由高安异常升级而来）；
> 6/10 为低安 + 00；7/10、8/10、10/10 仅 00；**没有 9/10**。

## 4. 远征（76 条）

> 「触发来源」一列照抄 wiki：`<Faction>` 是势力名占位符（例：Angel Cartel → `Angel Hideout`）；
> 斜杠后写的是无人机的对应项（如 `Haunted Yard`）。
> wiki 表里"Expeditions may appear in lower security systems."那三行是分隔说明、不是站点，已排除。

| 触发来源（无评级复合体） | 势力 | 远征名 |
|---|---|---|
| <Faction> Hideout Haunted Yard | Angel Cartel | Blue Pill |
| <Faction> Hideout Haunted Yard | Blood Raiders | Frentex |
| <Faction> Hideout Haunted Yard | Guristas Pirates | Sooth Sayer |
| <Faction> Hideout Haunted Yard | Sansha's Nation | Drop |
| <Faction> Hideout Haunted Yard | Serpentis Corporation | Mindflood |
| <Faction> Hideout Haunted Yard | Rogue Drones | Pulverize The Pioneers |
| <Faction> Lookout Desolate Site | Angel Cartel | Chasing the Dragon |
| <Faction> Lookout Desolate Site | Blood Raiders | Following the Blood |
| <Faction> Lookout Desolate Site | Guristas Pirates | Trap? |
| <Faction> Lookout Desolate Site | Sansha's Nation | Slave Breeding Plants |
| <Faction> Lookout Desolate Site | Serpentis Corporation | Angel Kickbacks |
| <Faction> Lookout Desolate Site | Rogue Drones | Mare Sargassum |
| <Faction> Watch Chemical Yard | Angel Cartel | The Nuclear Small Arms Project |
| <Faction> Watch Chemical Yard | Blood Raiders | Medical Twilight |
| <Faction> Watch Chemical Yard | Guristas Pirates | Terrorist Plot! |
| <Faction> Watch Chemical Yard | Sansha's Nation | Nation on the Rise |
| <Faction> Watch Chemical Yard | Serpentis Corporation | Jet-Set Hooligans |
| <Faction> Watch Chemical Yard | Rogue Drones | Hunting the Drudge Factory |
| <Faction> Vigil | Angel Cartel | The Big Blue |
| <Faction> Vigil | Blood Raiders | The Rewards of Devotion |
| <Faction> Vigil | Guristas Pirates | Kidnapped! |
| <Faction> Vigil | Sansha's Nation | True Power Shipyards |
| <Faction> Vigil | Serpentis Corporation | Booster R&D |
| Provisional <Faction> Outpost Rogue Trial Yard | Angel Cartel | Domination Surveillance Squad |
| Provisional <Faction> Outpost Rogue Trial Yard | Blood Raiders | Blood Surveillance Squad |
| Provisional <Faction> Outpost Rogue Trial Yard | Guristas Pirates | Gurista Surveillance Squad |
| Provisional <Faction> Outpost Rogue Trial Yard | Sansha's Nation | Sansha Surveillance Squad |
| Provisional <Faction> Outpost Rogue Trial Yard | Serpentis Corporation | Guardian Angels Surveillance Squad |
| Provisional <Faction> Outpost Rogue Trial Yard | Rogue Drones | Moving Day |
| <Faction> Outpost Dirty Site | Angel Cartel | Salvation Angel's Shipment |
| <Faction> Outpost Dirty Site | Blood Raiders | Save The Slaves |
| <Faction> Outpost Dirty Site | Guristas Pirates | Gurista Productions Shipment |
| <Faction> Outpost Dirty Site | Sansha's Nation | Hidden Riches |
| <Faction> Outpost Dirty Site | Serpentis Corporation | Elite Playground |
| <Faction> Outpost Dirty Site | Rogue Drones | Loose Ends |
| Minor <Faction> Annex Ruins | Angel Cartel | Angel Owned Station |
| Minor <Faction> Annex Ruins | Blood Raiders | Blood Owned Station |
| Minor <Faction> Annex Ruins | Guristas Pirates | Guristas Owned Station |
| Minor <Faction> Annex Ruins | Sansha's Nation | Sansha Owned Station |
| Minor <Faction> Annex Ruins | Serpentis Corporation | Serpentis Owned Station |
| Minor <Faction> Annex Ruins | Rogue Drones | Menacing Mechanics |
| <Faction> Annex | Angel Cartel | Angel Powergrid |
| <Faction> Annex | Blood Raiders | Blood Raider Powergrid |
| <Faction> Annex | Guristas Pirates | Guristas Powergrid |
| <Faction> Annex | Sansha's Nation | Sansha Powergrid |
| <Faction> Annex | Serpentis Corporation | Serpentis Powergrid |
| <Faction> DED 6/10 | Angel Cartel | Angel Mineral Acquisition Outpost, 2nd Part |
| <Faction> DED 6/10 | Serpentis Corporation | Serpentis Logistical Outpost Part 2 |
| <Faction> Base Independence | Angel Cartel | Toxic Waste Scandal! |
| <Faction> Base Independence | Blood Raiders | Religious Fury |
| <Faction> Base Independence | Guristas Pirates | Consequences Smonsequences |
| <Faction> Base Independence | Sansha's Nation | True Power HQ |
| <Faction> Base Independence | Serpentis Corporation | Contract Killers |
| <Faction> Base Independence | Rogue Drones | The Drone Roulette |
| <Faction> Fortress Radiance | Angel Cartel | Operation Spring Cleaning |
| <Faction> Fortress Radiance | Blood Raiders | Dubious Assignment |
| <Faction> Fortress Radiance | Guristas Pirates | Hired Gun |
| <Faction> Fortress Radiance | Sansha's Nation | Shady Operation |
| <Faction> Fortress Radiance | Serpentis Corporation | Suspicious Job |
| <Faction> Fortress Radiance | Rogue Drones | Molting Season |
| <Faction> Military Complex Hierarchy | Angel Cartel | Pioneers Peril |
| <Faction> Military Complex Hierarchy | Blood Raiders | Frontier in Flames |
| <Faction> Military Complex Hierarchy | Guristas Pirates | Pirate's Path |
| <Faction> Military Complex Hierarchy | Sansha's Nation | David V Goliath |
| <Faction> Military Complex Hierarchy | Serpentis Corporation | Colony Under Fire |
| <Faction> Military Complex Hierarchy | Rogue Drones | Trouble in Paradise |
| <Faction> Provincial HQ | Angel Cartel | Special Forces |
| <Faction> Provincial HQ | Blood Raiders | Fountain of Youth |
| <Faction> Provincial HQ | Guristas Pirates | No Quarter |
| <Faction> Provincial HQ | Sansha's Nation | The Ancient City |
| <Faction> Provincial HQ | Serpentis Corporation | Serpentis Secrets |
| <Faction> Fleet Staging Point | Angel Cartel | Angel Domination Fleet Staging Point 2 and 3 |
| <Faction> Fleet Staging Point | Blood Raiders | Dark Blood Fleet Staging Point 2 and 3 |
| <Faction> Fleet Staging Point | Guristas Pirates | Dread Guristas Fleet Staging Point 2 and 3 |
| <Faction> Fleet Staging Point | Sansha's Nation | True Sansha Fleet Staging Point 2 and 3 |
| <Faction> Fleet Staging Point | Serpentis Corporation | Shadow Serpentis Fleet Staging Point 2 and part 3 |

## 5. 化学实验室（34 条）

| 势力 | 站点 | 区域 |
|---|---|---|
| Angel Cartel | Angel Chemical Lab | Heimatar, Molden Heath |
| Angel Cartel | Angel Gas Processing Site | Heimatar |
| Angel Cartel | Elohim Sooth Sayer Distribution Base | Molden Heath |
| Angel Cartel | Elohim X-Instinct Distribution Base | Heimatar |
| Angel Cartel | Elohim Sooth Sayer Production Facility | Wicked Creek |
| Angel Cartel | Elohim X-Instinct Production Facility | Feythabolis |
| Angel Cartel | Digital Matrix | Feythabolis |
| Angel Cartel | Digital Network | Wicked Creek |
| Blood Raiders | Blood Raider Chemical Lab | Aridia |
| Blood Raiders | Blood Raider Gas Processing Site | ? |
| Blood Raiders | CHAIN Mindflood Distribution Base | Aridia |
| Blood Raiders | CHAIN Mindflood Production Facility | Delve |
| Blood Raiders | Digital Complex | Delve |
| Guristas Pirates | Guristas Chemical Lab | The Forge |
| Guristas Pirates | Guristas Gas Processing Site | The Forge |
| Guristas Pirates | H-PA Crew Blue Pill Distribution Base | The Forge |
| Guristas Pirates | H-PA Crew Crash Distribution Base | Lonetrek |
| Guristas Pirates | H-PA Crew Blue Pill Production Facility | Vale of the Silent |
| Guristas Pirates | H-PA Crew Crash Production Facility | Tenal |
| Guristas Pirates | Digital Plexus | Vale of the Silent |
| Guristas Pirates | Digital Convolution | Tenal |
| Sansha's Nation | Sansha Chemical Lab | ? |
| Sansha's Nation | Sansha Gas Processing Site | Derelik |
| Sansha's Nation | PDW-09FX Frentix Distribution Base | Derelik |
| Sansha's Nation | PDW-09FX Frentix Production Facility | Catch |
| Sansha's Nation | Digital Circuitry | Catch |
| Serpentis Corporation | Serpentis Chemical Lab | Solitude |
| Serpentis Corporation | Serpentis Gas Processing Site | Placid |
| Serpentis Corporation | Core Runner Drop Distribution | Placid |
| Serpentis Corporation | Core Runner Exile Distribution Base | Solitude |
| Serpentis Corporation | Core Runner Drop Production Facility | Cloud Ring |
| Serpentis Corporation | Core Runner Exile Production Facility | Fountain |
| Serpentis Corporation | Digital Compound | Fountain |
| Serpentis Corporation | Digital Tessellation | Cloud Ring |

## 6. 三神裔入侵（Pochven，9 条）

| 阵营 | 等级 | 站点 |
|---|---|---|
| Triglavian | Class 1 | Emerging Conduit |
| Triglavian | Class 2 | Minor Conduit |
| Triglavian | Class 3 | Major Conduit |
| Triglavian | Class 4 | Stellar Fleet Deployment Site |
| Triglavian | Class 5 | World Ark Deployment Site |
| EDENCOM | Class 1 | EDENCOM Forward Post |
| EDENCOM | Class 2 | EDENCOM Staging Area |
| EDENCOM | Class 3 | EDENCOM Field Base |
| EDENCOM | Class 4 | Observatory Flashpoint |

## 7. 战斗遗迹（7 条）

| 站点 | 区域 |
|---|---|
| Ancient Ruins | Catch（尤其 9HXQ-G 星座） |
| Bloated Ruins | Wicked Creek（尤其 760-9C 星座） |
| Crumbling Ruins | Vale of the Silent |
| Festering Ruins | Fountain |
| Forgotten Ruins | Delve |
| Hidden Ruins | Cloud Ring |
| Wispy Ruins | Feythabolis（尤其 I-3ODK 星座） |

## 8. 特殊站点

| 站点 | 说明 |
|---|---|
| ISHAEKA Tactical Response HQ | 仅 Monalaz 星座；极难，需强坦 Marauder / T3C |
| Blood Raider Temple Complex | Dark Blood Fleet Staging Point 的强化变体，需多人 |
| Blood Raider Temple Complex (The Inner Sanctum) | 上者的后续层 |
| Besieged Covert Research Facility | 低安，Mordu's Legion；全游戏仅此处有 Mordu 怪 |

**页面提到但未逐条列举**：

- Faction Warfare complexes（势力战争复合体，作为异常出现）
- Homefront Operations（高安小队内容）
- COSMOS sites（8 个区域，静态信标，页面未逐条列举）