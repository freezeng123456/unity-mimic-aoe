# 071 Native Process1 原始文件审计

只读已退出的 Process1 与 ProfileA。**真实革命付款→取消全额退款→再次付款入队→暂停存档；革命尚未完成，原槽没有控制组。** 本报告不构成冷加载、单位转换或平民重建验收。

| 世界时间 / 秒 | 原始行为 |
|---|---|
| 45.999870 | Town1接受Revolution，实际扣800F/800W/800C；1300F/800W/1200C→500F/0W/400C |
| 61.599632 | 接受CancelQueue，退款800各；恢复1300F/800W/1200C，记录RevolutionCancelled |
| 90.399193 | 再次付款和入队，紧接manual_pause，槽一保存于此时 |

原槽world.schema4、stepCount904、paused=true、winner=-1，P0仍Empire。原工人ID3–8均存活且150HP。Revolution队列paid800各，**elapsed0.0/duration90.0**。90.399是世界时间，不是90秒革命进度。累计spent1600各、refunded800各，余额500F/0W/400C与交易一致。

原槽selection=[1]对应Town；**groups=[]**。Digit1输入日志中save=False，数字键尝试与选中状态不能证明控制组写入。后续可真实设置并保存新证据，须保留这个原槽原字节。

身份在槽、录像、退出报告和活动F9中一致：matchId `f496cf03970641248584b719cdf87515`，appVersion0.14.0，buildGuid `2d39f78f9f234ef092e172a35f800cae`，rulesHash `e0aca57faf2e1d08be2a6e341391be985fc368e296e410397327b38292024583`，scenarioId `controlled_iv_actions071`，scenarioHash `95eecd198899733720bcdb76556c2c8524e514881b002da9adf8756838977319`。这是声明的受控IV场景；autoplay=false、simulationSpeed1、hasGuided=false。

两份gzip-base64容器通过单成员gzip完整性、UTF8、JSON重复键及实际payload SHA校验。槽payload SHA `ec7ab2b96e60db2192a9a3aefd5e71260f48f603bdc0c4edbb51199db8ae8099`；录像payload SHA `584a4c64fb643547c8f94abf89ffd4f614ab6294f120efd964a1adaf663e2026`。录像finishReason=menu_exit、incomplete=false；5条记录包括3个accepted政治命令、manual_pause和flush。槽、录像、报告132条事件语义完全一致，无RevolutionCompleted、WorkerConverted、CivilianRebuilt或MatchEnded。

最后录像anchor同为904步、90.399193s。与槽World仅events不同（anchor按格式留空并另有event ledger），与simulation仅accumulator不同（anchor归零）；没有将它们误称为整个存档逐字相等。录像前向执行及stateDigest的Unity重算本次未进行。

9组F9的PNG SHA、1600×1000 IHDR尺寸、schema6和构建身份均核对；活动捕获的request/HUD世界时间和身份一致，HUD资源文本解析值与钱包一致。45.999870s捕获有0/90队列，61.699631s退款后队列为空，最后两组90.399193s捕获paused且新队列0/90。像素级画面检查由根代理原生验收另行完成。

Process1记录exited/exitCode0，Player.log SHA与process.json相符。25个原始文件的字节SHA已记录并在输出前再读确认未变。process.json的app/build二进制SHA作为声明保留，未读Native范围外二进制重新计算。

完整机器证据见同目录`071-native-process1-audit.json`，包括原始文件路径/SHA、F9配对SHA、交易、命令、最后anchor哈希和限制。根代理可用原槽字节SHA核ProfileB复制；复制成功不等于冷恢复成功。ProfileB须真实加载、继续推进剩余90秒，并单独证明转换／重建／控制组后续行为。审计未控制GUI、运行Unity、复制或修改任何原文件。
