# 078 / 0.21.0 · 私有交付读回

Google云盘实际新目录：[0.21.0 · 078 曲河接水边缘 · Candidate1](https://drive.google.com/drive/folders/1lAQVa2Gn6Wf302p9DvNW0txRdl48X6oM)。

- 23个计划文件逐个新鲜metadata及完整原字节下载，通过ID、owner-only、parent、MIME、长度与SHA校验。源码ZIP、macOS ZIP、三段证据字节分片、清单、README、checksums和五组原生PNG/JSON均在新目录。
- 远端三段证据重组逐字节等于本地完整143443677B ZIP，SHA `1745a899d7e6d5258bf4eed70f61a62b5300aa77534816bd95c3b0bd764ed85f`。分片不是独立可解压ZIP，按transport-manifest078.json顺序拼接。
- 原076C1与077目录的新鲜ID／owner／parent及直接子ID集合均与本次preflight一致；项目父目录只增加本次078目录。本轮未更改ACL、覆盖、删除、移动或重复上传，也未重新下载旧目录全部历史文件。
- 一次小JSON读回未通过本地关口；保留记录后，用同一已上传ID的新鲜文件引用，在独立目录重新通过完整字节校验，未重新上传。
- 三包独立审计通过。冻结portable源码快照 `e4608d94cc87f2f05fea001fdd1f195c865ae8f0`；实际测试／构建提交 `1914ee7d73d362b7f48833232e68ce8ff9ccca19`。运行时788输入FP `05dc944960c2724e08f2d6eeb02df282710d099ae36cf35bb7923d8918166217` 相同；打包后仅新增本交付文档／回执，不修改运行时代码。
- 本交付仍只是有界岸线修复、1835测试、实际开发构建和原生五视角；不扩张为付费港区、混编调度、FPS、完整人工胜局或四部分整体完成。

原生Native1部分会话与Native2已验收会话分别保留；失败粗探针也保留自身身份。源码和macOS ZIP分别SHA：

```
d28378abc9bc8f3dff6cb6043e9fb806e17851543c2b3ad1e8afad3dcc17a2d4  frontier-reckoning-0.21.0-source.zip
851ea382d4057e91b2d538077b8f402b5ff9340a2afec333c26da51eaaa1981f  frontier-reckoning-0.21.0-macos.zip
```

本文件与Docs/Evidence/078/Delivery1回执是打包后的记录，不属于冻结ZIP，也不宣称这些最终本地回执被额外上传。私有draft PR的分支与正文同步由单独API读回记账；未合并main或公开发布。
