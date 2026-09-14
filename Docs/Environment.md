# 实际环境与版本

2026-09-14 在本机核对：MacBook Pro，Apple M4 Pro，14 CPU 核、20 GPU 核、48 GB RAM，macOS 26.5，内屏 3456×2234。客户端用 Metal 渲染。性能记录只代表此机器、此构建及报告中所列分辨率。

工程固定 Unity **6000.6.0f1**，revision **f7f8ed4d1e24**。[官方发布页](https://unity.com/cn/releases/editor/whats-new/6000.6.0f1)确认正式发布，不标为 LTS。选择本机已安装、能实际导入和构建的版本。兼容性依据本工程真实编译、EditMode/PlayMode、macOS arm64 构建和客户端运行，不由安装状态单独推出。

URP **17.6.0**、Input System **1.20.0**、UGUI **2.6.0**、Test Framework **1.8.0**。manifest 和 packages-lock 对应。UGUI 的早期请求 2.0.0 被编辑器解析到内置 2.6.0，现已把 manifest 修正为实际值；旧文档中的 2.0.0 不是交付版本。所有间接版本由 lock 保存。

实际客户端为 macOS Apple silicon / Development / Mono，Mach-O arm64。默认窗口渲染 2560×1440，60 FPS 上限、vSync 关闭；不是 4K60 承诺。没有交付未构建的 Windows、Linux 或 Intel Mac 文件。当前包没有跨机器安装、签名和公证验收。

0.2.4 原创人物制作工具为本机已验证 Blender **5.2.1 LTS**，构建日期2026-08-25。源文件存放于SourceAssets，运行客户端和打开Unity已导入FBX均不依赖Blender；只有重新生成源网格才需要该工具。Tools/build-character.sh支持BLENDER_EDITOR和相对输出目录。
