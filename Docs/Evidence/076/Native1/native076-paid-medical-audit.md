# Native076 paid medical audit — original second match

- Match `ccc7b9fd6eb44cc3817dce2432df64af`; British/Ottoman,winding42/standard; originalPID78563.
- Version0.19.0; buildGUID `a86c515f3ab040649ea505e507193ff3`; sourceHEAD b1a9b6a.
- Scope ends at original paused381.3140563964844/menu_exit. ColdPID97963 excluded even when matchId/worldtime coincide. First failed audit untouched.

## Proven paid/native chain

| Item | Zero-based event | World seconds | Result |
|---|---:|---:|---|
| Church150W |423/526|19.600039→32.600075|300→150W;6builders complete|
| AttackGuardian122 |974/1009/1019|82.099319→84.299286→84.699280|Attack accepted→prepared→Fire|
| Naturalpatientdamage |1020…3336|84.699280…219.404190|18guardian×8HP +7enemyPathfinder×14HP;450→208HP|
| PaidAgeII800F |3402/4978|222.704391→312.809875|920.611206→120.611206F|
| LocalDefence |4980/4982–4984|312.809875|coast_militia_2;+5%training;Pikemen149/150|
| Priest100C/queue |5236/5237|328.510834|100→0C;reservedpop1|
| Priestbirth/trained |5763/5765|357.112579|Priest153;used9/reserved0|
| AutoHeal |5794…6037|358.712677…367.313202|87positive1HP;208→295HP|
| Stop |6038|367.313202|AutoOFF,Idle,no target/order|
| ManualHeal |6311|381.114044|Priest153→patient8;Auto remainsOFF|
| ManualpositiveHeal |6312/6315|381.214050/381.314056|295→296→297HP|

-89positive1HP receipts, rate10HP/s, one exact consecutiveHP chain. No Heal afterStop beforemanualcommand (13.80084228515625worldseconds).
-Basequeue30s shown inF9; LocalDefence+5% gives actual28.60174560546875s birth interval.
-Manualpatient already inrange: no manualout-of-range approach orbridgecrossing acceptance.

## Save/replay

-Save world schema**5**, pausedtrue,time381.3140563964844,selected153. Patient297HP/hold; doctorauto=false,target8,activeHealorder/work.
-Save decoded SHA256 `566780b7a4ef44a5b600b3027202c96ee9d8d2549a8cfee478585e199b5a2ab2`;6318events equal closed6337-event prefix. Remaining19same-time events areGather flushes; no furtherHeal.
-Replay hash valid;14anchors/20records/6337events equalReportJSONL;finishReasonmenu_exit,incomplete=false. Coldcontinuation/nativeplayback excluded.

### Original manifests

- `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.19.0/Candidate1/Native1/british/Data/Reports/20261009-214115-640-ccc7b9fd6eb44cc3817dce2432df64af-menu_exit.json` — 5445B; SHA256 `e7fd24255378379a3bb0e520b5342178b0cdc6b52bf662cc1186120820a7096c`.
- `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.19.0/Candidate1/Native1/british/Data/Reports/20261009-214115-640-ccc7b9fd6eb44cc3817dce2432df64af-menu_exit-events.jsonl` — 4828712B; SHA256 `c37e9957c9e3d0cf0793bb911b4c191031d5ab4793f3097bc4130fc12dff60b2`.
- `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.19.0/Candidate1/Native1/british/Data/Saves/slot-1.json` — 147801B; SHA256 `a654ba3b084a98003976d89907e0c1d58e2d58b7407662e1099ee7223d2bee05`.
- `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.19.0/Candidate1/Native1/british/Data/Replays/replay-90b7fc9666314d10a3d7013ac5da6ad3.replay.json` — 452549B; SHA256 `a4c890af758f1fd6336bcce88bc8cd2064c78038a60de61059f2dc19a22f4ebb`.

## Original pairedF9 index

