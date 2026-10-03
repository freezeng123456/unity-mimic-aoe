# 058 — 0.3.1 经典文明首批证据

英国、荷兰、法国、奥斯曼的核心陆战机制，加上保留的远驿盟市和山岭工联，共六文明。范围、公开资料和候选数值见[经典文明说明](../../Classic-Civilizations-058.md)。本版有自己的测试、矩阵和独立客户端身份，不借用历史版本完整人工对局、音频或性能作为新验收。

## 版本身份

| 字段 | 实际值 |
| --- | --- |
| 应用版本 / 内部构建号 | `0.3.1` / `0` |
| 最终源码提交 | `4d2f24afecfd9155bff4661837fbf0e407878d38` |
| 最终源码指纹 | `f49e6c08142d2b9513fe6e878a6e3df58ae3f802d1fbafd2078ba82e41b60577` |
| Unity / revision | `6000.6.3f1` / `45d8eee7de74` |
| 最终 Unity build GUID | `c1e7fe583e4f4e288cfa4fcd9885a543` |
| 初次构建 GUID（矩阵对应源码与两局原生整局） | `11d4fafce22a42ebb37dac73f0ef89fb` |
| 实际客户端 | macOS arm64，Development / Mono；299 文件、173258624 字节 |

最终证据文档提交晚于最终源码；最终交付包实际 HEAD 和 ZIP SHA 由仓库外 `manifest.json` 记录。

## 证据与结果

| 入口 | 实际结果与范围 |
| --- | --- |
| [Tests/source-evidence.json](Tests/source-evidence.json)、[EditMode](Tests/EditMode.xml)、[PlayMode](Tests/PlayMode.xml) | 最终完整 284 EditMode＋243 PlayMode，共527项全部通过；零失败、跳过或不确定。385文件指纹与当前源码一致，原始日志完整保留 |
| [Tests-Initial/source-evidence.json](Tests-Initial/source-evidence.json) | 初次源码79d88e2／指纹febbf21f…的526项全通过，绑定下面矩阵及初次构建，不改标为最终527项 |
| [Matrix/matrix.json](Matrix/matrix.json) | 36个有序组合，固定地图/seed314159、Standard、经济对军事预设、V上限；35自然结束、1山岭镜像在3600.061秒限时停止；初次源码前后稳定，未在最终UI源码重新运行 |
| [Matrix-analysis.md](Matrix-analysis.md)、[matrix-analysis.json](matrix-analysis.json) | 逐事件区分付费排队、真实训练完成、船运奖励和自动工人；涉及四个经典国的32组全部自然结束。超时不是战败或通过 |
| [Highland-Extended/matrix.json](Highland-Extended/matrix.json) | 同源码、同配置，独立把山岭镜像时限延长为5400秒，在3652.012秒自然结束；原3600秒超时记录保留，不能改称原矩阵36/36 |
| [Pilot/matrix.json](Pilot/matrix.json) | 正式矩阵前法奥双向2局自然结束；与矩阵对应样本重叠，不合计为独立场次 |
| [archive_matrix.py](archive_matrix.py) | 保留原matrix/result/catalog字节，事件和快照无损gzip；每个目录的compressed-events.json记录原始与压缩SHA/bytes，均已逐字节往返核验 |
| [Build.log](Build.log)、[build-receipt.json](build-receipt.json) | 直接BuildMac成功退出0，没有重建主场景；实际版本、全新GUID、运行库SHA绑定本轮通过的源码 |
| [Native-Initial/README.md](Native-Initial/README.md) | 初GUID两局法奥16x autoplay自然结算、8组截图、4报告；保留菜单溢出问题，未完成普通1x开局或原生鼠标按钮验收 |
| [Native/README.md](Native/README.md) | 最终GUID仅菜单回查：1600×1000奥斯曼完整介绍不遮挡按钮，1组F9与冻结日志；0对局，不把初GUID整局改称最终GUID实战 |
| [Menu-equivalence.md](Menu-equivalence.md)、[menu-equivalence.json](menu-equivalence.json) | 全部385文件精确核对：仅HUD显示和新增布局测试两路径变化，383文件包含Rules/Simulation/AI逐字节相同；526原测试保留且增加一项 |
| [client-bundle-manifest.json](client-bundle-manifest.json) | 本机app与实际客户端ZIP逐文件、目录、字节、SHA、Unix mode、CRC及GUID一致；不同于055/056/057历史GUID |
| [build_client_bundle_manifest.py](build_client_bundle_manifest.py) | 独立058清单生成器，只读取真实app和ZIP，不构建或伪造验收 |

本次矩阵实际付费完成法国猎民280、胸甲骑77、禁卫军293、阿布斯199、荷兰长戟兵143；奥斯曼自动生成工人377，手动付费招工0。5名Spahi来自船运并实际开火24次；胸甲骑范围命中321次。山岭猎炮有38次实际付费训练完成，补足057矩阵中仅由奖励出现的覆盖缺口。具体按队列/Spawn/付款/训练经验关联，报告关联错误0；本段仅汇总原36矩阵，不叠加pilot与延长样本。

山岭镜像在最后300秒仍有1039次Fire、81条Death和45次付费排队，延长样本前232717条事件与原样本逐字节相同；原超时是收尾耗时，不能因此证明所有组合都能在任意时限内结束。固定种子、单图、单对角色卡组与AI不构成统计平衡性证明。

## 验证边界

本轮覆盖核心机制、AI、规则、UI及模型回归和独立客户端运行。最终GUID只验收菜单，初GUID保留两局16x自动对战；旧矩阵与最终源码的引用由精确两文件差异支持，不改变旧记录身份。没有全文明完整人工I–V验收、正常与400单位各300秒的新性能窗口或新PCM音频专项。免费招工测试覆盖人口/工人上限、队列、暂停、多TC和完全堵出口；通用出生点搜索验证候选位置，不表示任意围堵地形的连通路径都已穷举。

数值是本项目候选值，尚无海军、工厂/英国火箭/奥斯曼巨炮、完整政治家/革命、全套皇家卫队/治疗、原住民结盟或官方完整卡池。平台签名、公证及跨机器安装未验收。完整范围见[已知限制](../../Known-Issues.md)。

最终交付使用[055打包器](../055/package_delivery.py)的 `--evidence-set 058 --playtest-guide Docs/Playtest-058.md`，核对干净HEAD全部Git文件、实际测试指纹和客户端清单。交付结论以实际生成的仓库外manifest为准。

## 菜单修正的完整记录

原生检查发现奥斯曼长简介覆盖卡组按钮。修正仅涉及HUD与布局回归；说明区域从80增至104、卡组/开始/退出按钮下移24，同时允许14–17字号适配。

- [Tests-Menu-First](Tests-Menu-First)：第一轮284 EditMode通过，PlayMode 242/243；新用例误把像素字号与逻辑字号直接比较，原报告与日志保留。
- [Tests-Menu-Targeted-First](Tests-Menu-Targeted-First)：修正测量单位后的定向用例仍发现小比例全文实际越界，0/1通过；通过增加实际空间修复，没有放宽越界断言。
- [Tests-Menu-Targeted-Final](Tests-Menu-Targeted-Final)：六文明、测试窗口比例和原生比例下完整字形与按钮几何检查1/1通过。最终全量527/527与实际菜单截图分别保存在Tests与Native。
