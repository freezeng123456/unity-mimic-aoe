# 0.6.0 原生构建、窗口与短时性能

构建版本 0.6.0，GUID `3aa99dedafee4a34af86680f9cee7cab`；代码 `4f6f0e8`，源码指纹 `f634abbf679f22bc9fcf5d50921a65519eedd7668ef6bf241cf6505238d31dd8`。Unity 6000.6.3f1、Development / Mono / macOS arm64。构建成功且 exit 0，二进制为 arm64；[构建回执](build-receipt.json) 绑定 XML 与二进制 SHA。

## 真实窗口操作

使用 Computer Use 的 @oai/sky 操作实际客户端。菜单六图循环、选择蜿蜒河谷并正常开局、T 选城镇和生产面板、小地图移动镜头均有新鲜窗口截图。[ui-check.json](ui-check.json) 记录动作和截图名。最初连接返回了缓存图片并启动重复实例；关闭重复实例、重置 Node kernel、使用 disableDiff:true 后得到新鲜状态。早期误命名文件未归档为通过证据。

一次菜单、两次正常开局世界 F9，以及两次性能夹具自动截图，均出现 FRONTIER_CAPTURE_OK。五组 PNG/JSON 逐字节核对，PNG SHA 等于完成收据，GUID/会话一致；见[capture-verification](capture-verification.json)。菜单收据 mapId 为空，蜿蜒地图选择由菜单窗口图证明；世界收据 mapId 为 winding。此次没有完成手动付费建港、造船、指定攻击或玩家管理的完整战局，相关行为有独立自动回归而非本次原生人工成绩。

## 原生性能

两个样本串行运行，没有 Unity 编辑器、测试或压缩并行。Apple M3 Pro / 36 GB、1600×1000，60 FPS cap、vSync off，镜头正交尺寸 26。每组 90 活跃秒，前 10 秒预热，无暂停。夹具直接生成 200/400 混合陆军及 12 船，双方以 AttackMove 反复移动/战斗，HP 提升以维持数量；不是正常战局。

| 陆军+船 | 平均 / P95 / P99 ms | 测量最大 / 含预热最大 ms | 托管增长 MB | Unity 分配 MB | GC0/1/2 各次数 | 镜头内陆军/船 |
| --- | --- | --- | ---: | ---: | ---: | --- |
| 200+12 | 23.10 / 66.50 / 82.00 | 96.77 / 2683.85 | 7.80 | 324.27 | 1457 | 127 / 4 |
| 400+12 | 28.86 / 75.34 / 86.56 | 114.59 / 3044.72 | 11.52 | 526.63 | 2263 | 244 / 4 |

MB 使用十进制。全部 212 / 412 单位结束存活；可见与镜头内数分别记录，不能称为全部单位同时在屏幕上。样本分别推进 88.899 / 88.199 模拟秒。原始 JSON 与 SHA 在[benchmark-summary](benchmark-summary.json)；两个原报告未经修改。

大规模战斗仍有长帧、启动尖峰和频繁 GC，没有稳定 60 FPS 成绩。navigation/combat/economy 每 Tick 字段是在每帧读取上一个 Tick 值，不是整个窗口的真实 Tick 平均。90 秒窗口不能证明长期内存稳定或泄漏根因。三个进程正常 exit 0，但退出日志仍有 prematurely finalized threads 和 MemoryLeaks 遥测，需后续定位。

测试日志与构建/原生日志的归档副本将 licensing 标识和 IP 地址脱敏；原始文件本地保留，回执分别记录原始与脱敏 SHA。截图、JSON 和测试 XML 未做内容改写。
