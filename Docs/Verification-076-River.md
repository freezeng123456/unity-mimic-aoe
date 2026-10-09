# 076 C2医护地形联动验证

2026-10-10。[预先合同](Content-076-River-Contract.md)。C1的1795测试、原生和交付仍见[原验证](Verification-076.md)，不重标为C2。原始C2运行输出位于 `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.19.0/Candidate2/`。

## River1：保留首次实际失败

HEAD `d917cab0e4da15a5fa090136e464db791f45bcae`，776输入FP `d73591041dae31d39bbbae9565b86167f183704a6abc4997173464224570eb1f`，运行前后源指纹一致。实际8例中6过、2失败，exit2，XML耗时137.484秒；原始 `River1/EditMode.xml`、log、runner receipt保留。

两座整桥跨越／磁盘冷读、两侧栏阻断／绕行、patient及healer真实Town出入共6例通过。两付费船例在建立医疗关系和真实Embark之前，第457行普通Move预泊断言失败，均偏离请求泊点6.36396122米；尚未覆盖后续医疗船载链。

只读核验：`World.IssueOrders` 对单船Move也调用水域FormationSlots；船半径1.05、间距4.5，槽位用1.71米清障半径筛选，而真实泊点只需船身1.05米合法。普通Move的实际槽位不承诺等于请求泊点。修正仅限夹具：绑定并严格检查普通Move的实际目标，另记录请求／槽位／实际泊点；真实Embark后仍须首次步行、12秒内入船、船到达真实resolved berth（.25米生产门槛）、所有医疗／容器／冷读／真实航程断言。未修改生产逻辑。新轮运行和新指纹另记，River1不重标通过。

## River2：真实乘客半径揭示第二处夹具假设

HEAD `2d9889b7b87c8a6c37cdb57fbebe58f1e93be66f`，FP `5d1e5ca1fb2abc421d9b6e2baec5866ddddce56a9f6511f57b12e6b058c59270`，源指纹稳定；实际仍6/8，exit2，XML138.810秒，原件保留。新增日志确认普通Move请求泊点 `(46.57,30.96)`、实际槽位及船位置 `(51.07,26.46)`，与4.5米对角槽偏移一致。普通Move目标修正已通过；两失败前进到第469行：预勘测使用.9米陆地半径，真实乘客半径所得shore相差1米，故把勘测请求点当实际入口仍是夹具错误。

下一修正仅将入船前的公共 `TryTransportShore` 查询使用该真实乘客半径（与生产入口同样下限.4），严格比对实际Embark shore和resolved berth均小于.001米；短路程和首Tick步行断言也绑定该实际shore。医疗、真实船程和冷读门槛不变；未改生产逻辑，尚未宣称船载链通过。
