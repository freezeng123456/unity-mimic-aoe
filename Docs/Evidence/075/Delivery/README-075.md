# 烽岸纪元 0.18.0 · 075 私有试玩交付

本目录位于原Frontier Reckoning私有项目下，七个封存原件共168805570B已实际完整读回，bytes/SHA与本地一致；文件元数据均shared=false、仅原账号owner。没有修改共享权限。

运行代码51bd61ff1abc92f224dd449784671234a20a5555，758输入，FP a3e1e3518a23d000f6ce1e80bef628f72bd55c631fcb9ddcd6e9f2be80066594。源码/文档快照67b13e77d47ccdf648a5e6113a43b2a5eb50bb0a。客户端macOS Apple silicon arm64 Development/Mono、Unity6000.6.3f1，版本0.18.0，GUID67594b51e2dd40f593ec8e956f6f4960。

## 下载

|原件|bytes|SHA256|链接|
|---|---:|---|---|
|source.zip|32909939|`055e2179f5baa88d3842a60c06f56afa3e3107cbd9f7df0c033acfcff0baf2df`|[文件](https://drive.google.com/file/d/1bErm0DyBZum6YLEqbLH6XzoVTtH9YF_y/view?usp=drivesdk)|
|macOS.zip|75982487|`6585afda9f86735f54383e0e4c83243ea269623856de792d315a3360e9d7c6f2`|[文件](https://drive.google.com/file/d/1xRAqY466hjxylwJjnx5IhFfRR5Kel6Vq/view?usp=drivesdk)|
|evidence.zip|33255350|`c066de84e720df1384b7e9f569c495df75635c2d579e856474656da8a9bb7978`|[文件](https://drive.google.com/file/d/1seA1-mex_rmniaqc5dxF5aatPM2MeCAQ/view?usp=drivesdk)|
|normal.zip|25035388|`a9716b594fb1cce9a7954120c687457fe77aa1d69ac6972aca3ec3d4296078c0`|[文件](https://drive.google.com/file/d/1Pl-NHPyXv6gyBqHp76JHwjY4mKzpgBPI/view?usp=drivesdk)|
|package-manifest075.json|1275031|`e67fdcdf65e4bd84c62b3fb2ee8e19d8c489a64c43db229c1ce464da709979de`|[文件](https://drive.google.com/file/d/1_hUfeeaONRqVPQpga5nkckfXyFiEZwmO/view?usp=drivesdk)|
|source.manifest.json|143157|`0447f5ac71df5bcb1c5accb846033529ce658c2d6938ec7799fc879df8809517`|[文件](https://drive.google.com/file/d/1_HDiFOCYpLpv2SSYVUeJZG-AVZ4AimMb/view?usp=drivesdk)|
|actual-app-manifest075.json|204218|`b3945578e031991b29c376476dde33504b79771987cde9c6f29414bd25599f65`|[文件](https://drive.google.com/file/d/1fKJCmqOePxVII6TUj1uA9GHiwiUfmO0y/view?usp=drivesdk)|

- macOS.zip：解压后打开Frontier Reckoning.app，主菜单选择荷兰或法国，普通开局体验新兵。Development构建，尚未签名公证Release。
- source.zip：1148文件，使用既有源码allowlist，仅包含075证据；不含Unity缓存、旧证据或Git历史。用Unity6000.6.3f1打开FrontierReckoning目录。
- evidence.zip：293成员，含原始专门/全套测试、失败记录、构建与Native1，保留完整原件及声明。
- normal.zip：完整NormalObserved1的25节点，包括两局所有World/Simulation/JSONL/sample。

## 本轮实际结果

075加入荷兰使节Envoy、法国Native Scout、自动侦察、潜行/反隐及原创造型动作；没有开局赠兵。专门85项、正式1291Edit+432Play=1723项全部通过，源指纹稳定；Build1实际Succeeded/exit0/sourceStable。

荷兰普通AI镜像局实际付费产生4名使节，2400.09秒达到观察上限，无赢家；法国局实际付费产生2名斥候并使用潜行，1907.38秒自然终局、P1获胜。聚合钱包重建的浮点残差如实记录，不声称逐tick经济恒等式精确复原。

同GUID原生已实际操作两国付费训练、荷兰移动生产者/集结点/出兵、自动侦察、法国潜行及slot2新进程冷恢复/继续移动/解除潜行。15对F9及53原始文件完成身份/字节核对。荷兰slot2未落盘；原生录像仅保存、未播放，FPS、完整人工比赛及双物理Mac未验收。原生进程已退出，有quit/cleanup记录，没有额外机器exitCode receipt。

独立封包审核对四ZIP共1785成员实际核CRC/EOF/路径/全部SHA/字节/Unix模式，以及源Git blob、实际app和全量普通/原生证据，无失败；它与1723个Unity用例分别统计。独立报告SHA963499f4eebd378f4f3951712df20334f5c374b0b6b01194c809fbaa880ab9e3，后续包外Git记录附原件。

## 冻结与包外记录

七原件与ZIP均不重建。ZIP内交付文档保留封包前状态；本README和drive075-original-seven-verified.json是后续包外说明。两者上传后另做完整读回，最终九文件收据仅保存在包外Git/本地，不递归声称上传了自身。

既有私有开发分支/draft PR1后续更新；main不合并，不公开发行。治疗、皇家升级、完整国家制度/卡池、海战与性能仍继续迭代，075不代表整个AoE3内容差距已消除。
