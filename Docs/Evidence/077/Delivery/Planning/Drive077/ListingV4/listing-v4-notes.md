# ListingV4 — one provider-normalized first evidence part

Prepared external variant only. Root already uploaded the first evidence part once; provider metadata chose `application/x-zip` instead of requested `application/octet-stream`. No repeat upload is needed. This agent made no network/cloud write/helper-execution call while preparing this correction; all originals, immutable packages and upload-plan bytes remain unchanged.

Factory: `/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.20.0/Candidate1/Planning/Drive077/ListingV4/tool-orchestration077.js` SHA `88bcd10d1ec2766e0a2f6b73f2ce45f9391330b5576383080c86bb5b03535401`.
Helper: same directory `drive077-local.py` SHA `53e65bb7effbffe5a06f2a7fe869a9567d8e743f554c04c66e1a12800d229580`.
Python AST and bundled Node syntax-only checks passed.

`verify-plan` returns a derived in-memory `providerMimeException` fact, without writing the plan. It is tied to the unchanged hash-bound transport manifest, exact first ordered `frontier-reckoning-0.20.0-evidence077.zip.part-001`, requested octet MIME, declared bytes/SHA, exact package path, full actual local part bytes/SHA and PK0304 header. Preparation independently observed manifest order1, size67,108,863 and header504b0304; the helper's future gate hashes the entire part.

Only that exact part may use observed `application/x-zip`. Other files and parts retain exact planned MIME. Accepted fresh metadata MIME binds binary raw wrapper/reference MIME; Drive ID, filename, owner, parent, full downloaded bytes/SHA and final remote concatenation remain strict. Each new readback receipt records `requestedMime`, `observedMime`, `providerMimeNormalized`, alongside unmodified observed metadata.

Resume root's existing state and five completed readbacks with the newly loaded factory. For the one explicit known uploaded first-part ID:

```js
const p = await D.plan();
const state = await D.reconcileReadBackOne(
  p, load("drive077State"), p.providerMimeException.file,
  KNOWN_OBSERVED_UPLOADED_ID, FRESH_ABSOLUTE_READBACK_DIRECTORY
);
store("drive077State",state);
```

The method clones pending uploads/readbacks, validates fresh known-ID metadata, fetches raw bytes and completes full SHA before adding the known ID and receipt to the original accepted state. A failure retains the pending artifact/receipt and original journal; it does not add metadata-only acceptance or issue an upload. Existing receipts are retained. Normal future `uploadOne` still has its explicit write gate and duplicate prevention; all V2 listing and V3 reference identity checks remain.
