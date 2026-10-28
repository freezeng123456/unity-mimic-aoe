# 交付验证工具

`python3 Tools/verify-delivery.py --reports-dir <目录>` 使用 Python 3 标准库执行只读检查。返回码 0 表示所列静态检查及测试报告通过；1 表示失败；2 表示存在未运行报告。没有源码证据的旧 XML 明确显示 `UNBOUND`，仅表示历史报告中的结果，不能视为当前源码验证。

当前版本验证使用 `bash Tools/test.sh`。入口会寻找工程锁定版本的 Unity 编辑器，也支持 `UNITY_EDITOR` 指定可执行文件；编辑器不存在时返回 2 并说明安装和许可证要求。每次运行创建独立的 `Builds/Tests/run-*` 目录，不复用前次 XML。EditMode 与 PlayMode 完成后，记录 `source-evidence.json`，再执行 `--require-current-source` 验证。成功后 `Builds/Tests/latest-run.txt` 指向本次目录；不传报告目录的验证命令会读取这个成功运行记录。

源码证据记录 Assets、Packages、ProjectSettings 和 Tools 每个文件的 SHA-256、整体指纹，以及两个 XML 的 SHA-256 和通过数。运行前后源码不同、XML 不完整或失败时不生成通过证据；后续源码或 XML 被替换时检查失败。Python 缓存、构建和日志不参与源码指纹。此清单用于匹配内容，不是签名认证，也不替代实际 Unity 测试。首次导入若改变项目设置或元数据，需核对改动后重新运行测试。

测试工具本身的回归可在没有 Unity 的环境运行：`python3 -m unittest discover -s Tools/tests -v`。它验证旧报告、报告替换、源码增删和运行期间编辑被正确拒绝，不能代替游戏的 EditMode / PlayMode。

工具读取 ProjectVersion、manifest 和 lock，检查直接依赖的实际解析版本一致性；只在 Assets 范围检查文件及文件夹对应的 .meta。它确认启用的真实 .unity 场景和 GameController 脚本 GUID 引用，并扫描源码/项目设置/工具中的开发者私人绝对路径。

测试部分解析最新对应 XML 的实际 `test-run`，要求 `result=Passed`、`passed=total>0`、`failed=0`，同时输出报告内的 UTC 时间。仅能解析 XML、文件存在、编辑器退出码为 0 都不足以显示 PASS。报告缺失显示 NOT RUN。

该工具不验证原生输入、声音、画面、卡顿或完整对局；这些内容在 Verification-Report.md 分别记录。源码压缩包不能携带 Library/Temp/Logs/obj 等再生成缓存；实际客户端必须单独验证 .app 和 Mach-O 架构。
