# 047 证据说明

baseline-* 来自未修改0.2.22，after-* 来自当前0.2.23运行时；均为无图形正常规则双AI样本，seed314159、Standard、I–V、玩家0 economy/玩家1 military，2400模拟秒上限，实际均自然结束。没有开启额外资源、生命、出生或揭雾。result明确记录模拟边界。

Construction-red.xml是冻结5项用例在旧代码下的2通过/3失败；red-test-sha256.txt关联相同的红绿测试源。EditMode与PlayMode为最终通过结果。analysis.json由analyze.py核对原始事件生成；脚本在本交付目录可重跑，但重跑会覆写其同目录analysis.json。build-inputs与build-receipt记录0.2.23实际构建。没有本版原生截图、声音、人工完整局或性能数据。
