# Native076 cold-load and resumed-healing audit

-Actual oldPID78563→newPID97963; same0.19.0/buildGUID/executable/sourceFP and explicitData directory.
-Match `ccc7b9fd6eb44cc3817dce2432df64af`; exactcoldreport`214348-190-menu_exit` only. Recording-library playback excluded.

## Closed native chain

| Step | World | Patient8 | Priest153 |
|---|---:|---:|---|
|ActualLOADslot1/F9f3829|381.3140563964844|297/450|Paused,auto=false,Healing,target8,manualorder|
|Resume/F9f4834|391.9147033691406|403/450|Same manualHeal,auto=false|
|Full/F9f6616|400.3152160644531|450/450|Idle,target0,noorder,auto=false|

-**153newpositive1HP Heal receipts**, source153→patient8; firstevent6319@381.4140625, lastevent6786@396.614990234375. Nooverheal/positiveHeal afterfull.
-NoP0Command,Payment,ProductionQueued orUnitTrained in coldtail: persistedmanualorder resumes without reissuingHeal orpaying/spawning again.
-Allrestoredworldentities exactlyequal originalschema5save. Otherworldfields equal except anchorsomit archivedevents; simulationaccumulator resets saved0.0944029688835144→0.
-Coldreport**6888events =saveprefix6318+570new**. Original6337report has19extra same-timepost-saveGather flushes: never concatenate bothreport histories.

## Original file manifests

- `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.19.0/Candidate1/Native1/british/Data/Saves/slot-1.json` — 147801B; SHA256 `a654ba3b084a98003976d89907e0c1d58e2d58b7407662e1099ee7223d2bee05`.
- `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.19.0/Candidate1/Native1/british/Data/Reports/20261009-214348-190-ccc7b9fd6eb44cc3817dce2432df64af-menu_exit.json` — 5446B; SHA256 `5df388ee6bffa707d457a3f55fce0146ff647a37b1a9d4368fa1db38c1991622`.
- `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.19.0/Candidate1/Native1/british/Data/Reports/20261009-214348-190-ccc7b9fd6eb44cc3817dce2432df64af-menu_exit-events.jsonl` — 5248599B; SHA256 `d2893856f621ab0ea6aca06ba94beaf7728d565b16ddcc6c2f6cf76a0ffea13a`.
- `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.19.0/Candidate1/Native1/british/Data/Replays/replay-adea3e8512e44f20acd4cefc3c420b1b.replay.json` — 187757B; SHA256 `d1a0959e5afe48c9362b65d119210acf6d70806b8351489a874de1f7dd1f3c8c`.
- `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.19.0/Candidate1/Native1/british/Cold1/launch-receipt076.json` — 1482B; SHA256 `a1b6f9137e92acb55fdbc60ac36ea661c72183a3b01eddf179554842ca2bef1a`.
- `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.19.0/Candidate1/Native1/british/Cold1/activation-race076.json` — 641B; SHA256 `2c16cc0e0a7e7e7a4fb9a73b48bf1ea02ded57235a9516aade698884d151573b`.

ColdReplayadea: loaded_checkpoint,2anchors/5records/6888events, fullsequenceequalcoldreport,payloadSHAvalid. Playbacknotcountedhere.

## Coldlivecapture original pairs

| JSON | World | JSON SHA256 | PNG SHA256 |
|---|---:|---|---|
|`Frontier-20261009-214235-487-f3829-8bb9fe348ee14708b4f31ce9f184e426.json`|381.3140563964844|`8834361d14f688a0c9dd6caed9824f332edb3ce4633251087346ada936dc3fc4`|`6f351131f4d59bf5c32a219f077bbd33443c084b8c3cda570bd19376dae17912`|
|`Frontier-20261009-214252-488-f4834-15405fa824e647e1a670c4cfac2ede42.json`|391.9147033691406|`f4d971d0fbeb485f9ff252e5b6d0dca7b2e1ee2cf2aa414bdb7c404f000e8e07`|`406421ceeaad3edbe91769cac0e710ac7c89307de0a21a6d8a98928f005f69ad`|
|`Frontier-20261009-214322-422-f6616-eb81e6ba52754829a7ce88a6f2c7c328.json`|400.3152160644531|`99c93aa3bdba600e34908799ccf872245b20c1de0baddc89ba5d4d6df2ee7881`|`cfbc384fbb8767312d4261ad12dc0cb9a66fac1b92ea1b5a9ef9ed36f6634cdd`|

All3sameframeworld/HUD andPNGhash1920×1080checks passed; replayWatching=false. Fullpaths/byte sizes/state inJSON.
-ContinuingCold1/player.log prefix15910B/164lines SHA256 `a14f940e62e3aa498e4b3bb23c722f851e92d8ce95c47d25f1628a525efdcd42` ends atcoldreport; excludes laterplayback.

## Retained activation issue

-Earlyopen-a createdtransientnoargsmenuPID97964. Existingactivation-race076.json preservesrootobservations andresolution. No97964UIiscountedasproperrestore; correct97963LOADlog+savedhash+pausedF9closeactualpath.
-Originalslot1 hash still matches paid-medical audit; originalfailed/paid audits untouched.
-Nooverallhumanwin/FPSthresholdacceptance; medicalcoldcontinuationonly.
