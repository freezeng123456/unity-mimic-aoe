# Native 坐标反证记录（0.2.5 / iteration 026）

## 范围

这份记录只核对主城分页与误发 `Food Convoy 2` 的坐标证据，不修改运行时代码，也不把一次人工操作直接归因于 UGUI 或 Sky 坐标转换。

## 已核对的输入序列

两个截图文件均为 `1280×752` JPEG：

- `outputs/Verification-0.2.5/native-home-city-age-two.jpeg`
- `outputs/Verification-0.2.5/native-next-page-unintended-shipment.jpeg`

`NativeManual.log` 中 H 键打开主城的事件为：

```text
line 1920: FRONTIER_INPUT Key H down p=(468.00, 598.00) shift=False save=False t=548.0809
```

随后收到的两次左键完整点击为：

```text
line 1937: FRONTIER_INPUT Left None down p=(1008.00, 415.00) shift=False save=False t=559.1626
line 1955: FRONTIER_INPUT Left None up   p=(1008.00, 415.00) shift=False save=False t=559.1795

line 1973: FRONTIER_INPUT Left None down p=(1002.00, 212.00) shift=False save=False t=559.2542
line 1991: FRONTIER_INPUT Left None up   p=(1002.00, 212.00) shift=False save=False t=559.2545
```

这里的 `FRONTIER_INPUT` 坐标是 Unity 原生窗口坐标，事件时间 `t` 是 Input System 的原生／unscaled 输入时间，不能直接当作对局 `World.Time`。因此事件出现在日志时间 559 秒附近，与对局事件文件中的 `World.Time=370.313385s` 并不矛盾。

## 坐标换算与截图位置

结合 2560×1440 原生窗口、1280×752 截图逻辑像素和顶部 32 像素标题栏，当前记录采用：

```text
logicalX = nativeX / 2
logicalY = 752 - nativeY / 2
```

因此两次点击对应：

```text
(1008, 415) -> (504, 544.5)
(1002, 212) -> (501, 646)
```

截图核对显示：第一点 `(504,544.5)` 位于 `Food Convoy 2` 卡牌区域；第二点 `(501,646)` 位于 `NEXT` 按钮区域。该序列与“截图最后显示第二页，但前一次点击已经选中并发送 Food Convoy 2”相符。

## 当前可以证明的结论

1. Unity 收到的第一组左键按下／抬起坐标是 `(1008,415)`，按上述窗口到截图坐标关系对应 `(504,544.5)`。
2. 该第一点击在截图坐标中位于 `Food Convoy 2` 卡牌上，因此不能认定 UGUI 把 `NEXT` 的点击错误地触发成了卡牌发送。
3. 第二组点击对应 `(501,646)`，位于 `NEXT` 区域，随后页面显示第二页是正常的分页路径的可行解释。
4. 主城代码中卡牌按钮和 NEXT 的创建位置分别见 `FrontierHud.cs:222-229`；卡牌点击回调在 `:226` 发送卡牌并按原页重建，NEXT 回调在 `:229` 调用 `ShowHomeCityPage(page+1)`。
5. 对局事件 `native-manual-mirror-events.jsonl:8245` 的 `370.313385s` `ShipmentSent` 只证明 Food Convoy 2 已发送，不能单独证明是哪一个屏幕输入触发。

## 尚未知、不能越界声称的部分

“Sky 请求的 `(501,646)` 为什么在 Unity 首次收到的点击中表现为 `(1008,415)`，而第二次才表现为 `(1002,212)`”仍未知。可能涉及自动化操作的点击序列、窗口坐标／客户区坐标、标题栏补偿、截图时机或移动／按下／抬起发送方式，但本记录没有足够证据在这些解释之间选择。

因此当前结论必须写成：**Unity 收到的第一 click 坐标已在卡牌上，不能认定 UGUI 翻页触发错卡；为何 Sky 请求坐标与 Native 首 click 不一致仍未知。**

## 下一步可证伪记录

为区分剩余原因，应在同一操作中同步记录 Sky 请求坐标、窗口客户区尺寸、鼠标移动／按下／抬起的原始事件坐标、截图帧和每次 `Button.onClick` 的稳定按钮 ID。只有当 `pointerPress`／`pointerClick` 已明确指向 NEXT 而卡牌 `onClick` 仍被执行时，才可继续审查 UGUI 的按钮路由；当前证据尚未达到该条件。
# 后续验证入口

本文件记录0.2.5原生坐标疑点的调查。0.2.6随后复现并修复的抬起后移动、UGUI创建帧命中、军队公共按钮位置变化和小地图混合输入边界，见 [Iteration-026.md](Iteration-026.md)；各候选构建的原生证据与验收范围见 [Native-Verification-026.md](Native-Verification-026.md)。旧版本坐标调查不是最终候选完整体验验收。
