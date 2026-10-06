# 072 本地打包工具预备说明

这是 outside Diagnostics 的打包预备工具，不是正式验收回执。本轮没有封存最终源码，没有完整通过的最终回归、实际原生验收或已审计的 16 局矩阵，也没有生成正式交付 ZIP。模板的身份、路径、字节和 SHA 均保持空值，状态为 `pending_actual_acceptance`。

脚本只调用本地 Git 读取命令和已有的独立 Python 矩阵阅读器；不会启动 Unity、构建、GUI、账号工具、网络、上传或提交。`independent-normal072.py` 原件保持不变，允许的 SHA-256 固定为 `8579ec7a2bfdda5987c830a29bebfd94c1c0c17fc58b04af93f710c63661858c`。

## 已准备的工具与合成自测

```sh
python3 -B /Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.15.0/Candidate1/Diagnostics/package072-prepared.py --self-test
python3 -B /Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.15.0/Candidate1/Diagnostics/package072-prepared.py --template
```

自测只在 Diagnostics 下新建临时 fixture 目录，检查后删除这个目录。微型 ZIP 和手工构造的 Git blob/tree/commit 对象都属于合成输入，不使用真实项目、客户端或旧证据。这些检查不证明未来最终源码、构建、测试、原生或矩阵通过。首次输出保留在 `package072-selftest-prepared.json`；完成 descriptor 复核后的实际输出和工具 SHA 绑定另存 `package072-tool-selftest.json`。

## 071 profile 的延续

五个正式输出的预定名称为：`source-current-072.zip`、`client072.zip`、`evidence072.zip`、`gallery072.zip`、`normal-routes-originals072.zip`。成功后才写外置的 `package-manifest072.json`。

源码直接读取明确 Git 提交的所有跟踪路径及实际 blob，不使用当前工作树、HEAD、分支、标签或 `git archive` 属性替换。只省略 `Docs/Evidence/` 紧邻子目录中，三位 ASCII 数字且不属于 `070/071/072` 的旧批次及其后代。070、071、072、Benchmarks、Captures、证据根文件和所有其它跟踪路径均保留；源码快照必须确实包含三个保留批次。清单逐项记录选中与省略的跟踪路径、Git mode/object ID、选中 payload SHA/字节，以及完整 Git 树元数据 SHA。这个 profile 是运输子集，不是完整历史 Git 证据树归档。

所有 Assets/Packages/ProjectSettings/Tools 非隐藏、非 `__pycache__` 运行输入都重新匹配测试 source-evidence 的完整映射与最终指纹，并匹配 runtime commit 和 source snapshot commit。两次提交可以不同，但运行输入必须相同；Docs 的后续回执提交不会代替运行来源身份。源码 ZIP 使用显式 Git 文件/执行/link mode，目录规范为 0755；没有声称保持 071 原 ZIP 的隐式 DOS 属性或原时间戳。

## 最终前置条件

复制 `package072-seal-template.json` 到一个新的实际封存文件，填入真实原件引用。每个引用必须是 `{path, bytes, sha256}`，不得用预期值、正在运行的结果、草稿或补写的虚拟通过回执。模板本身不可直接用作正式 seal。

- 最终固定 `sourceSnapshotCommit`、`runtimeCommit`、完整 `sourceFingerprint`、`sourceInputs`、0.15.0 和真实 32 位 `buildGuid`。`sourceSealed=true`、`status=sealed`、`scope=local_only_no_upload` 只应在下面所有原件已完成后填写。
- `sourceFreeze`、`testsEvidence`：完整 source map，fingerprint 必须与 Git 运行输入一致。`testsEvidence` 应指向实际完整 Tools/test.sh 的 `source-evidence.json`。
- `fullTestsAudit`：实际 `audit-tests072.py` 产生的 `passed_frozen_full_regression` 原件；两个 XML 的根/实际 case/嵌套 suite 全部 Passed、没有 skipped/failed/inconclusive，数量与封存中的实际 `fullTestTotals` 相同。`testBaseline.EditMode/PlayMode` 引用原 071 完整 XML，必须匹配保留 Git 证据 blob；所有 baseline fullname 必须在新 XML 中，且 fullname 无重复。Tools/test.sh 不产生 focused runner，因此这里不要求虚构 full runner。
- `buildIdentity`、`buildRunner`、`buildSourceBefore/After`：实际成功且已退出的 frozen build。读取真实 build.log，核其 SHA 和 Succeeded；核客户端 exe、Frontier.Runtime.dll 的字节/SHA、Info.plist 版本和 boot.config 的实际 build-guid。
- `nativeJournal`：实际最终 journal，`status=observed`、`releaseAcceptance=accepted_with_documented_scope_and_limits`、`pending=[]`、`manualObservationStatus=observed_selected_native_scope`、原件前后不变。必需过程要有真实已退出的 process metadata；必需人工/GUI观察要有具体 capture ID 和描述。未完成的回放、未观察像素、计划步骤或进程启动不算验收。身份可取 journal 的明确顶层、identity、buildProvenance 字段，但凡重复字段均须一致。
- `matrixManifest`、`matrixAudit`、`matrixRunner`、`matrixSourceBefore/After`、`matrixDeclaration`：两张预声明地图、四文明、每图两个 seed 的完整 16 局，3600 秒上限/2400 秒对照不改变，已退出、source stable、原件齐全，独立 audit 为 `Consistent`。`Prepared` 不算 matrixExecuted。`observed_routes_incomplete` 可以作为如实完成的观察打包，保留 route reached 数量，不改写成全通过或平衡验收。

