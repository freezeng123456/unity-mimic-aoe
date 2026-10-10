# 077 · 私有候选交付记录

2026-10-10。本轮0.20.0先精修Winding内外弯与沙色可读性，不将四部分河流路线全部列为完成。[内容](Content-077.md)、[验证](Verification-077.md)、[状态](Completion-Status.md)。

## 身份与门槛

- 最终784输入FP `8c858fe82932c1c4909af27aea8dab71a183812902c937ee4931003f33840335`，生产代码与Render2一致，仅新增材质读回测试使用逐RGBA 1e-6精度及R格式诊断。
- 原Full1实际1823通过／1失败、exit2与原始PNG/XML/日志保留；Bank2实际20/20，不将其替代最终完整回归。
- 最终Full2实际1372 Edit＋452 Play＝1824/1824、exit0／源稳定，原失败Full1保留。同源Build1实际exit0／GUID `6d3fd495bd8f48be852c1e6c62bcb02f`；普通原生七组F9、Move抵达／到点后Hold、26／16镜头、menu_exit与QUIT已独立限定验收。付费港区／编组攻击／FPS仍另验。

## 计划封包结构

三类逻辑ZIP：便携源码、完整实际macOS app、完整本轮证据。源码只包含077证据目录，历史Evidence保留在原仓库／旧私有包；README中的历史链接可能指向未纳入本轮便携包的旧证据。源码与app单文件严格小于100MiB。若完整证据ZIP达到100MiB，则完整原ZIP在本地保留，以严格小于64MiB的有序原字节分片上传；这些分片不是可分别解压的ZIP。

按 `transport-manifest077.json` 的 `orderedParts` 顺序拼接，核对该清单中的完整ZIP字节数／SHA后再解压。所有原始PNG保持字节，不通过重压、裁图或删旧失败记录来满足大小门槛。

`release-manifest077.json` 最后生成，记录最终源码快照、运行源FP、build GUID、实际最终FullN与三包／上传载荷；每包自己的成员清单记录全部文件SHA／字节／Unix模式。ZIP不承诺保留xattrs／ACL或macOS公证，Development arm64与Release分开。

## 当前交付状态

尚未创建本轮云盘目录、上传或推送本轮提交。后续仅使用原owner-only Google Drive项目下新0.20候选目录，旧C1／其他候选保持不变。云盘门槛为每个实际文件全字节读回／SHA／owner-only／父目录／完整子ID集合，以及有序远端分片与本地完整ZIP的逐字节一致。

Git仅更新既有私有 `codex/land-rts-completion` 与open draft PR1，main提交、仓库可见性和分享边界保持原值。封包后的云盘／PR回执在包外另记，不重建或回写已冻结ZIP。

## 不随交付扩大的结论

限定原生Move／Hold／镜头/F9不是付费码头、运输／医疗、指定攻击编组、完整人工局或FPS验收。外岸接水线缺口、港船后续轮廓和热带／支流／河口新精修仍在后续顺序。
