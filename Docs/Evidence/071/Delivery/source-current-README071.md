# Frontier Reckoning 0.14.0 当前源码与 070/071 证据工作集

本包是当前开发源码与最近两批证据的交付工作集，并非完整 Git 全树归档。冻结源码提交为 `47d7927d1cd7db7e9f96e876b779bdefa2ed9d6d`；ZIP 顶层仍为 `source/`，ZIP comment 也保留该提交号。

唯一筛选规则是排除 `Docs/Evidence/` 下除 `070/`、`071/` 之外的 38 个三位数字批次目录及其子项。`070/`、`071/`、`Benchmarks/`、`Captures/`、Evidence 根级 18 个普通文件、全部其他跟踪文件和目录均保留。没有按大小再删任何内容。

- 新包：`source-current-071.zip`
- ZIP 字节数：78,832,824 B（78.833 MB），低于 120 MB 传输阈值。
- SHA-256：`dbb757bacf300ba8f0b1da75536fbcd8ef98b54784750071d26870e79f55967c`
- ZIP 成员：1,473，包括 1,352 个跟踪文件及 121 个目录条目；symlink 0 个。
- 解压 payload：120,429,817 B。
- 本运输工作集排除：3,635 个跟踪文件、4,032 个 ZIP 成员。

已独立读取实际 Git tree 及所有 1,352 个被选中 blob，并对照原 ZIP 内容、blob object ID、SHA-256、字节数及 Git mode。新 ZIP 完成后全部成员再次完整读取，通过 ZIP CRC、SHA-256、字节数、目录/文件类型、原始 ZIP 模式字段及路径核对；无重复、漏项或额外成员。文件和目录的原 Git archive ZIP 模式字段为隐式值，原值原样保留；实际 Git `100644`/`100755` 模式另在清单列明。本提交没有 symlink；打包过程不读取工作树文件或 symlink 的目标。

704 个冻结运行输入均与实际 Git blob 及新 ZIP 内容一致，来源收据为 `source/Docs/Evidence/071/Tests/source-evidence.json`。重新计算的 source fingerprint 为 `098e5c97eb8e1a3f5cff1e0dd7db30a7fe761fc8f480c6b8517811fab5d8795a`，输入集合无遗漏也无额外项；运行源码提交为 `3aeb0cb4b5f63ecd18553254583726b88cef9079`。

`source-current-manifest071.json` 包含完整的 selected/excluded tracked path 清单（Git object ID 与 mode）、所有 ZIP 成员核对结果、38 个明确排除目录及 704 个运行输入清单。该清单本身不在源码 ZIP 内，避免伪装成冻结 Git 树文件。

完整本地原归档 `source.zip` 保持不变：995,895,490 B、5,505 个成员、4,987 个 Git 跟踪文件，SHA-256 `f0cd64486ecb7563b4615a1ab76db0d49f8fbf1e1e336bbba318eb64ce22aece`。原归档及原 `package-manifest.json` 已在本次操作前后重新 SHA-256 验证。旧数字批次证据仍保留在该完整原包、冻结的私有 Git 提交/历史及既有私有云盘历史中；本次没有删除或修改那些原件，也没有重新核验旧云盘远端内容。

验收范围仅为本地交付包的实际字节、Git 来源与运行输入完整性。本包不新增原生玩法、视觉、完整《帝国时代 III》内容或远端上传下载的验收声明。ACL、xattrs 和 resource forks 不在本次保留声明内；ZIP 压缩表示及 ZIP64 局部字段可以与原包不同。交付上传由主代理另行处理。