`matrixChecker` 引用保持原样的独立工具。由于该工具回执没有导出逐文件输入 hash，正式 `--check/--package` 会在已冻结原件上只读重跑这个固定 Python 审计器，并比较 source binding、16 个 case、达成数量、终局/对照状态和前后不变结果。这不是 Unity 重跑。矩阵原目录和项目运行输入必须仍能被该审计器读取；不要提前移动或删去原件。

原件 schema 若与以上实际 071/072 合同不同，脚本会拒绝。应保留原件，另行审查明确的 parser 适配；不能把原件字段改成脚本期待的成功字符串。

## 独立冻结四个完整树

只有输入都已退出且整理完毕，才对真实 app、072 Evidence、选定 Gallery、完整 NormalRoutes runner 目录生成独立 lstat inventory。四个树各用一个新文件和自己的 archive root，例如：

```sh
python3 -B /Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.15.0/Candidate1/Diagnostics/package072-prepared.py \
  --inventory '/absolute/Build/Frontier Reckoning.app' \
  --archive-root 'Frontier Reckoning.app' \
  --inventory-output /Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.15.0/Candidate1/Diagnostics/app072-final-inventory.json
```

对 Evidence、Gallery、NormalRoutes 重复，分别使用 `Evidence072`、`Gallery072`、`NormalRoutes072` archive root。输出只允许新建的 Diagnostics 文件，已存在文件不会覆盖。清单含根目录、全部子目录/空目录、每个普通文件和 symlink 的 type/mode/size/SHA/link text。审核这些确切清单后，再在 seal 的 `trees.*.approval` 中填入清单原件的精确字节/SHA；打包过程中不自行生成“批准清单”。Gallery 的完整树必须正好是选定 inventory，不能混入未选定的原件。

`normal.directory` 必须是原 NormalRoutes runner 根，包含 `matrix/manifest.json` 和全部 16 局原文，不能只放 result 摘要。`nativeOriginalDirectory` 指向已退出的真实 Native 根；`nativeEvidencePrefix` 默认为 `Native/Originals`。每个 journal.originalFiles 原件都必须在 Evidence tree 的该前缀下按相对路径原样保留，并核字节/SHA。根的打包前后完整 lstat 清单、所有文件 hash 和签名必须不变。

不要将 Library、Temp、Python cache、外部凭据或账号秘密加入输入树。脚本拒绝这些缓存目录与常见凭据/私钥文件名，但不会把文件名扫描当作完整秘密审计；最终选定材料的内容应随独立 inventory 一起审核。

## 未来正式运行方式

以下命令仅供全部真实条件闭合后的操作；本轮没有运行。

```sh
python3 -B /Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.15.0/Candidate1/Diagnostics/package072-prepared.py \
  --check \
  --seal /absolute/new-final-seal072.json \
  --seal-sha256 ACTUAL_REVIEWED_SEAL_SHA256

python3 -B /Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.15.0/Candidate1/Diagnostics/package072-prepared.py \
  --package \
  --seal /absolute/new-final-seal072.json \
  --seal-sha256 ACTUAL_REVIEWED_SEAL_SHA256 \
  --output-directory /absolute/new-delivery072
```

输出目录必须尚不存在，父目录必须已存在且真实，输出不能位于 repo/app/evidence/gallery/normal 输入树中。所有 ZIP 与最终 receipt 均排他创建。失败退出 2，并保留已有的部分输出供诊断，不写通过清单、不删除已有资料、不覆盖旧交付。

每个 ZIP 写后重新打开所有成员，核 CRC、SHA/字节、精确集合、Unix type/mode、内部 symlink graph、大小写/Unicode/path alias、local/central header/name/size/CRC 一致；拒绝 descriptor、加密、Unicode path 等额外拓展，仅允许 ZIP64。不会跟随 link target 或解压 ZIP。写完五包后再次核所有输入清单/hash，核固定 Git blob 集合，并重新读整个输出 ZIP SHA/字节，再写成功 receipt。

归档时间戳规范为 1980-01-01，不编码 ACL、xattrs、resource forks。打包校验不证明逐像素观察、原生输入、主机网络、两个物理 Mac、长期性能、签名/公证、商业完整性、外部上传或远端字节。它保存已有且真实闭合的证据及其局限，不增加新的游戏/平衡通过结论。
