#!/usr/bin/env python3
"""Archive one completed audit without altering its results or raw evidence."""
import argparse
import gzip
import hashlib
import json
from pathlib import Path
import shutil


def sha(data):
    return hashlib.sha256(data).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source', type=Path)
    parser.add_argument('destination', type=Path)
    args = parser.parse_args()
    matrix = json.loads((args.source / 'matrix.json').read_bytes())
    if matrix['status'] == 'running' or any(c['status'] in ('running', 'pending') for c in matrix['cases']):
        raise ValueError('Audit is still in progress')
    if args.destination.exists():
        raise ValueError('Refusing to overwrite an archive')
    args.destination.mkdir(parents=True)
    inventory = []
    for source in sorted(args.source.rglob('*')):
        if not source.is_file():
            continue
        relative = source.relative_to(args.source)
        raw = source.read_bytes()
        compressed = source.suffix == '.jsonl'
        target = args.destination / (str(relative) + '.gz' if compressed else str(relative))
        target.parent.mkdir(parents=True, exist_ok=True)
        if compressed:
            payload = gzip.compress(raw, compresslevel=6, mtime=0)
            assert gzip.decompress(payload) == raw
            target.write_bytes(payload)
            inventory.append({'rawPath': str(relative), 'rawBytes': len(raw), 'rawSha256': sha(raw),
                              'gzipPath': str(target.relative_to(args.destination)),
                              'gzipBytes': len(payload), 'gzipSha256': sha(payload)})
        else:
            shutil.copy2(source, target)
            assert target.read_bytes() == raw
    (args.destination / 'compressed-events.json').write_text(json.dumps({
        'schema': 1,
        'note': 'Lossless gzip archive. Original matrix and reports are unchanged. Decompress to rawPath before checking their original event hashes; every archive round trip was compared byte-for-byte.',
        'files': inventory}, indent=2) + '\n')
    print(json.dumps({'archive': str(args.destination), 'status': matrix['status'],
                      'cases': len(matrix['cases']), 'sourcesStable': matrix['sourcesStable'],
                      'eventArchives': len(inventory)}))


if __name__ == '__main__':
    main()
