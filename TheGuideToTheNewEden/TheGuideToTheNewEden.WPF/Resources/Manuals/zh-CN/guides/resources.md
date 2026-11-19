---
id: guides/resources
title: 攻略 · 资源
group: Nav.Guides
page: Views/Pages/ResourcesPage.xaml
tools: [ded_info]
aliases: [资源, 矿石, 气云, 冰带, 遗迹, 数据站点, ore, gas, ice, relic, data site]
updated: 2026-11-18
---

# 攻略 · 资源

## 一句话 {#tldr}

采矿与探索去处的**图鉴**：矿石站点、气云站点、冰带、战斗遗迹、数据站点，共 171 个，
左边列表加分类筛选，右边是矿物构成、波次与打法的中文正文。

## 数据从哪来 {#source}

EVE University Wiki 的五个分类（`Ore sites` / `Gas sites` / `Ice belts` / `Relic sites` / `Data sites`），
存在 `static.db` 的 `CombatSite` 表里，分类值 11 / 12 / 13 / 5 / 10。

## 怎么用 {#usage}

1. 左上搜索框打中英文名（`Crokite`、`冰带` 都行）。
2. 左下分类筛选：全部类型 / 战斗遗迹 / 数据站点 / 矿石站点 / 气云站点 / 冰带。
3. 点左边任一条，右边显示该站点的正文：属性行（区域、船型限制）、矿物成分表、
   NPC 波次表、打法说明。

## 分类说明 {#categories}

| 分类 | 是什么 | 例子 |
|---|---|---|
| 矿石站点 | 小行星带异常的矿物构成（矿种、小行星数、单位体积） | `Average Crokite and Dark Ochre Deposit` |
| 气云站点 | 可采集气云，含气云总量与守卫 | `Red Dragonfly Nebula` |
| 冰带 | 冰矿带，分高安/低安不同成分 | `Clear Icicle Belt` |
| 战斗遗迹 | 冬眠者缓存（Sleeper Cache）等高价值遗迹 | `Superior Sleeper Cache` |
| 数据站点 | 可破译的数据遗迹（Abandoned Research Complex） | `Abandoned Research Complex DA005` |

虫洞站点不在这里——它在「战斗异常」页的列表里，分类显示「冬眠者」、出现空间显示「虫洞」。

## 常见问题 {#faq}

**矿物数量是固定的吗？**
wiki 给的是平均样本（站点名里的 `Average` / `Small` / `Large` 就是规模档），实际会有浮动。

**为什么有的站点正文只有一张表？**
矿石/气云/冰带站点本身就只是"成分表"，没有攻略可写；遗迹与数据站点才有多层走法与波次。
