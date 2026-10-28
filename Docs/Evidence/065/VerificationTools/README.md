# Frozen file-verification tools

These four existing Python tools are byte-exact copies. They read evidence and optionally create a NEW report; they do not run Unity or control the GUI. File/hash/codec checks do not substitute for native input, gameplay, visual review, performance or cloud readback.

| Tool | SHA-256 |
| --- | --- |
| save-envelope-reader.py | `7d6b3157999d7393198b3d04ee09b621ccb5a45e203c50dffcf883def0a2cff4` |
| verify-native-captures.py | `05a2877acac10f30430225463cb9b7ecaa1e3d0786186cbf60d1792e680cd3de` |
| audit-preview-fixtures.py | `4ca42e9d6f3d667d1ee5e45126876363aaf6fc2bc4a9a615b91837c36fa34b14` |
| verify-package-contents.py | `385df69ee1236c4b86a0b3476014f560021382fdd1c129badd23017cb79d559e` |

Run against explicit paths, with output reports outside the frozen source archive. Do not rely on original machine-specific defaults. No packaging, save-slot mutation, uploading or UI helpers are distributed here.

- save-envelope-reader.py is an import-only library: read_envelope(path) returns decoded data/codec information, enforces64MiB stored/256MiB raw caps and supports legacy/schema2. It has no CLI entrypoint.
- verify-native-captures.py requires --captures and --build-receipt; use --output for a new JSON report, otherwise stdout. --project is not an option.
- audit-preview-fixtures.py: explicitly supply --project PROJECT --previews PREVIEW_DIR --tests TEST_DIR --output NEW_REPORT; --runtime-commit is optional operator provenance. Keep save-envelope-reader.py alongside it.
- verify-package-contents.py: supply --project PROJECT --output RELEASE_DIR --testdir TEST_DIR --buildapp ORIGINAL_APP --gallery-dir GALLERY_DIR --client-manifest EMBEDDED_MANIFEST --client-guide GUIDE. Optional --client-receipt and --report NEW_REPORT override receipt/report paths. Existing reports are refused. It reads Tools/package-source.py from PROJECT as the source allowlist; that repository tool remains separately included in the source payload.

Diagnostics/save-envelope-helper-tests-aligned.json records17 synthetic file-only checks, not a Unity/runtime/native pass. Diagnostics/legacy-preview-reader-audit-aligned.json records4 earlier schema1 controlled preview files (old eb19/fdf7/952 identity), proving legacy-reader alignment only; old fixtures themselves and player save slots are not shipped here. These diagnostic receipts reference other external helpers that are deliberately not copied. Current final schema2 exports and958 Unity/native evidence have separate archives.
