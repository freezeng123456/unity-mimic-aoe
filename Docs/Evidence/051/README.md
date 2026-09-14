# 051证据入口

Destination-red为冻结4例旧版全部失败；green4是同4例加2个取消/摧毁生命周期、6个原有群体通行共12例通过。原始initial-red的混编夹具撞贸易点，修正夹具后才冻结并重跑。control控制只把施工者移开；initial-control断言额外要求恰好两单位失败，未考虑新出现的第三个拥堵，保留初始错误与最终因果控制。临时控制/trace源码只供阅读，不参与Assets编译。

green是首轮终点修复9/10，trace显示剩余炮兵的hasResolved=false、目标一直可用，真实问题为路线受站定友军挡住。green2/green3是未采用的局部绕行尝试，均8/10；failed-local源码保留26方向尝试，不属于正式实现。最终额外友军占位路径使用生产MovementRecovery.cs。

Destination-play是一个真实InputSystem Shift右键集成测试；EditMode/PlayMode是全套173/177。World由夹具推进，不代表独立客户端人类操作。

normal-*为正常Standard、I–V、seed314159、玩家0经济卡组对玩家1军事卡组的双AI模拟，没有额外资源、出生或揭雾。analyze.py在源码包中可直接运行，基线是../050/normal-*，允许因实际导航修复产生对局差异。build-inputs为303项构建输入；build-receipt有实际成功构建日志和二进制哈希。

无本版桌面截图或耳听、客户端帧率数据。所有测试与模拟不代替完整陆战/阶段D验收。
