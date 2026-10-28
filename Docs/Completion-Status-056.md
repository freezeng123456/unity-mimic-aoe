# 完成状态与交付证据 — 0.2.32

2026-10-03，原定 **A–D 四个里程碑全部通过**，0.2.32的完整回归、独立构建、原生菜单/开局/采集/退出及客户端ZIP核验通过。范围以[开发合同](Development-Contract.md)为准。源码包、中文指南与最终提交清单由已保存的打包程序在干净提交后生成；实际交付身份以输出目录的 `manifest.json` 为准。

## 原定里程碑：0.2.31完整原生证据

| 里程碑 | 结果与证据 |
|---|---|
| A：基础RTS | 通过。Unity/URP独立工程，选择、命令、采集、建造、人口、生产、战斗、修理和驻军均有原生记录；群体导航有绑定源码的自动回归。 |
| B：海岸镜像I–III完整局 | 通过。[B10](Evidence/055/NormalPlayer/coast-attempt-010/summary.json)正常1x、Economy25、autoplay=false，严格17/17；付费炮186对敌马厩156六次各130伤害。958.612976秒自然战败，无Resign、败方清零。 |
| C：河谷I–V与文明差异 | 通过。[C7](Evidence/055/NormalPlayer/river-attempt-007/summary.json)同局17/17检查、19/19覆盖，2528.415771秒自然获胜；I–V、银行、特色兵、付费科技、炮击敌建筑、宝藏、贸易XP及资源交付全部成立。 |
| D：原创视听与规模 | 通过。原创人物/骑兵/火炮、11类建筑、程序动画；正常/暂停/主静音各20秒真实混音。50/100/200/400活动规模核验通过，正常与400长窗各自满足300有效秒。 |

正常玩家B9单窗335.999332有效秒，400单位单窗317.674076有效秒，两窗独立核验，不相加。详见[性能实测](Performance-055.md)；不推导400单位无遮挡同屏或通用FPS保证。

C7 `cd717dda89684d87b979629d3fbdf1a9` 使用River镜像/Easy/V/Military25，正常1x且无autoplay。116,739条事件中唯一MatchEnded，败方存活单位/建筑为0，无Resign；24组原生F9及真实胜利JPEG均保全。97文件共220,994,877字节，[归档receipt](Evidence/055/NormalPlayer/river-attempt-007/archive-receipt.json) SHA256为 `73304c7dcf6d9541950b34479ece0ff691f39642d48b996833ad8dd3ba44027e`，全部96成员bytes/SHA及集合已独立复核。

同一老Musk221升级前后由200HP/24ATK变为240HP/29ATK；新付费Musk290在科技完成后出生，并显示240HP/29ATK。原始截图、输入上下文和付费/出生/死亡链关联见[科技专项审核](Evidence/055/NormalPlayer/river-attempt-007/technology-visual-audit.json)。付费炮261对敌贸易站179的130伤害与原图1648→1518血量一致。

C7终局尾部Escape直接返回菜单，缺少终局F9及菜单前Player-ended副本；[偏差记录](Evidence/055/NormalPlayer/river-attempt-007/end-capture-deviation.json)如实保留。实际胜利JPEG、完整冻结日志、两份终局报告/事件及返回菜单F9共同支持自然结算，没有伪造缺失文件。C1–C6原失败结论不变，详见[里程碑历史](Milestones-055.md)。

## 0.2.32独立验证与来源

| 项目 | 0.2.31验收基线 | 0.2.32交付客户端 |
|---|---|---|
| 源码提交 | `50d303b76519a3a9b448c27cd2cdeb22779a1e5d` | `59a66b64c027224289761ad6bb409e8e41804fee`；最终文档/证据提交由外部manifest记录 |
| 源码指纹 | `1b45f97f3d90c9b76388419492a83c5622ccb2cf3a365030044403ccd309c1c7` | `7340a04ba7f4d10c35a4dde158a8c517e0205e875daacc1a58005dcb61730adf` |
| 构建GUID | `128a731d440e4b7a94abfcc74b88fa97` | `947f7c0c0da44ec3a806c2dcb69ca04e` |
| Unity全量测试 | 214 EditMode + 210 PlayMode通过 | 新一轮214 + 210通过，424唯一测试身份一致，无跳过/不确定项 |
| 构建 | macOS arm64，Development / Mono | 新建299文件、173,040,973字节；Unity6000.6.3f1直接BuildMac成功 |
| 原生验证 | 完整B/C对局、视听与性能 | 独立菜单→普通开局→实际采集→返回菜单→正常QUIT |

仅 `FrontierHud.cs` 两处菜单文字和 `ProjectSettings.asset` 的bundleVersion改变，其他355个清单文件逐字节一致。源码规则、AI、资源和BuildTools保持原字节；[等价审核](Evidence/056/release-equivalence.json)核对准确差异、新XML时间/内容、完整测试身份、新GUID和runtime SHA。新完整测试在11:53:40Z/11:54:55Z结束，原始五文件见[056/Tests](Evidence/056/Tests/source-evidence.json)。

macOS营销版本为0.2.32，内部构建号沿用冻结设置 `buildNumber.Standalone=0`。初始核验脚本误把这两个字段视作同一版本，现已按固定基线设置纠正，18项工具测试通过；[诊断记录](Evidence/056/build-number-review.json)保留原假设与实际plist字节，源码及客户端没有因此改动。

新客户端普通检查 `fd093483132f4bff991d9082260fd7a4` 持续27.400068秒、1x、autoplay=false，实际采得46.799976食物，随后主动回菜单并正常退出。这是启动/操作检查，不是自然整局；证据见[NativeSmoke](Evidence/056/NativeSmoke/README.md)和[新构建回执](Evidence/056/build-receipt.json)。

## 交付文件

交付目录 `outputs/frontier-reckoning-0.2.32` 包含客户端ZIP、Unity源码ZIP、`试玩说明.md`及`manifest.json`。客户端ZIP SHA256为 `f3ec2180ef38c602db43cc5a1db001cf162ec4634cc066704c02492eb16e9fef`；其299个应用文件和全部618个归档文件已经按bytes/SHA/Unix mode、目录集合、CRC、版本和GUID核验，见[客户端清单](Evidence/056/client-bundle-manifest.json)。

最终源码包通过[打包程序](Evidence/055/package_delivery.py)绑定打包时实际clean HEAD，核对每个已提交Git blob及357个测试源码SHA。交付manifest保存在Git仓库外，记录最终提交、各包大小/SHA和这些内容检查；不要求受跟踪文档嵌入包含自身的提交号。

**0.2.31 acceptance evidence retained; 0.2.32 menu/version-only delta separately verified.** 支持范围仍为测试过的macOS arm64开发构建，其他平台、存档、联网、战役、海战和编辑器不属于此次退出条件。详见[已知限制](Known-Issues.md)。
