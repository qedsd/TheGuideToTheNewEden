# EVE Simulation

给主程序（TheGuideToTheNewEden.WPF）造日志的开发工具：模拟**聊天频道日志**、**游戏日志**和**战斗日志**，不用真的上游戏就能测频道预警、日志监控、多开战斗叠加层。

窗口标题用于模拟多开预览的游戏窗口：**未开始时是 `EVE`，点「开始」后变成 `EVE - 角色名`**（主程序按标题第一个 `-` 之后取角色名，进程名同样是 `exefile`），「停止」后恢复 `EVE`。

## 界面

左侧是**角色列表**（新增 / 复制 / 删除），选中角色后在右侧配置；每个角色独立保存自己的频道、发言人和战斗选项。配置写在 exe 同目录的 `Configs.json`。

| 页签 | 内容 |
| --- | --- |
| 角色 | 角色名称 / ID、军团 ticker、舰船（中文名 + 英文 hint）、是否模拟聊天 / 游戏日志 |
| 频道日志 | 聊天频道表格、发言人表格（名称 + 常用舰船）、自动模拟（星域多选，消息会带上舰船名）、手动发言 |
| 战斗日志 | 对手角色、事件类型（伤害/未命中/毁电/吸电/遥修/反跳/ECM）、数值、自动战斗、手动发送 |
| 原始日志 | 手写一行游戏日志（原样写入） |

战斗日志行式与真实中文客户端一致（2026-11-19 日志校准）：

```
[ 2026.11.20 10:09:33 ] (combat) 258 来自 <localized hint="Merlin">小鹰级*</localized> [I K N] [QEDSD2] - <localized hint="Vespa II">胡蜂 II*</localized> - 轻轻擦过
[ 2026.11.20 10:09:33 ] (combat) 45 GJ能量中和 <localized hint="Merlin">小鹰级*</localized> [I K N] [QEDSD2] - - <localized hint="Small Energy Neutralizer I">小型能量中和器 I*</localized>
[ 2026.11.20 10:09:34 ] (combat) 来自 <localized hint="Merlin">小鹰级*</localized> [I K N] [QEDSD2] - 试图跃迁扰频 你！
```

“同时生成出向”会额外写“我打/我修别人”的行，用来验证主程序的**方向区分**：日志窗口里主数值是“承受”、第二行小字是“输出”（只有输出时主数值带「输出」标记）；多开叠加层只显示“承受”侧。

## 从 WPF 导入角色

左侧「**从 WPF 导入**」→ 选择 TheGuideToTheNewEden 的数据目录（默认 `%LocalAppData%\TheGuideToTheNewEden`，里面应有 `Configs`）：

- **名称 + ID**：`Configs/Auth.json` 的 `CharacterName` / `CharacterID`（最可靠）；
- 只有 ID：`Configs/GameLogInfoSettings.json` 的 `ListenerID`，用**游戏日志头**（文件名里的 ID + `收听者:`）补名字；
- 只有名字：`ChannelMonitorSetting.json` / `IntelSettings.json` / `ChannelMarketSettings.json` / `ChannelTranslationSettings.json` / `GamePreviewSetting.json` 的 `ProcessOrder`，用 Auth 或日志头补 ID。

两边都凑不齐的条目会跳过（保证导入的是真实名称 + 真实 ID），占位名（`*Untitled[...]`）也会跳过。导入后按名称去重：已有角色更新 ID，同名更新名字，其余新增（默认伊毕斯号/本地频道）。

命令行自检：

```
exefile.exe --importtest "C:\Users\<你>\AppData\Local\TheGuideToTheNewEden"
```

结果写到 `%TEMP%\wpf-import-test.txt`（每行：名称 | ID | 来源）。

## 无界面自检

```
exefile.exe --selftest D:\temp\EVECombatSimTest
```

会生成一份包含全部事件（进/出向各一条）的游戏日志；再用主程序仓库里的解析探针回归：

```
probe\CombatLogProbe\bin\Debug\net8.0\CombatLogProbe.exe "D:\temp\EVECombatSimTest\gamelogs"
```

## 日志位置

日志根目录默认取**游戏真实路径**：优先 TheGuideToTheNewEden.WPF「设置 → 游戏日志」里配置的 `EVELogsPath`，没配置时用 `我的文档\EVE\logs`（EVE 客户端默认写这里）。顶栏「**恢复默认**」可随时恢复；启动时发现配置里是 `%TEMP%` / `D:\temp` 这类调试路径也会自动纠正。

- 聊天：`<日志根目录>\Chatlogs\频道名_日期_时间_角色ID.txt`（UTF-16，与真实聊天日志一致）
- 游戏/战斗：`<日志根目录>\gamelogs\日期_时间_角色ID.txt`（UTF-8，与真实游戏日志一致）

主程序「设置 → 游戏日志」指到同一个根目录即可读到。
