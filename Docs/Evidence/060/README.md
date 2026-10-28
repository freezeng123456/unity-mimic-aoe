# 060 · 0.4.0 建模升级证据

- [Tests](Tests/source-evidence.json)：287 EditMode + 293 PlayMode = 580/580；最终 run-SApqy8SX，419文件指纹904627eb5d4e486cb11058815e564b35bdcadc0a8db235ed39b03f48a122fed6。
- `Architecture/`：11张3072×2048实际URP研究图；六文明、建筑目录、城镇/住宅五时代、施工及策略距离场景。每张有JSON收据与共同source inventory。
- `Models/`：4张实际URP研究图；单位近景、全部25单位、骑兵炮兵姿态、全部11资源。独立source inventory的419文件映射与Tests完全一致。
- [Native](Native/README.md)：0.4.0同GUID的基地、交战、贸易站被攻击、200/400单位压力样本、原生日志与战报。
- [构建回执](build-receipt.json)、[来源转换](build-source-transition.json)、[客户端逐文件清单](client-bundle-manifest.json)、[交付ZIP清单](delivery-manifest.json)。

Architecture指纹使用路径+制表符+文件SHA+换行；Models/Tests使用排序紧凑JSON。算法不同，比较时核对实际文件映射，不能直接要求两种指纹字面相同。

截图为游戏自身渲染，未使用图像生成或后期补画。研究图为合成摆放，不证明正常建造、解锁、操作、战斗或帧率。原生演示与压力测试单独标识。

完整范围及限制见[建模改造](../../Model-Upgrade-060.md)。初次定向测试72/74暴露隐藏网格释放和山岭轮廓不足，两问题修复后原阈值通过；失败XML保留Diagnostics供复查。完整单位目录有部分长杆靠近上一行颜色标签，仍能识别，未称完美排版。
