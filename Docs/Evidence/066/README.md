# 066 · 0.9.0 批次3证据索引

本批实现本地四槽团队/FFA、七个中文课程、三章原创河流战役与持久设置/可访问性。完整自动化与代表性原生操作已通过；最终三包本地核验和六载荷/第七回执上传、云端元数据读回已通过；远端SHA/下载字节一致性未验证，GitHub最终推送/PR更新仍待，本版性能未采样。实现不是完整商业内容对等性。

## 统一身份

- 应用版本：`0.9.0`；原生构建GUID：`1969c7ee14f944879510f4ca0d0f241d`。
- 运行代码提交/来源基线：`c9747695d91fa203b05116a1fc0c15a8aac32de2`。
- 冻结输入：573文件，指纹 `66cf7cc57e080eb40cfdf98310ee26affd93d690745dde2b457e8fb3eb0ee1ee`；实际清单见[源码证据](Tests/source-evidence.json)。
- [066本地完成快照](../../Completion-Status-066.md)记录本轮验收；[当前状态](../../Completion-Status.md)可另行记录后续交付。[内容说明](../../Content-066.md)和[玩家指南](../../Player-Guide-066.md)描述规则与按钮。

## 按证据类型查阅

| 入口 | 实际证据与范围 |
| --- | --- |
| [Tests](Tests/README.md) | `run-qBfLgMDn`完整测试：710 EditMode+352 PlayMode，1062全部Passed；两XML及source-evidence原字节保留，日志仅替换2个设备标识 |
| [针对性Diagnostics](Diagnostics/preliminary-runs.json) | 历史针对性运行清单，不与正式测试加总 |
| [修复前完整运行](Diagnostics/Full-run-before-ui-repairs/diagnostic.json) | UI修复前完整运行的真实诊断，包括失败；不作为最终输入通过结果 |
| [Diagnostics脱敏回执](Diagnostics/redaction-receipt.json) | 8个许可设备标识替换，记录原始/归档SHA、bytes和行号；普通诊断保留 |
| [Native](Native/README.md) / [严格验收回执](Native/native-acceptance.json) | 同一GUID的代表性GUI、真实检查点/冷恢复、课程实际完成与账号记录；20组原始F9 PNG/JSON |
| [截图身份索引](Native/Captures/capture-index.json) | 各F9原始文件、身份及范围；不把菜单截图当终局或输入记录 |
| [构建回执](Native/client-build.json) / [日志回执](Native/log-redaction-receipt.json) | 原构建/运行身份及7份日志；仅构建日志1个设备标识脱敏，六session原字节保留，退出警告不删 |
| [存储保护](Native/storage-restoration.json) | 槽1/2原SHA未变；本次创建的槽3、Preferences、Account按精确SHA恢复原先不存在 |

## 原生实际覆盖

- **设置：** GUI保存主音量0.619632、UI比例1.292178、阵营形状、高对比小地图及攻击移动Q；新进程读回，Q实际攻击移动命令被接受。
- **单课：** `lesson_selection`在21.0000439秒存槽3，新进程恢复并通过真实移动抵达，在31.2000828秒完成；后续又一冷启动保留完成徽记、计数1。
- **2v2：** 曲河同局 `58062d16dc194299b429a5799fb6170a` 于103.4989929秒存档并新进程恢复；玩家投降后盟友存活、世界继续且无终局胜者。
- **FFA及呈现：** 英国/荷兰/法国/奥斯曼四独立队伍实际开局；中文课程/目标/结果、战役列表与章节锁、共享盟友视野小地图已呈现。

这是代表性操作验证，未原生完成全部七课加三章战役、三章解锁链或四人完整自然终局。没有本版性能采样、400单位结论、网络/回放、签名/公证或Intel平台验收。课程和战役更广泛的命令/关系回归在自动化Tests中，与原生人工范围分开。

session01/04/05/06退出0，02/03为显式SIGTERM退出143以建立冷恢复条件。GUI退出检查意外自动重开的无参数候选进程被终止，不作为任何通过证据，默认共享Player.log未读取或上传。MemoryLeaks与Thread finalized等Unity Development退出提示原样保留；字样出现次数不等于测得泄漏数，也没有根因或零泄漏结论。

## 包外交付证据与边界

[Delivery归档](Delivery/README.md)逐字节保存最终release manifest、435753字节/8部分全Passed的验包回执、六载荷cloud-upload-readback、第七回执的cloud-delivery-final、upload-inputs、辅助脚本、客户端指南和首轮失败回执。三ZIP以e754源码/文档快照冻结，运行代码c974/573输入/66cf指纹不变；上传后未重建，后来包外文档不在原源码ZIP中。具体下载、大小和本地SHA见[Delivery-066](../../Delivery-066.md)。

七对象的名称、大小、父目录、shared=false与owner-only元数据核验通过，远端内容SHA/下载字节一致性未验证。最终GitHub推送/PR更新仍待主任务，私有draft保留；Drive上传不等于GitHub已更新。首轮失败未删除或改称最终通过。Completion-Status-066分包前快照、Native/Tests/Diagnostics及065历史保持原字节/身份，本版性能仍未采样。
