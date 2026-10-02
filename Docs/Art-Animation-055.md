# 原创视觉与程序化动画 — 055 / 0.2.31

2026-10-02源码记录。当前Unity项目版本为0.2.31，编辑器6000.6.3f1。本文记录实际实现与验证边界，不把源码存在、静态编译或离屏研究图当作阶段D的完整玩家验收。统一定向重跑67/67通过，完整EditMode 214/214、PlayMode 210/210通过；5组修正朝向、标签与建筑屋顶的研究图已导出并读审。新0.2.31原生正常玩家验收进行中；冻结0.2.30的历史玩家截图不能证明本轮视觉已经通过。

## 几何与轮廓

本轮由 [ProceduralArt.cs](../Assets/Frontier/Presentation/ProceduralArt.cs) 生成原创纯色低多边形网格与Unity基础几何，使用既有共享URP材质。未导入外部模型、贴图或动画，没有改模拟数值、建筑占地、实时阴影或战斗规则。

| 对象 | 实际表现 |
|---|---|
| 村民、探索者、长枪兵、长弓兵、散兵及其余程序化人物 | 八角斜角截面的肩、腰、外套下摆、袖子、头部、马裤和分段靴；整圈宽队色腰带和帽带。村民浅外套、宽草帽与资源植物/开口箱采用不同轮廓。 |
| 火枪手 | 保留原 [FrontierInfantry.fbx](../Assets/Resources/Models/FrontierInfantry.fbx) 身体及 [AuthoredInfantry.cs](../Assets/Frontier/Presentation/AuthoredInfantry.cs) 的刚性关节挂接；没有用程序化身体覆盖既有FBX。运行时枪仍挂在真实手部枢轴。 |
| Hussar、Dragoon、Carabineer | 马身、倾斜马颈、头吻、双耳、鬃、尾、分段腿与蹄；队色鞍毯、皮鞍和缰。骑手髋、前伸大腿、弯膝和下垂靴构成坐姿。Hussar使用弯军刀，Dragoon长枪与Carabineer短枪有不同实际管长。 |
| Cannon | 长细空心炮管、炮口圈、轮架侧板、轮毂/辐条/铁轮缘及队色车架板。 |
| Mortar | 短宽空心炮管、较高仰角、独立转台和短底座；拖行轮径与Cannon不同。炮管长度、直径、角度及后座幅度为实际几何/动画差异。 |
| Barracks | 后侧训练厅、前庭、两侧武器架与训练靶。 |
| Stable | 开放厩门、后墙、侧墙、分隔栏、草捆与留有入口的前围栏。 |
| Foundry | 砖色炉房、两根烟囱与深色帽沿、铁框炉口和暖色炉床；没有新增烟粒子或灯光。 |
| Market | 后库房、带浅色条纹的摊棚、前柜台与三组货筐。 |
| Bank | 平顶及石围檐、石色墙体、粗石壁柱、深色金库门与金色金库徽。 |
| Estate / Farm | Estate有绿色四坡顶记账屋、前侧绿色作物与账桌；Farm是完整耕地、成行金色作物及田界，没有复制住宅体块。 |
| TradePost | 单坡顶后仓、前仓棚、货箱与装卸台。Town、House、Tower保留主屋、钟楼或望楼结构。 |
| F/W/C资源箱 | 开口木箱与铁带；Food露出粮袋/农产品，Wood露出多根木料及浅色端面，Coin露出金色锭。没有依靠字母或改占位名称来区分内容。 |

建筑的屋檐、庭院、围栏、摊棚、货箱及旗帜均按当前width/depth半宽内的尺寸编写。原Barracks外侧枪架、Market与TradePost外伸棚不再越出这一区域；11类建筑的实际网格顶点占地回归均通过；这一结果不替代实战遮挡和正常镜头识别。动态单位武器和马匹轮廓不参与碰撞/导航范围计算，仍使用原模拟半径。

## 已实现的姿态

[UnitAnimation.cs](../Assets/Frontier/Presentation/UnitAnimation.cs) 从模拟时间、实际工作状态、发射/受伤/死亡时间和部署/冷却状态只读求姿态；[UnitRig.cs](../Assets/Frontier/Presentation/UnitRig.cs) 保存并恢复刚性枢轴。本项目没有导入外部动画片段，也没有新增骨骼蒙皮或物理布料。

