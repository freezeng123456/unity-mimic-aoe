# ListingV3 — authenticated reference schema only

Prepared external variant; no new helper or factory method was executed here, and this agent performed no cloud write/reupload. Root's live raw-fetch schema identified `file_uri` as an object with `download_url`, opaque connector `file_id`, `file_name`, `mime_type`. The connector artifact ID is **not** the Drive ID. No signed URL/reference is persisted in these files.

Load `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.20.0/Candidate1/Planning/Drive077/ListingV3/tool-orchestration077.js` (SHA256 `3c7353d5475186a9f5696e3d1cfc629ad86521b4bb892edd4417cf3e14107166`). It points to the new `drive077-local.py` in this directory (SHA256 `a138309049dfacd7e7cf8edcf3bb8d1b5970fce849c569141a680451b02addeb`); deeper `BASE.parents[3]` resolves Candidate1 correctly.

Narrow corrections:
- `readBackOne` binds actual top-level `raw.id` to fresh Drive metadata `id`; non-null top-level filename/MIME must equal the immutable plan.
- Python accepts exact returned `file_uri.download_url` (and legacy url/uri), validates an optional artifact `file_id` only as a nonempty string, and binds optional object filename/MIME to the plan before download. It never guesses an artifact-ID/Drive-ID equivalence.
- All existing HTTPS/curl-stdin/whole-byte SHA+size/private-parent/owner/exact-set checks remain. No raw URI logging. V2 full folder-fetch enumeration and finish hydration are retained.

Python AST and bundled Node syntax-only checks passed. Original Python79c28… and ListingV2 factorya9e5… remain unchanged. Root may reuse its already verified current state and known uploaded IDs with a fresh readback attempt directory; no replacement upload is needed merely to retry binary readback. This variant does not modify immutable packages or their manifests and makes no new remote-delivery acceptance claim.
