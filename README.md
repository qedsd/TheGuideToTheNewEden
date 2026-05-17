[中文](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/README.md "中文Reamme")  | [English](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/README_en.md "English Reamme") 

# 新伊甸漫游指南
这是一个免费、开源的适用于游戏EVE Online的辅助工具集合软件，涵盖了商业管理、预警提醒、多开预览、攻略查询等多种功能。所有功能都不会违反游戏的最终用户许可协议（EULA），请放心使用。


软件部分功能、实现思路源自社区内各位前辈的开发成果，在此特向他们致以诚挚的敬意与感谢。同时，也衷心感谢在开发过程中积极提出想法、协助发现问题的所有玩家。

![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/zh/Home_Dark.png?raw=true?raw=true)

## 运行环境
- 电脑系统：Windows 10 19041.0及以上，推荐Window 11。不支持各种LTSC、特供版、精简版Windows系统，不支持Mac系统，不支持Linux系统，无手机版本。
- .NET 桌面运行时 9.0 ：[.NET 9.0](https://dotnet.microsoft.com/zh-cn/download/dotnet/9.0)

## 下载
[Releases](https://github.com/qedsd/TheGuideToTheNewEden/releases)

---

# 功能
- **角色:** 管理角色授权中心，以及查看角色个人信息，如技能点、技能训练队列、钱包余额、钱包日志、合同、邮件、工业
- **市场:** 查看各个星系或玩家建筑的市场订单、历史价格
- **订单:** 查看个人、军团订单列表
- **倒货:** 对比、推荐两个市场间不同物品利润差额
- **多开:** 将游戏画面显示在一个置顶的小窗口上，常用于多开时查看并快速切换到其他角色游戏窗口，类似于eve-o-preview
- **频道预警:** 通过监控聊天频道实时通知对敌对位置，类似于SMT、Near2
- **频道监控:** 监控聊天频道信息，对符合条件的信息发出通知
- **频道统计:** 统计频道内玩家势力，且可通过ZKillboard查询每一个玩家的威胁值、常用船等信息
- **频道查价:** 游戏内聊天频道发送物品软件会弹窗显示物品市场价格
- **日志监控:** 监控游戏战斗日志、异常日志，对符合条件的信息发出通知
- **翻译:** 查询游戏专有名词中英文翻译、加入游戏专有名词库后的通用文本翻译
- **死亡远征:** 部分死亡远征攻略（部分攻略可能已过时，仅供参考）
- **任务攻略:** 上古时期国服wiki任务攻略（大部分已过期）
- **虫洞:** 查看虫洞等级、永连洞、漫游洞信息
- **快速链接:** 收录了一些常用的官方、第三方网站链接
- **星图:** 游戏星图，可按安全等级、主权归属、行星资源查看，带有导航、主权插件需求表等小工具
- **ZKillboard:** 通过zkillboard开放的API开放的青春版击杀榜
- **数据库:** 可以查看软件数据库信息
- **快捷输入:** 避免内置输入法看不见候选词用的打字窗口
- **游戏延迟:** 测试电脑与游戏服务器间网络延迟
- **按键间隔:** 监控指定按键过去多长时间、按下多少次，数鱼雷专用

## 软件截图
- 白色模式
  ![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/zh/Home_Light.png?raw=true?raw=true)

- 角色中心
  ![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/zh/Characters.png?raw=true?raw=true)

- 频道预警
  ![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/zh/Intel.png?raw=true?raw=true)

- 市场
  ![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/zh/Market.png?raw=true?raw=true)

- 多开预览
  ![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/zh/Overview.png?raw=true?raw=true)

- 倒货
  ![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/zh/Business.png?raw=true?raw=true)

- ZKillboard
  ![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/zh/ZKB.png?raw=true?raw=true)

- 星图
  ![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/zh/Map.png?raw=true?raw=true)



# 项目说明
- **TheGuideToTheNewEden.Core** 基于.NET Standard 2.1的核心库，放与平台无关的代码，如Model、Helper、数据库管理等
- **TheGuideToTheNewEden.WinUI** 软件最主要项目，负责UI交互及数据处理，基于WindowAppSDK+WinUI3+.NET9，解决方案的**启动项目**，若想调试、修改软件代码请从此入手
- TheGuideToTheNewEden.ServerLogger V1版本遗留，目前未启用，用于在服务器记录每个小时游戏在线人数、每天贴吧活跃度
- ZKB.NET 将Zkillboard的API二次封装成.NET库，一键调用，ZKB.Net.Test为该库的测试项目
- TheGuideToTheNewEden.PreviewWindow 使用WPF实现的多开预览窗口，通过内存映射将WPF与WINUI建立通讯，具体通讯实现在项目TheGuideToTheNewEden.PreviewIPC
- TheGuideToTheNewEden.ExefileSimulation 测试工具，游戏模拟器，模拟游戏进程供多开预览测试用、模拟聊天频道自动发言、模拟游戏日志
- TheGuideToTheNewEden.DevTools 配置生成工具，用来生成各种数据给到软件当作配置文件使用，如生成星系间位置关系的json文件
- TheGuideToTheNewEden.SystemCheck 测试工具，检查数据库星系名字与官方API的名字是不是一致
- TheGuideToTheNewEden.WhomholeCrawler 配置生成工具，到网站上抓虫洞的信息
- TheGuideToTheNewEden.SDEBuilder 将官方SDE转换为sqlite数据库，一键生成软件需要的中英文数据库
- TheGuideToTheNewEden.CrashReporter 软件崩溃报告器

# 新伊甸往事
## V1
![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/homeV1.jpg?raw=true?raw=true)
## V2
![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/HomeV2.png?raw=true?raw=true)


# 后续计划
软件会持续修BUG及添加更多功能，欢迎提bug提想法，但无法保证更新频率。

# 联系我
- 游戏ID：QEDSD
- [QQ群：784194289](https://jq.qq.com/?_wv=1027&k=m8Ttv1DX)
- ![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/qq.jpg?raw=true?raw=true)
