# 068 · 可信 LAN 合作证据

当前版本0.11.0、运行提交`dca073708b671c60a1fe9ff243a67e4ace3fe157`，630输入指纹`62240c2373c773b5aaf807c72815acab2e76577504f3a0569218702864730ebe`。原生GUID为`fa26339934914cd2813df9763de6bba7`，Unity6000.6.3f1 / macOS arm64 Development / Mono。

- [Tests](Tests/README.md)：当前完整回归817 EditMode＋368 PlayMode，1185/1185通过；XML、源码绑定和独立核验。
- [Native](Native/README.md)：一台物理Mac上两个独立原生进程，经真实loopback TCP合作。UI操作、截图/sidecar、共享暂停收敛、短暂心跳中断和恢复、生产付款/取消退款、录像及退出账本各按其实际证据说明。
- [Candidate1测试](Diagnostics/Candidate1-Tests/README.md)、[Candidate2测试](Diagnostics/Candidate2-Tests/STATUS.md)、[Candidate2原生诊断](Diagnostics/Candidate2-Native/README.md)：历史候选，身份和结果分别保留，不充当当前构建通过证明。
- [随包说明](Delivery/README-zh.md)：启动和合作操作，以及本轮验收范围。ZIP冻结后的打包/云端/私有PR回执在后续Delivery文件单独记录，不改变已冻结ZIP。

该阶段没有两台实体Mac的连通验收、正常资源合作自然终局、当前LAN直接指定攻击/冷回放或长窗性能数据。ACK丢失重传/至多一次付款由真实TCP自动化覆盖，不描述成原生故障注入已经验证。没有公网匹配、主机迁移、竞技敌情隔离或跨平台互通保证。
