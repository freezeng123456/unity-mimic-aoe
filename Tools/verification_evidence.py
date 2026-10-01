"""Bind successful Unity test reports to the exact source that was tested."""
import argparse
import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path
from xml.etree import ElementTree as ET

MANIFEST = 'source-evidence.json'
SOURCE_DIRS = ('Assets', 'Packages', 'ProjectSettings', 'Tools')


def file_hash(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def source_files(root):
    files = {}
    for directory in SOURCE_DIRS:
        for path in sorted((root / directory).rglob('*')):
            if path.is_file() and not any(part.startswith('.') or part == '__pycache__' for part in path.relative_to(root).parts):
                files[path.relative_to(root).as_posix()] = file_hash(path)
    return files


def fingerprint(files):
    return hashlib.sha256(json.dumps(files, sort_keys=True, separators=(',', ':')).encode()).hexdigest()


def report_result(path):
    node = ET.parse(path).getroot()
    total = int(node.attrib.get('total', '0'))
    if node.tag != 'test-run' or node.attrib.get('result') != 'Passed' or total <= 0 or int(node.attrib.get('passed', '0')) != total or int(node.attrib.get('failed', '0')) != 0:
        raise ValueError(f'{path.name} is not a complete passing test run')
    return total


def record(root, reports, expected):
    files = source_files(root)
    current = fingerprint(files)
    if current != expected:
        raise ValueError('Source changed while Unity tests were running; rerun the tests')
    results = {}
    for mode in ('EditMode', 'PlayMode'):
        path = reports / (mode + '.xml')
        results[mode] = {'file': path.name, 'sha256': file_hash(path), 'passed': report_result(path)}
    evidence = {'schema': 1, 'recordedAtUtc': datetime.now(timezone.utc).isoformat(), 'sourceFingerprint': current, 'files': files, 'reports': results}
    target = reports / MANIFEST
    temporary = target.with_suffix('.tmp')
    temporary.write_text(json.dumps(evidence, indent=2) + '\n')
    temporary.replace(target)
    return evidence


def verify(root, reports):
    evidence = json.loads((reports / MANIFEST).read_text())
    if evidence.get('schema') != 1:
        raise ValueError('Unsupported source evidence schema')
    files = source_files(root)
    recorded_files = evidence['files']
    if fingerprint(recorded_files) != evidence['sourceFingerprint']:
        raise ValueError('Source evidence fingerprint is inconsistent')
    if files != recorded_files:
        changed = sorted(path for path in set(files) | set(recorded_files) if files.get(path) != recorded_files.get(path))
        raise ValueError('Reports belong to different source: ' + ', '.join(changed[:8]))
    for mode in ('EditMode', 'PlayMode'):
        result = evidence['reports'][mode]
        if result['file'] != mode + '.xml':
            raise ValueError('Unexpected test report filename')
        path = reports / result['file']
        if file_hash(path) != result['sha256'] or report_result(path) != result['passed']:
            raise ValueError(f'{mode} report changed after its evidence was recorded')
    return evidence['sourceFingerprint']


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument('--record', type=Path)
    parser.add_argument('--expected-fingerprint')
    args = parser.parse_args()
    try:
        if args.record:
            if not args.expected_fingerprint:
                parser.error('--record requires --expected-fingerprint captured before the tests')
            print(record(args.root, args.record, args.expected_fingerprint)['sourceFingerprint'])
        else:
            print(fingerprint(source_files(args.root)))
    except (OSError, ValueError, KeyError, TypeError, ET.ParseError) as error:
        parser.exit(1, f'Cannot record test evidence: {error}\n')


if __name__ == '__main__':
    main()
