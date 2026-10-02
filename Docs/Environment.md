# 实际环境与版本

2026-10-02 本轮实际环境：MacBook Pro（Mac15,7），Apple M3 Pro，12 CPU 核、18 GPU 核、36 GB RAM，macOS 27.0（build 26A428），内屏 3456×2234。客户端用 Metal 渲染。内屏物理分辨率与客户端窗口渲染分辨率分别记录；本轮仍在进行的性能测试尚未作为结果列入此文件。

工程当前固定 Unity **6000.6.3f1**，revision **45d8eee7de74**，与 `ProjectSettings/ProjectVersion.txt` 一致。[官方发布页](https://unity.com/releases/editor/whats-new/6000.6.3f1)记载该版本于 2026-09-24 发布。本轮已实际导入、编译并完成 macOS arm64 构建。`Tools/unity-editor.sh` 严格核对编辑器实际 `-version` 输出与工程固定版本；即使设置 `UNITY_EDITOR`，版本不符也会拒绝运行构建和测试。兼容性依据真实执行结果，不由安装状态单独推出。

直接依赖保持不变：URP **17.6.0**、Input System **1.20.0**、UGUI **2.6.0**、Test Framework **1.8.0**，manifest 与 packages-lock 对应。UGUI 的早期请求 2.0.0 被编辑器解析到内置 2.6.0，manifest 已修正为实际值；旧文档中的 2.0.0 不是交付版本。本轮编辑器升级后，间接依赖 Searcher 从 **4.9.3** 解析为 **4.9.5**，实际版本由 packages-lock 保存。

当前客户端 **0.2.30** 为 macOS Apple silicon / Development / Mono，Mach-O arm64，buildGuid 为 `21261224be4f40e692a3629fc4cc42fe`。默认窗口渲染 **2560×1440**，60 FPS 上限、vSync 关闭；不是 4K60 承诺。没有交付未构建的 Windows、Linux 或 Intel Mac 文件。当前包没有跨机器安装、签名和公证验收。

本轮构建后完整测试实际通过 **194/194 EditMode**、**181/181 PlayMode**，对应源码指纹 `5a64b4e474570909a98ab787ae6b522294cde5b399629ea909d9980b07846eb4`。这是 6000.6.3f1 / 0.2.30 的本轮结果；旧测试 XML、旧构建和旧性能报告仍需按各自版本、硬件及报告分辨率读取。

历史环境保留：2026-09-14 核对的旧机器为 MacBook Pro，Apple M4 Pro，14 CPU 核、20 GPU 核、48 GB RAM，macOS 26.5，内屏 3456×2234。升级前工程固定 Unity **6000.6.0f1**，revision **f7f8ed4d1e24**（[旧版官方发布页](https://unity.com/cn/releases/editor/whats-new/6000.6.0f1)），仓库客户端版本为 **0.2.29**。旧机记录及其性能数据不代表本轮 M3 Pro 环境。

历史人物制作环境：0.2.4 原创人物源网格制作曾验证 Blender **5.2.1 LTS**，构建日期 2026-08-25；本轮未重新验证该工具。源文件存放于 `SourceAssets`，运行客户端和打开 Unity 已导入 FBX 均不依赖 Blender；只有重新生成源网格才需要该工具。`Tools/build-character.sh` 支持 `BLENDER_EDITOR` 和相对输出目录。
