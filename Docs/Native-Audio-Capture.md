# 原生音频输出采样

独立客户端中按 **F10** 开始录制，再按 F10 提前结束；录音最多20秒。文件位于游戏持久目录的 `AudioCaptures`，包含PCM16 WAV与对应JSON回执。菜单、暂停和静音也可以采样，便于对照静音边界。换局、返回菜单、音频设备配置变化和退出会结束当前采样，禁止把两局合并成一个对局证据。

`NativeAudioCapture` 连接到现有策略镜头的AudioListener，读取Unity送入该监听器滤镜的DSP样本。它不修改传入样本，不创建录音麦克风，不替换监听器，也不改变声道、音量或正常声音播放。录音证明的是客户端收到的游戏混音数据，不是物理扬声器输出或听觉品质。

F10开始时，在主线程为20秒、最高8声道预分配浮点PCM缓冲。`OnAudioFilterRead`只复制完整交错帧及更新计数；不做文件IO、Unity API调用、WAV编码、动态分配或归一化。短锁保证结束后DSP线程不再修改已取得的缓冲。结束时主线程编码PCM16，并先保存WAV、后保存作为完成记录的JSON。截断最后一个DSP块以满足20秒窗口的样本单独计入 `samplesExcludedAtDurationLimit`；格式不符与不完整交错帧计入 `discardedSamples`，不混入正常布局。

回执含版本、build GUID、Unity与平台、起始/结束帧、原matchId与结束时matchId、模拟时间与单调墙钟范围、UTC、采样率/声道/帧数、DSP块数、停止原因及文件SHA-256。Listener与录音组件的GameObject EntityId必须相同，同对象不可有AudioSource，以区分当前接法与孤立的音源滤镜；真实收到非零mix仍须独立客户端WAV验证。采样期间记录暂停、静音、焦点、菜单、环境与音乐开关变化，最多128条，超过上限会记省略计数。没有DSP样本时 `hasDspData=false`；声道、采样率、音频设备配置变化或丢弃数据使 `validFormat=false`；非有限或剪切使 `validSignal=false`；换局或重绑定使 `singleSession=false`。这些结果不会输出CAPTURE_OK，也不能写成录音通过。

峰值、RMS、幅度大于等于1的样本数和非有限样本数根据原始浮点混音计算。PCM16编码把越界值限制到输出范围，把非有限值写为0，但不改原始缓冲；因此编码后的文件不会掩盖回执中的剪切或无效样本。采样窗口与主线程开始/结束时间之间可能存在一个DSP块和渲染帧的调度差异，回执不宣称每笔事件具有样本级同步。

六种项目原创通知由 `NoticeSoundClips` 生成：船运上行双音、时代上行和弦、人口双警告、资源耗尽下降短音、建筑完成双音、宝藏上行三音。它们保留既有Notice类别与两个保留通知声道及80毫秒节流；枪炮、活动和通知声道仍为8/2/2。不同通知不会改变事件权威、资源结算或战争迷雾过滤。片段有渐入/渐出、首末为0和有限幅度，但这些信号形状检查不能代替原生混音或耳听。

`AmbientAudio`另有两个常驻循环source，全部混音最多14个source，不占事件池。原创16秒风声采用固定独立随机种子与低通噪声，24秒音乐为D小调/C大调/G小调/A悬挂和弦的低声部背景与八音符短旋律；两者仅初始化时合成，循环边界短渐隐，源码可再生，没有外部录音或曲谱输入。环境声只在镜头焦点为当前可见地面时播放；音乐不追踪敌方实体。两者在菜单/胜负结束/换局停止，随游戏暂停、静音和重开处理，并有暂停菜单中的独立开关。事件音量维持既有数值，风/音乐source音量分别为0.065/0.045，最终听感仍需真实混音与听觉检查。

自动检查覆盖交错帧、容量、停止并发、格式变化、WAV头与峰值统计，通知检查覆盖独立音色和自身/敌方事件路由。下方已经收录同版本独立客户端的正常非零混音、暂停全零和主静音全零窗口；离屏/迷雾过滤、各提示音识别与密集战斗听感不能由这些窗口代替。本文不宣称已经耳听。

API依据为[Unity 6000.6 OnAudioFilterRead](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/monobehaviour/onaudiofilterread)：回调运行于音频线程，样本按声道交错。客户端与采样器只使用已有Unity API与.NET标准库，没有新增依赖、外部录音或商业游戏资产。

