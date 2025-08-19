[中文](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/README.md "中文Reamme")  | [English](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/README_en.md "English Reamme") 

# 新伊甸漫游指南
一个EVE Online实用工具集合APP，支持中英文

![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/zh/Home_Light.png?raw=true?raw=true)

---

# 新伊甸往事

多年前，闲来无事会弄一些eve的小工具自用，后面慢慢地功能就多了起来，虽然每个功能都很粗糙，代码也很暴力，但发现还是可以与大家分享的，于是推出了第一个版本新伊甸漫游指南
![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/homeV1.jpg?raw=true?raw=true)
如果你有幸看见过上面这个V1版本，那你现在一定是个巨佬了。但不幸，V1版本后续更新一段时间后，我弃坑了，软件及游戏都暂告一段落。

# 新生
弃坑多年后，发现新伊甸还流传着漫游指南的传说，于是决定重新开坑，把软件重构复活，软件框架从UWP升级到WinUI3（从一个坑到另一个坑，微软你行不行啊？），去除一些过时功能，也加入更多好用的新功能。

# 功能
- **角色:** 管理角色授权中心，以及查看角色个人信息，如技能点、技能训练队列、钱包余额、钱包日志、合同、邮件、工业
- **频道预警:** 通过监控聊天频道实时通知对敌对位置，类似于SMT、Near2
- **多开预览:** 将游戏画面显示在一个置顶的小窗口上，常用于多开时查看并快速切换到其他角色游戏窗口，类似于eve-o-preview
- **市场:** 查看各个星系或玩家建筑的市场订单、历史价格
- **商业:** 查看玩家市场上卖单或买单状态，比较不同的星系、星域间每一个物品价格差异及倒货利润
- **公开合同:** 查看各个星域公开合同信息
- **延迟测试:** 测试电脑与游戏服务器间网络延迟
- **翻译:** 游戏内专有名词中英互译
- **虫洞:** 查看虫洞等级、永连洞、漫游洞信息
- **死亡远征:** 部分死亡远征攻略（部分攻略可能已过时，仅供参考）
- **背景故事:** 收录了一些官方的背景故事
- **任务:** 上古时期国服wiki任务攻略（大部分已过期）
- **日志监控:** 监控游戏战斗日志、异常日志，对符合条件的信息发出通知
- **频道监控:** 监控聊天频道信息，对符合条件的信息发出通知
- **快速链接:** 收录了一些常用的官方、第三方网站链接
- **ZKillboard:** 通过zkillboard开放的API开放的青春版击杀榜
- **数据库:** 可以查看软件数据库信息
- **星图:** 游戏星图，可按安全等级、主权归属、行星资源查看，带有导航、主权插件需求表等小工具
- **频道玩家统计:** 统计频道内玩家势力，且可通过ZKillboard查询每一个玩家的威胁值、常用船等信息

## 项目说明
- **TheGuideToTheNewEden.Core** 基于.NET Standard 2.1的核心库，放与平台无关的代码，如Model、Helper、数据库管理等
- **TheGuideToTheNewEden.WinUI** 软件最主要项目，负责UI交互及数据处理，基于WindowAppSDK+WinUI3+.NET6，解决方案的**启动项目**，若想调试、修改软件代码请从此入手
- TheGuideToTheNewEden.WPF WPF版软件，仅供测试用
- TheGuideToTheNewEden.UWP UWP版软件，仅供测试用
- TheGuideToTheNewEden.ServerLogger V1版本遗留，目前未启用，用于在服务器记录每个小时游戏在线人数、每天贴吧活跃度
- TheGuideToTheNewEden.Updater 软件更新器，软件检查到更新后，启动该项目生成的exe去执行下载、解压
- ZKB.NET 将Zkillboard的API二次封装成.NET库，一键调用
- TheGuideToTheNewEden.PreviewWindow 使用WPF实现的多开预览窗口，通过内存映射将WPF与WINUI建立通讯，具体通讯实现在项目TheGuideToTheNewEden.PreviewIPC
- TheGuideToTheNewEden.ExefileSimulation 测试工具，游戏模拟器，模拟游戏进程供多开预览测试用、模拟聊天频道自动发言、模拟游戏日志
- TheGuideToTheNewEden.DevTools 配置生成工具，用来生成各种数据给到软件当作配置文件使用，如生成星系间位置关系的json文件
- TheGuideToTheNewEden.ExefileSimulation 测试工具，一个简单的控制台程序，模拟游戏进程供多开预览测试用，无实际功能
- TheGuideToTheNewEden.SystemCheck 测试工具，检查数据库星系名字与官方API的名字是不是一致
- TheGuideToTheNewEden.WhomholeCrawler配置生成工具，到网站上抓虫洞的信息
- ZKB.Net.Test 测试工具，测试ZKB.NET库的功能

## 软件截图
- 黑暗模式
  ![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/zh/Home_Dark.png?raw=true?raw=true)
  
- 频道预警
  ![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/zh/ChannelIntel.png?raw=true?raw=true)

- 市场
  ![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/zh/Market.png?raw=true?raw=true)

- 多开预览
  ![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/zh/Overview.png?raw=true?raw=true)

- 倒货
  ![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/zh/Scalper.png?raw=true?raw=true)

- ZKillboard
  ![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/zh/ZKB.png?raw=true?raw=true)

- 星图
  ![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/zh/Map.png?raw=true?raw=true)

- 频道玩家统计
  ![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/zh/ChannelScan_Statistics.png?raw=true?raw=true)
  ![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/zh/ChannelScan_Detail.png?raw=true?raw=true)


# 下载
[Releases](https://github.com/qedsd/TheGuideToTheNewEden/releases)

# 后续计划
软件会持续修BUG及添加更多功能，欢迎提bug提想法，但无法保证更新频率。

# 联系我
- 游戏ID：QEDSD
- [QQ群：784194289](https://jq.qq.com/?_wv=1027&k=m8Ttv1DX)
- ![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/qq.jpg?raw=true?raw=true)
