# 049 证据说明

最终Activity-red/green使用同一隔离守卫后的测试文件（red-test-sha256）：旧0/5、新5/5。initial/pre-isolation/first-green/diagnostic为早期证据，确认守卫的正常脚步和近战被最初的总计数负例误算；不将改动前后的不同测试混为一条红绿链。before为原0.2.24源码对照，不参与编译。

EditMode/PlayMode是完整套件。audio-clips为Unity生成样本、metrics及PCM16 WAV；它们不是游戏实时录音、原生耳听或混音验收。normal-*为seed314159/Standard/I–V、玩家0 economy/玩家1 military的正常无图形双AI模拟；基线在../048/normal-*，已核实逐字节一致。analyze.py可在当前目录重跑，会覆写同目录analysis.json；无私有路径依赖。build-inputs与build-receipt对应本版285项实际输入和构建。没有本版原生截图、人工完整局或性能数据。
