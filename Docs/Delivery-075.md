# 0.18.0 私有交付 · 075

2026-10-10。四包、三个sidecar、七文件收据和下载说明共**九文件168818150B**，已上传原私有Google Drive并实际完整读回；全部bytes/SHA相同，metadata均shared=false、只有原user owner。目录直接子文件数量与ID集合也已核对，没有共享修改、main合并或公开发行。

运行提交 `51bd61ff1abc92f224dd449784671234a20a5555`，758输入FP `a3e1e3518a23d000f6ce1e80bef628f72bd55c631fcb9ddcd6e9f2be80066594`；源码ZIP快照 `67b13e77d47ccdf648a5e6113a43b2a5eb50bb0a`。macOS arm64 Development/Mono、Unity6000.6.3f1、GUID `67594b51e2dd40f593ec8e956f6f4960`，Build1实际Succeeded/exit0/sourceStable。Focused6为85/85；Full2正式1291Edit+432Play=1723/1723通过。

[本版私有目录](https://drive.google.com/drive/folders/1MOcc27AB4Ay56AJ3dfcjUUWc3ULXHwZA)位于原Frontier Reckoning项目下，需原账号权限。

|原件|bytes|SHA256|下载|
|---|---:|---|---|
|source.zip|32909939|`055e2179f5baa88d3842a60c06f56afa3e3107cbd9f7df0c033acfcff0baf2df`|[文件](https://drive.google.com/file/d/1bErm0DyBZum6YLEqbLH6XzoVTtH9YF_y/view?usp=drivesdk)|
|macOS.zip|75982487|`6585afda9f86735f54383e0e4c83243ea269623856de792d315a3360e9d7c6f2`|[文件](https://drive.google.com/file/d/1xRAqY466hjxylwJjnx5IhFfRR5Kel6Vq/view?usp=drivesdk)|
|evidence.zip|33255350|`c066de84e720df1384b7e9f569c495df75635c2d579e856474656da8a9bb7978`|[文件](https://drive.google.com/file/d/1seA1-mex_rmniaqc5dxF5aatPM2MeCAQ/view?usp=drivesdk)|
|normal.zip|25035388|`a9716b594fb1cce9a7954120c687457fe77aa1d69ac6972aca3ec3d4296078c0`|[文件](https://drive.google.com/file/d/1Pl-NHPyXv6gyBqHp76JHwjY4mKzpgBPI/view?usp=drivesdk)|
|package-manifest075.json|1275031|`e67fdcdf65e4bd84c62b3fb2ee8e19d8c489a64c43db229c1ce464da709979de`|[文件](https://drive.google.com/file/d/1_hUfeeaONRqVPQpga5nkckfXyFiEZwmO/view?usp=drivesdk)|
|source.manifest.json|143157|`0447f5ac71df5bcb1c5accb846033529ce658c2d6938ec7799fc879df8809517`|[文件](https://drive.google.com/file/d/1_HDiFOCYpLpv2SSYVUeJZG-AVZ4AimMb/view?usp=drivesdk)|
|actual-app-manifest075.json|204218|`b3945578e031991b29c376476dde33504b79771987cde9c6f29414bd25599f65`|[文件](https://drive.google.com/file/d/1fKJCmqOePxVII6TUj1uA9GHiwiUfmO0y/view?usp=drivesdk)|
|drive075-original-seven-verified.json|8240|`cf12fb1c0c0a27eca417378894f29046793ce21d0f36415eb5a0bda0cb2df965`|[文件](https://drive.google.com/file/d/1alWQ-dl9sOv661_myJOJ_8vyBxa5TFus/view?usp=drivesdk)|
|README-075.md|4340|`02f04d048e67af7517921edbe4cacb31b467715f38590e4d1730c8f7b5a38c07`|[文件](https://drive.google.com/file/d/1Bypo7EW2IyXSmmI0_EThQYJvD9XkHsbu/view?usp=drivesdk)|

源码使用既有Tools/package-source.py --evidence-version075：1148文件/49512452B，包含全部758运行输入与075证据，排除缓存、旧Docs/Evidence和Git历史；不称完整Git归档。Mac包319成员（298文件、21目录，实际app无symlink）；evidence293成员；normal完整25节点，不裁剪World/Simulation/JSONL/sample。四ZIP全部小于100MiB。

独立封包复核实际读取四ZIP共1785成员至EOF，CRC、无重复/路径逃逸/尾随字节、逐成员SHA/byte/Unix模式均通过；source全部Git blob和执行位对应67b13e77，实际app、Native1全树与普通观察全部原件匹配。独立报告SHA `963499f4eebd378f4f3951712df20334f5c374b0b6b01194c809fbaa880ab9e3`，见[独立审查](Evidence/075/Delivery/independent-package075-audit-001.json)。这些文件检查与1723 Unity用例分别统计。

[最终九文件收据](Evidence/075/Delivery/drive075-final-nine-verified.json)记录真实ID、名称、父目录、owner与全部字节/SHA；[七原件收据](Evidence/075/Delivery/drive075-original-seven-verified.json)本身也已上传并独立完整读回。最终九文件收据仅在包外保存，不递归声称上传了自身。封包后只添加包外记录，七原件与ZIP没有重建、覆盖或重传；源码ZIP内交付文档保持封包前快照，本页和云盘README记录后续实际结果。网络中断/连接超时后使用每次请求的只读IPv4/curl传输完成核验，未修改系统网络或全局配置。

两局普通AI默认时代I和资源、无注资/强制命令：荷兰4次付费使节出生，2400.09秒cap，无赢家；法国2次付费斥候出生并使用潜行，1907.38秒自然终局、P1获胜。原始事件与final-world匹配；聚合钱包重建有已记录浮点残差，不宣称逐tick恒等式精确闭合。

同GUID原生已实际付费训练两国新兵、荷兰移动生产者/集结/出兵、自动侦察、法国潜行及slot2新进程冷恢复/继续移动/解除潜行。15对F9与53原始文件身份/字节核对完成。荷兰slot2未落盘；原生录像仅保存未播放，FPS、自然完整人工比赛、双物理Mac未验收；有真实Quit/cleanup/PID消失，没有额外机器exitCode receipt。[内容](Content-075.md)、[验证](Verification-075.md)、[原生](Native-075.md)、[限制](Known-Issues.md)。

仅更新既有私有开发分支与[draft PR1](https://github.com/freezeng123456/unity-mimic-aoe/pull/1)，075交付head为`e411875c55ba57a2aac4a890aac0b8166e9988af`，已实际读回private/open/draft、精确标题正文且main不变，见[远端收据](Evidence/075/Delivery/github075-final-draft-readback-001.json)。后续076开发沿用该分支，不改变075封包身份。旧074及更早原件、GUID和结果保持原身份。治疗、皇家升级、完整国别制度/卡池、海战和性能仍继续推进，075交付不代表整体目标已完成。
