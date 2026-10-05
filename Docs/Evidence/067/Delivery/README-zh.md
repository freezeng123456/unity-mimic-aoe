# Frontier Reckoning 0.10.0

本包是macOS Apple Silicon（arm64）的Unity Development / Mono客户端，Unity6000.6.3f1，构建GUID `2d94fdd6d9734b259f5231c1fd3f2b99`。尚未完成Release、签名、公证、Intel Mac、Windows或Linux验收。

解压后打开 `Frontier Reckoning.app`。主菜单可选英国、荷兰、法国、奥斯曼及两个原创文明，地图包含曲河、热带河湾、支流与河口；本地1v1、2v2和四方自由混战由一个玩家加AI组成。七个中文引导课程和三章原创河流战役也可从菜单进入，后续章节按实际完成记录解锁。

## 新增录像

正常游戏自动录制。按Escape暂停，点 `SAVE REPLAY`，等待 `Recording saved`。`RECORDING LIBRARY`打开已存录像；支持播放/暂停、0.5/1/2/4×、观察槽位、±30秒和时间轴跳转。观战期间可以查看单位和移动镜头，不能下令或改写任务完成进度。

保存录像用于重新观看；`SAVE MATCH`的检查点用于以后继续操作，两者独立。退出或返回菜单会尝试导出当前片段，仍需关注保存失败提示。`RECORDED SEGMENT VERIFIED`仅表示记录区间校验通过，不表示赢得整局。

录像只能在匹配的原构建中打开，同版本名的另一次构建也可能不兼容。图集包附本构建生成的实战样例；默认将自己的录像放在下方Replays目录，或使用命令行路径：

```sh
"/绝对路径/Frontier Reckoning.app/Contents/MacOS/Frontier Reckoning" \
  -frontierReplay "/绝对路径/replay-片段身份.replay.json"
```

默认个人数据位于 `~/Library/Application Support/Frontier Workshop/Frontier Reckoning/`，录像在Replays，对局检查点在Saves，任务完成记录在Account，偏好在Preferences。包不预置个人检查点、课程徽记或偏好文件。完整回放指南见随包 `Player-Guide-067.md`，资源声明见Licenses。

## 验证范围

595运行输入，指纹 `8180b02b55ed3957b94042d779a6d74bdda78cd46073cf70d93353c330363466`。完整自动化通过1,097/1,097（740 EditMode +357 PlayMode）；实际原生验收与已知限制以图集/源码中的067证据及完成状态为准。

当前没有真人局域网联机。商业游戏完整文明/卡池/革命、录制语音、美术精度、地图/触发器编辑器及跨平台仍待推进。长录像原文限制256MiB、存储限制64MiB，各自独立；不保证固定时长或进程内存。最终三包的bytes/SHA与验包回执在包外release-manifest记录；云盘元数据读回不证明远端下载字节SHA。
