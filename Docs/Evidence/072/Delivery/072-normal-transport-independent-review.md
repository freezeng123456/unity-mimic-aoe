# 072 自然对战传输分片独立复核

本地逐字节复核通过。三个分片按 001、002、003 顺序，完整对应原 ZIP 的连续字节段，没有额外头部、尾部或重新压缩。该结论不代表云盘上传、下载回读或发布验收。

- 原件：`/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.15.0/Candidate1/DeliveryFinal1/normal-routes-originals072.zip`
- 原件及拼接结果：209,058,503 B，SHA256 `036fde3891836a12f25a98f8af1bf3c0fa5974711580ffed72864b82d3c9c33a`。
- 独立检查：37 项通过，0 项失败；六个传输文件和原 ZIP 均为普通非符号链接文件，模式及文件身份在复核前后稳定。

| 分片 | 字节范围（左闭右开） | 字节数 | 独立 SHA256 |
| --- | --- | --- | --- |
| normal-routes-originals072.zip.part001 | 0–80000000 | 80,000,000 | `4aa0b88b5cbbe918de6025e92f10c7895df4d240070efd07997a542ab44caf34` |
| normal-routes-originals072.zip.part002 | 80000000–160000000 | 80,000,000 | `f53f4ff237a3c22315d01944682ad60adf6a25a2f032edfb819df90624f450b8` |
| normal-routes-originals072.zip.part003 | 160000000–209058503 | 49,058,503 | `82f739532ca60800658cc3b9d5403d832b8bfe500a1d8a148d083ec2a75dd8e8` |

合并脚本仅阅读并解析 AST，没有运行。文档所示 `python3 assemble-normal072.py` 在关闭优化的普通调用下，会先核对三段字节数和 SHA256，再逐字节拼接、核对固定原件字节数和 SHA256，源代码中还声明完整 ZIP CRC 检查。临时文件采用 `open('xb')`，最终用 `os.link(temp, target)` 发布；这两步均拒绝覆盖已有路径，后者也能挡住检查之后出现的目标文件。

**调用限制：** 校验语句全部使用 `assert`。`python -O` 或 `PYTHONOPTIMIZE` 会关闭校验；本次静态结论限定为文档中的普通调用。独占临时创建和硬链接防覆盖仍然生效。失败可能留下 `.assembling` 文件，下一次尝试将拒绝覆盖它。没有执行故障注入。

| 控制文件 | 字节数 | SHA256 |
| --- | --- | --- |
| normal072-transport-manifest.json | 1187 | `9886965934abd5e5e58b59f5ead5f52ebf5b305214e648bf1a4cee02be41aab8` |
| assemble-normal072.py | 1657 | `b6a311117ca8d535dc28cca15dc02ca082df0e30574d9a62142f5f6e9e950d20` |
| README-072-下载与对战记录合并.md | 1151 | `539281d7bc533d70e85586a5a13aa2e9052bee0b7311e8a614bc436ba0219b70` |

传输 manifest 的 `verified_exact_transport_of_unchanged_zip` 和两个布尔标记属于制作者声明；本报告用独立流式摘要、全部字节比较和前后身份记录另行证明本地一致性。连接器上限及其他 ZIP、矩阵和经济验收未在这里重跑。CRC 源码逻辑已阅读，既有独立 ZIP 审计未重复执行。

完整机器收据：`/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.15.0/Candidate1/Diagnostics/072-normal-transport-independent-review.json`。
生成时间：2026-10-06T14:37:05.764970+00:00。
