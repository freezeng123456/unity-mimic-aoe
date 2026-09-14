# 037 source review

只读静态核验完成。当前 `RtsInput.cs` 相对 `work/verification/037/before/RtsInput.cs` 的差异限于双击候选位置与 8 input pixel 距离判定，以及候选在 UI、右键、框选、小地图、键盘、目标模式、设备 reset、focus/pause/modal、选择、编队和世界命令路径的清除；未见 gather、attack、movement、AI 或 Rules 数值改动。

冻结 `DoubleClickSelectionTests.cs` 当前 SHA-256 为 `e42a72182b6e307bd596ae6d4dbba340809c0cad64e07b77fa9267deeb0488d5`；其 `.meta` 与 `RtsInput.cs.meta` 均存在。当前 ProjectSettings `bundleVersion` 为 **0.2.16**。manifest 与 packages-lock 当前 SHA 已记录于同名 JSON，锁定 Input System 为 **1.20.0**；检查未运行 Unity，因此不把绿色结果或原生硬件行为升级为已验证事实。

由于 `work/verification/037/before` 中没有可用的 manifest 快照，本次不能声称与前一版本 manifest 字节级未变；只能确认当前锁定版本和当前文件哈希。最终发布前应由 root 用已保存的 037 构建输入/manifest 记录补做该比较。
