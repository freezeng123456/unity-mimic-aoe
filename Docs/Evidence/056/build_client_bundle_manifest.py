#!/usr/bin/env python3
"""Freeze a real .32 app/ZIP identity; never build, zip, or fabricate a client.

The schema matches 055/client-bundle-manifest.json. Full local .app file bytes
and Unix modes must equal ZIP entries before invoking the existing package
archive verifier. Final package commit and native smoke are separate evidence.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import importlib.util
import json
from pathlib import Path, PurePosixPath
import plistlib
import re
import stat
import subprocess
import sys
import zipfile

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[3]
EVIDENCE = ROOT / 'Docs/Evidence/056'
OLD_GUID = '128a731d440e4b7a94abfcc74b88fa97'
RUNTIME = ('Contents/Info.plist', 'Contents/MacOS/Frontier Reckoning',
           'Contents/Resources/Data/Managed/Frontier.Runtime.dll')


def module(path, name):
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


def require(condition, message):
    if not condition:
        raise ValueError(message)


def digest(path):
    h = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()


def identity(path):
    return {'path': str(path.resolve()), 'bytes': path.stat().st_size, 'sha256': digest(path)}


def local_bundle(app):
    require(app.is_dir() and not app.is_symlink() and app.name == 'Frontier Reckoning.app',
            'Use the real local Frontier Reckoning.app directory')
    result = {}
    for path in sorted(app.rglob('*')):
        require(not path.is_symlink(), 'Unsupported bundle symlink; no target bytes may be silently substituted: ' + str(path))
        if path.is_file():
            require(stat.S_ISREG(path.stat().st_mode), 'Bundle entry is not a regular file: ' + str(path))
            result[path.relative_to(app).as_posix()] = {'bytes': path.stat().st_size,
                'sha256': digest(path), 'unixMode': path.stat().st_mode}
    require(bool(result), 'App bundle is empty')
    return result


def zip_inventory(client):
    files, directories = {}, []
    with zipfile.ZipFile(client) as archive:
        entries = archive.infolist()
        require(len(entries) == len({item.filename for item in entries}), 'Duplicate client ZIP entry')
        for item in entries:
            path = PurePosixPath(item.filename)
            require(item.filename and not path.is_absolute() and '..' not in path.parts and '\\' not in item.filename,
                    'Unsafe or ambiguous ZIP path: ' + item.filename)
            allowed = item.filename.startswith('Frontier Reckoning.app/') or item.filename == '__MACOSX/' or item.filename.startswith('__MACOSX/Frontier Reckoning.app/')
            require(allowed, 'Unexpected ZIP root or auxiliary file: ' + item.filename)
            if item.is_dir():
                directories.append(item.filename)
            else:
                require(not stat.S_ISLNK(item.external_attr >> 16), 'Unsupported ZIP symlink: ' + item.filename)
                files[item.filename] = {'bytes': item.file_size,
                    'sha256': hashlib.sha256(archive.read(item)).hexdigest(), 'unixMode': item.external_attr >> 16}
        require(archive.testzip() is None, 'Client ZIP CRC failed')
    return files, sorted(directories)


def head():
    return subprocess.check_output(['git', '-C', str(ROOT), 'rev-parse', 'HEAD']).decode().strip()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for arg in ('app', 'client', 'receipt', 'source-evidence', 'output'):
        parser.add_argument('--' + arg, type=Path, required=True)
    args = parser.parse_args()
    app, client, receipt_path, source_path, output = (path.resolve() for path in
        (args.app, args.client, args.receipt, args.source_evidence, args.output))
    if output.exists():
        parser.error('Refusing to overwrite an existing output, including any previous manifest')
    if not output.is_relative_to(EVIDENCE.resolve()) or output.suffix != '.json':
        parser.error('Output must be a new JSON under Docs/Evidence/056;055 evidence is immutable')
    if receipt_path != (EVIDENCE / 'build-receipt.json').resolve() or source_path != (EVIDENCE / 'Tests/source-evidence.json').resolve():
        parser.error('Explicit receipt/source-evidence must be the independent056 build receipt and full Tests evidence')
    if output in (receipt_path, source_path):
        parser.error('Output cannot replace an input')
    parse = module(EVIDENCE / 'verify_release_equivalence.py', 'manifest_existing_strict_parser')
    sources = module(ROOT / 'Tools/verification_evidence.py', 'manifest_existing_source_verifier')
    package_path = ROOT / 'Docs/Evidence/055/package_delivery.py'
    package = module(package_path, 'manifest_existing_package_verifier')
    try:
        receipt_bytes = receipt_path.read_bytes()
        receipt = parse.parse_json(receipt_bytes)
        evidence = parse.strict_json(source_path)
        require(isinstance(receipt, dict), 'Build receipt must be a JSON object')
        require(receipt['appVersion'] == '0.2.32', 'A real independent0.2.32 build receipt is required')
        guid = receipt['buildGuid']
        require(isinstance(guid, str) and re.fullmatch(r'[0-9a-f]{32}', guid) and int(guid, 16) != 0 and guid != OLD_GUID,
                'Build GUID must be a nonzero unique .32 GUID, different from .31')
        require(set(receipt['compiledRuntimeSha256']) == set(RUNTIME), 'Receipt must identify all three expected compiled runtime files')
        require(len(evidence['files']) == 357, 'Expected the source-bound357-file release inventory')
        require(evidence['reports']['EditMode']['passed'] == 214 and evidence['reports']['PlayMode']['passed'] == 210,
                'Expected full214 EditMode/210 PlayMode evidence')
        tested = sources.verify(ROOT, source_path.parent)
        require(tested == receipt['sourceFingerprint'] == evidence['sourceFingerprint'], 'Current source/test/build fingerprints differ')
        source_before = sources.source_files(ROOT)
        observed_head = head()
        watched = {'clientArchive': client, 'buildReceipt': receipt_path, 'sourceEvidence': source_path,
            'manifestBuilder': Path(__file__), 'archiveVerifier': package_path,
            'sourceVerifier': ROOT / 'Tools/verification_evidence.py',
            'releaseEquivalenceParser': EVIDENCE / 'verify_release_equivalence.py'}
        input_ids = {label: identity(path) for label, path in watched.items()}
        require(client.is_file(), 'The separately supplied actual client ZIP is missing')
        bundle = local_bundle(app)
        archive, directories = zip_inventory(client)
        app_prefix = app.name + '/'
        archived_app = {name[len(app_prefix):]: value for name, value in archive.items() if name.startswith(app_prefix)}
        require(archived_app == bundle, 'Local .app file set/bytes/SHA/Unix modes differ from the client ZIP')
        info = plistlib.loads((app / 'Contents/Info.plist').read_bytes())
        require(info.get('CFBundleShortVersionString') == receipt['appVersion'], 'Local Info.plist version differs from receipt')
        for name, sha in receipt['compiledRuntimeSha256'].items():
            require(bundle[name]['sha256'] == sha, 'Actual local compiled runtime differs from receipt: ' + name)
        boot = (app / 'Contents/Resources/Data/boot.config').read_text(encoding='utf-8')
        guids = re.findall(r'^build-guid=([^\r\n]*)\r?$', boot, re.MULTILINE)
        require(guids == [guid], 'Independent local boot.config GUID is absent, repeated or different from receipt')
        manifest = {'schema': 1, 'generatedAtUtc': datetime.now(timezone.utc).isoformat(),
            'scope': 'Exact local .app file bytes and Unix modes matched the separately supplied actual client ZIP; all ZIP files including AppleDouble are frozen. Content identity/GUID/version verification, not a signature, gameplay or native smoke acceptance.',
            'appName': app.name, 'appVersion': receipt['appVersion'], 'buildGuid': guid,
            'unityVersion': receipt['unityVersion'], 'sourceFingerprint': tested,
            'headObservedAtManifestCreation': observed_head,
            'headScope': 'Observed checkout HEAD during manifest creation; final committed source ZIP identity is separately recorded by package_delivery.py. The build is bound to sourceFingerprint, not an inferred build commit.',
            'sources': input_ids, 'localAppRoot': str(app), 'localAppMatchesArchive': True,
            'bundleFileCount': len(bundle), 'bundleFileBytes': sum(value['bytes'] for value in bundle.values()),
            'bundleFiles': bundle, 'archiveFileCount': len(archive),
            'archiveFileBytes': sum(value['bytes'] for value in archive.values()),
            'archiveFiles': archive, 'archiveDirectories': directories,
            'auxiliaryScope': '__MACOSX AppleDouble bytes/modes are recorded and verified; they are not runtime app files.',
            'guidExtraction': {'path': 'Contents/Resources/Data/boot.config', 'field': 'build-guid', 'guid': guid, 'count': len(guids)},
            'checks': {'crcPassed': True, 'uniqueArchiveEntries': True, 'localAppFileSetAndBytesMatch': True,
                'localAppUnixModesMatch': True, 'receiptRuntimeHashesMatch': True, 'plistVersionMatchesReceipt': True,
                'bootGuidMatchesReceipt': True, 'current357SourceAnd424TestsMatchReceipt': True},
            'buildReceiptSnapshotUtf8': receipt_bytes.decode('utf-8'),
            'receiptSnapshotScope': 'Exact receipt bytes observed at manifest creation. Later acceptance wording may change the receipt, but version/GUID/sourceFingerprint/compiled-runtime identity must remain the same; original snapshot/SHA are retained.'}
        # Independent existing package code repeats full ZIP CRC/file/mode/GUID
        # verification; do not duplicate or replace its acceptance logic.
        manifest['packageArchiveVerification'] = package.verify_client_archive(client, manifest, receipt, tested)
        require(sources.source_files(ROOT) == source_before and sources.verify(ROOT, source_path.parent) == tested,
                'Source or complete test evidence changed during manifest creation')
        require(head() == observed_head, 'Git HEAD changed during manifest creation')
        require(local_bundle(app) == bundle, 'Local app file bytes/modes changed during manifest creation')
        require(all(identity(path) == input_ids[label] for label, path in watched.items()), 'Input ZIP/receipt/evidence/tools changed while manifest was being built')
        output.parent.mkdir(parents=True, exist_ok=True)
        with output.open('x', encoding='utf-8') as stream:
            stream.write(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n')
        print(json.dumps({'output': str(output), 'appVersion': receipt['appVersion'], 'buildGuid': guid,
            'bundleFiles': len(bundle), 'archiveFiles': len(archive), 'sourceFingerprint': tested,
            'packageArchiveVerification': manifest['packageArchiveVerification']}, ensure_ascii=False))
        return 0
    except (OSError, ValueError, KeyError, TypeError, zipfile.BadZipFile, plistlib.InvalidFileException, subprocess.CalledProcessError) as error:
        parser.exit(1, 'Cannot generate actual .32 bundle manifest: ' + str(error) + '\n')


if __name__ == '__main__':
    raise SystemExit(main())
