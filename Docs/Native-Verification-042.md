# 0.2.19 原生镜头、移动与训练专项验证

2026-09-14，macOS 26.5 / Apple M4 Pro / 48GB / Metal，Unity 6000.6.0f1（正式非 LTS）/ arm64 Mono Development。原生画面 2560×1440，对应当前桌面约 1280×720 点。人工 GUI 操作、正常速度、未启用 autoplay。只检验本文列出的专项，不构成完整对局验收。

新客户端版本 `0.2.19`，GUID `41a7c09fede248e6b47e9732be4e6cdd`；match `8afe40fb71de4df283ae3abd272f95b5`，seed 314159，玩家海岸盟约，AI 河谷联盟，时代上限 V。正常开局 300F/300W/100C、六名村民与一名探险家；没有注入资源或单位。

原始证据均位于 [Evidence/042](Evidence/042)。截图 JSON 保存原始 PNG 文件名和 SHA-256；交付采用便于阅读的文件名，PNG 内容未改动。根审计 native-audit.json 对 18 对截图和 3098 条事件进行核验。

| 截图文件前缀 | 对局时间（秒） | 实際观察与结论 |
|---|---:|---|
| 042-01-old-right-edge-10pt | 345.212 | 旧 0.2.18：距窗口右边缘 10 桌面点没有滚屏 |
| 042-02-old-right-edge-2pt-paused | 358.613 | 旧 0.2.18：距右边缘 2 点后镜头滚动，随后暂停；旧版 JSON 没有 camera 字段，依原生画面观察 |
| 01-new-baseline | 0.500 | 新局镜头焦点 (25,25) |
| 02-right-edge-10pt | 30.600 | 同样距右边缘 10 点，焦点到 (40.882,25)，向右成功 |
| 03-left-edge-10pt | 32.300 | 保留尝试；与 02 之间右侧仍滚动，不能直接比较两张证明向左 |
| 04-left-from-town | 65.700 | 先按 T 回到 TC (24,28)，距左边缘 10 点后焦点 (7.742,28)，向左成功 |
| 05-top-from-town | 67.700 | 先按 T，距上边缘 10 点后焦点 (24,44.466)，向上成功 |
| 06-bottom-from-town | 69.700 | 先按 T，距下边缘 10 点后焦点 (24,12.181)，向下成功 |
| 07-training-click-without-selection | 115.099 | 前一点击清除选择后误点训练位置，没有训练、没有扣费；保留失败尝试 |
| 08-minimap-outside-vision | 116.699 | V 选择村民，小地图左键把镜头移到 (74.608,30.762)，画面中部仍黑暗 |
| 09-move-into-unexplored | 135.699 | 右键黑暗区域，界面显示 Move order accepted、Moving / Orders 1 |
| 10-arrived-beyond-original-vision | 163.201 | 镜头与 08/09 相同；所选村民从左侧基地走到画面中部，周围地形可见，Idle / Orders 0 |
| 11-paid-training-accepted | 181.002 | T 明确选中 TC 再点 Settler；300F→200F、人口 6+1/20，XP 仍 36 |
| 12-training-completed | 228.305 | 村民已出生，人口 7+0/20、队列空，XP 41，费用没有重复扣除 |
| 13-villager-details | 261.407 | V 定位到远处村民，相机焦点 (74.473,31.825)；详情显示 build 0 / training 5 / kill 10 XP 与 Vision 18 |
| 14-paused-camera-baseline | 262.607 | ESC 关闭详情，再 ESC 暂停，记下相机位置 |
| 15-paused-edge-stays | 262.607 | 鼠标移到右边缘；新帧中世界时间和相机位置保持不动 |
| 16-return-menu | 0（无对局） | 从暂停页返回菜单；inMenu=true、hasPlayer=false、生产队列和资源文字空 |

14/15 的 PNG 哈希相同是预期结果：暂停画面未变化，硬件鼠标不在 F9 PNG 中；JSON 的 captureFrame 与 pointer 坐标发生变化。菜单快照保留最后的相机位置及历史 hudMatchId/hudWorldTime，仅作诊断，不将它描述为所有字段清零。

事件记录在 135.199 秒确认玩家 0 对实体 2 发出 Move ×1；Command.position 是命令时实体原点 (29,25)，不是移动目的地。最终位置的证据来自实际画面和 V 定位后的镜头，事件文件没有逐帧实体位置或完整目标坐标，不能伪称具备轨迹回放。

训练链条在 180.501816 秒记录唯一一笔 100F Payment 和 ProductionQueued；200.503036 秒先出现新村民 Spawn，再出现 Unit trained 5XP。其他经验为三次战斗赏金 20、8、8，共 41XP。玩家 0 gathered=0、spent=100F、最终资源 200F/300W/100C，与 UI 相符。原生本轮没有升级、造兵攻城或发送船运；规则测试覆盖训练退款、阻塞、免费赠送排除等更广边界，但不把测试视作实际玩家经历。

最终报告 reason=menu_exit、time=262.6068115、winner=-1。新版本专项结束后留在主菜单；旧 0.2.18 进程未退出，用户旧局未被本轮替换。无本轮完整胜负、声音质量或性能验收。完整游戏的阶段 B/C/D 门槛保持开放。
