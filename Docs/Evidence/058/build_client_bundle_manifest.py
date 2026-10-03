#!/usr/bin/env python3
"""Bind a real 0.3.1 app and prebuilt ZIP to 058 source/test/build evidence.

This script never builds, zips, edits receipts, or claims gameplay acceptance.
Its schema-1 output is consumed by 055/package_delivery.py --evidence-set 058.
Run only after the actual build, successful test evidence and receipt exist.
"""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
import math
from pathlib import Path
import plistlib
import re
import subprocess
import sys
from xml.etree import ElementTree as ET
import zipfile

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[3]
EVIDENCE = ROOT / 'Docs/Evidence/058'
RUNTIME = ('Contents/Info.plist', 'Contents/MacOS/Frontier Reckoning',
           'Contents/Resources/Data/Managed/Frontier.Runtime.dll')


def module(path, name):
    spec = importlib.util.spec_from_file_location(name, path)
    loaded = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(loaded)
    return loaded


def require(condition, message):
    if not condition:
        raise ValueError(message)


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result, 'Duplicate JSON key: ' + key)
        result[key] = value
    return result


def finite_float(value):
    result = float(value)
    require(math.isfinite(result), 'Non-finite JSON number: ' + value)
    return result


def reject_constant(value):
    raise ValueError('Non-finite JSON constant: ' + value)


def parse_json(data):
    result = json.loads(data, object_pairs_hook=unique_object,
                        parse_float=finite_float, parse_constant=reject_constant)
    require(isinstance(result, dict), 'Expected a JSON object')
    return result


def head():
    return subprocess.check_output(['git', '-C', str(ROOT), 'rev-parse', 'HEAD']).decode().strip()


