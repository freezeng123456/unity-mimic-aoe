# 077 · 曲河精修验证记录

2026-10-10。新输出目录：`/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.20.0/Candidate1/`。旧076/C1及C2证据保留，未重建或重标。

## 接入

审查后的 V2 补丁 SHA256 `8afef9bac7ab00a1ad2ddf0a6dba95ea22b7a8b7b77d0ba0f276eb3f07a230b4` 已实际应用九个路径，包含 0.20.0 版本提升。接入后源码 FP `fdd60bfa136f944cee07e75b157bf8143a7ca63da11171d517feafb57a56716d`。版本提升避免把改变的 Winding 高度语义混入旧0.19.0存档／录像；本轮没有存档迁移承诺。

独立静态审查已核对：原始中心线的内外岸 oracle、真实共享布局恢复消费者，以及实际地表 mesh.colors 接线。静态审查不是编译或画面通过。

## Bank1

真实 Unity 专项已闭合：17 EditMode + 2 PlayMode 全过，0失败／跳过，两个进程 exit0，源码 FP `fdd60bfa...` 运行前后一致。XML分别耗时14.476及1.828秒；原件在新输出目录的 Bank1。仅是专项合同与实际网格数据验收，不是渲染／原生／FPS结论。

## Mutations1：检测灵敏度

在单独记录的临时源码身份下执行两项故意破坏，每项只跑指定的一例；两项都产生预期失败 exit2，并在 finally 中将源码逐字节恢复为 Bank1 指纹。原始失败 XML／日志保留，不重标生产版本失败或通过。

| 临时破坏 | 实际检测 |
|---|---|
| StoreBankBend 岸侧偏移反向 | 独立西内岸 oracle 期望 InnerBend > .75，实际0，失败 |
| 地表颜色接线回退旧局部公式 | 实际 mesh 第47顶点沙层期望 .024863，实际 .006735，失败 |

两个检测覆盖的是方向接线与实际颜色消费者，未推断所有潜在缺陷都已覆盖。

## Compatibility1 与 Render1

初始077指纹 `fdd60bfa...` 的旧曲河／运输／驻扎／地图开局兼容专项实际131 Edit＋13 Play全过，源稳定。其结果不替代后续加色板／渲染测试后的最终源码回归。

加入实际渲染 study 后，784输入 FP `e98ac815888d142300f9312c07116c2fd9f5d44321c100f9e899c49e7b2efb62`，Render1真实1例通过，15张1920×1080 PNG、5对视角与完整来源记录。重复current帧像素漂移均0；默认26东弯95307、近景16东弯192975个像素变化超过3级。该基线只是新build内重建旧ground公式，始终共享该轮材质，不是旧版本原生图。

实际查看默认／近景原件后，确认差异真实但色调偏接近草地；外岸接水边存在细小多边形缺口，尚未修复。后续仅 Winding 沙色提升为(.74,.67,.48)并独立material cache，新增实际材质隔离测试，另开Render2，保留Render1。该小修不声称解决接水缺口，保护区只保证高度／权重不变而非原沙色像素。

## Render2：Winding 独立沙色板

该轮784输入 FP `b7783d5d2af85f67a26ba96f32591e9f20664e56cf3f11693d29095ab5ab1484`、运行提交 `43346c284b3c47963a30c728481ebe49f09c1d10`。真实单例 URP study 通过，14.870秒、exit0、源码稳定；15张1920×1080原件和5组配对均保留。独立复核逐一验证两轮30张PNG字节SHA及来源、世界／相机／网格配对与两份784输入指纹；只接受已查看默认26／近景16的沉积带可读性改善。

默认26与近景16相对同材质重建旧ground基线分别有121853／236870像素超过RGB差值阈值3。五组重复帧均无超过阈值3的像素；默认26仍有极小非零均差且PNG SHA不同，另外四组均差0且PNG相同。阈值零不等于所有帧逐字节相同，像素指标也不代表审美量化验收。

外侧接水线多边形缺口保留。保护港址的画面只是空港址而非完工码头；保护区沙色有意变化，原高度／权重合同不变。Render2没有执行新增材质隔离测试，该测试随当前源码Full1另验。

独立审查原件为 `Render2/visual-review077.json`（53069B，SHA `0c078ccda056ef7ef2618e554d18665b818fa6a353e43a426c64785a17d3ae6d`）及 `.md`（6311B，SHA `5ac3b19382e36878e65486c8179e4fc30eaaf33f699397fcbe2dde4256a0a428`）。