| 状态 | 源码实际支持 |
|---|---|
| Idle | 步兵基础持握与肩部姿态；骑手保持坐姿，一手持缰位置、一手持刀/枪。没有独立呼吸或面部动画。 |
| Move | 步兵双腿交替；骑兵对角腿步态、轻微头点与尾摆，骑手膝盖保持坐姿；火炮轮转。WorldView另提供微小整体上下起伏。 |
| Gather | 真实工作时，伐木用斧、采矿用镐，其余采集用篮。工具和双臂随模拟时间工作；篮保持直立。 |
| Build / Repair | 锤击与支撑手姿态，取决于实际付费施工/修理工作状态；无有效工作时隐藏工具。 |
| PreparingAttack | 长枪双手前向持握，近战准备挥击，长弓拉弦且弦端连接真实拉弦手，枪兵双手瞄准。骑手手臂目标继承座位高度。 |
| Fire | 长枪短刺、刀/近战挥击、弓弦释放/恢复、枪的短促回缩；火炮沿炮管自身轴线后座，Cannon与Mortar后座幅度不同。 |
| Reloading / recovery | 基于实际攻击冷却绘制持枪装填手势、弓/近战恢复姿态。它是表现状态，不新增弹药或改变攻击周期。 |
| Deploying | 火炮从拖行角度过渡到各自部署仰角。未实现支腿开合或独立炮组成员。 |
| Hurt | WorldView按受伤时间提供短暂整体前倾；不驱动新的伤害或事件。 |
| Death | 约0.65模拟秒整体倾倒并降低位置，随后沿原显示/模型池生命周期处理。没有布娃娃、肢体分解或个别死亡片段。 |

每次Apply先恢复初始关节位置/旋转，避免重复读同一时间或模型复用时累积旋转。新增膝、马头/尾与炮挂点也参与缓存。[WorkerEquipment.cs](../Assets/Frontier/Presentation/WorkerEquipment.cs) 的斧、镐、锤与篮只在创建工人模型时生成，切换工具使用已有对象，不逐帧重建。

## 网格与来源生命周期

[ProceduralMeshLifetime.cs](../Assets/Frontier/Presentation/ProceduralMeshLifetime.cs) 只拥有MeshPart创建的独占Mesh；模型池停用保留网格，销毁对象时释放。它不销毁FBX资产、Unity共享基础网格或项目共享材质。WorkerEquipment原独立Mesh销毁清单已并入同一机制，避免重复所有权。

保留的Noto字体、FBX、Blender源和Package Manager许可见 [Asset-Licenses.md](Asset-Licenses.md)。本轮没有使用图像生成模型、商业游戏资源、外部录音或第三方乐谱。

## 提示音、环境与音乐

| 声音 | 本轮实际源文件与生成方式 |
|---|---|
| 战斗与单位活动 | 保留 [EventAudio.cs](../Assets/Frontier/Presentation/EventAudio.cs)、[ActivitySoundClips.cs](../Assets/Frontier/Presentation/ActivitySoundClips.cs) 的本项目正弦/噪声合成及脚步、蹄、车轮、伐木、采矿、搬运、施工反馈。 |
| 六类战略通知 | [NoticeSoundClips.cs](../Assets/Frontier/Presentation/NoticeSoundClips.cs) 为船运到达、时代完成、人口不足、资源耗尽、建筑完成、宝藏取得分别定义频率序列与包络；22050Hz单声道，约0.24–0.56秒。通知只为玩家本人事件播放，不播敌方战略通知。 |
| 风声 | [AmbientAudio.cs](../Assets/Frontier/Presentation/AmbientAudio.cs) 中AmbientSoundClips以固定种子平滑噪声与周期起伏合成16秒循环；只有镜头所在当前可见地面允许环境风床，不跟踪隐藏敌人。 |
| 原创主题 | 同文件用四组和弦频率、八音旋律和正弦谐波/拨奏包络合成24秒循环；没有外部音源、采样乐器或引用现成乐曲。 |

环境和音乐各占一个独立循环AudioSource，不消耗原12个事件/活动声音槽。暂停、总静音、结束、回菜单和重新开局处理已有循环；暂停菜单独立提供ENVIRONMENT与MUSIC开关，SOUND仍控制全部音频。设置存在不证明混音或耳听品质通过；须核对实际AudioListener混音与玩家体验，方法见 [Native-Audio-Capture.md](Native-Audio-Capture.md)。

## 当前验证与待收录证据

