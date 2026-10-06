# 070 完整正式测试

运行 `run-mfr2pucl`，实际 EditMode 966/966、PlayMode 397/397，合计 1363 项。失败、跳过、未定均为0。

运行提交 `573e017c7c95f07548bc47f902408c53fd2ce88a`，682输入，指纹 `e2ab75d010ebcde449d94a5b5ca23c7507ac837b1efa2b957f41facde8d5333d`。`source-evidence.json` 保存实际输入及两份 XML SHA；Tools/verify-delivery.py --require-current-source 已通过。该身份属于0.13.0最终候选，旧680输入的1359项基础回归另存为诊断。

通过模拟与控制器测试不能代替原生输入、自然比赛、地图平衡、两台物理设备或长窗性能验收。新内容操作见[玩家指南](../../../Player-Guide-070.md)。
