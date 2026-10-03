# 059：指定目标与机动验证

详细口径与结果见 [Maneuver-059](../../Maneuver-059.md)。

- `Baseline`：0.3.1 旧导航，一次合成机动基线。
- `Clearance-candidate`：仅静态通行/连通缓存，源码版本当时仍为 0.3.1。
- `Rejected-smoothing`、`Rejected-tests`：随后尝试的路径简化及真实回归失败；已撤回，不能当作最终通过结果。
- `Input-initial`：3 个新输入用例最初通过记录；最终全量结果在 `Tests`。
- `Final`：最终 0.3.2 的 8 个机动场景、1088 个逐单位记录、完整 Tick CSV。源码指纹与 `Tests` 一致。
- `Tests`：287 EditMode + 246 PlayMode；`source-evidence.json` 绑定最终源码。
- `comparison.json`：基线与最终逐单位记录全等；CPU 数据为单次样本，不保证其他硬件或渲染帧率。
- `Native`：新 GUID 的真实客户端短时压力报告、原图元数据、退出日志；独立于普通对局和合成窄口。
- `build-receipt.json`：最终 GUID、版本、构建与运行库哈希；客户端 ZIP 全清单另存。

基线的运行时代码来自 `8fd420e353c641ed1cf5285fa06937d97850416c`，新增测量脚本未修改运行时；最终代码提交为 `75b39b0`。旧六文明矩阵、原生整局和三张展示图继续保留在 058；没有把它们重新标作本版证据。
