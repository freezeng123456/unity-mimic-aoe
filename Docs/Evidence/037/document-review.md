# 037 文档证据复核

只读核对 `Iteration-037.md`、`Normal-Player-037.md`、`Native-Verification-037.md`、README 037 段、`Full-Scope-Audit.md` 末尾及 `work/verification/037` 的 XML/log/json。未运行 Unity。

## 已核实一致

版本边界清楚：037 原生短局是 0.2.16，GUID `af1e524273704830bbd0445a24446435`，而正常长局明确标为封存 0.2.15，GUID `28e06a3b43184bdeaa73fdf70f10d6c2`。`native-audit.json` 对应长局 969.910217 秒、winner 1、player 0 无 Resign；`v16-native-audit.json` 对应 0.2.16 的 15 组 capture，且文档另说明旧版/新版没有合并为同一客户端验收。

`DoubleClick-red.xml` 与 `DoubleClick-red-repeat.xml` 均为 3/7（4 fail），`DoubleClick-green.xml` 为 7/7；`EditMode.xml` 119/119，`PlayMode.xml` 148/148。冻结测试 SHA、版本、依赖和构建数字与现有 source/build 记录相符。

原生远距证据准确写为两次 release 相隔约 **258.9 ms**、距离 **76 px**，选择从一个实体保持一个；同点双击选择 **6 个**。文档把 6 写作 selection count/六人选择圈，没有把它误称为“实体 6”。长局也明确 III 只付款、TC 提前毁灭，预计升级完成前约 3.026 秒，不能算 III 完成或炮兵攻城。

`Stop` 被按真实 `Command` 事件记录；正常局 Damage 目标映射与建筑伤害为 0 的口径没有被改写为炮兵验收。首次 group recall PNG 显示旧选择、稍后 confirmed PNG 才显示六人，文档明确列为 UI 同步边界。

## 需保持的范围说明

未发现高置信数字错误或版本混用。文档仍应保持“037 原生短局验证双击/选择/局部操作，037 长局是历史 0.2.15 正常败局”的表述；这些材料不能合并宣称 0.2.16 完整正常对局。258.9 ms/76 px 只证明该次原生远距点击没有误触双击，不证明所有硬件节奏、缩放或系统焦点场景。

脚本与 JSON 能核对事件、selection count、Command/Stop、PNG/sidecar SHA 和 match identity，但不证明物理 Ctrl/Command 修饰键、桌面白屏/焦点恢复、即时 F9 面板同步、III 炮兵攻城或完整五时代体验。当前文档已将这些列为未解决/未验收边界。
