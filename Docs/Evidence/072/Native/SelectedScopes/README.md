# 072 选定原生范围审计原件

同一GUID `3288d9fe24e844748a1183f756874fbf`。本目录逐字节复制已封存的E/F/G/H独立审计，不包含完整PNG、保存、录像或profile；其实际原路径与SHA在审计JSON内。复制前后源文件bytes/SHA/mode一致，并与副本一致，见[复制索引](selected-audits-copy-index072.json)。旧审计当时的整体pending字段保留原字节；合并[最终journal](../FinalJournal/README.md)已另行封存并接受9个声明范围。

| 实际范围 | 独立审计 | 检查 |
|---|---|---:|
| E指定Guardian84与House83、实际伤害/Death、90秒革命及政府取消/退款/重排 | [JSON](072-final-processe-audit.json) · [说明](072-final-processe-audit.md) | 92/0失败 |
| E Slot1/367事件/20锚点/10记录及规范化完整保存状态 | [JSON](072-final-processe-save-replay-audit.json) · [说明](072-final-processe-save-replay-audit.md) | 205/0失败 |
| F冷初始完整World/Controller与E Slot一致、政府完成及100F工人89 | [JSON](072-final-processf-cold-save-replay-audit.json) · [说明](072-final-processf-cold-save-replay-audit.md) | 397/0失败 |
| F付费Settler20s与150HP新工人原图，保留实际变尺寸 | [JSON](072-final-processf-continuation-pixels-audit.json) · [说明](072-final-processf-continuation-pixels-audit.md) | 35/0失败 |
| G自动观战仅Details/Focus、A/S被入口忽略、minimap镜头移动 | [JSON](072-final-processg-camera-readonly-audit.json) · [说明](072-final-processg-camera-readonly-audit.md) | 77/0失败 |
| H实际1×前向step600→1111；起播前seek、播放期间无seek；Verified仍false | [JSON](072-final-processh-forward-audit.json) · [说明](072-final-processh-forward-audit.md) | 506/0失败 |

E使用声明受控IV，不是普通I路线；Guardian不是移动工人追击证明；H没有播完EndStep5490，也没有导出终点完整World/Controller。G 52公共锚点和65117事件前缀与D一致，退出flush尾部62条另行保留。F截图由1600×1000变3456×2168、3416×1884；不作统一尺寸、采木或帧率声明。五进程均正常退出0，关闭诊断仍保留Mono/MemoryLeaks，不称零泄漏。
