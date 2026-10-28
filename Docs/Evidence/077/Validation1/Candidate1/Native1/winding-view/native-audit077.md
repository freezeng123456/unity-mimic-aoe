# Native077 — independent bounded audit

**Accepted: Move arrival → after-arrival Hold → camera/F9 → menu_exit → Quit only.**

Audit UTC 2026-10-10T00:19:17.373233+00:00. JSON `native-audit077.json` SHA256 `6060428ea4747f29572f36801118b0311e8ef736637b947a0e374be547928580`. Exact index of **20 original files / 8725394 bytes** is in JSON; originals remained byte-identical during this read-only audit.

## Exact source and actual app

- Version **0.20.0**, fresh build GUID `6d3fd495bd8f48be852c1e6c62bcb02f`, match `bbe9d6ade4e14d9594af8d4aa21ca552`.
- Current recomputed **784-input** source FP `8c858fe82932c1c4909af27aea8dab71a183812902c937ee4931003f33840335` exactly equals Full2/Build1 inventories. Launch source head `a74fc436dc08c50f9d23dc506df92f7ff2942d76`; later documentation changes are outside this fingerprint.
- **Full2 actual closed exit0/source-stable: 1372/1372 EditMode + 452/452 PlayMode**, zero failures/skips/inconclusive. XML byte hashes/attributes and source-evidence map independently match. Receipt SHA `87d917ce3cde4aa4302d38152831dd938fc25658d091a61c310e551fd480154a`.
- **Build1 actual closed exit0/source-stable**, same FP. Receipt SHA `d4427d47cccebbca4af4966f678b3fbd8ec2b34b7f7eedad8193beaeadc3c718`.
- Whole actual-app manifest SHA `3ba141de6d4faa290b1009b116c2ec65e256efe92c144c79cc1cb6c56dfde0df`; independently rehashed **298 files / 191066644 bytes**, exact bytes/SHA/modes and empty symlink set. Executable SHA `52176c28bb520d6ca2ef0245c698798764f30ab6ecc0da4c3655c8ede96f40cb`, **0755**, arm64. Actual plist version/bundle and boot GUID match; read-only codesign verification exit0.
- Helper/launch argv match normal menu start coast/British vs Ottoman, Winding42 duel; no benchmark/autoplay/scenario/load/replay/max-age overrides. Original launch receipt remains `launched_not_accepted`/GUI not-performed, honestly preserving launch-time state; this audit adds the bounded actual result.

## Seven original F9 pairs

All PNG SHA values match metadata, actual IHDR is 1920×1080, request/capture/HUD frame and world time agree, match/GUID agree, application focus true. All samples show AgeI, resources **300F/300W/100C**, pop **6+0/20**, Explorer8 **450/450HP**.

|Frame|World seconds|Explorer|Zoom|Paused|
|---:|---:|---|---:|---|
|824|1.400000215|Idle|26|false|
|837|1.700000286|Moving|26|false|
|2841|35.400032043|Idle|26|false|
|2851|35.700027466|Holding|26|false|
|5366|37.000007629|Holding|16|false|
|5406|37.799995422|Holding|26|true|
|6871|38.499984741|Holding|26|false|

## Real Move, then after-arrival Hold

JSONL line **204**, t `1.600000262260437`: `Move ×1` Explorer8. Replay step16 accepted destination **(65.17864990234375,68.47880554199219)**. F837 has real partial position **(31.223262786865234,32.390708923339844)**; F2841 is exact destination/Idle/empty order.

JSONL line **484**, t `35.500030517578125`: `Hold ×1`; replay step355 accepted. **Hold was issued after Idle arrival, not during movement.** Four Holding F9 samples and final schema5 anchor retain exact position, 450HP, no queued orders. Holding F9 span **2.799957275391s**; command→last F9 **2.999954223633s** (approximately3s); command→same-position final anchor **3.699943542480s**. This is sampled stationary Hold, not continuous per-frame or army maneuverability acceptance.

