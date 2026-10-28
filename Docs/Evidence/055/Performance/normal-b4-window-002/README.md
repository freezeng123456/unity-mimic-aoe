# B4 second native F8 performance window

This is the second F8 window of normal player match `fd3381d7dfee44cfb12332568c767c10`, with autoplay false, benchmark false and simulation speed 1x. Raw capture and verifier are archived byte-for-byte. Byte sizes and SHA-256 hashes are recorded in `frame-only-audit.json`. Build version is 0.2.31, GUID `128a731d440e4b7a94abfcc74b88fa97`, native resolution 3456x1924.

The capture starts at world time 427.2169 seconds and automatically stops at the 600.0173-second wall limit, with world time 619.2958 seconds. Of 30,998 raw frames, 11,376 have both current and previous frames focused and unpaused; 19,622 are excluded by pauses or pause transitions. All recorded frames are focused. Independently recomputed eligible active duration, mean, P95 and P99 are in the audit JSON.

Only raw frame intervals are checked at this stage. The ongoing match has no final associated report and Spawn/Death journal yet, so identity association, actual unit counts and memory samples await full verification. This window has fewer than 300 eligible active seconds and does not complete the planned long-duration performance gate.

## Final report association

The natural B4 match report and raw Spawn/Death journal are now archived alongside this capture. `summary.json` passes all identity/frame/count checks, without a minimum-duration requirement. The earlier `frame-only-audit.json` is preserved as a historical preliminary result; its pending status describes that earlier stage. Neither this window nor adding another short window meets one 300-second capture requirement.
