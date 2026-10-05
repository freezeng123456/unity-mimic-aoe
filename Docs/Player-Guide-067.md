# 0.10 玩家指南：录制与观战（067）

本指南对应 `0.10.0` 当前操作实现。录像文件用于在原构建中重新观看模拟；它与手动检查点、长期完成记录分开。机制和兼容细节见 [回放说明](Replay-067.md)，运行验收结果另行记录。

## 先玩，再保存录像

在主菜单配置文明、地图、AI 难度和时代上限，点击 `BEGIN SKIRMISH`。`1 VS 1 DUEL`、`2 VS 2 TEAMS`、`4 PLAYER FREE FOR ALL` 都是一个本地玩家加 AI；`GUIDED LESSONS` 和 `RIVER CAMPAIGN` 可以进入中文目标场景。当前没有联网大厅，LAN 留待后续阶段。

正常开局会自动开始录制。按 `Escape` 打开暂停菜单，点击 `SAVE REPLAY` 保存截至当前时刻的片段；保存后仍可继续玩，同一段再次保存会更新同一个录像文件。等待 `Saving recording…` 变成 `Recording saved`；出现错误时按提示处理，不能只凭按钮已点击认定保存成功。

自然结算、返回菜单、重开、加载检查点、结束引导场景或正常退出时，也会尝试关闭并保存当前录制。返回菜单或主动结束的录像可以只有一段经过，不代表已经完成整局。报告写入失败时录像仍有独立的保存流程，分别查看报告错误和录像保存结果。

## 打开录像库

从主菜单或暂停菜单点击 `RECORDING LIBRARY`。列表每页显示四个文件，`PREVIOUS` / `NEXT` 翻页，`REFRESH` 重新读取目录。

| 列表状态 | 含义 |
| --- | --- |
| `RECORDED SEGMENT` | 有效片段，末尾尚未出现对局终局 |
| `MATCH ENDED` | 片段末尾的世界已结算；是否获胜仍看实际对局结果 |
| `INTERRUPTED` | 录制已中断，只能观看保留下来的有效前缀；原因会显示 |
| `Unreadable` / `Incompatible` | 文件损坏或身份不匹配；显示错误，打开按钮禁用 |

点击可打开的条目，等待读取。打开成功后默认暂停。版本、构建 GUID、Unity 版本或规则等不匹配时，需要生成该录像的原始应用构建；仅把文件改名或使用同名版本不能解决兼容问题。读取失败不会替换当前世界或改写个人数据，原文件保留。

## 观看控制

| 控件 | 操作 |
| --- | --- |
| `PLAY` / `PAUSE` | 播放或暂停；验证到片段末尾后按钮变为 `REPLAY FROM START` |
| `SPEED` | 循环 `0.5×`、`1×`、`2×`、`4×` |
| `VIEW P1` 等 | 切换实际存在的观察槽位；最多 P1–P4，内部编号为 0–3 |
| `−30s` / `+30s` | 在录像区间内跳转 30 秒模拟时间，跳转后保持暂停 |
| `TIMELINE / MENU` | 打开时间轴；拖动滑块选择目标，再点 `JUMP`；`FROM START` 回片段起点 |
| `BACK TO VIEW` | 关闭时间轴返回观看；需要继续时再点 `PLAY` |
| `MAIN MENU` | 结束观看并返回主菜单 |

跳转显示 `SEEKING`，从附近保存点重建模拟，每帧最多推进 20 步；相机仍可移动。等跳转结束后再播放。来自检查点的录像可能从较晚的时间开始，不能拖到它没有记录的更早时刻。

观看时，若没有其他窗口打开，`Escape` 切换播放/暂停；有弹窗时先关闭弹窗。可以用方向键、鼠标滚轮和小地图移动镜头，左键选择或框选对象，查看 `DETAILS`，用 `FOCUS` 聚焦；默认 `T`、`M`、`V`、`F` 分别选择观察方城镇中心、军队、闲置村民及聚焦选择。若改过键，以当前设置为准。

右键及攻击、建造、生产、修理、船运等动作不会给录像中的单位下令。观看不能创建对局存档、投降、重试场景或保存课程完成记录。播放已完成课程不会增加账号完成次数或解锁章节。

切换观察槽位后，顶栏资源与人口、生产面板、选择、迷雾、小地图及事件反馈跟随该玩家；盟友共享视野按原规则显示。不会因切换观察者重新编号或重涂全局槽位颜色，也不会获得全地图视野。

看到 `RECORDED SEGMENT VERIFIED` 表示记录区间通过回放检查，不等于赢得对局或录像包含整局。看到 `REPLAY STOPPED` 时读取具体差异原因，再用录像库或主菜单退出；中断标记不会因有效前缀验证通过而消失。

## 录像和检查点怎么选

要以后继续自己操作，用暂停菜单 `SAVE MATCH` → `SLOT 1`、`SLOT 2` 或 `SLOT 3`；用主菜单 `LOAD SAVED MATCH` 恢复。当前检查点要求相同 `0.10.0` 应用版本及规则，恢复后先暂停，点击 `RESUME` 继续。

加载检查点会创建新的录制段，从恢复位置开始；旧段和新段可以有相同对局身份，但文件身份不同。要重新观看经过，用 `SAVE REPLAY` 和 `RECORDING LIBRARY`；录像不能替代可继续操作的检查点。

## 文件位置与错误处理

macOS 默认数据目录：

```text
~/Library/Application Support/Frontier Workshop/Frontier Reckoning/
  Replays/replay-<segmentId>.replay.json
  Reports/<身份>.json
  Reports/<身份>-events.jsonl
  Saves/slot-1.json、slot-2.json、slot-3.json
  Account/guided-progress.json
  Preferences/player-settings.json
  Captures/<身份>.png 与 <身份>.json
```

录像存储上限为 64 MiB，解压后的 UTF-8 文档上限为 256 MiB。单段最多记录 200,000 个外部操作、1,000,000 个事件和 10,000 个锚点；录制限额中断会保留可验证的前缀。文件字节超限则显示保存失败，不会自动生成一份裁剪过的新录像。旧文件在写入失败时保留。

录像库单目录最多扫描 256 个匹配文件；超限会显示 `Library unavailable`，不会自动删除。可以退出应用、先备份，再按目录归档自己的文件，或用下方参数直接打开已知文件。坏文件保留原文并禁止覆盖；当前没有界面内自动修复或删除按钮。

保存任务尚未完成或失败时，录制导出可能仅在内存中。应在退出前确认成功；强制终止不能代替一次成功写入。录像目录参数不会迁移检查点、账号进度或偏好。

F9 仍可保存画面及附属 JSON，新的捕获格式为 schema 4，记录实际观察槽位、回放片段、播放位置和错误状态。截图不是回放文件，也不会解除战争迷雾。

## 命令行打开与独立目录

将下面路径替换为本机实际可执行文件和数据位置；路径含空格时保留引号。

```sh
FRONTIER_GAME_BIN="/绝对路径/游戏.app/Contents/MacOS/游戏可执行文件"
"$FRONTIER_GAME_BIN" \
  -frontierReplayDirectory "/绝对路径/my-replays" \
  -frontierReportDirectory "/绝对路径/my-reports"
```

启动后直接读取指定录像：

```sh
"$FRONTIER_GAME_BIN" \
  -frontierReplay "/绝对路径/my-replays/replay-片段身份.replay.json"
```

`-frontierReplayDirectory` 决定录像保存和列表扫描位置；`-frontierReportDirectory` 只决定普通对局报告和事件输出位置；`-frontierReplay` 打开一个文件，成功后默认暂停。这些参数不绕过身份或完整性校验。
