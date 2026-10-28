# ListingV6 — narrow connector artifact-name aliases

Prepared external variant only; this agent performed no helper invocation, network request, cloud write or reupload. V5 scripts, current plan, immutable packages and known IDs remain unchanged.

Factory: `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.20.0/Candidate1/Planning/Drive077/ListingV6/tool-orchestration077.js` SHA256 `934b80072e3e4adee9aa17461dcd45688c0be4d2e2e996b475879285df2787b8`.
Helper: same directory `drive077-local.py` SHA256 `2bdf3559290f4fa3aa649e2d422634866bbb350277b09864c8ee2cd10e86bc0e`.
Python AST and bundled Node syntax-only checks passed.

Root observed two separate identities: Drive metadata title and connector materialization artifact filename. Drive title remains strictly equal to the plan. The binary fetch wrapper's Drive ID equals fresh metadata ID; opaque file_uri.file_id is not equated with Drive ID. Wrapper and reference filenames bind to one observed artifactName, passed only via stdin and recorded as connectorArtifactName.

Accepted connector artifact names:
- Exact planned filename for every payload.
- Planned filename + `.bin` only for hash-bound, manifest-ordered evidence transport parts with requested and observed MIME `application/octet-stream`.
- Exactly `checksums.sha256.txt` only for the exact `checksums.sha256` payload with requested and observed MIME `text/plain`.

No other extension alias is accepted. First-part-only provider MIME normalization (`application/x-zip` instead of requested octet) remains separately bound to unchanged transport order1/name/path/bytes/SHA and actual local PK0304 header. Other metadata MIME remains exact.

Resume existing state and receipts. Already recorded known part002/checksums IDs use ordinary `D.readBackOne` with fresh attempt directories, not upload. Declared part003 uses ordinary upload/readBack; `.bin` is accepted only if its actual observed MIME is octet. No new Drive title or MIME is guessed from an artifact suffix. Full raw-body size/SHA, owner-only/exact-parent/child-ID set and complete evidence-part concatenation checks remain unchanged. These prepared files make no remote delivery success claim.
