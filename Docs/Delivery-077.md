# 077 · 私有候选交付记录

2026-10-10。本轮0.20.0先精修Winding内外弯与沙色可读性，不将四部分河流路线全部列为完成。[内容](Content-077.md)、[验证](Verification-077.md)、[状态](Completion-Status.md)。

## 身份与门槛

- 最终784输入FP `8c858fe82932c1c4909af27aea8dab71a183812902c937ee4931003f33840335`，生产代码与Render2一致，仅新增材质读回测试使用逐RGBA 1e-6精度及R格式诊断。
- 原Full1实际1823通过／1失败、exit2与原始PNG/XML/日志保留；Bank2实际20/20，不将其替代最终完整回归。
- 最终Full2实际1372 Edit＋452 Play＝1824/1824、exit0／源稳定，原失败Full1保留。同源Build1实际exit0／GUID `6d3fd495bd8f48be852c1e6c62bcb02f`；普通原生七组F9、Move抵达／到点后Hold、26／16镜头、menu_exit与QUIT已独立限定验收。付费港区／编组攻击／FPS仍另验。

## 冻结封包与独立验收

实际冻结源码快照 `a6a50c3c8827c248762eb5fde292dd7a9cc8cb31`，运行／测试／构建源码提交仍为 `a74fc436dc08c50f9d23dc506df92f7ff2942d76`，最终784输入FP不变。源包中的交付文档是上传前快照；下述包外回执与后补文档不回写冻结包。

| 逻辑ZIP | 字节 | SHA256 |
|---|---:|---|
| frontier-reckoning-0.20.0-source.zip | 36387734 | `f929733a6ab1de92a37c8a921a8907c4ebdade8d31fa203f2e35b6d6e7e28061` |
| frontier-reckoning-0.20.0-macos.zip | 75998128 | `d0d289c2594fdc548b473657e2e62e1364e05615e90e041fa29b02898496cb04` |
| frontier-reckoning-0.20.0-evidence077.zip | 181144431 | `f2b1d7cfcdfabd0fefe4fcf10ad3e60776065d50497dc2f950af198bd6635664` |

独立封包审查2实际PASS：三包2174成员的完整CRC／EOF／SHA／Unix模式、Git blob／源码FP／实际app清单均匹配；1849份输入原件再哈希未变。JSON SHA `f478c05990dc0501be31f48d6a47b019271bb06254bbe5063504ccc43acca9f0`。最初封包预检发现两份选定原生日志被Git忽略，尚未创建输出；仅强制纳入原字节后另提交。首次独立审计误把明确允许的顶层Delivery-077文档列为禁入，原FAIL保留；只修正外部审计器并另开审查2，三包始终未改。

三类逻辑ZIP：便携源码、完整实际macOS app、完整本轮证据。源码只包含077证据目录，历史Evidence保留在原仓库／旧私有包；README中的历史链接可能指向未纳入本轮便携包的旧证据。源码与app单文件严格小于100MiB。若完整证据ZIP达到100MiB，则完整原ZIP在本地保留，以严格小于64MiB的有序原字节分片上传；这些分片不是可分别解压的ZIP。

按 `transport-manifest077.json` 的 `orderedParts` 顺序拼接，核对该清单中的完整ZIP字节数／SHA后再解压。所有原始PNG保持字节，不通过重压、裁图或删旧失败记录来满足大小门槛。

`release-manifest077.json` 最后生成，记录最终源码快照、运行源FP、build GUID、实际最终FullN与三包／上传载荷；每包自己的成员清单记录全部文件SHA／字节／Unix模式。ZIP不承诺保留xattrs／ACL或macOS公证，Development arm64与Release分开。

实际证据分片为67108863／67108863／46926705B，均严格小于64MiB。三片完整拼接与181144431B原ZIP逐字节一致，source／app包各严格小于100MiB；所有60张URP原PNG与七张原生PNG均在完整证据包保留。

## 实际私有云盘交付

新建[0.20.0／077私有Candidate1目录](https://drive.google.com/drive/folders/19LXjSvkWNkk8KbRMSKqLR1K0c3G0RLZt)，仍在原Google Drive项目内。17文件／297226713B已实际全字节下载并核对SHA，owner-only／父目录／文件名与完整子ID集合匹配。远端三片按清单顺序重组与完整原ZIP逐字节一致；旧076 C1十二个子ID及项目所有旧候选目录保持不变。

最终本地回执 `drive077-final-verified.json` 为35484B／SHA `a4829475b9fed15a108a6e19041e6fad79e515f8d2b66c8e0caef2b15d24c7a4`。它在冻结包及云端17文件集合之外，不称作已经上传。

另一次独立只读审查PASS：重新逐字节比较十七份实际下载／原件，并重算全部SHA和三片拼接，复核root最终实际元数据账本的权限／父目录／精确ID集合、C1十二ID与项目15→16。该审查未另发云端请求；JSON42937B／SHA `dd20493015a77a2942bb0ef73234243e7a0a1cb15496132eb7612aa3b1ef27e2`，MD5833B／SHA `a6526fd9b19519769fe501cfc056bb244f3fc7371d2f6f451ca4f17e4f116ea0`。

实际连接器差异单独限定：legacy search只返回C1三个文本文件，换用严格少于100条、wrapper身份核对的folder fetch才得到完整十二ID；原三ID预检记录保留但未作为最终验收。原读取器两次未识别file_uri字典的download_url，在HTTP前停止，另版仅支持实际返回字段；artifact file_id不冒充DriveID。首片被服务端识别为application/x-zip，只对清单首片／本地PK头／原SHA接受该MIME；后两片与checksums的连接器暂存名分别追加.bin／.txt，仅接受对应声明对象，Drive真实文件名仍精确不变。首片已返回ID后通过完整SHA才纳入状态，全部文件均只上传一次，无盲目重传／ACL修改。原读取失败与外部helper各版保留。

## 私有开发分支与草稿PR

已非强制推送冻结快照到既有私有 `codex/land-rts-completion`；[open draft PR1](https://github.com/freezeng123456/unity-mimic-aoe/pull/1)保留全部原076正文再追加077的实际范围、云盘与剩余门槛，标题／精确正文／HEAD已GET读回。main保持 `0bb044adbe40d0cab952e8c3b44e40eca8a6d3fe`，仓库private、PR open／draft、分享边界均未变。后补本文件及交付回执属于docs-only提交，最末HEAD另在包外核验，不重建冻结ZIP。

交付审计／云盘／首轮Git回执的原件及复制哈希索引在 `Docs/Evidence/077/Delivery/`。最终docs-only HEAD的API回执留在外部Candidate1，避免为归档自身无限追加提交。

## 不随交付扩大的结论

限定原生Move／Hold／镜头/F9不是付费码头、运输／医疗、指定攻击编组、完整人工局或FPS验收。外岸接水线缺口、港船后续轮廓和热带／支流／河口新精修仍在后续顺序。
