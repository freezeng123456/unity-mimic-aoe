# 0.2.9 资源右键上下文与生产集结（开发检查点）

日期：2026-09-14。0.2.9 本轮把资源右键从单一目标类型判断推进为按选择能力分流，并补充生产集结点的资源目标传递。规则数值和文明配置没有改变。macOS arm64 Development 客户端已构建并完成216.004秒正常资源的局部原生检查；这不能把本轮称为完整陆战版本或阶段 C/D 通过。

## 红绿诊断证据

资源上下文 PlayMode 使用 5 项冻结测试源码：`work/verification/029/ResourceContextTests.cs`。`Context-red.xml` 为 1/5 通过、4/5 失败，`Context-green.xml` 为 5/5 通过。

生产集结点 EditMode 使用 8 项冻结测试源码：`work/verification/029/ResourceRallyTests.cs`。`Rally-red.xml` 为 2/8 通过、6/8 失败，`Rally-green.xml` 为 8/8 通过。两份冻结源码及其哈希记录在 `work/verification/029/red-green-source-hashes.json`：

- `Assets/Frontier/Tests/EditMode/ResourceRallyTests.cs`: `6309d18b5cce056ce63ba3609acac00b6d6a7f46610abc2b9b40f81cb5262304`
- `Assets/Frontier/Tests/PlayMode/ResourceContextTests.cs`: `ef42f0051907e269b3eaf5ee07aed5ea67c1d5ebce5ac70800f670740f9517e7`

随后加入两项驻守释放回归后，全量结果为 EditMode 104/104（6.337 秒，UTC 05:49:17）和 PlayMode 89/89（20.284 秒，UTC 05:49:41）。这些 XML 是当前源码测试证据，不能替代独立客户端体验。

## 已覆盖的行为

当前实现与测试覆盖以下项目规则：

- 纯村民右键可见资源会产生 `Gather`，目标为实际资源实体；
- 非村民右键资源会产生无目标 `Move`，避免士兵进入采集订单；
- 混合村民/军事单位选择会按能力分流，Shift 排队分别保留原订单；
- 非探险家右键宝藏会移动，不会错误执行 `Claim`；
- 城镇中心右键资源会把可见资源位置传入生产集结点路径，并保留资源目标关系；后续训练出的新村民应沿该资源目标集结并能继续采集；
- 新增金色地面集结旗/青色资源集结旗及 HUD 说明；旗帜显示在已记录位置，不能泄露隐藏资源的实时位置。

资源可见性、资源实体资格、探险家领取资格和最终命令合法性仍由统一 World 规则入口判断。这里记录的是本项目主动设计的交互分流，不声称是 AoE3:DE 内部输入实现或精确规则。

## 当前交付边界

本次独立客户端 Build GUID 为 `4e851ef57c2f460885559d364e1edf6e`，Unity 构建结果 Succeeded，172870769 bytes，125 项构建输入哈希均与当前源码一致。实际216.004秒原生检查覆盖资源集结、三名付费村民、驻扎释放自动采矿、地面集结覆盖与新局清理；报告2919条事件，autoplay=false、simulationSpeed=1、正常开局，详见 [Native-Verification-029.md](Native-Verification-029.md)。混合选择和Shift由PlayMode覆盖，本次未扩展为原生混编整局结论。正常玩家III/炮兵攻城仍是下一项验收。

本轮没有改变五时代、第二文明、成熟 AI、完整炮兵攻城、声音、最终美术或目标规模性能的验收状态。完整陆战和阶段 C/D 仍未通过。
