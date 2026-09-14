# Player032 archived match audit

Sources: `outputs/Verification-0.2.11-Player032/20260914-074744-match_finished.json` and its 32,470 line event stream. Read-only parsing; no source or archive mutation.

## Match result

The match ended at simulation time 936.7183 s with `reason=match_finished`, `winner=1`, seed 314159, build 0.2.11 (`buildGuid=961d...6fc`), and `autoplay=false`. Player 0 population was 0 at the final report. The event stream contains 110 Death events; the final player-0 Town Center death is at 901.2270 s, while the match-finished report time is 936.7183 s.

## Player-0 artillery and building damage

Exactly one player-0 artillery unit spawned: entity 234, `Field Cannon`, at 753.2631 s (position 27.8971,30.25). No player-0 mortar spawned. Machine recount of the event stream gives `Fire=6`, `Impact=5`, and `Siege Damage=15` for this cannon. The six recorded `Siege` fire events were:

| Fire time | Target | Target type/owner | Direct damage event | Result |
|---:|---:|---|---:|---|
| 758.4619 | 214 | Pikeman, p1 | 140 at 758.7618 | unit hit; splash also hit 215 for 70 |
| 777.2573 | 235 | Hussar, p1 | 70 at 777.8571 | unit hit |
| 782.3560 | 235 | Hussar, p1 | no damage event | target/impact path produced no recorded damage |
| 788.4545 | 222 | Musketeer, p1 | 140 at 789.1544 | unit hit; splash also hit 221 for 70 |
| 812.5486 | 227 | Longbow Guard, p1 | 70 at 813.3484 | unit hit; splash hit 212, 230 for 70 and 240, 241 for 35 |
| 817.6474 | 227 | Longbow Guard, p1 | 70 at 818.4472 | second shot at the same unit; splash targets also recorded |

The cannon died at 821.1465 s. Its player-0 `Siege` Damage target set is exactly `{214,215,235,222,221,227,212,230,240,241}`; every target is a player-1 unit. There is zero player-0 `Siege` Damage, Fire, Impact, or Damage event whose target is a building. This archive therefore does not demonstrate player cannon damage to an enemy building.

The stream does show that the cannon attack and projectile path worked against units: player-0 Fire=6, player-0 Impact=5, and player-0 Siege Damage=15 (including splash). The absence of building targets is an observed target-selection/result fact, not proof that building siege damage is impossible in the implementation.

## Building deaths and winner ordering

Player-0 buildings completed: Manor 137 at 92.3992 s, Barracks 151 at 283.4081 s, Watchtower 156 at 314.4100 s. Their deaths were Barracks at 786.6550 s, Watchtower at 816.2480 s, and Manor at 935.7186 s. The Town Center (entity 1) died at 901.2270 s. No player-1 building death appears in the event stream; the only building Death events are those four player-0 buildings. Thus player 0 lost its Town Center before its Manor, and the final report names player 1 the winner. There is no evidence of an enemy building being killed by player 0.

## Economy, ages, and shipments

Final report totals:

- Player 0: age 3; gathered food 4689.2407, wood 714.1680, coin 1800.5787; spent food 3670, wood 840, coin 1250; final resources food 1320.0736, wood 174.2417, coin 649.8832; kills 39, losses 37; shipments 3; treasure 1, guardian 1.
- Player 1: age 3; gathered food 6932.8325, wood 4122.3262, coin 4604.4336; trade income food 975 and coin 100; spent food 8000, wood 3970, coin 4525; kills 37, losses 39; shipments 7; final resources food 210.6511, wood 448.7869, coin 278.0938.

The stream has 27,055 Gather events, 21 ResourceDepleted events, 15 BuildingComplete events, 6 Technology events, 4 AgeAdvanced events, 10 ShipmentSent, 9 ShipmentArrived, 10 ShipmentEarned, and 4 AgeRewardArrived. Player 0 advanced at 225.4046 s (age 2) and 686.0795 s (age 3). Its shipment timeline is: New Settlers sent 160.9006/arrived 195.9028; Coin Convoy 2 sent 299.4091/arrived 334.4112; Cannon Support sent 718.2717/arrived 753.2631. The Cannon Support arrival is the single Field Cannon spawn.

Player-0 garrison/food context is also explicit: five workers entered the Town Center between 444.2179 and 455.3186 s; five were released at 543.5143 s. The archive supports the reported shelter explanation and does not by itself establish a hunt navigation defect.

## Acceptance boundary

Verified from the archive (machine recount rechecked): one player-0 cannon was spawned, generated six `Fire` events, five `Impact` events, and fifteen `Siege Damage` events against player-1 units; the cannon died before the Town Center; player 0 killed no enemy building; player 1 won; final economy/age/shipment totals above. Not established: a player cannon direct hit on an enemy building, enemy building damage/death, a successful building-target command in this match, or a general claim that the building-target path is broken. The six-fire/five-impact sequence is also insufficient to assess all possible artillery units, ranges, visibility states, or target-command modes.
