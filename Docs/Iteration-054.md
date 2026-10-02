# 054 / 0.2.30 · 经济转移、队列点击与当前环境验证

2026-10-02，本轮从0.2.29继续开发，使用实际可运行的Unity6000.6.3f1，revision45d8eee7de74。间接Searcher解析为4.9.5；URP17.6.0、InputSystem1.20.0、UGUI2.6.0、TestFramework1.8.0直接依赖不变。首次导入发现GarrisonFirepowerTests.cs.meta的executionOrder类型错误，已修正为整数0。场景通过已有BuildTools生成，原生Camera/Light/GameController与关闭实时阴影设置保留。

## 资源耗尽自动转移

原耗尽分支缺少Complete判断，最近的未完工农田/钱币庄园可能成为新采集目标，村民等待没有完工的资源建筑。改为复用GatherableResource，保留同资源、己方建筑、当前可见、有效余量、18单位距离、最近目标和Shift尾订单边界。AI可能在下一决策重新分配，不能称为AI永久死锁。

四个新增用例覆盖food/coin、存在较远完工替代和只有未完工目标两种情形。旧运行时对应9例中5通过4失败；修复后新例与完整经济回归均通过。它们清除同类自然资源并创建特定地基，属于受控机制夹具，不是自然完整局。

## 队列取消与离散手势

旧按钮捕获队列索引，按下与释放之间若前序完成，可能取消后继或无法取消仍存活的被点订单。按钮现在捕获ProductionJob引用，释放时解析现时索引；已完成订单仅提示，不取消后继或发退款。相同选择且手势已按住时保留按钮和订单引用，独立下一次手势先同步最新动作上下文并等待Canvas。

两个真实InputSystem长按红例在旧HUD均失败。首次组合修复后完整PlayMode为180/181，原有ConsecutiveNativeTrainAndCancelClicksSettleExactlyTwice发现独立连续点击仍可能使用旧按钮；同步上下文修复后SelectionLifetimeTests定向9/9通过，原有断言没有删除或放宽。

## 实际验证与边界

构建后完整EditMode **194/194**、PlayMode **181/181**通过；验证工具 **8/8**通过。原始XML与source-evidence.json在`Evidence/054/Tests`，源码指纹为`5a64b4e474570909a98ab787ae6b522294cde5b399629ea909d9980b07846eb4`。历史报告不自动计为当前结果。

本机Apple M3 Pro/36GB/macOS27.0，客户端Mach-O arm64、Development/Mono、2560×1440。构建日志明确Succeeded，实际运行buildGuid为`21261224be4f40e692a3629fc4cc42fe`。三组正常规则原生自动局均自然结束，含双方V与真实炮兵建筑伤害，详见Native-Verification-054.md；事件审计排除零GUID测试夹具。

人工玩家整局、原生输入矩阵、当前规模性能、耳听及最终美术尚未通过。本轮人工操作受Mac锁屏影响；并行客户端与锁屏期间首个50单位性能样本不计为合格结果。Estate单资源8工人策略可能影响后期吞吐，但未获得当前故障证据，本轮没有修改AI策略或规则数值。完整陆战/阶段D仍开放。
