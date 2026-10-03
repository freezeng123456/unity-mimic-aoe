# 0.3.1 初次构建原生烟测：两局自动对战，菜单布局待修

本目录仅归档初次构建 GUID **`11d4fafce22a42ebb37dac73f0ef89fb`**、版本 `0.3.1` 的材料，平台 `OSXPlayer`、Unity `6000.6.3f1`、1600×1000。后续菜单 UI 修订会使用另一实际 GUID，不能把这里的对局、截图或日志改标为修订后客户端证据。初次构建 receipt 由主任务另行保留。

## 实际完成范围

4 份报告对应 **2 局 16× autoplay**，每局均有 `match_finished` 和 `menu_exit`，不是 4 局。重复报告的原始事件需逐字节相同。两局使用固定地图、种子 `314159` 和 Standard AI。

| 唯一 Match ID | P0 → P1 | 最终模拟时间 | 胜者 | 原始事件数 |
|---|---|---:|---|---:|
| `71ec8ef7f4c743779068193845a9b5f0` | france → ottoman | 1133.470337 秒 | P0，法国 | 43093 |
| `df61440944f14036a8748a52e54c44d2` | ottoman → france | 1165.662476 秒 | P1，法国 | 43086 |

实际结算以唯一 `MatchEnded`、胜者及时间吻合、败者无存活单位或建筑、无投降为核验要求。资源箱仍可能归属败者，不能把这类资源实体计为存活单位或建筑；`Rules.json` 按本轮实际定义分类，其 SHA 与初次测试源码清单一致。16× 自动局不构成正常速度性能验收，也不构成统计平衡结论。

**普通 1× 开局未完成。** 主执行代理报告所有鼠标 CUA 操作均返回 `noWindowsAvailable`，reset、重新绑定和 AX raise 后仍失败，无法从菜单启动计划中的普通开局。本轮没有对应 1× 原生报告或手动采集/付费/晋级账本，不标为 `manual opening passed`。主执行代理报告 T、F、M、F9、Esc、Cmd+Q 键盘操作成功，并在结束后退出客户端；这些现场观察不是完整输入回放，不从事件名称推断所有鼠标或键盘操作均已验证。

## 已保存画面与范围

- [法国城镇中心](Captures/Frontier-20261003-173135-526-f1281-fa090393dfeb4eff891f105fa9a1dd57.png)：训练按钮显示 Coureur des Bois、120F；队列截图为剩余约 6 秒，对应 JSON 记录 duration 28 秒、elapsed 22.60005 秒。不能把截图剩余时间称为完整训练时间。
- [法国局 Victory](Captures/Frontier-20261003-173237-565-f3731-28be1d868a7f47a1865f25785f3b4e09.png) 与 [战场画面](Captures/Frontier-20261003-173210-300-f3195-b069d525fb2c4349bde5e043b9e5e6f3.png) 属于第一局。
- [奥斯曼早期城镇中心](Captures/Frontier-20261003-173521-546-f462-98a4190c2d0048aa9a1c553352741cca.png)：Free settlers 23.8/50.0s、Workers 10/25、Recruiting automatically；显示 AUTO SETTLERS: ON，没有付费 Settler 按钮。
- [奥斯曼后期城镇中心](Captures/Frontier-20261003-173609-921-f3221-31aca347c9364349a55777876c32f2a6.png)：Free settlers 10.4/39.6s、Workers 26/45。实际免费出生和科技完成另外按事件统计，画面数字不替代完整机制因果审核。
- [第二局 Defeat](Captures/Frontier-20261003-173634-215-f3689-64b72251524a43d697619f86b685e6b0.png) 与 [返回菜单](Captures/Frontier-20261003-173727-686-f4802-9677466495204fed9ea5f71a116b90c4.png) 均实际保存。

返回菜单的奥斯曼简介文本超出说明区域，末行覆盖 PRESET / EDIT DECK 按钮所在区域。这是初次构建已复现的布局问题，主任务已安排仅 UI 修订并重新构建。此归档保留问题原图，不能以旧图宣称问题已修复。鼠标工具失效与游戏布局问题分别记录；目前无法据工具错误确定鼠标失效属于游戏引擎缺陷。

## 原文校验与限制

保留 4 份原始报告、4 份 lossless gzip 事件、8 组 F9 JSON/PNG、2 份完整冻结客户端日志。`summary.json` 由原始资料重算，按唯一 Match ID 区分自然结束与普通未完成；`manifest.json` 同时记录 gzip 容器和原事件字节数/SHA，`manifest.sha256` 核验清单本身。两份日志保留退出时的 premature thread finalization 和 MemoryLeaks 诊断，不预先判定原因，也不宣称无警告或无泄漏。

独立离线校验和源原文逐字节比较：

```sh
python3 Docs/Evidence/058/Native-Initial/archive_native.py verify \
  --source "$HOME/Library/Application Support/Frontier Workshop/Frontier Reckoning" \
  --log-source Builds/Native-0.3.1 --rules Assets/Resources/Rules.json
```

脚本严格核验事件 JSON、生命周期和自然结算身份；Payment 次数与 Spawn 名称是描述性统计，不是完整 debit/队列/出生配对或所有特效机制验收。此轮不覆盖普通 1× 开局、手动 I–V、六文明人工全流程、全部牌组效果、多地图、多人或跨机器性能。脚本没有启动或操作客户端。
