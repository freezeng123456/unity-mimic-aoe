# 061 · 河流与码头证据

版本0.5.0，2026-10-04。源码提交0868a61，437源文件指纹 `1cb09c62d52f9e317d6521adf4883bc17c050146ddfc896e50b2f9a20fed3b9d`，客户端GUID `963f4744895a413bae47d7ed58f868c7`。

- [Tests/source-evidence.json](Tests/source-evidence.json)：323/323 EditMode、310/310 PlayMode；最终run-o9hqyFWI，构建后源文件一致。
- [simulation-acceptance.json](simulation-acceptance.json)：正常AI码头/捕鱼时间，以及200/400陆军加12船双向通行输出。前者保留原始经济、对手静止且时代II上限；后者直接布置军队、关闭伤害，逐tick检查域/速度/高度，不能当作原生FPS。
- [Studies](Studies/source-inventory-water-061.json)：三张真实WorldView/URP图，逐图配套JSON记录相机、实体、命令、像素对照和来源。图中直接布置单位并揭示视野，AI关闭；捕鱼须真实消耗鱼量，炮战须有Fire/Damage和飞行弹体。
- [Native/duel-acceptance.json](Native/duel-acceptance.json)：8倍速英荷河图AI实战，2790.071秒英国胜，双方V时代。正常付费7码头、8渔船、13炮艇出生；6269捕鱼记录、467炮艇开火、483伤害事件。完整194996条事件用gzip无损存储，未压缩125285013字节，SHA记于收据；naval-events.jsonl是便于审查的子集。
- [Native/screenshot-index.json](Native/screenshot-index.json)：5张F9原图和JSON。01是AI胜利画面；02/05是菜单；03/04属于另一局1倍速窗口检查。这局未经营经济、最终506.022秒战败，没有明确手动跨桥到达或指定攻击成功证据，不拼接为人工整局验收。
- [build-receipt.json](build-receipt.json)、[Build.log](Build.log)、[client-bundle-manifest.json](client-bundle-manifest.json)：macOS arm64 Development/Mono构建成功，298个客户端文件逐项SHA。
- [delivery-manifest.json](delivery-manifest.json)：61998844字节ZIP；SHA256 `d9d3d654f9716f268fa86804347f7a21cf6568ed90d833349e454b085841d6f4`；CRC及包内全部客户端文件核验通过。

Diagnostics保留失败轮次XML：付款日志匹配错误、菜单旧文字定位错误、荷兰文明说明截断。均经修复后完整复跑；另有早期编译错误保留在本地Builds/Tests日志，不计入最终通过数。

原生退出仍出现线程提前终结和MemoryLeaks遥测；未证明长期无泄漏。本版未重做原生200/400帧率基准，400人集中寻路峰值207.7091ms仍是已知性能限制。运输船、可毁桥、随机河图、动态水面及完整海军科技尚未实现。
