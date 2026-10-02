#!/usr/bin/env python3
"""Package committed source and verify the separately built macOS archive."""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import plistlib
import re
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


def verify_client_archive(client, bundle_manifest, receipt, fingerprint):
    """Verify every frozen ZIP file and independently read its Unity build GUID."""
    if bundle_manifest['schema'] != 1 or bundle_manifest['localAppMatchesArchive'] is not True:
        raise ValueError('Unrecognized or unverified client bundle manifest.')
    for key, expected in (('appVersion', receipt['appVersion']),
                          ('buildGuid', receipt['buildGuid']),
                          ('unityVersion', receipt['unityVersion']),
                          ('sourceFingerprint', fingerprint)):
        if bundle_manifest[key] != expected:
            raise ValueError('Client bundle identity mismatch: ' + key)
    if 'buildReceiptSnapshotUtf8' in bundle_manifest:
        snapshot_bytes = bundle_manifest['buildReceiptSnapshotUtf8'].encode('utf-8')
        if hashlib.sha256(snapshot_bytes).hexdigest() != bundle_manifest['sources']['buildReceipt']['sha256']:
            raise ValueError('Original build receipt snapshot SHA mismatch.')
        snapshot = json.loads(snapshot_bytes)
        for key in ('appVersion', 'buildGuid', 'unityVersion', 'sourceFingerprint', 'compiledRuntimeSha256'):
            if snapshot[key] != receipt[key]:
                raise ValueError('Build receipt identity changed since the bundle snapshot: ' + key)
    identity = bundle_manifest['sources']['clientArchive']
    if client.stat().st_size != identity['bytes'] or sha256(client) != identity['sha256']:
        raise ValueError('Client ZIP bytes/SHA differ from the frozen bundle manifest.')
    app = bundle_manifest['appName'] + '/'
    if bundle_manifest['appName'] != 'Frontier Reckoning.app':
        raise ValueError('Unexpected application root.')
    expected = bundle_manifest['archiveFiles']
    bundle = bundle_manifest['bundleFiles']
    with zipfile.ZipFile(client) as archive:
        entries = archive.infolist()
        names = [item.filename for item in entries]
        if len(names) != len(set(names)):
            raise ValueError('Duplicate client ZIP entries.')
        files = {item.filename: item for item in entries if not item.is_dir()}
        directories = sorted(item.filename for item in entries if item.is_dir())
        if set(files) != set(expected) or directories != bundle_manifest['archiveDirectories']:
            raise ValueError('Client ZIP file/directory set differs from its manifest.')
        if archive.testzip() is not None:
            raise ValueError('Client archive CRC check failed.')
        if len(files) != bundle_manifest['archiveFileCount'] or sum(item.file_size for item in files.values()) != bundle_manifest['archiveFileBytes']:
            raise ValueError('Client ZIP file count/bytes mismatch.')
        actual_bundle = {name[len(app):]: record for name, record in expected.items() if name.startswith(app)}
        if actual_bundle != bundle or len(bundle) != bundle_manifest['bundleFileCount'] or sum(item['bytes'] for item in bundle.values()) != bundle_manifest['bundleFileBytes']:
            raise ValueError('Client bundle manifest file set/count/bytes is inconsistent.')
        for name, item in files.items():
            record = expected[name]
            if item.file_size != record['bytes'] or hashlib.sha256(archive.read(item)).hexdigest() != record['sha256'] or item.external_attr >> 16 != record['unixMode']:
                raise ValueError('Client ZIP file content/mode mismatch: ' + name)
        info = plistlib.loads(archive.read(app + 'Contents/Info.plist'))
        if info['CFBundleShortVersionString'] != receipt['appVersion']:
            raise ValueError('Client bundle version mismatch.')
        for name, digest in receipt['compiledRuntimeSha256'].items():
            if bundle[name]['sha256'] != digest:
                raise ValueError('Client runtime hash mismatch: ' + name)
        boot = archive.read(app + 'Contents/Resources/Data/boot.config').decode('utf-8')
        guids = re.findall(r'^build-guid=([^\r\n]*)\r?$', boot, re.MULTILINE)
        guid = str(receipt['buildGuid']).lower()
        if not re.fullmatch(r'[0-9a-f]{32}', guid) or int(guid, 16) == 0 or guids != [guid]:
            raise ValueError('Client Unity boot.config build GUID is missing, repeated or differs from its receipt.')
    return {'bundleFileCount': len(bundle), 'archiveFileCount': len(files),
            'buildGuidReadFromBootConfig': guid, 'archiveSha256': identity['sha256']}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--repo', type=Path, default=Path(__file__).resolve().parents[3])
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--evidence-set', default='055')
    parser.add_argument('--playtest-guide', default='Docs/Playtest-055.md')
    args = parser.parse_args()
    root, output = args.repo.resolve(), args.output.resolve()
    if git(root, 'status', '--porcelain', '--untracked-files=all').strip():
        raise SystemExit('Commit all source and evidence files before packaging.')
    sys.path.insert(0, str(root / 'Tools'))
    from verification_evidence import verify
    evidence_dir = root / 'Docs/Evidence' / args.evidence_set
    tests = evidence_dir / 'Tests'
    fingerprint = verify(root, tests)
    evidence = json.loads((tests / 'source-evidence.json').read_text())
    receipt = json.loads((evidence_dir / 'build-receipt.json').read_text())
    bundle_path = evidence_dir / 'client-bundle-manifest.json'
    bundle_manifest = json.loads(bundle_path.read_text())
    if receipt['sourceFingerprint'] != fingerprint:
        raise SystemExit('Build receipt differs from tested source fingerprint.')
    commit = git(root, 'rev-parse', 'HEAD').decode().strip()
    output.mkdir(parents=True, exist_ok=True)
    version = receipt['appVersion']
    shutil.copyfile(root / args.playtest_guide, output / '试玩说明.md')
    client = output / f'Frontier-Reckoning-{version}-macOS-arm64.zip'
    try:
        client_checks = verify_client_archive(client, bundle_manifest, receipt, fingerprint)
    except (OSError, ValueError, KeyError, TypeError, zipfile.BadZipFile) as error:
        raise SystemExit('Client verification failed: ' + str(error))
    tree = git(root, 'ls-tree', '-rz', commit).decode().split('\0')
    tracked = {}
    for entry in tree:
        if not entry:
            continue
        header, name = entry.split('\t', 1)
        mode, kind, object_id = header.split(' ')
        if kind != 'blob':
            raise SystemExit('Unsupported non-file tracked entry: ' + name)
        tracked[name] = object_id
    names = list(tracked)
    source = output / f'Frontier-Reckoning-{version}-Unity-Source.zip'
    temporary = source.with_suffix('.zip.partial')
    prefix = f'Frontier-Reckoning-{version}-Source/'
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
        # Also bind Docs, original art sources and every other tracked file to
        # HEAD; pre/post clean status alone cannot detect an edit restored while
        # an archive is being written.
        for name, expected in tracked.items():
            data = archive.read(prefix + name)
            algorithm = 'sha1' if len(expected) == 40 else 'sha256' if len(expected) == 64 else None
            if algorithm is None:
                raise SystemExit('Unsupported Git object identity: ' + name)
            digest = hashlib.new(algorithm, b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest()
            if digest != expected:
                raise SystemExit('Packaged source differs from committed HEAD: ' + name)
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
        'sourceArchiveHeadFilesVerified': len(tracked),
        'clientBundleManifest': {'file': str(bundle_path.relative_to(root)), 'sha256': sha256(bundle_path), **client_checks},
        'archiveChecks': 'Both ZIP CRCs passed; every source file matched its committed Git blob; tested source SHA-256 matched; every client ZIP file bytes/SHA/mode matched the frozen full bundle manifest; plist version and independently read Unity boot.config build GUID matched.',
        'verification': receipt['verification'],
        'limitations': receipt['limitations'],
        'files': {path.name: {'bytes': path.stat().st_size, 'sha256': sha256(path)}
                  for path in [client, source, output / '试玩说明.md']},
    }
    (output / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n')
    print(json.dumps(manifest, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