## Full1：保留一次实际失败

784输入FP `b7783d5d...` 的完整回归实际1372/1372 Edit、451/452 Play，exit2、源码稳定；唯一失败是新增材质隔离测试的首次GetColor精确Color相等断言。XML预期／实际均打印RGBA(.740,.670,.480,1)，该舍入显示并不证明位相同。没有修改生产岸形或材质；仅将三处色板断言变为逐RGBA绝对误差1e-6，并输出R格式精度。材质对象不共享、其他地图颜色隔离、最终Winding未被覆盖，以及原地形／网格断言仍保留。

失败Full1原件不重标通过。修正后784输入FP `8c858fe82932c1c4909af27aea8dab71a183812902c937ee4931003f33840335`、提交 `a74fc43` 的Bank2实际17 Edit＋3 Play＝20/20通过、两个exit0、源码稳定。R精度原件确认Winding蓝分量预期.48实际.479999959，其他地图分量最大差约6e-8；没有变成另一套色板，Winding末次未被覆盖。根因仅归为实际Material读回的微小差值，不推定具体GPU半精度机制。

## Full2：最终完整回归

同最终784输入FP `8c858fe82932c1c4909af27aea8dab71a183812902c937ee4931003f33840335`、运行提交 `a74fc436dc08c50f9d23dc506df92f7ff2942d76` 的实际Full2已闭合：1372 Edit＋452 Play＝1824/1824，失败／跳过／inconclusive均0，runner exit0、源前后完全一致。真实独立报告目录 `Builds/Tests/run-QOgQGatR`；Edit882.153秒，Play246.198秒。

新增材质隔离与真实URP study都实际执行通过；另保留本次Full2/Captures的15张原始PNG／JSON。完整XML与source-evidence绑定SHA，独立原case集合复核另附审查原件。旧144专项、Render2和失败Full1各保留自身身份。

## 剩余验收

- 当前最终源码Full2已1824/1824，同源Build1 exit0／源稳定，新GUID `6d3fd495bd8f48be852c1e6c62bcb02f`。
- 普通原生单Explorer Move到点、到点后Hold、26／16镜头、七组F9、menu_exit与QUIT已独立限定验收，详见[077原生](Native-077.md)。封包／私有交付另验。
- 原生付费港区／指定攻击调度、性能和完整人工对局另验，未由网格测试覆盖。

076医疗 fixture 中部分版本字符串是显式合成存读 envelope 参数，不代表0.20运行时版本，也不代表旧存档跨版本兼容。新077网格验收直接检查 Application.version；原生构建另以自身GUID与版本绑定。

## Build1

同最终784输入FP的实际macOS Build1已exit0、源码前后稳定；Application版本0.20.0，GUID `6d3fd495bd8f48be852c1e6c62bcb02f`。完整app 298文件／191066644B、arm64 Development Mono，codesign严格校验exit0；并非Release公证或FPS验收。实际app清单SHA `3ba141de6d4faa290b1009b116c2ec65e256efe92c144c79cc1cb6c56dfde0df`，逐文件字节／SHA／Unix模式绑定Build1 runner与最终源码。

## Native1：普通曲河限定验收

同Build1实际PID10729，英国／奥斯曼Winding42普通AgeI开局，Explorer8实际Move到点，抵达后接受Hold，后续F9保持位置／450HP；镜头26→16→26，七组原始PNG／JSON保留。Hold在抵达后下达，不推定移动中取消能力。最后zoom26帧已恢复运行，上一异步帧处于暂停，保留其真实身份。

menu_exit世界39.199974秒、520条事件，与录制段完整账本一致；序列化anchor的world.events为空，只核对eventCount游标，不称作内嵌事件前缀重算。录制容器gzip／CRC与解压SHA复核通过，但未播放验证。可见QUIT、日志及PID消失成立，OS退出码未知。原生6条线程提前终结／1条PlayerConnection泄漏告警及构建非致命licensing／Curl42诊断保留，不声明零告警。

独立审查状态 `accepted_bounded_move_hold_camera_capture_menu_exit_quit`；20份原件8725394B未改变，完整298文件app／GUID／源码／Full2身份再次核对。JSON SHA `6060428ea4747f29572f36801118b0311e8ef736637b947a0e374be547928580`，MD SHA `aa12477fd13aea5827da171de2a794aab667304b8e41732fb21c99c8c8485836`。原件位于 `Docs/Evidence/077/Validation1/Candidate1/Native1/winding-view/`。
