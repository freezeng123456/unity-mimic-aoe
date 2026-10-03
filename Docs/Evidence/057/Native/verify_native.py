#!/usr/bin/env python3
"""Verify this self-contained native archive; optionally compare original source bytes."""
import argparse
import collections
import gzip
import hashlib
import json
from pathlib import Path


def sha(data):
    return hashlib.sha256(data).hexdigest()


def require(ok, message):
    if not ok:
        raise ValueError(message)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, help='Original Frontier Reckoning support directory')
    parser.add_argument('--log-source', type=Path, help='Original Native-0.3.0 logs directory')
    args = parser.parse_args()
    root = Path(__file__).resolve().parent
    manifest_bytes = (root / 'manifest.json').read_bytes()
    expected_sha = (root / 'manifest.sha256').read_text().split()[0]
    require(sha(manifest_bytes) == expected_sha, 'manifest SHA-256 mismatch')
    manifest = json.loads(manifest_bytes)
    actual = {str(p.relative_to(root)) for p in root.rglob('*') if p.is_file()}
    expected = {f['path'] for f in manifest['files']} | {'manifest.json', 'manifest.sha256'}
    require(actual == expected, 'Archive file inventory differs from manifest')
    for f in manifest['files']:
        data = (root / f['path']).read_bytes()
        require(len(data) == f['bytes'] and sha(data) == f['sha256'], f"File mismatch: {f['path']}")
        original = gzip.decompress(data) if 'rawSha256' in f else data
        if 'rawSha256' in f:
            require(len(original) == f['rawBytes'] and sha(original) == f['rawSha256'], f"Raw events mismatch: {f['path']}")
        source = args.log_source if f.get('sourceCategory') == 'Logs' else args.source
        if source is not None and f.get('sourcePath'):
            require((source / f['sourcePath']).read_bytes() == original, f"Original source bytes differ: {f['path']}")
    reports = sorted((root / 'Reports').glob('*.json'))
    captures = sorted((root / 'Captures').glob('*.json'))
    require(len(reports) == 5 and len(captures) == 15, 'Expected 5 reports and 15 captures')
    by_match = collections.defaultdict(list)
    for path in reports:
        report = json.loads(path.read_bytes())
        require(report['buildGuid'] == manifest['buildGuid'] and report['appVersion'] == manifest['appVersion'], f'Wrong build: {path.name}')
        raw = gzip.decompress(path.with_name(path.stem + '-events.jsonl.gz').read_bytes())
        events = [json.loads(line) for line in raw.splitlines()]
        require(len(events) == report['eventCount'], f'Event count mismatch: {path.name}')
        ended = [e for e in events if e['kind'] == 'MatchEnded']
        resigned = [e for e in events if e['kind'] == 'Resign' or (e['kind'] == 'Command' and e['message'].startswith('Resign'))]
        require(not resigned, f'Unexpected resign: {path.name}')
        if report['autoplay']:
            require(report['simulationSpeed'] == 16 and len(ended) == 1, f'Autoplay ending mismatch: {path.name}')
            require(ended[0]['player'] == report['winner'] and ended[0]['message'] == 'All opposing units and buildings eliminated', f'Ending result mismatch: {path.name}')
        else:
            require(report['simulationSpeed'] == 1 and not ended and report['winner'] == -1 and report['players'][0]['age'] == 1, 'Manual opening scope differs')
        by_match[report['matchId']].append((report['reason'], sha(raw)))
    require(len(by_match) == 3, 'Expected 3 distinct match IDs')
    for group in by_match.values():
        require(len({row[1] for row in group}) == 1, 'Different event bytes for repeated match reports')
        require(sorted(row[0] for row in group) in [['match_finished', 'menu_exit'], ['quit']], 'Unexpected report reasons')
    for path in captures:
        capture = json.loads(path.read_bytes())
        require(capture['buildGuid'] == manifest['buildGuid'] and capture['appVersion'] == manifest['appVersion'], f'Wrong capture build: {path.name}')
        png = path.with_suffix('.png').read_bytes()
        require(sha(png) == capture['pngSha256'], f'Capture metadata PNG mismatch: {path.name}')
        require(not capture['matchId'] or capture['matchId'] in by_match, f'Unknown captured match: {path.name}')
    print(json.dumps({'verified': True, 'payloadFiles': 43, 'manifestedFiles': len(manifest['files']), 'totalFiles': len(actual), 'reports': len(reports), 'eventArchives': len(reports), 'captures': len(captures), 'distinctMatches': len(by_match), 'naturalAutoplayMatches': 2, 'manualOpeningMatches': 1, 'manifestSha256': expected_sha, 'originalSourceBytesCompared': args.source is not None, 'originalLogBytesCompared': args.log_source is not None}, indent=2))


if __name__ == '__main__':
    main()