def bundle_directories(app):
    return sorted([app.name + '/'] + [app.name + '/' + path.relative_to(app).as_posix() + '/'
                  for path in app.rglob('*') if path.is_dir()])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for argument in ('app', 'client', 'receipt', 'source-evidence', 'output'):
        parser.add_argument('--' + argument, type=Path, required=True)
    args = parser.parse_args()
    app, client, receipt_path, source_path, output = (path.resolve() for path in
        (args.app, args.client, args.receipt, args.source_evidence, args.output))
    if output.exists():
        parser.error('Refusing to overwrite an existing manifest')
    if output != (EVIDENCE / 'client-bundle-manifest.json').resolve():
        parser.error('Output must be Docs/Evidence/058/client-bundle-manifest.json')
    if receipt_path != (EVIDENCE / 'build-receipt.json').resolve():
        parser.error('Receipt must be the independent 058 build-receipt.json')
    if source_path != (EVIDENCE / 'Tests/source-evidence.json').resolve():
        parser.error('Source evidence must be 058/Tests/source-evidence.json')
    helper_path = ROOT / 'Docs/Evidence/056/build_client_bundle_manifest.py'
    source_verifier_path = ROOT / 'Tools/verification_evidence.py'
    package_path = ROOT / 'Docs/Evidence/055/package_delivery.py'
    try:
        # Reuse only generic inventory/hash routines; no .32 equivalence logic runs.
        helper = module(helper_path, 'bundle058_generic_inventory')
        sources = module(source_verifier_path, 'bundle058_source_verifier')
        package = module(package_path, 'bundle058_package_verifier')
        receipt_bytes = receipt_path.read_bytes()
        receipt = parse_json(receipt_bytes)
        evidence = parse_json(source_path.read_bytes())
        require(receipt['appVersion'] == '0.3.1', 'Expected an actual 0.3.1 build receipt')
        guid = receipt['buildGuid']
        require(isinstance(guid, str) and re.fullmatch(r'[0-9a-f]{32}', guid)
                and int(guid, 16) != 0, 'Expected a nonzero lowercase Unity build GUID')
        require(set(receipt['compiledRuntimeSha256']) == set(RUNTIME),
                'Receipt must identify the plist, executable and Frontier.Runtime.dll')
        require(all(isinstance(value, str) and re.fullmatch(r'[0-9a-f]{64}', value)
                    for value in receipt['compiledRuntimeSha256'].values()), 'Invalid runtime SHA-256')
        require(type(evidence.get('schema')) is int and evidence['schema'] == 1,
                'Unsupported source-evidence schema')
        require(isinstance(evidence['files'], dict) and evidence['files'], 'Empty tested source inventory')
        for mode in ('EditMode', 'PlayMode'):
            count = evidence['reports'][mode]['passed']
            require(type(count) is int and count > 0, 'Missing passing ' + mode + ' result')
        tested = sources.verify(ROOT, source_path.parent)
        require(tested == receipt['sourceFingerprint'] == evidence['sourceFingerprint'],
                'Current source, successful tests and build receipt fingerprints differ')
        source_before = sources.source_files(ROOT)
        observed_head = head()
        watched = {'clientArchive': client, 'buildReceipt': receipt_path, 'sourceEvidence': source_path,
                   'manifestBuilder': Path(__file__), 'inventoryHelper': helper_path,
                   'archiveVerifier': package_path, 'sourceVerifier': source_verifier_path}
        for mode in ('EditMode', 'PlayMode'):
            watched[mode + 'Report'] = source_path.parent / (mode + '.xml')
        for previous in ('055', '056', '057'):
            path = ROOT / 'Docs/Evidence' / previous / 'build-receipt.json'
            require(guid != parse_json(path.read_bytes())['buildGuid'], 'Reused historical build GUID: ' + previous)
            watched['historicalReceipt' + previous] = path
        input_ids = {label: helper.identity(path) for label, path in watched.items()}
        require(client.is_file(), 'The separately built client ZIP is missing')
        bundle = helper.local_bundle(app)
        archive, directories = helper.zip_inventory(client)
        prefix = app.name + '/'
        archived_app = {name[len(prefix):]: value for name, value in archive.items() if name.startswith(prefix)}
        require(archived_app == bundle, 'App/ZIP file sets, bytes, SHA-256 or Unix modes differ')
        local_directories = bundle_directories(app)
        require([name for name in directories if name.startswith(prefix)] == local_directories,
                'Local app and ZIP directory sets differ (preserve bundle directories in the ZIP)')
        info = plistlib.loads((app / 'Contents/Info.plist').read_bytes())
        require(info.get('CFBundleShortVersionString') == receipt['appVersion'], 'Plist version differs from receipt')
        require(str(info.get('CFBundleVersion')) == str(receipt['bundleBuildNumber']),
                'Plist build number differs from receipt')
        for name, expected in receipt['compiledRuntimeSha256'].items():
            require(bundle[name]['sha256'] == expected, 'Compiled runtime differs from receipt: ' + name)
        require(receipt['bundleFileCount'] == len(bundle) and receipt['bundleFileBytes'] ==
                sum(item['bytes'] for item in bundle.values()), 'Actual bundle count/bytes differ from receipt')
        boot = (app / 'Contents/Resources/Data/boot.config').read_text(encoding='utf-8')
        guids = re.findall(r'^build-guid=([^\r\n]*)\r?$', boot, re.MULTILINE)
        require(guids == [guid], 'Actual boot.config GUID is missing, repeated or differs from receipt')
        manifest = {
            'schema': 1, 'generatedAtUtc': datetime.now(timezone.utc).isoformat(),
            'scope': 'Exact actual app/ZIP file bytes, SHA-256, Unix modes and directory sets matched; current source and passing test evidence match the build receipt. No signature, gameplay, performance or native-smoke acceptance is inferred.',
            'appName': app.name, 'appVersion': receipt['appVersion'], 'buildGuid': guid,
            'unityVersion': receipt['unityVersion'], 'sourceFingerprint': tested,
            'headObservedAtManifestCreation': observed_head,
            'headScope': 'Observed checkout HEAD, not an inferred build commit. Final source archive HEAD is separately recorded by package_delivery.py.',
            'sources': input_ids, 'localAppRoot': str(app), 'localAppMatchesArchive': True,
            'bundleFileCount': len(bundle), 'bundleFileBytes': sum(item['bytes'] for item in bundle.values()),
            'bundleFiles': bundle, 'localAppDirectories': local_directories,
            'archiveFileCount': len(archive), 'archiveFileBytes': sum(item['bytes'] for item in archive.values()),
            'archiveFiles': archive, 'archiveDirectories': directories,
            'auxiliaryScope': '__MACOSX AppleDouble bytes/modes are frozen archive entries, not runtime app files.',
            'guidExtraction': {'path': 'Contents/Resources/Data/boot.config', 'field': 'build-guid', 'guid': guid, 'count': len(guids)},
            'testedSourceFileCount': len(source_before),
            'testCounts': {mode: evidence['reports'][mode]['passed'] for mode in ('EditMode', 'PlayMode')},
            'checks': {'crcPassed': True, 'uniqueArchiveEntries': True, 'localAppFileSetAndBytesMatch': True,
                       'localAppUnixModesMatch': True, 'localAppDirectoriesMatch': True,
                       'receiptRuntimeHashesMatch': True, 'plistVersionAndBuildNumberMatchReceipt': True,
                       'bootGuidMatchesReceipt': True, 'newGuidDiffersFrom055And056And057': True,
                       'currentSourceAndPassingTestsMatchReceipt': True},
            'buildReceiptSnapshotUtf8': receipt_bytes.decode('utf-8'),
            'receiptSnapshotScope': 'Exact receipt bytes observed here. Later acceptance prose may change, but the packaging verifier requires the version, GUID, Unity version, source fingerprint and compiled runtime hashes to remain identical.'}
        manifest['packageArchiveVerification'] = package.verify_client_archive(client, manifest, receipt, tested)
        require(sources.source_files(ROOT) == source_before and sources.verify(ROOT, source_path.parent) == tested,
                'Source or successful test evidence changed during verification')
        require(head() == observed_head, 'Git HEAD changed during verification')
        require(helper.local_bundle(app) == bundle and bundle_directories(app) == local_directories,
                'Actual app changed during verification')
        require(all(helper.identity(path) == input_ids[label] for label, path in watched.items()),
                'Input archive, receipt, test report or verifier changed during verification')
        with output.open('x', encoding='utf-8') as stream:
            stream.write(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n')
        print(json.dumps({'output': str(output), 'appVersion': receipt['appVersion'], 'buildGuid': guid,
                          'bundleFiles': len(bundle), 'archiveFiles': len(archive), 'sourceFingerprint': tested,
                          'packageArchiveVerification': manifest['packageArchiveVerification']}, ensure_ascii=False))
        return 0
    except (OSError, ValueError, KeyError, TypeError, zipfile.BadZipFile, plistlib.InvalidFileException,
            subprocess.CalledProcessError, ET.ParseError) as error:
        parser.exit(1, 'Cannot generate actual 0.3.1 bundle manifest: ' + str(error) + '\n')


if __name__ == '__main__':
    raise SystemExit(main())