Camera focus (24,28) → explorer selection (31,32) → real minimap bend focus **(78.73294830322266,68.47880554199219)**. Actual zoom **26→16→26**. F5406 is coherently paused: F9 queued just before Escape and completed after pause. Selected default F6871 and close F5366 are unpaused.

## Report and recording integrity — no playback

`Data/Reports/20261010-000507-512-bbe9d6ade4e14d9594af8d4aa21ca552-menu_exit.json` is **menu_exit**, t **39.199974060058594**, **520 events**, winner **-1**, autoplay false, speed1. P0 remains AgeI/6pop, 300/300/100, gathered/spent/refunded0 and kills/losses0. Selected final report fields for **both players** equal final anchor state. Ordinary AI combat exists globally (**33 Fire,33 Damage,5 Death**), but P0 has no Fire/Damage/Death/Heal events. No combat or natural win/loss claim.

Replay `replay-e74d71b50bb14d4da906330193af9206.replay.json` strict base64 and gzip CRC decode: **2535927 bytes**, decoded SHA `903c3ee17083c1f540c4098131a05ce9aa0f229522d70ea4181ee198b1d9d79a` equals envelope. Global replay **520 events exactly equal report JSONL**; three schema5 anchors steps0/300/392, final time/winner and both-player selected report fields match, all8 records accepted, normal→menu_exit, not incomplete. **Serialized anchor world.events arrays are empty**; their eventCount cursors are bounded against global events, not alleged embedded-prefix copies. Stored event/input/state hashes are quoted, **not independently recomputed**. No playback or deterministic Verified run was performed.

## Lifecycle and diagnostics retained

- player.log line 50: `FRONTIER_UI action=BEGIN SKIRMISH world=0.000 frame=820`
- player.log line 182: `FRONTIER_UI action=RETURN TO MENU world=39.200 frame=7474`
- player.log line 226: `FRONTIER_UI action=QUIT world=0.000 frame=8159`

Exactly7 capture_OK, one bound report and one bound replay-save marker. Root performed visible buttons; audit did not call GUI/get_app_state. Read-only PS at **2026-10-10T00:19:17.337195+00:00** confirms **PID10729 absent**, and no owned Editor/native process. **Native OS exit code remains unknown/null**; Quit marker plus absence is not wait()/exit0 evidence.

No emitted exception-header/C# compile/shader-error/Frontier capture-report-replay-save failure marker under the documented classifier in native logs. Preserve **6 native premature-thread-finalization warnings +1 PlayerConnection MemoryLeaks diagnostic**. Build retains real **licensing Access token error at line104** and **Curl error42 at line6398**, despite successful build/exit0. Exception `.cs` filenames, `FallbackError` shader names, `--continue-on-failure` flags are classified separately—not emitted exceptions. Complete classified line text remains in JSON. No blanket zero-warning claim.

## Selected original demos

- Default26: `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.20.0/Candidate1/Native1/winding-view/Data/Captures/Frontier-20261010-000457-283-f6871-4a8602e8d2334605981c24a1e6f37499.png`; SHA `a405108976c860ef614a4bc428266409d147a8bd722fac5af34e2156a3c18e78`.
- Close16: `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.20.0/Candidate1/Native1/winding-view/Data/Captures/Frontier-20261010-000431-933-f5366-80caf2d994984c149432400312b64730.png`; SHA `e9282596cf54b208f4f5f4aa342c47a85a0d54117016a9dde360bbfa489dc72a`.

Both actual originals were visually inspected: native HUD/fog and warmer inside sediment visible. **Outer contact-line polygon notches remain**, separate from blocky fog boundary. Images are unretouched, not reconstructed baseline or all-map/no-fog views; no notch-repair claim.

## Not accepted by this slice

Paid dock/ship chain; medical/healer; bridge/container/embark lifecycle; mid-move Hold or army maneuverability; combat targeting/return to battle; FPS/stress; natural full-game win/loss; replay playback/Verified; dual-Mac networking; notch repair; all four content parts complete. Controlled render studies and this narrow native smoke remain separate evidence types.

Only these two audit files were created. Original evidence, source, Unity/native/GUI, Git and cloud were untouched.
