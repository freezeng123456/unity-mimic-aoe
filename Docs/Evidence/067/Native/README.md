# 0.10 final 原生验收

最终客户端 GUID `2d94fdd6d9734b259f5231c1fd3f2b99`，运行提交 `2668c47a426f2cea8d4bc7b309cdbfa71533cc9d`。595 输入指纹 `8180b02b55ed3957b94042d779a6d74bdda78cd46073cf70d93353c330363466`；与完整740 EditMode＋357 PlayMode通过的冻结输入一致。构建为 macOS arm64 Development / Mono。

[操作者验收回执](native-acceptance.json)说明实际观察范围；[归档回执](archive-receipt.json)核对45项原生材料的字节、哈希和去敏规则，不自行判定游戏行为。SupportingReceipts、本文和操作者回执由主代理另行审阅冻结，并由最终ZIP成员清单核验。

现场4×曲河seed314159四方FFA，英国、荷兰、法国、奥斯曼采用普通初始资源和正常AI。真实按钮训练村民成功（步4819），无可用单位的停止命令被拒绝（步5606）。实际SAVE REPLAY更新成功后保留[手动片段原字节](Replays/manual-export-bef1332be33c4e38b0594b3e4bf8eb13.replay.json)，再正常退出形成另一份quit文件；两者末端均为5622步，9:22，未自然获胜。

随后全新原生进程冷打开手动片段，默认在0暂停；从0开始4×完整前向重演到末端，显示RECORDED SEGMENT VERIFIED，包含成功及拒绝的真实人工操作。四槽观察、±30秒和实际拖动时间轴后JUMP至2807步通过。预览滑块在起点、中点、末端的填充条及手柄均保持在弹窗内；没有将预览本身当作模拟跳转。

代表F9（每个JSON均配同名PNG，schema4绑定同GUID、比赛身份、帧、PNG SHA和1600×900尺寸）：

- 保存成功：`Frontier-20261005-142139-930-f26347-9e1815012f764fe5831a4c1e759fa140`。
- 四观察槽：14:25:15 / 17 / 18 / 20（P1/P2/P3/P4）。
- 完整前向末端通过：[JSON](Captures/Frontier-20261005-143052-148-f28169-f8a14961e48f4017aca7137d0b6ad6ec.json)、[PNG](Captures/Frontier-20261005-143052-148-f28169-f8a14961e48f4017aca7137d0b6ad6ec.png)。
- −30/+30：14:31:24 / 26，对应5322/5622步。
- 实际中点跳转：[JSON](Captures/Frontier-20261005-143716-406-f51020-645b648ae90149ce94afb5832fc465e0.json)、[PNG](Captures/Frontier-20261005-143716-406-f51020-645b648ae90149ce94afb5832fc465e0.png)。
- 坏文件提示：[JSON](Captures/Frontier-20261005-143813-787-f54441-a385a578287a4ddca59d231c2213c6df.json)、[PNG](Captures/Frontier-20261005-143813-787-f54441-a385a578287a4ddca59d231c2213c6df.png)。列表读取合法测试副本后，仅该自建副本变成单字节坏JSON；实际打开显示错误，保留原片段和2807步暂停世界。测试副本已移出有效录像目录；未改两个正式样例。

[录制操作及容器检查](recording-inspection.json)、[截图索引](SupportingReceipts/final-capture-index.json)、[坏文件回执](SupportingReceipts/corruption-check.json)。两个owned进程正常退出0；[存储保护](storage-protection.json)及[观看前](SupportingReceipts/pre-watch-output-inventory.json)/[观看后](SupportingReceipts/post-watch-output-inventory.json)核对原生录像、报告四文件完全一致。个人Saves两个槽、Account/Preferences空目录的存在性、文件清单、大小和SHA也完全一致；只归档元数据，未上传个人文件内容。

本构建未做正式正常速度长窗帧率、内存或导出延迟验收，未断言该段原生精准攻击命令、自然胜利、所有课程/战役、联网、签名Release或其他平台通过。旧候选35:50长段和4×F8仅在[Diagnostics/FrozenCandidate1](../Diagnostics/FrozenCandidate1)保存，不属于本GUID证明。
