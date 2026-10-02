# 0.2.32真实客户端完整清单生成

[`build_client_bundle_manifest.py`](build_client_bundle_manifest.py) 是只读CLI准备工具，按055现有client-bundle-manifest schema生成**新的056清单**。它不构建游戏、不创建ZIP、不生成假客户端、不覆盖任何已有输出。055的.31清单、GUID及原生玩家/音频/性能记录始终保留原身份。

未来root完成原定B/C/D、只应用已批准两文件菜单/版本delta，实际跑fresh214 EditMode+210 PlayMode、BuildMac、新菜单/开局/退出smoke，写独立056 build-receipt并制作真实客户端ZIP后，再执行：

```bash
FR_PROJECT='/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project'
python3 "$FR_PROJECT/Docs/Evidence/056/build_client_bundle_manifest.py" \
  --app "$FR_PROJECT/Builds/macOS/Frontier Reckoning.app" \
  --client '/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.2.32/Frontier-Reckoning-0.2.32-macOS-arm64.zip' \
  --receipt "$FR_PROJECT/Docs/Evidence/056/build-receipt.json" \
  --source-evidence "$FR_PROJECT/Docs/Evidence/056/Tests/source-evidence.json" \
  --output "$FR_PROJECT/Docs/Evidence/056/client-bundle-manifest.json"
```

所有五个参数必须显式给定。输出必须是056内尚不存在的JSON，拒绝055路径及覆盖；receipt/source-evidence必须来自独立056。文件缺失/不匹配时失败，不借用.31产物凑齐。

检查包括：当前完整357源文件和214+210通过XML/源证据/receipt指纹一致；真实local.app全部文件集合、bytes、SHA与Unix mode等于ZIP对应集合；全ZIP含AppleDouble每笔SHA/mode、唯一安全路径、CRC；本地Info.plist版本0.2.32；独立boot.config GUID非零、仅一笔、与receipt一致且不同.31；receipt的Info.plist、启动程序、Frontier.Runtime.dll三项SHA均匹配。读取过程中检测源、HEAD、客户端、receipt、证据与工具漂移；HEAD只记录观察值，最终committed源码ZIP与HEAD对应关系仍由package_delivery记录。

输出保留`buildReceiptSnapshotUtf8`原始UTF8字节文本及`sources`每项路径/bytes/SHA，包括客户端、receipt、测试源证据及所用工具；后续receipt仅更新验收文字时可凭快照回查，核心版本/GUID/源码/compiled-runtime身份不能改变。

生成前直接调用现有 `Docs/Evidence/055/package_delivery.py:verify_client_archive` 对**同一实际ZIP与待写manifest**再完整复核，结果保存为`packageArchiveVerification`；不修改driver或重写另一套包装验收。最终发布还需root以`--evidence-set 056`执行原driver，先提交全部源码/Docs、按最终HEAD生成源码ZIP与总manifest。这一工具不替代此前[两文件等价核验](README.md)、原生smoke或正常游戏证据。

当前仅做语法/CLI检查，并对**现存真实0.2.31**的299个app文件、618个ZIP文件执行只读文件/Unixmode/CRC与原package verifier复查，通过；见[bundle-manifest-tool-check.json](bundle-manifest-tool-check.json)。这不是.32客户端/manifest验收，当前没有生成.32清单，也没有启动GUI/Unity/客户端。未新增跨机器签名、公证或全400单位无遮挡门槛。
