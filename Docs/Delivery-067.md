# 0.10.0 交付与核验（067）

源码、macOS arm64客户端、原生图集三个最终ZIP已冻结，本地身份和全部成员字节/权限/链接核验通过；已上传Google Drive并读回名称、大小、父目录、shared=false和仅所有者权限。清单、指南、独立验包回执和上传读回回执也已上传，共七个私有对象；元数据均读回通过。GitHub私有开发分支与原草稿PR同步待本次完成。

下载入口：[私有0.10交付目录](https://drive.google.com/drive/folders/1fS0we1PNCNuHOucbVrUXnzdgvuygSUZ1)。下列SHA和bytes属于本地冻结ZIP；没有远端SHA或下载字节对照证明。

| 包 | 本地bytes | 本地SHA-256 |
| --- | --- | --- |
| [源码](https://drive.google.com/file/d/1PpUW_k1vO7lYNuPpHSy1HldQx3k1Ngbb/view?usp=drivesdk) | 76236751 | `b3672da86bf9fff43614a7f5256880ca828ca1b4380d687b65f185030e5db9fb` |
| [macOS arm64客户端](https://drive.google.com/file/d/1FNM67io-ZTunn3_HTnbjjTrbda6HiYwU/view?usp=drivesdk) | 75833081 | `adf0e9ef0cdb12199d6e8a7400f57cc3ce10eeacb5d0ff9a0364852396a82482` |
| [原生图集](https://drive.google.com/file/d/11Nw20wI2sDPjl4ATMkhasfrLqvunu0k1/view?usp=drivesdk) | 18526668 | `c86299fb382969f5b42819198649fb049202ee294086328b2b908b5f5d8eb5c0` |

## 身份和包外文档

- 源码包快照提交：`ccc7fcb15a608269a1a63c665ff8e4653ca0546d`。
- 运行提交：`2668c47a426f2cea8d4bc7b309cdbfa71533cc9d`。
- 595运行输入FP：`8180b02b55ed3957b94042d779a6d74bdda78cd46073cf70d93353c330363466`。
- 原生GUID：`2d94fdd6d9734b259f5231c1fd3f2b99`，Unity6000.6.3f1，macOS arm64 Development/Mono。

[Completion-Status-067](Completion-Status-067.md)保留分包前验收状态，本页和[Delivery证据](Evidence/067/Delivery/README.md)属于源码ZIP冻结后的包外文档。三ZIP上传后不再重建，后续文档提交不改变其哈希。

## 核验与实际范围

[release-manifest](Evidence/067/Delivery/release-manifest.json)绑定946源码成员、378客户端成员、62图集成员及三个完整ZIPSHA。[验包回执](Evidence/067/Delivery/package-contents-verification.json)四部分Passed：source/tests/build identity、Native pairs/replay container、every ZIP member bytes/modes/links、outside manifest hashes。客户端可执行arm64、GUID、场景源SHA、1097测试与595输入均匹配。该核验不重新运行游戏或扩大原生验收。

[正式测试](Evidence/067/Tests/README.md)为740+357、1097/1097Passed。[17组原生证据](Evidence/067/Native/README.md)包含真实手动SAVE、同一9:22手动片段新进程完整前向Verified、成功Train/拒绝Stop、四观察槽、±30、真实中点JUMP、时间轴轮廓、坏文件保留旧世界、个人存储及观看四输出精确保护。quit样例单独通过容器/事件校验，未宣称它也完成原生观看Verified。旧35:50候选保留旧GUID，仅为诊断。

客户端完整解压后启动Frontier Reckoning.app；操作见[玩家指南](Player-Guide-067.md)和[随包说明](Evidence/067/Delivery/README-zh.md)。未预置个人存档、偏好或课程徽记。本版没有正式1×长窗性能/内存/导出延迟、所有课程/战役自然通关、真人LAN、签名Release或其他平台结果。

下一阶段按[LAN方案](LAN-Next-068.md)推进房主P1＋远端P3及房主AI合作，区分本机双进程协议验证与两台物理Mac真实LAN。项目继续保留私有仓库、原draft PR #1；不合并main或公开发布。

## 独立验包与云端读回

[独立回执](Evidence/067/Delivery/independent-zip-verification.json)由单独代理使用Python标准库核对全部1,386个成员及49项检查，全Passed，16,934字节，SHA `c33f719ca1c31acfb4bf0275de932a56acce235e765df9f9c275f06f384909ba`。原包实际没有symlink，回执明确记0；946源码成员Git blob/类型/执行位均匹配ccc7快照，缓存、outputs、个人内容和旧app排除通过。

[六载荷上传读回](Evidence/067/Delivery/cloud-upload-readback.json)包含三ZIP、release-manifest、README-zh及独立验包JSON，各自复核本地bytes/SHA，云端名称、大小、父目录和owner-only私有权限均通过。[第七个回执的上传读回](Evidence/067/Delivery/cloud-delivery-final.json)记录5,420字节cloud-upload-readback回执的远端身份及本地SHA。七个对象均shared=false，没有共享权限变更；两份回执明确未验证远端内容SHA或下载字节相等。cloud-delivery-final是本地外层资料，没有计成第八个云端对象。
