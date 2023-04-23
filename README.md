# 新伊甸漫游指南
EVE Online实用工具集

![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/home.png?raw=true?raw=true)

---

# 新伊甸往事

几年前，闲来无事会弄一些eve的小工具自用，后面慢慢地功能就多了起来，虽然每个功能都很粗糙，代码也很暴力，但发现还是可以与大家分享的，于是推出了第一个版本新伊甸漫游指南
![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/homeV1.jpg?raw=true?raw=true)
如果你有幸看见过上面这个V1版本，那你现在一定是个巨佬了（伸出乞讨的小手）。

但不幸，V1版本后续更新一段时间后，我弃坑了，软件及游戏都暂告一段落。

# 新生
半年前，上班闲得慌，逛贴吧无意间发现还有人记得新伊甸漫游指南，甚是惊喜，随后决定把V1版本的代码整理一下开源，也算是放到GitHub上备份一下避免丢失。很巧，后面紧接着就出官中了，发现有人对中文星系频道预警有需求，辅助多开的eve-o-preview因为中文bug标题栏不显示角色名称导致多开窗口显示异常，嘿嘿，这道题我会。然后就有了
新伊甸漫游指南V2

# 功能
新版本不会加入更多新功能，甚至还会砍掉不少功能，更多功能是V1版本重构。目前确定有的功能如下
- 频道预警-已完成
- 多开预览-已完成
- 角色中心-开始重构
- 市场-等待重构
- 商业-等待重构

其他功能目前V1版本应该还是可以用的，暂不提上V2版本规划，如果时间允许，会考虑全部复活。

## 频道预警
频道预警是模仿near2、SMT之类的预警软件而来，监控预警频道本地日志文件，捕获关键词，通过弹窗、系统通知、声音来提醒玩家。
![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/intel1.png?raw=true?raw=true)
简单使用说明：
1. 选择要监听的角色
2. 选择预警频道
3. 选择角色当前位置，默认会通过玩家本地频道日志分析玩家当前位置
4. （可选）勾上自动更新位置，玩家移动到别的星系时，预警功能会自动同步玩家当前位置。
5. 设置预警跳数，当相对于玩家当前位置多少跳范围内的预警才需响应
6. 设置预警口显示方式，如果选择必要时才显示，那就当有预警时才会显示。
7. 设置窗口透明度
8. （可选）勾上自动解除预警，设置时间，星系从最后一次预警到设定时间内都没有新预警，则自动取消该星系预警状态（预警窗口星系不再红色高亮显示）
9. （可选）勾上自动降低预警级别，设置时间，星系从最后一次预警到设定时间内都没有新预警，预警窗口自动将星系预警状态由红色变为黄色
10. 设置预警声，勾上后在发生预警时电脑会播放声音，可选择播放声音文件
11. 设置系统通知，勾上会由Windows系统发出通知
12. 设置忽视关键词，聊天里有出现相关关键词时，不触发预警
13. 设置解除预警关键词，聊天里有出现相关关键词时，相应的星系不再处于预警状态
14. 设置星系名语言数据库，与near2、smt等前辈不同，新伊甸漫游指南不仅支持英文星系名，还可以支持其他语言星系名，只要按照规范在软件目录Resources\Database\Local下放置其他语言数据库就可在此处列表中显示，勾上相应数据库后即可支持相应语言的星系名预警，默认只提供中文。当然，很多00玩家会吐槽这个功能用处不大，00又没有中文名，但我只是在纯粹解决一个有没有的问题。
15. 设置完参数就可以点击开始监控了，可鼠标拖动预警窗口显示大小、位置

### 效果图
- 预警窗口：
- ![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/intel2.png?raw=true?raw=true)

- 鼠标移动到预警窗口星系位置会显示该星系名及距离，以及连线显示其周围一跳范围内星系
- ![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/intel3.png?raw=true?raw=true)

- 有预警时相应星系会红色高亮显示
- ![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/intel4.png?raw=true?raw=true)

- 如果勾上系统通知，有预警时会在系统通知中心弹出信息
- ![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/intel5.png?raw=true?raw=true)

- 如果软件设置了中文数据库，预警窗口的信息名将由中文显示
- ![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/intelInZhDb.png?raw=true?raw=true)

- 如果勾选了多个星系名数据库，可同时支持多种星系名预警
- ![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/intelMutilLang.png?raw=true?raw=true)

- 支持多个角色同时开启预警
- ![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/mutilIntel.png?raw=true?raw=true)

## 多开预览
eve-o-preview青春版，将游戏画面置顶显示在系统上，这样一来多开时就可以操作一个游戏窗口时还可以看到别的游戏进程实时画面。
![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/preview0.png?raw=true?raw=true)
简单使用说明：
1. （可选）设置游戏进程名，刷新游戏列表，实际上游戏进程名是固定的exefile，所以可以忽略这个设置。
2. 选择要预览的游戏进程
3. （可选）设置标识名，如果游戏是英文，此处会自动识别出角色名称，并以此名字自动匹配以前可能使用过的设置，如果此标识名没有使用过，加载默认配置。
4. （可选）对于设置了中文的游戏，因为游戏标题栏不显示角色名，标识名会识别异常，可以通过标识名右边的列表按钮选择以前使用过的设置，或者新建设置。
5. 设置是否要显示预览窗口的标题栏
6. 设置预览窗口的透明度
7. (可选)设置快速显示此游戏窗口的快捷键
8. （可选）设置在游戏间循环切换的快捷键
9. 设置完后点击开始即可看到预览窗口，可鼠标拖动显示大小、位置


### 效果图
- 整体效果：
![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/preview1.png?raw=true?raw=true)
- 英文下自动识别角色名称：
![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/previewInEn.png?raw=true?raw=true)


## 支持黑暗模式
![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/darkthemeHome.png?raw=true?raw=true)
![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/darkthemeAll.png?raw=true?raw=true)

# 下载
[Releases](https://github.com/qedsd/TheGuideToTheNewEden/releases)

# 后续计划
目前虽然只有两个功能可用，但是bug多呀，修修补补又一年。争取在接下来一两个月内把角色中心、市场、商业三个功能重构完，后续的功能就随缘了。

# 联系我
- 游戏ID：QEDSD
- [QQ群：784194289](https://jq.qq.com/?_wv=1027&k=m8Ttv1DX)
- ![Img](https://github.com/qedsd/TheGuideToTheNewEden/blob/master/Img/qq.jpg?raw=true?raw=true)
