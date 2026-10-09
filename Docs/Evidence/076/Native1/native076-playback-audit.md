# Native076 recording playback audit

**Two actual native recording-library segments continuously reached RECORDED SEGMENT VERIFIED.**

-Same0.19.0/buildGUID `a86c515f3ab040649ea505e507193ff3`; PID97963; match `ccc7b9fd6eb44cc3817dce2432df64af`.
-NoSeekcontrolaction observed in boundedinputlogs; all5playbackF9seek=false. Originalevidence/prioraudits unchanged.

| Segment | Native range | Speed | Finalworld | Patient8/Priest153 |
|---|---|---:|---:|---|
|adea3e8512e44f20acd4cefc3c420b1b|3813→4004|1×|400.41522216796875|450HP;doctorIdle/AutoOFF/target0|
|90b7fc9666314d10a3d7013ac5da6ad3|0→3813|4×|381.3140563964844|297HP;doctorHealing/AutoOFF/target8|

## Verification

-BothfinalF9: watching=true,recording=false,verified=true,paused/worldPaused=true,incomplete=false,allerrorfields empty,currentStep=endStep=replayfinalanchorstep.
-Replaygzip payloadSHA andbuildidentity match; finalF9worldtime/resources/population,all10ownedunit projections,2productionbuildings andallplayercivilization/team/status matchfinalanchors exactly.
-Fullhiddenworld is not emittedbyF9: independentprojectioncomparison andnativeverifiedflag are separate evidence, not a fabricatedfullworlddump.
-BothfinalPNGs visually showRECORDED SEGMENT VERIFIED, withactual1×/4×speed.

## Final original pairs

### adea3e8512e44f20acd4cefc3c420b1b
-JSON `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.19.0/Candidate1/Native1/british/Data/Captures/Frontier-20261009-214458-815-f12305-5f4e33e77a534e9bb2de9661f8c97a7c.json` — 17669B;SHA256 `1ed948ad4860c033a647d74ff6035edf180a8268117250e65d419de35f158ca4`.
-PNG `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.19.0/Candidate1/Native1/british/Data/Captures/Frontier-20261009-214458-815-f12305-5f4e33e77a534e9bb2de9661f8c97a7c.png` — 1346317B;SHA256 `8a8656f54be57aff4dbed5ebf0a23b062db01ab1a4c70503b8c43ea727f9e05a`.
-Replay `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.19.0/Candidate1/Native1/british/Data/Replays/replay-adea3e8512e44f20acd4cefc3c420b1b.replay.json` — SHA256 `d1a0959e5afe48c9362b65d119210acf6d70806b8351489a874de1f7dd1f3c8c`;decodedpayloadSHA256 `35a1dccf15099eb9f848f4fc75fc85b2935eeae1ea6fc190b176b6a8134d5da9`.

### 90b7fc9666314d10a3d7013ac5da6ad3
-JSON `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.19.0/Candidate1/Native1/british/Data/Captures/Frontier-20261009-214747-935-f22181-e3adf960be654f988434293c221cc852.json` — 17705B;SHA256 `5569f2cefef03e086b4d112dcc20322c49337283950ee4a9ca2184206564b512`.
-PNG `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.19.0/Candidate1/Native1/british/Data/Captures/Frontier-20261009-214747-935-f22181-e3adf960be654f988434293c221cc852.png` — 1614979B;SHA256 `5dc9dd1c18cff5f5bf7be854ea04a110c3e27116027fc118ff4d6a4134f269ed`.
-Replay `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.19.0/Candidate1/Native1/british/Data/Replays/replay-90b7fc9666314d10a3d7013ac5da6ad3.replay.json` — SHA256 `a4c890af758f1fd6336bcce88bc8cd2064c78038a60de61059f2dc19a22f4ebb`;decodedpayloadSHA256 `e632bcbcac2989182e6bd736794fa81935df4f18e90dbad118a693cf3f18a7b8`.

All5playbackpairs have exactPNGhash,1920×1080 dimensions andrequest/capture/HUDsameframe/time. Start/intermediatecaptures boundinJSON.
Cold1logprefix throughfinalverifiedF9: 40215B/374lines;SHA256 `3c39ce022094d84e86c223acff71ac8021713b7ef1ea90a126baa1a4f192b56e`.

## Scope limits

-Neithersegment is a wholehumanmatchwin; bothrecordedmenu-exitsegments havewinner-1.
-NoFPS,bridgecrossing,garrison/boarding/disembarkmedicalacceptance inferred.
-Openingsegment ends297HP/manualHeal; coldsegment independentlycontinues297→450. Do not mergehistories byconcatenatingreportedarchivedprefixes.
