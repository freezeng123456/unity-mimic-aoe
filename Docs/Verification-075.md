# 075 验证记录

2026-10-09，0.18.0 Candidate1。Focused6专门回归85/85通过；Full1实际1290/1291失败已修复，Full2正式1723/1723通过；Mac构建和两个普通AI开局已关闭。075原生付费训练、侦察、潜行和法国冷恢复已操作；私有交付以包外回执为准。074正式1646项与原私有包保持独立身份。

候选目录：`/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.18.0/Candidate1`。`run075.py`为外部运行器，逐次使用新目录，记录实际命令、PID、退出码及执行前后源码输入。

| 运行 | 实际结果 | 源码身份与处理 |
|---|---|---|
| Focused1 | EditMode 57/58，1失败；exit2，未运行PlayMode | 提交d85ba3f，FP7f81bc5e090e1211abb02a8c7658d32dee9e7639c9bcc1620e61dc32a04970e0。最后单位已不能潜行，却先提示冷却；修正原运行逻辑的原因优先级，保留原用例和失败XML。 |
| Focused2 | EditMode 58/58；PlayMode 11/13，2失败；exit2 | 提交362e60e，FPdb7f5943f67557254171483caab12d5a6658e5789acf6c46c0dce06d8d734350。Rally夹具误点宝藏实际触发Claim；Build夹具在已有0.22秒界面屏障内放置而被忽略。修正为空地、验证真实Pick，并等待实际界面过渡，保留付款和施工断言。 |
| Focused3 | 编译exit1，无测试通过结果 | 新增旗标断言误用LineRenderer不存在的isActiveAndEnabled属性，改用Renderer.enabled与GameObject.activeInHierarchy；保留编译日志。 |
| Focused4 | EditMode64/64，PlayMode12/13；exit2 | 旗标检查发生在输入LateUpdate之后、视图下一帧刷新之前。11.6297的误差精确对应初始旗点；增加实际渲染帧等待，保留精确旗标位置断言。 |
| Focused5 | EditMode64/64，PlayMode13/13，77/77；exit0 | 提交f07619f，FPf39f8a36116b654be27e0163347e2590a6aa1d230211b10580952c581edd25bf。新增6例实际战斗/避险/编队回归也通过。 |

Focused1–6执行前后输入均稳定；失败原件保留在各自目录。最终修复可见武装敌方斥候的经济/守家威胁判定、探险家Envoy旗标与详情反馈；隐藏斥候不能通过候选列表泄漏给AI，己方斥候仍不计入军队人数。

专门回归覆盖实际生产生命周期、侦察/潜行、LAN/冷录像、真实InputSystem/UGUI操作和模型/动画。通过专门回归后才运行完整EditMode/PlayMode、Mac构建、两个预声明普通AI开局，以及同构建原生交互/F9画面与云盘完整字节核验。计划数量不记作通过数量；Unity输入用例不等于操作系统原生验收。

Full1实际报告在`Builds/Tests/run-DTnTLSAh`：EditMode1291项、1290通过/1失败，exit2，未启动PlayMode；FPf39f8a执行前后稳定。原`DutchAiUsesItsEarlySkirmisherAgeOverride`失败，新斥候先支付50食物，抢走首批散兵费用。提交51bd61f将可选侦察兵安排在军队生产/研究之后，并保护炮兵储备；原用例不改，Focused6额外纳入整个经典文明AI回归类，之后重新全套运行。Full1原XML和闭合runner保留；runner原tests字段为空不代表没有实际失败XML。

Focused6在当前提交51bd61ff1abc92f224dd449784671234a20a5555与FP a3e1e3518a23d000f6ce1e80bef628f72bd55c631fcb9ddcd6e9f2be80066594下实际72Edit+13Play，共85/85通过；新增8个经典文明AI用例包括Full1原失败项，源码稳定。Full2使用同指纹，在`run-nCezPnid`新目录重新执行完整测试，最终EditMode1291/1291、PlayMode432/432，共1723/1723通过，零失败/跳过/未决，exit0、执行前后源码稳定。

Build1同758输入/源码指纹实际Succeeded，exit0/sourceStable；macOS Apple silicon Development/Mono，应用0.18.0，GUID `67594b51e2dd40f593ec8e956f6f4960`，构建报告191116426B。构建不调用Prepare，实际应用与原生F9身份一致。

两个预声明普通AI镜像局均从时代I、默认钱包与零新增侦察兵开始，无场景注资或强制玩家命令。荷兰traders_bend070/seed42：4次真实支付50F→Envoy排队/出生，4次Scout命令，2400.090576秒cap，无赢家。法国winding/seed44：2次真实支付50F25W→Native Scout出生，2次Scout、2次Stealth，1907.381348秒PlayerEliminated(P0)与MatchEnded(P1)自然终局。法国P0斥候在终局Eliminate清理中死亡，不能写成敌军直接击杀。

原始事件逐条与final-world匹配，实际支付/退款累计与最终spent/refunded一致，最后sample与最终钱包一致；按聚合收入重建“初始+收入-支出”仍有浮点记账残差（最大49.1764），不宣称逐tick钱包恒等式精确重建。全部原件在normal包，所选manifest和只读审查在[证据索引](Evidence/075/README.md)。普通观察与原生客户端曾同机运行，headless计时与原生FPS不互相代替，也不作为隔离性能验收。

075同GUID原生范围、存档/录制原件与冷恢复限定见[原生记录](Native-075.md)。保存回放不等于原生播放通过。完整1723项只统计Unity用例，外部文件/SHA/封包检查另列。
