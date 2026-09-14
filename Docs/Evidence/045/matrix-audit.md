# 045 三档600秒模拟局部审计

matrix-audit.py读取同目录baseline-standard与matrix。Standard的events、snapshots均与旧基线逐字节相同；result只新增aiDifficulty、aiDecisionSeconds、aiArmySeconds三个字段。

每条hasEconomy事件检查三资源before+actualDelta≈after（绝对误差0.02）、after非负与Payment的actualDelta非正；每条hasPopulation事件检查used/reserved/capacity非负，不要求used+reserved<=capacity。Easy/Standard/Hard分别为76/80/73条经济记录、286/320/285条人口记录，异常均为0。该样本结构化经济记录均为Payment。每方支付及发运/到达计数见JSON，不能将发运数与到达数之差判作接收错误，观察截点可能仍有运输中的船运。

这是固定seed314159、Coast镜像、相同档位控制双方的600秒headless观察。均winner=-1；不是Hard对Standard，不能证明难度平衡、完整经济/人口生命周期、Fog、攻击冷却、UI、音频或人工整局。
