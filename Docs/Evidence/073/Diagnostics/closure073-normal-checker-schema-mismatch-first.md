# 073 三局自然观察 · 独立实际审计

Status: failed_independent_closed_case_audit。9981项检查，3项失败；这里只验收声明的三局实际输出。

Actual runtime `519d7d34a0be85f75ee19e53458c636d1ba0b3e4`；732项source map等before-full/full/current，FP `9b92812a922adb69a5ae501925417d8eba8e24b836f496274ebc8ec8679ba03d`。runner实际exit0、sourceStable、无Prepare/注资/强制命令/运行干预；版本0.16.0。manifest与全部world的rulesHash `8eaf0299c315a303b056c272591793b29a94bd2f320f4eddbf17de3d1f2506ae`一致；raw Rules.json SHA与语义rulesHash分别处理。

| Case | 结果 | Events / samples | Market / assembled | 最后300秒Fire / Damage / 正PvP Damage | wall / step / max step |
|---|---|---:|---:|---:|---|
| winding-ottoman-44 | natural_terminal 2084.883057s P0 | 110105 / 142 | 11 / 14 | 2941 / 2749 / 2749 | 387.153s / 385.482s / 1800.297ms |
| winding-ottoman-45 | natural_terminal 1754.918579s P1 | 87150 / 120 | 15 / 14 | 2533 / 2280 / 2280 | 319.654s / 319.039s / 1862.106ms |
| river_bend-river-42 | observation_cap 2400.090576s P-1 | 101471 / 162 | 0 / 21 | 317 / 330 / 330 | 125.207s / 123.777s / 1832.645ms |

开局均普通I时代且钱包等文明默认值；1500 crossing实际1500.080811s、15003 steps；final-world时间、胜者与final-simulation stepCount逐项等manifest。两Ott实际清除敌方所有单位建筑自然终局；Dutch2400.090576s cap并未结束，不称赢局。所有sample配对、最终钱包/账本/army状态与终局快照相符。

两Ott共26次真实市场卖Food，0次买入；全部逐笔依据边际报价、前序Payment的真实扣款、MarketTrade到账、钱包连贯性、归属及完成市场核对。每个opening/comparison/final和全部samples的marketIncome/Sold/Bought与price pressure等事件前缀。P0两局分别1400F→707C与3000F→1042C；P1均200F→137C。Dutch0次交换与零market账本明确保留。

两Ott P1在827.944885s由80.705048C卖100F增加70C至150.705048C，同tick实际付200W/150C研究Cargo Ledgers，873.033875s实际完成，897.727844s收到50C。它验证不足150C到可支付账簿的实际桥梁；P0没有完成账簿，不扩大为双方均完成。Dutch双方实际完成Cargo Ledgers并收到商路资源，市场交换未触发。终局tradeIncome逐资源等完整TradeDelivery之和。

Army assembled14/14/21与manifest精确相符；AttackMove/Attack发令与真实Fire、正PvP Damage分别计数，三局最后300秒均存在实际交战。30秒sample不认证每支cohort全程路径或每条命令均达目的地。

Step、observer、finalWrite与wall分项均为实际Stopwatch；Native1真实运行15:52:56–16:05:10，与三局均重叠并共享CPU。max step仍约1.8秒，慢步数量保留；不称native FPS或隔离性能改善。1500状态 crossing有真实快照，performance samples只给累计step成本上下括界，不制造精确1500 wall字段。

原生既有scope审计仅引用：市场137实际100W付款并完成、3组F9，而buy/sell/modal/500/unaffordable、当前GUID cold/replay未完成。后续点击noWindowsAvailable、0 MarketTrade保留；本审计未重新验像素。release/delivery仍false。

所有选定原件两次SHA/bytes/mode/identity读取稳定，原件未改。没有Unity/测试/模拟重跑、repo编辑、旧072原始局/云盘重复审计、提交或上传。详细逐笔事件行号和原件见[closure073-normal.json](closure073-normal.json)。

实际失败检查：
```json
[
  {
    "check": "river_bend-river-42:opening:identity",
    "details": null
  },
  {
    "check": "river_bend-river-42:comparison:identity",
    "details": null
  },
  {
    "check": "river_bend-river-42:final:identity",
    "details": null
  }
]
```
