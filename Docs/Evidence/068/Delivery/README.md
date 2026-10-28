# 068 包外交付证据

本目录的README-zh及原Tools快照形成于源码包冻结前；以下清单、验包、云端和GitHub回执形成于三ZIP冻结后，属于包外资料，不通过重建ZIP补入。

[交付入口与全部范围](../../../Delivery-068.md)；[release-manifest](release-manifest.json)；[root打包读回](package-execution-readback.json)；[首次规则误拦截](independent-zip-verification.json)；[最终独立核验](independent-zip-verification-final.json)；[六载荷上传读回](cloud-upload-readback.json)；[第七回执外层读回](cloud-delivery-final.json)。GitHub推送前只读权限/范围检查保留在[prepush](github-private-scope-prepush.json)，实际[PR读回](github-delivery-readback.json)及[完整说明](pull-request-body.md)已保留；回执记录首轮交付head，最后包外文档提交另行最终只读核对。

独立核验修订只允许Native/Build的三份身份文件，继续禁止缓存、个人数据和完整app进入图集/源码；源码仍逐项绑定冻结Git全集。旧工具快照保持原字节，包外修订为[final verifier](Tools/independent-zip-verify-068-final.py)，SHA60fce3e3e70414313b03cfb0dfbf9a0ed2563dce9b2597dc4a58433de1ccdbf0。首次失败与最终Passed均保留。

云端证据只覆盖名称、大小、位置及owner-only权限；本地SHA不等于下载后远端内容核对。当前原生范围仍为同机两进程真实TCP，物理双机、冷LAN原生回放、自然终局与性能保持pending。运行630输入、原生GUID与已上传三ZIP均不变。
