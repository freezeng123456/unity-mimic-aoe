# 交付验证工具

`python3 Tools/verify-delivery.py --reports-dir <目录>` 使用 Python 3 标准库执行只读检查。返回码 0 表示所列静态检查及测试报告通过；1 表示失败；2 表示存在未运行报告。

工具读取 ProjectVersion、manifest 和 lock，检查直接依赖的实际解析版本一致性；只在 Assets 范围检查文件及文件夹对应的 .meta。它确认启用的真实 .unity 场景和 GameController 脚本 GUID 引用，并扫描源码/项目设置/工具中的开发者私人绝对路径。

测试部分解析最新对应 XML 的实际 `test-run`，要求 `result=Passed`、`passed=total>0`、`failed=0`，同时输出报告内的 UTC 时间。仅能解析 XML、文件存在、编辑器退出码为 0 都不足以显示 PASS。报告缺失显示 NOT RUN。

该工具不验证原生输入、声音、画面、卡顿或完整对局；这些内容在 Verification-Report.md 分别记录。源码压缩包不能携带 Library/Temp/Logs/obj 等再生成缓存；实际客户端必须单独验证 .app 和 Mach-O 架构。
