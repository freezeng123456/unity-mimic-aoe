#!/usr/bin/env python3
"""Package committed source and verify the separately built macOS archive."""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import plistlib
import shutil
import subprocess
import sys
import zipfile


def sha256(path):
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(chunk)
    return digest.hexdigest()


def git(root, *args):
    return subprocess.check_output(['git', '-C', str(root), *args])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--repo', type=Path, default=Path(__file__).resolve().parents[3])
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    root, output = args.repo.resolve(), args.output.resolve()
    if git(root, 'status', '--porcelain', '--untracked-files=all').strip():
        raise SystemExit('Commit all source and evidence files before packaging.')
    sys.path.insert(0, str(root / 'Tools'))
    from verification_evidence import verify
    tests = root / 'Docs/Evidence/054/Tests'
    fingerprint = verify(root, tests)
    evidence = json.loads((tests / 'source-evidence.json').read_text())
    receipt = json.loads((root / 'Docs/Evidence/054/build-receipt.json').read_text())
    if receipt['sourceFingerprint'] != fingerprint:
        raise SystemExit('Build receipt differs from tested source fingerprint.')
    commit = git(root, 'rev-parse', 'HEAD').decode().strip()
    output.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(root / 'Docs/Playtest-054.md', output / '试玩说明.md')
    client = output / 'Frontier-Reckoning-0.2.30-macOS-arm64.zip'
    with zipfile.ZipFile(client) as archive:
        if archive.testzip() is not None:
            raise SystemExit('Client archive CRC check failed.')
        app = 'Frontier Reckoning.app/'
        info = plistlib.loads(archive.read(app + 'Contents/Info.plist'))
        if info['CFBundleShortVersionString'] != receipt['appVersion']:
            raise SystemExit('Client bundle version mismatch.')
        for name, expected in receipt['compiledRuntimeSha256'].items():
            if hashlib.sha256(archive.read(app + name)).hexdigest() != expected:
                raise SystemExit('Client runtime hash mismatch: ' + name)
    names = git(root, 'ls-tree', '-rz', '--name-only', commit).decode().split('\0')
    names = [name for name in names if name]
    source = output / 'Frontier-Reckoning-0.2.30-Unity-Source.zip'
    temporary = source.with_suffix('.zip.partial')
    prefix = 'Frontier-Reckoning-0.2.30-Source/'
    with zipfile.ZipFile(temporary, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for name in names:
            if name.split('/')[0] in {'.git', 'Library', 'Temp', 'Logs', 'Builds', 'obj'}:
                raise SystemExit('Cache or build file tracked unexpectedly: ' + name)
            archive.write(root / name, prefix + name)
    with zipfile.ZipFile(temporary) as archive:
        if archive.testzip() is not None:
            raise SystemExit('Source archive CRC check failed.')
        for name, expected in evidence['files'].items():
            if hashlib.sha256(archive.read(prefix + name)).hexdigest() != expected:
                raise SystemExit('Packaged source differs from tested source: ' + name)
    if git(root, 'status', '--porcelain', '--untracked-files=all').strip():
        raise SystemExit('Source or evidence files changed during packaging.')
    if git(root, 'rev-parse', 'HEAD').decode().strip() != commit:
        raise SystemExit('HEAD changed during packaging.')
    temporary.replace(source)
    manifest = {
        'schema': 1,
        'generatedAtUTC': datetime.now(timezone.utc).isoformat(),
        'repository': 'freezeng123456/unity-mimic-aoe',
        'branch': git(root, 'branch', '--show-current').decode().strip(),
        'commit': commit,
        'pullRequest': 'https://github.com/freezeng123456/unity-mimic-aoe/pull/1',
        'appVersion': receipt['appVersion'],
        'buildGuid': receipt['buildGuid'],
        'unityVersion': receipt['unityVersion'],
        'platform': receipt['platform'],
        'architecture': receipt['architecture'],
        'buildOptions': receipt['buildOptions'],
        'sourceFingerprint': fingerprint,
        'sourceArchiveFileCount': len(names),
        'sourceArchiveFrozenFilesVerified': len(evidence['files']),
        'archiveChecks': 'Both ZIP CRCs passed; tested source SHA-256 and selected compiled runtime SHA-256 matched.',
        'verification': receipt['verification'],
        'limitations': receipt['limitations'],
        'files': {path.name: {'bytes': path.stat().st_size, 'sha256': sha256(path)}
                  for path in [client, source, output / '试玩说明.md']},
    }
    (output / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n')
    print(json.dumps(manifest, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