[Unity WebRTC 3.0官方音频说明](https://docs.unity3d.com/Packages/com.unity.webrtc@3.0/manual/audiostreaming.html)展示AudioListener同对象的OnAudioFilterRead取得输出数据的接法。本项目没有安装或调用WebRTC，只引用该官方接法说明；不能从文档推断当前硬件已经输出了声音。

## 回执严格复算

`Docs/Evidence/055/verify_native_audio.py`按显式JSON路径、build GUID、app版本和match ID选择证据，不自动找“最新录音”。它核对WAV文件SHA、完整PCM16头/帧对齐、采样率/声道/帧数、20秒容量、浮点与量化后峰值/RMS、时间窗、状态时间线、Listener接法、固定池和所有无效标记。`--signal nonzero`要求确有非零PCM；`--signal silence`要求全零PCM，两者均不是耳听结论。自动局或benchmark必须明确使用`--allow-diagnostic`，并保留标签；菜单采样须用`--match-id menu`。

13项Python工具回归仅用临时目录中的合成fixture检查拒收替换/截断WAV、伪造指标、不同版本/GUID/对局、孤立音源、丢帧/剪切、非有限值、静音冒充非零声音和缺失时间线，以及Unity真实`.NET DateTime.ToString("O")`七位小数UTC的严格兼容，不能作为原生听觉验收。命令为 `python3 -m unittest discover -s Docs/Evidence/055 -p 'test_verify_native_audio.py' -v`。

## 0.2.31 原生窗口

2026-10-02，独立macOS客户端build GUID `128a731d440e4b7a94abfcc74b88fa97`，autoplay=false、benchmark=false、1倍速。正常混音与暂停来自coast镜像对局 `9f0d7b0c338243b4a3c57107db5e1019`；主静音对照来自重新开始后的独立对局 `0d281a3fa942437fabb6798fe2acf664`，不合并为同一对局。每份窗口的原始JSON与PCM16 WAV按原字节归档，`audit.json`独立重算并保留auditor SHA；三个通过窗口均为48kHz、双声道、960000帧（20秒），没有剪切、非有限样本、格式改变或丢弃样本。

| 窗口 | 实际状态和严格复算 | 证据 |
|---|---|---|
| 暂停 | paused=true、muted=false、focused=true，音乐/环境开关ON；世界时间11.700008保持不动。938个真实DSP块，PCM全部为0，peak/RMS=0，`--signal silence`全部检查通过。 | [暂停回执与复算](Evidence/055/Audio/paused/audit.json) |
| 正常混音 | paused=false、muted=false、focused=true，音乐/环境开关ON；世界时间50.8998→70.9995。PCM共有1912524个非零样本（总1920000），原浮点peak=0.06268056、RMS=0.00421665；`--signal nonzero`全部检查通过。 | [正常混音回执与复算](Evidence/055/Audio/normal-mix/audit.json) |
| 主静音 | paused=false、muted=true、focused=true，音乐/环境开关ON；世界时间19.90004→39.89996正常推进。PCM全部为0，peak/RMS=0；`--signal silence`及未暂停/主mute/世界推进的功能判据全部通过。 | [主静音回执与复算](Evidence/055/Audio/master-muted/audit.json) |

正常窗口的事件播放计数从155到282，只表示事件音源播放请求增加，不能据此推断每一种声响都已辨识。WAV SHA为`9bda3151defef558fa42e19b8241973f5c9e255c9a6e7152f921aa873cbd6f56`；真实收到非零混音排除了本次采样为全零孤立滤镜的情况，但不证明物理扬声器、音量合适或人的听觉品质。原始文件可直接播放供人工听取；本次代理没有耳听能力，未给出听觉结论。

首份真实回执的七位小数UTC曾被系统Python的`fromisoformat`拒收，具体失败及旧脚本SHA保留在[修复前解析记录](Evidence/055/Audio/paused/parser-before-fix.json)。修复先严格匹配ISO日期、1–7位小数和显式UTC，再仅舍弃Python不支持的第七位亚微秒；不接受无时区、非UTC偏移或任意文本。这次修复只涉及Docs解析器及回归，没有改冻结的运行时源码或WAV。

另有结算时的主mute候选，世界时间恒定在534.2166，PCM仅642048帧（13.376秒）。它被19秒最低窗口和PCM/墙钟一致性判据拒收，功能mute=false；[原始候选与失败复算](Evidence/055/Audio/ended-muted-candidate/audit.json)仍保留。结束画面本来就会停止环境/音乐，不能拿该零信号代替正常模拟推进时的主mute对照。
