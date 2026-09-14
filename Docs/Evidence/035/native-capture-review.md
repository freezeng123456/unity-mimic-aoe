# 035 NativeCapture 静态差异审查

审查对象是当前 `NativeCapture.cs`、`GameController.cs`、`FrontierHud.cs`、`EconomyEvents.cs`、`Model.cs`、`World.cs`、`Economy.cs`，并对照 `work/verification/035/before` 快照；未运行 Unity、未修改 Assets。

## 已确认没有的严重问题

- `NativeCapture.AtFrameEnd` 把 `yield return new WaitForEndOfFrame()` 放在 `try/catch` 之外，因此没有 IEnumerator 在带 catch 的 try 中 yield 的编译错误。
- `Request` 检查 `game`、组件启用状态、`Busy`、batchmode 和 null graphics device；成功启动协程后才设置 `Busy`。协程 finally 释放 `Texture2D` 并复位 Busy。
- 协程在渲染帧后再次核对 `MatchId`；重开局会使旧协程输出 `CAPTURE_CANCELLED`，不会把旧局画面写成新局元数据。组件禁用时 `StopAllCoroutines` 并复位 Busy。
- PNG 的宽高来自实际 `texture.width/height`，metadata 同时保存 app version、build GUID、matchId/requestMatchId、请求/采集 frame 与 world time、seed、pause/menu 状态、PNG SHA 和同 stem 文件名。PNG 与 JSON 写入发生在成功编码/哈希后。
- `CaptureMetadata.Read` 只枚举 `owner==0 && Alive && Building` 的实体及其队列；没有敌方实体、敌方队列或全局实体列表字段。经济快照只是复制 player 0 的资源/人口值，没有调用扣款、生产或刷新逻辑。
- `GameController.Capture` 只转发到 `Captures.Request`；F9 路径没有新增经济副作用。`MatchId` 在 `StartMatch` 每局用新的 GUID，报告也带 `matchId` 与 `eventSchemaVersion`。

## 经济事件核对

`EconomyEvents.RecordTransaction` 先调用 `Emit` 再给同一 `SimEvent` 填充 `economy` 对象；事件对象已存入 `World.Events`，后续 JSONL 序列化可看到填充后的经济记录。Repair 路径在成功扣款/治疗后补写 `hasRepair`、目标 ID 与 HP 前后值；失败、满血和无法支付分支不调用 Pay。训练入队在 Pay 后增加 reservation，再发 `ProductionQueued`；`World.Emit` 为 Payment/Refund/ProductionQueued/Spawn/Death/BuildingComplete 添加 population snapshot。未见重复扣款或改变原规则计算的路径。

## 仍需注意的边界

1. 写 PNG 成功而写 JSON 失败时会留下孤立 PNG；代码只规定 JSON 是完成记录，但没有在 catch 中删除已写入 PNG。审计上应把“存在 JSON 且 JSON 的 `pngSha256` 匹配 PNG”定义为有效配对；若产品要求无孤儿文件，可在后续实现中记录/清理该路径。
2. `hudResources` 是最近一次 `FrontierHud.Update` 写入的实际文本，并伴随 `hudUpdatedFrame`、`hudWorldTime`、`hudMatchId`；截图采集发生在 EndOfFrame，故两者若跨帧不一致是可观察的证据，而不是自动刷新后的伪一致。候选验收应验证并报告这种差异。
3. 当前审查未发现 `NativeCapture` 的实际编译/运行结果或原生 F9 配对产物；这些仍需由根 agent 在 Unity/native 客户端中验证。尤其 batchmode 的拒绝分支是静态可见，不能当作已运行证明。