| JSON original | UTC | World | JSON SHA256 | PNG SHA256 |
|---|---|---:|---|---|
|`Frontier-20261009-213407-569-f60304-033ab1415dac4dd08e67c67764a274e1.json`|2026-10-09T21:34:07.4939220Z|7.39999532699585|`689e6082d11e0053b8ea73f140de225ca84513598272d51b63820f4f7e61640e`|`84c2c7cfa2826553f84071ae84ee06663bc86e8a56ceff2fbaace018100933b0`|
|`Frontier-20261009-213419-916-f61037-a280020c57a04497a797953f88db348a.json`|2026-10-09T21:34:19.8390800Z|19.70003890991211|`2ffa1f97f86724537d1305c310384f639ec7c9a025f57817ce3e3c89b6672c23`|`2eae10cbee4db5e200945246cbbdac67afc8d0edea899209a95a20e5a860a455`|
|`Frontier-20261009-213514-382-f64244-fc11de15ea424cd489679f2fb3fc84e1.json`|2026-10-09T21:35:14.3008150Z|74.1994400024414|`c84bf67c855e4cac59daa3fdc80934a3033ea5d8488e72d086ebb1d5d901c0ea`|`938b16ea39aa180a70a1d40ac2c6b84be87418f50483d13a410452f6c171ba84`|
|`Frontier-20261009-213522-359-f64700-2fbd7dd68e584a74bcd660be27cffa60.json`|2026-10-09T21:35:22.2775430Z|82.09931945800781|`18d7ed8526df92241f719b8800fd6d137d2dbdf2680e9380930982fb4027b8a5`|`db760ce58c54c6447bcb0830162176d56ba05db44647da795f6f0f84548aa141`|
|`Frontier-20261009-213547-296-f66180-7a396367d6fc473ea231a642c2724d33.json`|2026-10-09T21:35:47.2123340Z|107.09893798828125|`4c2e11ec16079a74f17a22c5119185031f96380919827d8c95944dd4d46c38d4`|`1c8d8051ebec7b37b843dac185504fc6872dd4b837c6a11453e07980af20d222`|
|`Frontier-20261009-213641-391-f69376-4d77e03f4e2640d79fe60bf597a335c4.json`|2026-10-09T21:36:41.3071980Z|161.2006378173828|`56a7509f1f95466965a1bfdad9e0cb8a4739049d35bd967f252e3b9f17c7cba3`|`df0824106cca770fbe2fca95cc61a971715b680e9ace065caf0adbad35dea8da`|
|`Frontier-20261009-213742-962-f73014-303e63cba68b4889b81e61956a288f5e.json`|2026-10-09T21:37:42.8797760Z|222.7043914794922|`279579b60b43be152129f00e5026ff2ff290ee534a6e1947c01795abace07537`|`f936e592b90dcde75e9b594f4716e92a9fda65d2f1fa17343e8e4e19a778cc83`|
|`Frontier-20261009-213751-064-f73484-9e08b6264fda464284490c090d7e5aba.json`|2026-10-09T21:37:50.9777860Z|230.8048858642578|`b844e1a7323f44fbbe5c1a5638807a226347d80b6c126079e9c9d9f8e71f30c1`|`375dfdb38f56f7c6bf32834167a1c111e1777fefb922876c180b5a7b9178a272`|
|`Frontier-20261009-213821-313-f75271-75a572f159194a88b17859f7e30e1025.json`|2026-10-09T21:38:21.2293230Z|261.1067199707031|`da52d860013b0a03eab7fdd2b6f6900868e8149595d541d70a4620c3c1f3f799`|`a2abac133d0d18f999aa2e0d87b803389e90bad1367f98c9d884707ed06731bf`|
|`Frontier-20261009-213836-078-f76138-b8206f2de4d94ffda7f5dc4addb715af.json`|2026-10-09T21:38:35.9946370Z|275.9076232910156|`ed0076fbaed8b6e35d99452cb0abec52b1f84c3cfebba3d5d727623e366b4813`|`e3f0792335ba897553e039177bd79d048ac72a913560a36d27797aae5ab58eb9`|
|`Frontier-20261009-213907-050-f77961-ea389d7809d945639be5e749224e2131.json`|2026-10-09T21:39:06.9647270Z|306.80950927734375|`f1c2b756112f4d5c61b7d0c014d76e08d9cca54a96c56083833fdff427d6c40e`|`5e5e2a7ea16916bda0608298b9ff15ecec4bca66b6418b41fc9f7925fb2b3df1`|
|`Frontier-20261009-213928-745-f79234-3bc93b1c1ced484f8eb4029d021c736a.json`|2026-10-09T21:39:28.6601200Z|328.5108337402344|`b37bb896e7588724a9a9554e9f9303f1ee91f37e79dfffc4b6d0fee96e098d99`|`bf827afb5587654f083c160a8c380e4973c91ec1365ca72e9f078566112d4c74`|
|`Frontier-20261009-213958-579-f81007-e97f6596887048aa958b8420e1861804.json`|2026-10-09T21:39:58.4958510Z|358.41265869140625|`846511abf28e17b433ad822350328adf3d0843716711a628d2cfd054faa38ff0`|`e9928e22601b998d6320cb7e1db1c45809ea9179af2e5f09bd12b49877074b89`|
|`Frontier-20261009-214007-526-f81535-b405b5214b804caab0dad8fa00e4901b.json`|2026-10-09T21:40:07.4406600Z|367.3132019042969|`a5a0ce853fba69db65418dd119c946b691d7953604e084a28d521f5f66d8c7d2`|`087a1328deb1d8e748aa07f7461cd173d5654f34bd6440d3dc820c55bc66ca22`|
|`Frontier-20261009-214028-084-f82755-6fafbace152d4bdfbe18cba4e69b4284.json`|2026-10-09T21:40:27.9959110Z|381.21405029296875|`a4992d6bc450645eb9ed3abe2714ed7e74f863ee205e50143ab62c0be4fc3afe`|`9587665eef6b0b495e5b5446b9d066a06d284879a6b39146cca48c06ca9586a4`|

All15pairs: JSONrequest/capture/HUD frame+worldtime, app/build/match identities, PNGSHA and1920×1080 dimensions agree. Fullpaths/sizes/stateexcerpts inJSON.

**Frame distinction:** lastF9 frame82755 at381.214050 has296HP; savetime381.314056 has297HP afterone moreHeal.

## Retained limits

-menu_exit,winner-1,population9,nohumanlosses: not an overallhumanwin.
-Frame metrics retained, notperformanceacceptance.
-Food floating-point residue: actual752.3831787109375 minus ledger estimate752.4515380859375 = **-0.068359375**. Originals unchanged.
-Logprefix throughsecondreport: 75897B/733lines; SHA256 `3a62f4f778b641c0954861479c3fd048fa96a5dc740d6dee638fdefb242f0622`.

## Compact acceptance rows

1.PaidChurch/payment423+complete526+pairedF9.
2.PaidAgeII/LocalDefence/payment3402+Age4978+Pikemen149/150.
3.PaidPriest/payment5236+queue5237+birth5763/trained5765+F9.
4.Naturalwound/retreat/autoHeal87HP.
5.Stop6038→zeroHealgap→manual6311/positive2HP+lasttwoF9.
6.Pausedschema5save+hashboundreport/replay; cold/playback separate.
