# 071 原生材料只读审核

当前状态：脚本与合成自测已准备；没有 071 actual Native 输入，原生验收仍 planned。脚本不操作 GUI、不运行 Unity、不写原始素材，不用自动测试填补原生空白。

```sh
python3 /Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.14.0/Candidate1/Diagnostics/independent-native-071.py \
  --native-root /Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.14.0/Candidate1/Native \
  --build-identity /Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.14.0/Candidate1/Build/build-identity.json \
  --journal /absolute/final-original-native-journal.json \
  --output /absolute/new-independent-native-verification-071.json
```

输出必须不存在。退出 0 表示列出的 required native scope 全部有独立核过的材料；3 表示尚有 pending；2 表示材料/合同无效。`--self-test` 只验证合成反例，不能作为实际 Native 通过。

journal 用 schema=1，实际完成后 status=observed；模板保持 planned。sourceInputs 必须等于 build-identity 的精确输入数，sourceFingerprint/buildIdentitySha256/rulesHash 都绑定原始身份。executablePath 是原实际二进制绝对路径，须等于 build-identity.executable.file 从 Candidate 根目录解析的路径，核 bytes/SHA/可执行 mode。rulesHash 来自原 F9 ReplayIdentity / slot / replay 一致的已加载 Catalog，不是 Rules.json 文件字节 SHA。

所有 file 都是 Native 下相对路径，SHA 是完整原字节 SHA；禁止 symlink / 父目录越界。为避免 slot 重写丢掉早期队列，保存后只在原生验收目录保留原字节副本与 SHA，不重新序列化；若引用副本，保持该保存 processId 和 slotNumber，原日志必须有 SAVE 完成。

各数组字段：

- processes: `{id,file,sha256,logFile,logSha256}`。file 指 launcher process.json，需 pid、command 数组、appVersion、buildGuid、startedAtUtc、endedAtUtc、exitCode=0；command 包含原二进制与隔离 data/report/replay/log 路径。已有 process 格式可直接复用。日志异常/泄漏警告照录，不据 exit=0 推断零异常。
- captures: `{id,file,sha256,processId}`。指 F9 原 JSON，原 PNG 放同目录，按 pngFile/SHA/尺寸/PNG chunk CRC、CAPTURE_OK 与时间窗核对。元数据包含某 unit 不意味着其显示在镜头内。
- slots: `{id,file,sha256,processId,slotNumber,expect:{player:0,phase:"Empire|Revolutionary|CivilianRebuilt",queuedKind:"Revolution|Research|null",requiredIds:[实际实体ID]}}`。严格单 gzip member、原载荷 SHA/CRC/size、阶段/身份、queued paid=800F800W800C/90s 或400F400C/60s；不靠 F9 队列证明 paid。队列原 item 与 phase 必须匹配。
- reports: `{id,file,sha256,eventsFile,eventsSha256,processId}`。须有原生 REPORT 完成标记、OSXPlayer、版本/GUID/同名 events.jsonl 数量；实际完成须有对应 Payment 账本与至少90/60秒。报告没有政治 phase。
- replays: `{id,file,sha256,processId,reportId,playbackCaptureIds:[起始F9id,中途F9id...,最终F9id]}`。processId 是记录保存进程；captures 可以属于另一个实际观看进程，但所有观看帧必须在同一个进程。原 recording SAVED 标记、accepted typed Revolution/Research/Train/Disband 与实际付款/队列/actor 事件核对；reportId 绑定原退出事件前缀（可以省略，但不可据省略声称已绑定该退出段）。观看帧核原 segment/file/match/身份、递增 currentStep 与 frame、seek=false/error空、最初 currentStep==真实StartStep、最终currentStep==EndStep 且 verified=true。真实 loaded_checkpoint 的 StartStep 可>0。日志中只允许最初起点 F9 之前 target=StartStep 的 FROM START 重置；禁止 forward seek 或起始F9之后 seek。同一观看进程若重复打开相同 segment，须换干净进程重看以消除日志歧义。完整 segment 不代表完整比赛。
- coldRestores: `{id,slotId,processId,replayId,slotNumber}`。须前进程已退出、新进程启动、真实 LOAD 标记、loaded_checkpoint 新录制第一 anchor 与原 slot 全部 world/simulation 相等。只允许 LoadMatch 强制 paused=true 与 ReplayDigest accumulator=0 的明确规范化；历史事件前缀必须相等。阶段/队列/definition/ID/container/group binding 均不抹掉比较。该检查不自动证明 UI 控制组已恢复。
- manualObservations: `{id,status:"observed|pending|failed",reviewer,description,captureIds:[...]}`。`id="native_worker_ui"` 用于操作者对实际原生可见画面的观察，核对旧革命军外观/工具切换、新工人出生和实际控件；只有操作者已实际查看的原 F9 图像才能引用。由 root 通过 ComputerUse 操作并查看原 PNG 时，准确填写 reviewer="Codex /root 实际GUI观察"，不暗示 human 已操作或确认。描述必须限于实际观察；planned/pending/failed 均不会填成通过。该项记录操作者对实际原生可见画面的观察，脚本不识别像素、不从 Edit/Play tests 推断。

required scope 为 process/capture/slot/exit_events、四类 accepted 命令、革命/重建/工人训练/遣散完成、cold restore、native forward replay、native_worker_ui。每项缺失都会列在 missingPending；已观察项只列入 observedScope。初始场景为 authored IV 时须在最终说明中保留其控制条件，不能声称从自然 Age I 通关或平衡验收。

付款与哈希属于本地一致性核对，均无签名。此脚本没有复制 C# 完整 Snapshot/Replay 状态机或自行模拟重放；实际 ReplaySession 运行由原生起始/终止图、日志和录制字节提供证据。Native scope 与 full test、普通8路线、两台物理 Mac LAN 分开记录。
