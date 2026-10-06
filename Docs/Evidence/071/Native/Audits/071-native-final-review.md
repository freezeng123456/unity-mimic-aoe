最终原生历史证据独立复核

实际observed收据与原始文件一致：8个验收进程1、3、4、5、6、7、8、9均退出0；46份F9元数据/PNG对；3slots、3replays、3reports、2coldRestores。Process2虽退出0，只是无对局的相对路径启动诊断，未计入8个验收进程。

15种scope，68checks（8+46+3+3+3+2+3），全部observed_verified；120个唯一输入文件，字节/SHA复核一致。三真实回放为0→904、904→2441、2441→5118。Native全目录142个文件、41,356,637字节；这不是120个检查器输入文件的同一统计口径。所有PNG CRC/1600×1000核对，无原始字节变动。

GUI观察人为Codex /root代理，不是人类用户。3项manual记录关联5份选定原F9图；本次只读审计不判读像素。其余41份截图不声称逐图人工审阅。

日志存在真实退出诊断，不能称“零泄漏”或“无警告”：

| 进程 | prematurely finalized提示 | MemoryLeaks Immediate allocatedMemory | Exception/FRONTIER_FAILED文本行 |
| --- | --- | --- | --- |
| process1 | 6（首行367） | 第379行，3,159,321 B | 0 |
| process3 | 7（首行415） | 第428行，3,151,617 B | 0 |
| process4 | 6（首行506） | 第518行，3,160,117 B | 0 |
| process5 | 6（首行141） | 第153行，3,154,005 B | 0 |
| process6 | 7（首行129） | 第142行，3,154,005 B | 0 |
| process7 | 6（首行168） | 第180行，3,154,005 B | 0 |
| process8 | 7（首行128） | 第141行，3,154,037 B | 0 |
| process9 | 6（首行128） | 第140行，3,159,141 B | 0 |

每条日志的末尾都含“Thread ... may have been prematurely finalized”和结构化MemoryLeaks。已有收据warnings=[]只表示其正则未匹配：正则没有覆盖该线程措辞和无空格MemoryLeaks字段。这些诊断未做Profiler/引擎或游戏代码归因；退出0及通过回放不会证明没有内存泄漏，也不能凭上述汇总直接认定游戏逻辑泄漏。本次完整读取日志后未发现Exception/FRONTIER_FAILED文本，只能限定到这8份日志。

历史observed journal仍保留title/draftState/manualObservationStatus/releaseAcceptance/pending等草稿占位，与已observed状态不一致；legacy originalFiles还未列入8个新HUD/Process8/9文件，但checker的120输入确实覆盖并验证它们。原件保留不改，root另建final journal/receipt消除占位，本报告不替代未来final收据。

此范围为英国受控IV原生动作及2个0.5秒真实触发/截止HUD样本，不证明普通年龄I经营、所有文明、平衡、两台实体机器LAN或长期性能。组1在转换后创建；未声称转换前编组跨转换保持、已打开母城跨转换刷新。slot3事件353是最终回放354严格前缀，多一条同秒0.05W日志刷新，未新增钱包资金。

审计未重跑检查器/Unity、未操作GUI、未编辑repo/原件。

Final artifact supplement

The separate final journal/receipt removes inherited pending placeholders and explicitly attributes five selected original F9 pixel observations to the root agent. Historical observed journal/checker originals remain unchanged. Other41 captures are not claimed as individually pixel-reviewed.

Final receipt:68 checks,15 scopes,0 errors,0 missing. Its120 raw input files exactly match the historical successful receipt. The complete142-file Native inventory(41,356,637 bytes) exactly covers the existing tree; every original SHA/size independently rechecked. The stale pending fields/partial inventory findings above apply only to the historical observed journal; new final files resolve them.

Final checker changes exactly one warning-regex line. Actual warnings per process are7,8,7,7,8,7,8,7:59 total(51 prematurely-finalized thread rows and8 structured MemoryLeaks rows). All exact original diagnostic rows are retained. Other raw checks remain identical; two HUD manual descriptions were updated after root inspected the same original selected PNGs. Causes remain unassigned; accepting the specified native behavior scope does not certify zero leaks. No checker rerun, Unity, GUI or original mutation.

Final journal SHA 29b2c19ec1681b0b4915b118d67381da829f954fd704112bd1b67f4143a91650
Final receipt SHA 3d31cbf460baa2f312b527aa381c92db0012aed00cd0475555fd3003f22a2175
