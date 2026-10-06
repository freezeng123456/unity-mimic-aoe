# 073 本地封包 · 独立只读审计

Status: passed_independent_local_archive_integrity。25319项核对、0项失败；root实际shell68608 exit0后读取。

Runtime `519d7d34a0be85f75ee19e53458c636d1ba0b3e4`；SourceSnapshot `0f656d39c6715a27a6d85414dac7c16c3c7645bd`；732运行SHA等Git blob/ZIP/source-map/freeze，FP `9b92812a922adb69a5ae501925417d8eba8e24b836f496274ebc8ec8679ba03d`。正式XML实际1192Edit+419Play=1611，与封包来源一致。

| ZIP | 字节 | 全成员 | 文件/目录/链接 |
|---|---:|---:|---|
| source.zip | 16296933 | 1049 | 1008/41/0 |
| macOS.zip | 75955219 | 319 | 298/21/0 |
| normal.zip | 11710868 | 33 | 28/5/0 |
| evidence.zip | 4945039 | 77 | 62/15/0 |

四ZIP共108908059B/1478成员，各低于100MiB。逐成员从原件或新读取Git blob生成预期，独立解压读全bytes核CRC/SHA/Unix type+mode、名字/EOF/成员集；app main executable flag存在，版本0.16.0/GUID一致。七个包对象前后SHA/identity稳定。

source.zip明确all-tracked-except-Docs-Evidence profile：1008个selected、4254个excluded（Git共5262），选中23985861B，排除3420117514B。每个选中对象按Git内容地址独立验证，再与ZIP SHA/mode一致；全部4254排除metadata逐项核对。没有读取旧072排除blob或称完整Git tree archive。

normal.zip完整28个实际文件/33成员，全开局/1500/final world+simulation、events/samples及Dutch scenario、真实runner/log/manifest保留；两Ott自然终局、Dutch2400cap状态与归档终态一致。此前逐笔市场/军队审计已封存，本检查不重跑模拟。

evidence.zip完整62文件/77成员；61复制原件的SHA/bytes/mode吻合，copy-index本身一起与SourceSnapshot Docs/Evidence/073的62 Git blob逐项核对。历史focused失败、checker初次schema假设错误和native partial均保留。

原生只限市场137建造100W付款/完成与3F9，零MarketTrade；native兑换、当前GUID cold/replay未验证。普通局与Native共享CPU，max慢步及scope保留，不认证FPS/全平衡/隔离因果性能。这里只验收本地封包，云盘字节/权限和GitHub交付由root另核；ACL/xattrs/resource fork/notarization不表示。

交叉核对：实际package073 --verify进程15284 exit0、status independently_verified，manifest SHA `3d9a6c4d5e1ab24daa26f686d0d782c010e59c9f5b40749c0b80c0579d6ac714`与独立读取一致；四ZIP SHA/数量相同。另一只读子审2410项、0失败，独立确认62证据Git副本/61原件及28普通局原件；这些审计数不增加正式1611测试。本组新审计现已封存。

本审计只写这组新JSON/MD，不改已sealed旧审计、repo、原包或原件，不执行app/Unity/测试/模拟/旧072整包检查、提交或上传。详细ZIP成员SHA、所有排除路径metadata及证据Git引用见[independent073-package-audit.json](independent073-package-audit.json)。
