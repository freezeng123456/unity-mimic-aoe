# 075 荷兰原生试玩只读核验

作者参与运行时实现，因此这是证据验证，不是独立实现审计。只读原件，未启动Unity/GUI、未改项目或原始证据。

- 版本0.18.0；GUID `67594b51e2dd40f593ec8e956f6f4960`；源码指纹 `a3e1e3518a23d000f6ce1e80bef628f72bd55c631fcb9ddcd6e9f2be80066594`；match `807730546eeb4a9ebaa740617c5d00a7`。实际地图`winding`、seed42。启动receipt原始错误`winding_valley`保留，不宣称该CLI地图ID有效；界面开始后的world、F9、录像、quit一致为`winding`。
- Pathfinder8在24.200056s支付50F（100→50F，木/金币不变），相邻事件入队`envoy ×1`。Slot1在24.300056s/step243保存该队列：elapsed0.1/15s、paid50F、population0，尚无Envoy；压缩payload摘要校验通过。未执行冷加载。
- 实际点击ENVOY RALLY，接受世界地面点(36.7592583,30.7205200)。原始游戏指针是(1225,584)，操作者报告桌面点(1225,560)；本核验没有独立换算两套坐标。RallySet、录像命令、producer.rally和新兵RallyAssigned完全一致。
- 随后独立AttackMove使Pathfinder移动并对neutral guardians造成实际伤害。39.199974s在当前producer位置(39.1758652,38.7121696)旁产出Envoy136；Spawn与UnitTrained唯一ID/时间相同，中间XP事件保留。44.099899s F9确认Envoy抵达原旗点并Idle，producer仍在交战。没有即时Rally前后快照，不单独宣称Rally绝对零位移。
- 86.999245s真实点击AUTO SCOUT并接受Scout命令。87.099243s和92.099167s两张F9均Scouting/Scout；Envoy从(36.3291550,40.6862450)到(28.8050041,18.4892559)，距离23.437559。后一张camera.focus等于实际新位置，HUD可见Scouting、Orders1；最终锚点保留OrderKind16/resolvedPoint。团队已探索格增长，但producer也揭雾，不全部归因于Envoy，也不是全图验收。
- Slot2没有文件，明确不计成功。SAVE REPLAY真实点击、文件落盘可证；录像incomplete=false、finishReason=quit，最终step921/time92.099167/eventCount1277；replay.events逐条等于quit-events.jsonl。录像锚点按格式省略world.events，完整事件只保留于顶层events。未播放/seek，不宣称重演验证。
- quit报告同match/GUID，reason=quit、winner=-1；日志有OnApplicationQuit、输入Shutdown、CodeReloadManager destroyed，本次检查原PID69195已不存在。实际退出收尾已证；原目录没有单独native进程退出码receipt，故不编造exit0，不将92秒试玩称为完整对局或性能验收。

机器核验45项通过；全部19个原始荷兰文件字节数/SHA256前后不变。全部六对PNG/JSON核验SHA、尺寸、GUID和帧/HUD/world配对；实际目视训练、交战/旗标、侦察、聚焦暂停四张PNG。

详细命令、事件行号、原件摘要见`audit-dutch075.json`。尚未验冷恢复、录像播放、Envoy造贸易站、Scout排队后续、完整自然终局、全图探索、性能因果；法国隐身由另一轮单独提供。