已新增 [ProceduralSilhouetteTests.cs](../Assets/Frontier/Tests/PlayMode/ProceduralSilhouetteTests.cs) 20例：11类建筑全网格占地、4类人物真实腰肩截面/宽队色、3类骑手坐姿/抓握/重复步态、两种火炮差异与后座复位、生成Mesh池化与销毁且FBX仍存活。另有 [PauseAudioSettingsTests.cs](../Assets/Frontier/Tests/PlayMode/PauseAudioSettingsTests.cs) 2例原生点击标签/独立开关和8个菜单按钮实际Rect不交叠/越界。

首次定向67例中62通过、5失败的历史记录保留在Builds/Regression055/Unified。4个队色用例源于URP材质颜色读回的浮点误差，原默认RGBA字符串只打印三位小数：蓝队0.13读回0.129999965、0.42读回0.419999957（各差−2.98023224e−8），0.64读回0.6399999（差−5.96046448e−8）；红队0.22、0.15也各差−2.98023224e−8。回归逐RGBA组件采用1e−5误差，保留原腰肩比例、宽队色实际网格尺寸、抓握和生命周期断言。音频独立组件布局修正后，同一定向组 [visual-audio-green.xml](Evidence/055/Targeted/visual-audio-green.xml) 67/67通过，其中20个几何、2个暂停菜单与既有输入/驻军回归均通过。随后最终冻结源码的完整EditMode 214/214、PlayMode 210/210通过，源指纹为`1b45f97f3d90c9b76388419492a83c5622ccb2cf3a365030044403ccd309c1c7`。

[FrontierArtStudy055Tests.cs](../Assets/Frontier/Tests/PlayMode/FrontierArtStudy055Tests.cs) 已重新导出5组URP原始PNG与SHA侧车JSON，文件在 [Evidence/055/Art](Evidence/055/Art)，5组PNG的SHA再次全部与侧车匹配。输出由FRONTIER_ART_STUDY_DIR指定；静态建筑/资源使用正常默认yaw0、单位yaw155，标签characterSize为.15、fontSize40。侧车明确记录两种朝向；下述结论来自这次新图。

同目录另有19张未带055后缀的023/024身体对比、姿态及火枪近景历史诊断补充，精确生成版本尚未核实；逐文件SHA与出处边界见 [historical-provenance.json](Evidence/055/Art/historical-provenance.json)，它们不计入上述5张0.2.31新研究图或正常玩家验收。

| 文件基名 | 尺寸与用途 | 当前状态 |
|---|---|---|
| units-strategy-055 | 2560×1440，正交26、俯仰50°，12类单位的蓝/红队阵列 | 新图已读审；队色、村民/骑兵/两种炮轮廓可辨 |
| units-study-055 | 相同单位，正交16的放大诊断 | 新图已导出；不替代常规距离 |
| buildings-strategy-055 | 2560×1440，正交26、俯仰50°，11类建筑 | 新图已读审；11类主要结构可比较，厩门/训练院仍较小 |
| resources-strategy-055 | 2560×1440，正交26、俯仰50°，村民与七类资源并排 | 新图已读审；村民、植物、矿、鹿和三类箱可比较 |
| resources-study-055 | 相同对象，正交12的放大诊断 | 新图已导出；不替代常规距离 |

新常规距离图中，草帽浅外套村民与树、矿、鹿、资源箱有不同轮廓，蓝/红队色明显，马匹与步兵有明显体量比例，Cannon长细管轮架与Mortar短宽高仰角炮口可直接区分。建筑11类的新前向图展示了Town钟楼、Tower高度、Foundry双烟囱、Farm作物行、Market摊棚、Barracks庭院和Stable围栏；Bank平顶围檐、Estate绿色四坡顶作物、TradePost单坡后仓与大棚已改变主体读感，不再全部共用房屋山墙轮廓。标签已清晰且没有覆盖模型。厩内分隔、武器架、金库徽等细节仍小，Hussar刀与两种骑枪的细微差别也不能仅依赖26距离静止阵列图宣称完全识别。

研究图不含普通玩家HUD、选择与实战遮挡，静止火炮为部署姿态，也不是混战或性能证明。正式验收仍需新原生客户端在通常镜头距离下检查单位/资源识别、队色、骑兵武器、炮种、建筑用途、真实工作/战斗/死亡以及暂停菜单布局；同一局的输入、可见性、结算、音频和规模验证继续按 [Milestones-055.md](Milestones-055.md) 留证。当前阶段D尚未据此通过。
