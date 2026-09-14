# 0.2.12 autoplay audit

The report and parsed event stream agree at 47,468 events. This was an 8x autoplay mirror run (`autoplay=true`, simulation speed 8), not a player-input acceptance run.

The match ended by elimination (`All opposing units and buildings eliminated`) at 1100.3784 s with player 0 winning. Player 1's Town Center died at 999.6030 s; there is no player-0 Town Center death. This is an elimination result, not resignation.

Strict Spawn-ID plus Rules-kind join shows player 0's six Field Cannons generated 114 Fire, 103 Impact, 131 Damage events, and one AttackCancelled. Of these, 86 Fire, 83 Impact, and 85 Damage events targeted buildings; building Damage targets were IDs 52, 147, 148, 157, 162, 176, 195, 222, 248, 261, and 316. Player 1's three Field Cannons generated 17 Fire, 17 Impact, 18 Damage, no AttackCancelled, and no building Damage events. Counts are reported without attributing Fire/Impact differences to visibility or another unverified cause.

Player 0 reached Age 2 at 214.9039 s and Age 3 at 623.6948 s. Player 1 reached Age 2 at 207.7035 s and Age 3 at 686.0795 s. The stream contains 12 ShipmentSent, 11 ShipmentArrived, 12 ShipmentEarned, and 4 AgeRewardArrived events overall. Player 0 sent New Settlers, Settler Reserves, Mounted Guard, Musketeer Company, Cannon Support, Dragoon Escort, and Coin Convoy 3; player 1 sent the corresponding early shipments plus Cannon Support.

The reproducible recount is `work/verification/033/recount_native_autoplay.py`; its output is the strict source-artillery/target-building join used above. These facts establish automated event-path activity, including player-0 artillery building damage, but do not establish that a human player successfully issued or maintained those siege targets through the screen UI.
