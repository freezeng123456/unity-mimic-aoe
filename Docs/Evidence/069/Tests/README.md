# 0.12候选2正式完整测试

`run-0zP2pnt7`，895 EditMode +391 PlayMode =1,286，全部Passed，failed/skipped/inconclusive=0。没有过滤旧测试。`source-evidence.json` 绑定664输入/指纹4cbc889a…3ad及两份XML；EditMode/PlayMode日志原字节保留。

`independent-case-audit-069.json` SHA256 `1cba1b1629c6f49d0f085d348b1936938cc48c30d37837f483e13e3636fe38e4`：完整fullnames与原0.11正式XML及候选1比较；保留全部1,185/1,283例、分别新增101/3，无删除、重复或失败；每个运行输入对冻结Git blob/模式及当前文件逐项核对。实际运行源为28cd1b3191fafd49417d544e60dc898986568aa8。构建与原生观察另验，不由这些测试替代。
