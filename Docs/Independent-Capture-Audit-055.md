# 055 原生采样与玩家证据独立审查

2026-10-02只读源码审查及Docs解析回归。未启动Unity、未操作GUI、未更改冻结的Assets/Packages/ProjectSettings/Tools。原合同D是正式美术/动画/音频与更广规模验证；本审查不新增跨机器签名、公证或第三方耳听退出条件。

## F8性能边界

`NativePerformanceCapture`在LateUpdate采样。前后焦点/暂停状态、失焦回调锁存及新的`PauseChanged`事件一起决定eligibleInterval。瞬时失焦后同帧恢复、同帧暂停后恢复、跨帧恢复邻接区间都排除；后续稳定区间可纳入，原始排除帧仍保留。四组临时构造的状态序列已独立复算；这只是判据检查，不能替代原生OS回调证据。

mean为有效帧间隔的平均值；p95/p99使用排序后`floor((N-1)*q)`，未伪装为插值分位数。暂停、失焦、transition及无效样本的排除原因计数可能重叠，不能相加冒充excludedFrames。正常玩家、自动局和高HP压力场景单独标明。`RuntimeBenchmark`只有最终focusedAtSave，不能独自证明整个压力窗口持续有焦点；应配对同GUID/matchId的F8逐帧回执。

初审复现了同步伪造frameMs与平均/分位数仍能误过最短时长的缺口：墙钟1.1秒的合成输入曾被算为2000秒eligible。问题已移交并修复于Docs verifier：所有frameMs必须为有限非负数，原始总和及eligible总和受固定0.25秒端点容差的墙钟包络约束，容差不随伪造frameMs变大。该负例现拒收；合法100毫秒采样阶段偏移及Finish晚于最后帧的正例保留。严格解析同时拒绝重复JSON键、NaN/Infinity/1e400、bool冒充整数。11项性能解析回归通过。最短duration/frame要求仍必须在命令行显式提供，工具默认不宣称十分钟或完整整局已经验收。

F8存活单位数来自`Alive && EntityKind.Unit`，按player0/player1/neutral分区，含守护者与驻军；不是人口、预留训练或屏幕模型数。`totalEntities`包含资源与历史死亡实体。verifier从Spawn/Death重建同一模拟tick内真实事件前缀，并同时校核上述全部数值；不猜该tick对应哪个渲染帧。`viewportVisibleUnitCenters`仅校核存在及0..livingUnits范围；不能从事件日志重构相机、迷雾或判断所有单位无遮挡同屏显示。

版本/GUID、associated MatchReport、显式matchId和原字节SHA绑定各自文件；单match世界时间、逐帧顺序和事件覆盖接受检查。焦点flag来自运行时回调，不能借模拟事件时间伪称具有操作系统独立时间戳。日志文件不是签名认证记录，源码和真实执行的绑定仍由统一构建回执负责。

## F10混音边界

组件与策略相机现有AudioListener同GameObject；OnAudioFilterRead只把传入交错PCM复制到预分配缓冲，不修改播放数据，不调用Unity主线程API、不编码或写文件。Stop等待有界复制结束后主线程输出WAV和receipt。`validFormat`处理无数据、格式变化与drop；`validSignal`另拒非有限或clipping；`singleSession`同时检查matchId与World引用。

真实0.2.31/GUID `128a731d440e4b7a94abfcc74b88fa97`已取得第一局的20秒暂停全零与正常非零窗口，以及第二局未暂停、世界正常推进时的20秒主mute全零窗口，见[音频记录](Native-Audio-Capture.md)。每份显式绑定自己的match，不合并两局。严格WAV维度/hash、DSP块、Listener接法、float/PCM16 peak/RMS、时间窗与所有无效标记复算通过。正常非零证明本次Listener路径具有混音信号；默认`--signal any`本身不要求非零，不能用于宣称有声。

真实`.NET`七位小数UTC首次暴露系统Python兼容错误，初始失败保留、仅Docs解析器修复；13项音频解析回归通过。暂停全零不证明主mute；结算时零窗也因世界恒定及PCM仅13.376秒被拒收，另保留失败候选。正式主mute窗口确在未暂停且世界仍推进时采样。stateChanges只有渲染帧级状态边界，不能证明样本级同步、各音色辨识、离屏/迷雾过滤或听觉舒适度。现有工具没有原生听音能力，本审查未耳听，原始WAV可提供给用户播放。

## 玩家对局解析

[verify_player.py](Evidence/055/verify_player.py)接受显式报告、版本、非零GUID、matchId、scenario和deckPreset；校核正常1倍速、普通起始配置、无time0清空死亡、完整事件数、付费与最低生产时间、自然结束与败者零存活单位/建筑。它保留原始文件SHA及关键事件行号，仍不能证明点击、手势或画面。卡组没有`DeckConfigured`事件或报告字段；菜单F9只提供PNG身份，preset是独立GUI记录的外部声明。合法卡集按配对Rules与DeckLibrary排序构造，船运卡合法性以该声明为条件，不伪称声明已经被事件记录。

coastIII要求玩家II/III、付费工人/军队/炮、炮击敌建筑、真实采集与到货；riverV另要求IV/V、付费银行及产币、特色carabineer、至少一个付费科技、宝藏、付费贸易站、XP交付和任一资源模式/交付。没有要求所有科技或三个资源模式全在同局操作。自然败局同样可以满足自然结束，工具不强迫玩家获胜。9项回归通过；其中真实旧第三局0.2.30保持ageIII/paidCannon/cannonEnemyBuildingDamage为false，未包装为达成路线，其余正例仅为临时合成解析fixture。

## 运行时源码指纹

| 文件 | SHA-256 |
|---|---|
| NativePerformanceCapture.cs | `79d1dc1fd56270f27206b584c5fba50f6fd8e0daa607d948a878a83f7558af5a` |
| RuntimeBenchmark.cs | `3a4a5fa218f315f11c263f71fa8934255caea80fd9317ab64c8a17e292db9e40` |
| NativeAudioCapture.cs | `549c38ae5125689854a5548461c1b99fe11b4728c857ca0308a59d2203d80f09` |

这里只记录本审查直接阅读的三个运行时文件；它不是整个工程的冻结指纹。全量来源以`Evidence/055/Tests/source-evidence.json`与构建回执为准。可变Docs auditor的SHA会由每份独立摘要记录，不借旧脚本通过值代表新脚本。
