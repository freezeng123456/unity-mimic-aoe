# French075 与 Cold1 独立原始证据审计

状态：accepted_with_documented_scope_and_limits；1400 项检查，0 失败。仅只读原件并新建本 JSON/MD，未运行 GUI、Unity、游戏或回放，也未修改源或原件。

- 构建0.18.0 / GUID 67594b51e2dd40f593ec8e956f6f4960 / source FP a3e1e3518a23d000f6ce1e80bef628f72bd55c631fcb9ddcd6e9f2be80066594；实际全路径 App、可执行 SHA 与稳定源码 build receipt 相符。原 PID69715、冷 PID69832 均已不存在；有 quit reports/cleanup，无独立 exit-code receipt，不宣称 exit0。
- 普通 French0/BritishAI1、winding42、Age I，初始6Coureurs、300F300W100C、未注入新侦察单位，只有P1自动控制器。
- Payment event[405] t22.100048065 实付50F25W，钱包300/300/100→250/275/100；Queued[406] reserved1；UnitTrained[578] t40.099960327 自然18秒完成，target136。slot1 t22.600049973 已付款、elapsed0.5/duration18，尚未产生Scout。
- original records[0] Train、[3] Scout16 step764、[4] ToggleStealth28 step771均accepted。event[975] t77.099395752 concealed。slot2 t82.999305725 / step830 /1057events 保存Scout136、HP100、stealth=true、Scouting、单一Scout order与实际resolvedPoint/path、selected136。
- 冷Replay initial World逐值同slot2，仅events投影为空且完整1057历史保留在events前缀；Simulation仅accumulator 0.04935357719659805→0，符合ReplayDigest.SimulationState。没有其他差异被剔除。slot2 SHA 68ea5ea2e9102b369d11c0bc24a5fdc184d5e8ea939f36ee00e5c6430188c7aa 与Cold launch-before相符，审计前后不变。
- 冷paused F9 time82.999305725位置与slot2完全同；resume后F9 time87.999229431，约5模拟秒实际位移6.067231591，同136/Scout/Scouting。Cold record[1] step1003 accepted LeaveStealth；event[1304] time100.299041748 visible，终态HP100、stealth=false、Scout order保留。
- 原始/冷回放4+2anchors、7+4records、1065/1330events。全部event/input prefixes与6stateDigest按原始JSON浮点字面和字段顺序独立重算相符；原报告事件与对应Replay逐JSON全量相同。原Quit相对保存点多8条P1 Gather flush，冷链从保存1057前缀接273条后续；该差异保留。
- 9对F9全部PNG SHA、每chunk CRC、完整1920×1080图像scanlines/IEND EOF与metadata/log相符；亲看182222付款队列18s、182342 Scouting ACTIVE、182611冷paused ACTIVE、182630 OFF。没有宣称全部像素人工审核。
- 37个选定输入前后SHA/size/mode/mtime以及French命名空间均稳定；未宣称整个Candidate1全球冻结。两原日志MemoryLeaks诊断保留。

未验收：原生Replay播放、敌方detector隐身可见性、Envoy建造/战斗、长局平衡、FPS性能、网络多主机。F9仅投影，完整恢复依据save与冷Replay raw World/Simulation。

机器报告audit-french075.json SHA256：9f358a4cc5d7c27ea11fde99c8f1b39cba52bdc2da037f22ee74b894ca186827
