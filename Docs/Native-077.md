# 077 · 普通曲河原生限定验证

2026-10-10。实际0.20.0 arm64客户端，GUID `6d3fd495bd8f48be852c1e6c62bcb02f`，最终784输入FP `8c858fe82932c1c4909af27aea8dab71a183812902c937ee4931003f33840335`；独立Full2 1824/1824与Build1 exit0之后启动新PID10729。原始运行在新 `Candidate1/Native1/winding-view`，与旧医疗原生数据分开。

## 实际操作

CLI配置英国／奥斯曼、Winding42／duel，真实主菜单点击BEGIN SKIRMISH。正常AgeI，300F／300W／100C，六名工人；没有场景注资、改位置／血量、清迷雾或自动对战开关。

| 原始F9世界时间 | 实际Explorer8与镜头 |
|---:|---|
| 1.4 | Idle，(31,32)，450HP，普通zoom26 |
| 1.7 | 真实右键Move，正在移动，目的地(65.17865,68.478805)，450HP |
| 35.4 | 已到上述目的地，Idle／空队列 |
| 35.7 | J守卫后Holding／空队列，左键小地图镜头焦点(78.73295,68.478805) |
| 37.0 | 同位置／Holding，实际五次放大到zoom16 |
| 37.8 | 恢复zoom26后异步截图已处于暂停，不能当作运行中恢复帧 |
| 38.5 | 再次恢复运行后的zoom26，位置／Holding保持，450HP |

实际曲河由探索者步行揭示，水湾外侧仍有战争迷雾。单单位Move完成和Hold稳定不等于多军编组／指定攻击的机动性全部验收。普通26与近景16均看到了内侧沙肩／外侧湿岸；外接水线缺口仍在。

## 收尾与证据

真实暂停菜单RETURN TO MENU生成 `bbe9d6ade4e14d9594af8d4aa21ca552` 的menu_exit报告、事件JSONL及录制段；随后从可见主菜单点击QUIT。退出后不调用自动激活UI探测，PID消失只能证明进程已离开，不推定等待得到OS exit0。七组实际F9 PNG／JSON及完整原始日志保留，独立只读审查另附 `native-audit077.json`／`.md`。

未执行付费港区／船运／医护链、存档冷恢复、录像播放、完整胜败比赛或FPS。记录到录像文件不等于播放验收。其他河图与港船新精修不计作本轮已完成。

## 独立只读闭合

审查状态 `accepted_bounded_move_hold_camera_capture_menu_exit_quit`，JSON61995B／SHA `6060428ea4747f29572f36801118b0311e8ef736637b947a0e374be547928580`；MD7476B／SHA `aa12477fd13aea5827da171de2a794aab667304b8e41732fb21c99c8c8485836`。20份原件8725394B未改变，298文件实际app清单／源码指纹／Full2绑定均匹配。

报告与录像完整520事件账本一致，gzip解压2535927B／SHA `903c3ee17083c1f540c4098131a05ce9aa0f229522d70ea4181ee198b1d9d79a`与容器声明匹配。anchor的world.events为空，只核对eventCount游标，不称作内嵌前缀重算。Hold在抵达后下达；后续Holding截图间隔约2.8秒，不声明中途Hold取消或连续逐帧位置证明。

保留6条线程提前终结和1条PlayerConnection泄漏告警；Build1也保留licensing Access token及Curl42非致命诊断，构建实际exit0不等于零警告。独立PS确认所有本项目Editor／原生PID均已离开；未获得OS wait退出码。
