# 070 独立内容与证据核对

核对日期：2026-10-06。依据实际 XML 叶子用例、源码和已记录输入指纹；本次没有启动 Unity，也没有修改运行源码。通过的自动测试证明模拟和控制器行为，原生玩家操作及自然对局另行验收。

当前冻结提交为 `573e017c7c95f07548bc47f902408c53fd2ce88a`。工作树仅有未跟踪的 `Docs/Player-Guide-070.md`，该文档不属于运行输入。独立复算为 **682 个输入**，与正式记录逐文件一致，指纹 `e2ab75d010ebcde449d94a5b5ca23c7507ac837b1efa2b957f41facde8d5333d`。

| 当前正式全套 | 当前状态 |
|---|---|
| `Builds/Tests/run-mfr2pucl` | 正式全套已完成，通过 |
| EditMode / PlayMode 实际叶子数及结果 | **966/966 + 397/397 = 1363/1363**，逐叶子读取全部 Passed，无失败、跳过或不确定项 |
| `source-evidence.json`、XML SHA 与 682 输入绑定 | 两个 XML SHA 与记录相同；记录输入指纹内部复算正确，当前 682 输入与记录逐文件相同，且等于冻结指纹 |
| 当前全套直接包含的相关 suite | `GuidedPlay066Tests` 29/29；070 Groups 27/27、Content 24/24、Restore 16/16、Branches 4/4、Controller 6/6 |

已读取的结果如下。定向诊断与上一轮基础全套分别列出，不能把旧基础全套当作当前 682 输入的最终验收。

| 已完成结果 | 实际叶子结果 |
|---|---|
| [fourth-edit/EditMode.xml](/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.13.0/Candidate1/Diagnostics/fourth-edit/EditMode.xml) | **40/40**：`RiverContent070Tests` 24、`RiverCampaignRestore070Tests` 16 |
| [second-play/PlayMode.xml](/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.13.0/Candidate1/Diagnostics/second-play/PlayMode.xml) | **6/6**：`RiverContentController070Tests` |
| [branches-edit-2/EditMode.xml](/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.13.0/Candidate1/Diagnostics/branches-edit-2/EditMode.xml) | **4/4**：`RiverMissionBranches070Tests` |
| 基础正式 `run-7hVOOzKF` | EditMode **962/962**、PlayMode **397/397**，合计 **1359**；无失败、跳过或不确定项。属于之前的 680 输入，指纹 `fe9f2e7c5888168e7bb9818bf5f8f068948fef9ca59e30980232519190f96cfa`；记录内部指纹和两个 XML SHA 已独立核对一致 |

实际内容与通过用例的对应关系：

| 内容或合同 | 已通过的具体证明 |
|---|---|
| 原 7 课和原 3 章保留 | 与 `5ce58df` 比较，`GuidedPlayCatalog` 原十项定义及原章节解锁链保留，revision 仍为 1，只追加三项。当前正式全套 `GuidedPlay066Tests` **29/29**；十个旧 ID 均通过 `EveryScenarioIsSolvedByAcceptedRealOrdersAndWorldResults` 与 `EveryScenarioContinuesAfterActualSlotRoundTripAtAStageBoundary` |
| 新三章独立解锁 | `NewArcUnlocksSequentiallyOncePerActualAttemptAndPreservesTheRevisionOneLegacyAccount`：首章可开始，后两章依次解锁，同一尝试重复记录只计一次，旧账号记录保留；Controller 的目录按钮、锁定入口、工作坊副本不得获账号解锁等用例通过 |
| 指定四人载兵护送 | `OnlyTheSpecifiedLivingSoldiersInsideTheSpecifiedShipCompleteBoardingAfterColdRestore` 与实际 Convoy 通关验证指定组、指定活运输舰和真实货舱成员；工人、其它船和其它部队不得替代。`EmptyTransportCannotCompleteTheEscortByLeavingTheDesignatedPartyOnItsHomeShore` 用真实六人登船、在家卸载、空船航行及冷恢复，验证 `sail` 保持 active、`land` 仍 locked |
| 三章真实通关 | `NewChaptersAreWonByRealCommandsCombatCargoAndTimedWithdrawal` 的 Scout、Convoy、Retreat 三个参数用例通过：使用接受的普通移动、可见目标攻击、登卸船和固定调度，未以直接赋值替代胜利 |
| 撤退两种路线 | Retreat 的上述正常解题路径完成陆路回撤；`RearGuardCanChooseActualBoardSailAndUnloadWithdrawalAfterHoldingTheSamePort` 完成真实装载、载兵穿越南桥下方河段、另一岸卸载和回营，前港仍存活 |
| 冷存档与恢复完整性 | 三章均通过 `StageBoundaryColdSlotContinuesWithoutDuplicatingBodiesSuppliesOrWaves`；控制器 Scout 实际保存/加载后完成并只解锁一次。恢复校验 16 项包括目标状态单独篡改、先决顺序、时长、结果事件不一致和坏槽字节保留；这些异常状态是明确的拒绝测试，不计为通关证明 |
| 护送整章录像 | `ActualAuthoredConvoyRecordingRebuildsCargoTriggersObjectivesAndGuidedResultForward` 完整重放护送章节，验证世界、Guided 状态及 digest 一致、`Verified` 成立、录像文件字节保持不变。当前直接证明的是 Convoy 整章 |
| 支线补给只发一次 | `ActualLookoutVisitAndRepeatedColdVisitsDeliverExactlyOneWoodShipment`：实际到瞭望点获得 **120 木材**，冷恢复后二次出入不重复。`ActualCacheDestructionPaysOneRepairDeliveryAndColdRestoreDoesNotRepeatIt`：守港后正常指定攻击毁仓库，实际入账 **100 木材、80 钱币**，冷恢复继续调度不重复 |
| 真实受伤后的付费修船 | `RealCounterattackDamageIsRepairedAtTheForwardDockWithActualPaidHealthReceipts`：护艇受到实际反击伤害并存活，普通 `DockRepair` 到前港修复；核验 HP 增量及 `Payment` 的实际木材、钱币扣款。没有注入损伤、资源或位置 |
| 目标截止失败与重试 | `TheSurveyDeadlineFailsThroughRealSchedulerStepsAndRetryHasNoOldRewardOrResult`：合法守卫/集火保营地，真实调度到 180 秒保存，再恢复推进至约 360 秒；调查目标产生 `Deadline expired`，失败存档可恢复，重试为新 attempt、零时钟、零旧奖励和零旧触发次数 |

仍需保留的验收边界：

- 当前冻结构建的原生鼠标/键盘、焦点暂停、屏幕画面、性能与新进程冷恢复尚未形成原生验收证据；控制器测试不能替代它们。
- 新内容的两台物理设备联机、断线恢复和跨设备画面一致性未验收。
- 普通 Traders' Bend 已证明正常经济开局、对称资源预算、两桥导航、河道和合法港区/登卸端点；几何探针没有实际造港或生成舰队。此图的玩家付费造港/造船/捕鱼/贸易、AI 完整歼灭对局、各文明和两侧自然对局平衡仍未证明。
- 新护送章的直接获胜证明采用西渠护航，东水道绕行尚无独立实际完成用例。
- 只验证了自然可达的约 360 秒调查截止失败。三章各必需目标的期限先于 1200 秒总时限结束运行状态，没有改时钟或改章节定义去伪造总时限分支覆盖。

以上未覆盖边界应保留在交付说明中。当前正式测试和源绑定已通过；构建及原生验收尚未纳入本报告。机器可读的独立核对见同目录 `independent070-test-audit.json`。
