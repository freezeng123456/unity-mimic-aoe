# ListingV2 correction — read-only preparation

Legacy Drive `search` returned only three C1 text files, while fresh default folder `fetch` returned all 12 original children including ZIPs, JSON manifests and binary parts. A short search result is therefore not complete membership evidence. The previous factory and preflight state remain preserved unchanged as diagnostic history; **start a new full-fetch preflight state** before any write.

New factory: `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.20.0/Candidate1/Planning/Drive077/ListingV2/tool-orchestration077.js`.
SHA256: `a9e5ebf3f4ce2b703a50c166a8915885353c1f14f5571619195c3e9f2d3b6c78`.
Original factory SHA remains `81274803fe817cff7cd9692ac5e6076a8ee949119cb3e90ae5db3ae834e70df2`.

Only two narrow deltas:
1. `children(id)` gets fresh folder metadata/observed URL, calls default `fetch({url})`, validates actual outer/nested wrapper ID and folder MIME, parses JSON `content.files`. Matching nested bodies must agree. The list must have fewer than 100 rows, no meaningful pagination/next/incomplete/truncated flags, no duplicate/empty IDs. Non-null row parents must match the wrapper; null/absent row parents are retained unchanged because membership is wrapper-bounded, never fabricated.
2. `finish()` hydrates candidate rows by ID from its already-fresh file metadata. Unknown extras retain their observed rows, and Python's exact child-ID set gate rejects them. Fresh file owner/parent/size/MIME gates remain authoritative; C1/project before/after compare full wrapper-bounded observed IDs.

No owner/upload/raw-byte/readback/stdin gate changed. Node syntax-only check passed. Fresh C1 metadata/default fetch was read only; actual response is preserved in `read-only-c1-folder-fetch077.json`. No factory method, cloud create/upload/move/edit/share/delete, repository source, Git or Unity execution occurred during this preparation. Old README search/exhaustiveness instructions are superseded by this correction. Load the new raw factory source explicitly, keep the preserved old state diagnostic-only, and use fresh preflight results. New remote IDs/URLs remain uncreated here.
