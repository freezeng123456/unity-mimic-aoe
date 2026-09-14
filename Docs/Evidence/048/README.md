# 048 证据说明

Search-red：旧运行时三个冻结用例失败；Search-green和最终测试：同一测试文件通过。red-test-sha256关联当前源码。before/SkirmishAI.cs为旧实现，仅作证据，不参与编译。

endgames/为明确布置的残局，移除敌方基地/其他单位与资源，提供六名火枪兵，村民实际移动到远角。它验证真实移动、观测、战斗和胜负，不是标准开局。result记录边界；army-samples每5秒观察己方位置；events为完整模拟事件。

normal-*为正常双AI规则模拟，seed314159、Standard、I–V、玩家0 economy卡组/玩家1 military卡组。旧基线位于../047/after-*；文件大小/哈希已核实，normal-comparison记录与新版逐字节相同。analyze.py在本目录可重跑，会覆写同目录analysis.json；读取原始记录和项目Rules，不依赖私有绝对路径。build-inputs/build-receipt对应0.2.24实际构建。本轮没有新版原生截图、声音、玩家完整局或性能证据。
