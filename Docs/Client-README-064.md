# Frontier Reckoning 0.7.0 中文试玩说明

解压 macOS-arm64 包后，打开 `Frontier Reckoning.app`。适用平台为 macOS Apple silicon；此包是 Unity 6000.6.3f1 的 Development / Mono 试玩构建，GUID `48cbcf92c92f409c945f83eeb9b8d500`。

本版保留六文明、五时代、六地图、25卡组、经济/贸易/宝藏与曲河港区，并新增英国、荷兰、法国、奥斯曼的工业工厂和三个本地存档槽。完整国家内容、军械库、随机地图、运输登陆、联网和战役仍在后续路线中。

## 开始一局

在主菜单选择文明、地图和时代上限，点击 BEGIN SKIRMISH。要体验工厂请选择英国、荷兰、法国或奥斯曼，时代上限 IV 或 V；经济、军事、科技预设卡组已包含 Factory Wagon 和 Robber Barons。

- 左键点选或拖框选择；右键地面移动、资源采集、敌军或建筑指定攻击。Shift 可追加命令。
- V 找闲置村民，T 找城镇中心，M 循环军事单位，H 打开主城船运，B 打开建造。
- 军队面板提供 ATTACK MOVE、STOP、HOLD；按钮名称和快捷键显示在面板上。
- 镜头使用方向键或窗口边缘，小地图左键查看、右键下令。
- 编队可用 GROUPS 面板的 SAVE 0–9 / RECALL 0–9；数字键召回。原生已验 SAVE 2 与数字2，组合创建快捷键未在本轮覆盖。

正常采集、训练和研究会花费资源及时间，升时代与战斗赚取XP。到IV时代并拥有船运机会后，在H主城面板发送工厂马车。选真实到货马车，使用BUILD FACTORY，把工地放在已探索、可建造的陆地。马车需要实际到达，跨河走桥；消耗马车后工厂继续施工，每方最多两座工厂或马车（包含在途）。

完工工厂可在FOOD / WOOD / COIN / ARTILLERY四种模式中切换。英国生产火箭炮，荷兰/法国生产重型加农炮，奥斯曼生产巨炮。研究期间暂停生产；切换模式会清除未完成重炮进度。出炮需要人口和安全出口，工厂生产炮时停止资源收入。数值为本项目平衡规则，不是官方游戏数值。

## 保存与继续

Esc暂停 → SAVE MATCH → 选择槽1–3；选择已有槽会覆盖。主菜单LOAD SAVED MATCH或暂停LOAD MATCH可载入，读入后保持暂停，点击RESUME继续。世界、AI、迷雾、命令、队列、工业进度、固定步余量、镜头和编队一并恢复。

文件在 `~/Library/Application Support/Frontier Workshop/Frontier Reckoning/Saves/`。目前仅支持相同应用版本及规则，缺少跨版本迁移和云存档。载入不接受校验不符的文件。

F9生成当前画面PNG与状态JSON，F8开始/结束性能观察；二者都不补资源。对应Captures / PerformanceCaptures也在上述游戏数据目录内。

## 本版验收与材料

796/796自动化测试通过；原生普通局保存覆盖、进程重启冷载入和续局通过。四文明工业展示为明确受控的IV时代、显式船运XP、两张卡、无AI，港口、马车、工厂和重炮通过正常命令与真实等待生成。图集包的PreviewSaves供检查此展示，不能作为正常开局工业战争证明；不要用它覆盖需要保留的个人存档。

一局法国对奥斯曼的正常规则双AI对局以16×模拟自然结算于1047.691秒，法国到IV并形成工厂地基，终局时未完工、未产重炮。原生200/400陆军加12船各约90活动秒（含10秒预热）的压力报告P95为17.06/32.55 ms；400组仍有116.48 ms长帧，长期运行、Release与其他平台尚未验收。

源码、工业截图、完整证据和独立校验清单位于[0.7.0 Google Drive目录](https://drive.google.com/drive/folders/1p3mi7cTsY26RNYRKTRIDmwjw13FNgbMs)。包的大小和本地SHA见release-manifest.json，实际ZIP内容见package-contents-verification.json，上传读回状态见drive-upload-verification.json。许可证随本客户端在Licenses目录提供。
