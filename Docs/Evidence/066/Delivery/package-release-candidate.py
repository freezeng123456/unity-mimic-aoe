#!/usr/bin/env python3
"""Prepare Frontier 0.9 packaging commands; no mutation without --execute.

Adapts the allowlisted Tools/package-source.py flow without editing the source
checkout. Packages are exclusive new files, with Unix modes and symlink bytes.
Identity manifests embedded in a ZIP never contain ZIP hashes. The outer release
manifest is created only after the independent package verifier has passed.
No Unity, native application, upload, signing or notarization is performed.
"""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import stat
import tempfile
import zipfile

VERSION = '0.9.0'
EVIDENCE = '066'
CLIENT_ROOT = 'Frontier-Reckoning-' + VERSION
ZIP_NAMES = {'source': CLIENT_ROOT + '-source.zip',
             'client': CLIENT_ROOT + '-macOS-arm64.zip',
             'gallery': CLIENT_ROOT + '-gallery.zip'}


def need(condition, reason):
    if not condition:
        raise ValueError(reason)


def load_verifier(output):
    path = output / 'verify-package-contents.py'
    spec = importlib.util.spec_from_file_location('frontier_090_verifier', path)
    need(spec is not None and spec.loader is not None, 'Missing package verifier')
    verifier = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(verifier)
    need(verifier.VERSION == VERSION and verifier.EVIDENCE_VERSION == EVIDENCE,
         'The verifier must be the 0.9.0 / 066 helper')
    return verifier


def frozen_evidence(args, verifier):
    # Uses the same strict XML, per-file and canonical fingerprint checks as the
    # independent verifier. Stale earlier test receipts cannot authorize packaging.
    return verifier.verify_tests(args, {'checks': {}})


def identity(args, verifier, evidence):
    receipt = verifier.parse_json(args.client_receipt.read_bytes())
    app = verifier.app_identity(args.buildapp, {'checks': {}})
    need(receipt.get('schema') == 1 and receipt.get('appVersion') == VERSION,
         'A final 0.9.0 build receipt is required')
    need(receipt.get('sourceFingerprint') == evidence['sourceFingerprint']
         and receipt.get('sourceFileCount') == len(evidence['files'])
         and receipt.get('sourceFingerprintVerifiedAfterBuild') is True,
         'Build receipt is not bound to the frozen runtime source')
    need(receipt.get('architecture') == app['architecture'] == 'arm64'
         and str(receipt.get('buildGuid', '')).lower() == app['buildGuid'],
         'Build GUID / architecture mismatch')
    need(receipt.get('binarySha256') == app['executableSha256']
         and receipt.get('bootConfigSha256') == app['bootConfigSha256'],
         'Binary / boot.config mismatch')
    need(bool(re.fullmatch(r'[0-9a-f]{40}', receipt.get('runtimeCommit', ''))),
         'A runtime commit identity is required')
    need(isinstance(receipt.get('buildType'), str) and bool(receipt['buildType']),
         'The receipt must state the actual build type')
    for mode in ('EditMode', 'PlayMode'):
        need(receipt.get('tests', {}).get(mode) == evidence['reports'][mode],
             'Build receipt test binding mismatch: ' + mode)
    counts = {mode: evidence['reports'][mode]['passed'] for mode in ('EditMode', 'PlayMode')}
    counts.update(total=sum(counts.values()), failed=0)
    return {'appVersion': VERSION, 'runtimeCommit': receipt['runtimeCommit'],
            'sourceFingerprint': evidence['sourceFingerprint'],
            'sourceFiles': len(evidence['files']), 'buildGuid': app['buildGuid'],
            'build': 'macOS arm64 / ' + receipt['buildType'] + ' / Unity ' + receipt['unityVersion'],
            'tests': counts,
            'scope': 'Identity snapshot. ZIP hashes and delivery status are recorded outside the payload after packaging.'}


def write_new_json(path, value):
    need(not path.exists(), 'Refusing to overwrite ' + str(path))
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open('x', encoding='utf-8') as stream:
        stream.write(json.dumps(value, ensure_ascii=False, indent=2) + '\n')


def stage_client(args, verifier, evidence):
    snapshot = identity(args, verifier, evidence)
    app_target = args.client_dir / args.buildapp.name
    licenses = args.client_dir / 'Licenses'
    receipt = args.client_dir / 'client-build.json'
    manifest = args.client_dir / 'release-manifest.json'
    guide = args.client_dir / 'README-zh.md'
    need(guide.is_file() and not guide.is_symlink(), 'Prepare README-zh.md in the client staging root first')
    need(not any(p.exists() or p.is_symlink() for p in (app_target, licenses, receipt, manifest)),
         'Client app, licenses or identity already staged; inspect them instead of overwriting')
    plan = {'action': 'stage-client', 'execute': args.execute, 'clientRoot': str(args.client_dir),
            'appFrom': str(args.buildapp), 'receiptFrom': str(args.client_receipt), 'identity': snapshot,
            'guideSha256': verifier.digest(guide)}
    if not args.execute:
        return plan
    original = verifier.tree_inventory(args.buildapp)
    args.client_dir.mkdir(parents=True, exist_ok=True)
    shutil.copytree(args.buildapp, app_target, symlinks=True, copy_function=shutil.copy2)
    shutil.copytree(args.project / 'Docs/Licenses', licenses, symlinks=True, copy_function=shutil.copy2)
    shutil.copy2(args.client_receipt, receipt)
    need(verifier.tree_inventory(app_target) == original, 'Staged app bytes/modes/links differ')
    need(verifier.tree_inventory(licenses) == verifier.tree_inventory(args.project / 'Docs/Licenses'),
         'Staged licenses differ')
    need(verifier.tree_inventory(args.buildapp) == original, 'Original app changed during staging')
    write_new_json(manifest, snapshot)
    plan['status'] = 'staged; independent package verification still required'
    return plan


def source_entries(args, verifier):
    # Same files-only source ZIP form as Tools/package-source.py. Directory
    # entries are optional for the source; app/galleries preserve directory modes.
    expected = verifier.packaged_files(args.project)
    return [('FrontierReckoning/' + name, args.project / name, value)
            for name, value in sorted(expected.items())]


def tree_entries(base, prefix, verifier, allow_links):
    need(base.is_dir() and not base.is_symlink(), 'Missing regular staging directory: ' + str(base))
    inventory = verifier.tree_inventory(base)
    entries = []
    for name, value in sorted(inventory.items()):
        path = base / name
        if value['kind'] == 'symlink':
            need(allow_links, 'Symlink not permitted in ' + str(path))
            target = value['target']
            need(not PurePosixPath(target).is_absolute()
                 and path.resolve().is_relative_to(base.resolve()),
                 'Symlink escapes its payload: ' + str(path))
        archive_name = prefix + ('/' + name if name else '')
        if value['kind'] == 'directory':
            archive_name += '/'
        need(verifier.safe_member(archive_name), 'Unsafe ZIP member: ' + archive_name)
        entries.append((archive_name, path, value))
    return entries


def client_entries(args, verifier, evidence):
    expected = identity(args, verifier, evidence)
    need(args.client_dir.name == CLIENT_ROOT, 'Client staging root must be ' + CLIENT_ROOT)
    children = {p.name for p in args.client_dir.iterdir() if not verifier.ignored(p.name)}
    need(children == {args.buildapp.name, 'Licenses', 'README-zh.md', 'client-build.json', 'release-manifest.json'},
         'Client root must contain exactly app, guide, licenses, build receipt and identity manifest')
    manifest = verifier.parse_json((args.client_dir / 'release-manifest.json').read_bytes())
    for key in ('appVersion', 'runtimeCommit', 'sourceFingerprint', 'sourceFiles', 'buildGuid', 'tests'):
        need(manifest.get(key) == expected[key], 'Embedded manifest identity mismatch: ' + key)
    need(not manifest.get('packages'), 'An embedded manifest cannot contain ZIP hashes')
    need(verifier.record(args.client_dir / 'client-build.json') == verifier.record(args.client_receipt),
         'Staged receipt differs from the original')
    need(verifier.tree_inventory(args.client_dir / args.buildapp.name) == verifier.tree_inventory(args.buildapp),
         'Staged app differs from the final build')
    need(verifier.tree_inventory(args.client_dir / 'Licenses') == verifier.tree_inventory(args.project / 'Docs/Licenses'),
         'Staged licenses differ from source')
    # An app link must stay within the app, not merely within the client payload.
    for name, value in verifier.tree_inventory(args.client_dir / args.buildapp.name).items():
        if value['kind'] == 'symlink':
            link = args.client_dir / args.buildapp.name / name
            need(link.resolve().is_relative_to((args.client_dir / args.buildapp.name).resolve()),
                 'App link escapes its bundle: ' + name)
    return tree_entries(args.client_dir, CLIENT_ROOT, verifier, True)


def gallery_entries(args, verifier):
    entries = tree_entries(args.gallery_dir, args.gallery_dir.name, verifier, False)
    files = [entry for entry in entries if entry[2]['kind'] == 'file']
    need(any(path.suffix.lower() == '.png' for _, path, _ in files), 'Gallery needs actual native PNGs')
    need(any(path.suffix.lower() == '.md' for _, path, _ in files), 'Gallery needs a guide')
    need(all(path.suffix.lower() in ('.png', '.json', '.md') for _, path, _ in files),
         'Gallery allowlist is PNG / JSON / Markdown; no saves, binaries or diagnostics')
    return entries


def create_archive(target, entries, verifier):
    need(not target.exists(), 'Refusing to overwrite ' + str(target))
    target.parent.mkdir(parents=True, exist_ok=True)
    temporary = None
    try:
        fd, name = tempfile.mkstemp(prefix=target.name + '.', suffix='.partial', dir=target.parent)
        os.close(fd)
        temporary = Path(name)
        with zipfile.ZipFile(temporary, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
            for name, path, original in entries:
                need(verifier.record(path) == original, 'Payload changed before archive write: ' + str(path))
                mode = path.lstat().st_mode
                info = zipfile.ZipInfo.from_file(path, name) if original['kind'] != 'symlink' else zipfile.ZipInfo(name)
                info.create_system = 3
                info.external_attr = (mode & 0xffff) << 16
                if original['kind'] == 'directory':
                    info.external_attr |= 0x10
                info.compress_type = zipfile.ZIP_DEFLATED
                if original['kind'] == 'directory':
                    archive.writestr(info, b'')
                elif original['kind'] == 'symlink':
                    archive.writestr(info, os.readlink(os.fsencode(path)))
                else:
                    with path.open('rb') as source, archive.open(info, 'w') as destination:
                        shutil.copyfileobj(source, destination, 1024 * 1024)
        with zipfile.ZipFile(temporary) as archive:
            need(archive.testzip() is None, 'Archive CRC check failed')
        for _, path, original in entries:
            need(verifier.record(path) == original, 'Payload changed during packaging: ' + str(path))
        # Same-volume exclusive link avoids the overwrite behavior of os.replace.
        os.link(temporary, target)
        target.chmod(0o644)
        return {'name': target.name, 'bytes': target.stat().st_size,
                'sha256': verifier.digest(target), 'members': len(entries),
                'status': 'packaged; CRC checked; independent contents verification still required'}
    finally:
        if temporary is not None:
            temporary.unlink(missing_ok=True)


def package(args, verifier, evidence):
    kinds = ('source', 'client', 'gallery') if args.kind == 'all' else (args.kind,)
    loaders = {'source': lambda: source_entries(args, verifier),
               'client': lambda: client_entries(args, verifier, evidence),
               'gallery': lambda: gallery_entries(args, verifier)}
    plans = []
    prepared = []
    # Preflight every requested payload before creating any archive.
    for kind in kinds:
        entries = loaders[kind]()
        target = args.output / ZIP_NAMES[kind]
        need(not target.exists(), 'Refusing to overwrite ' + str(target))
        if kind == 'source':
            need(not target.with_suffix('.manifest.json').exists(), 'Source package receipt already exists')
            names = {name for name, _, _ in entries}
            for name in ('Tests/source-evidence.json', 'Tests/EditMode.xml', 'Tests/PlayMode.xml', 'Native/client-build.json'):
                need('FrontierReckoning/Docs/Evidence/' + EVIDENCE + '/' + name in names,
                     'Source ZIP is missing final ' + name)
            identity(args, verifier, evidence)
        prepared.append((kind, target, entries))
        plans.append({'kind': kind, 'target': str(target), 'members': len(entries),
                      'files': sum(v['kind'] == 'file' for _, _, v in entries),
                      'symlinks': sum(v['kind'] == 'symlink' for _, _, v in entries),
                      'prefix': 'FrontierReckoning/' if kind == 'source' else CLIENT_ROOT + '/' if kind == 'client' else args.gallery_dir.name + '/'})
    if not args.execute:
        return {'action': 'package-plan', 'execute': False, 'sourceFingerprint': evidence['sourceFingerprint'], 'packages': plans}
    results = []
    for kind, target, entries in prepared:
        result = create_archive(target, entries, verifier)
        frozen_evidence(args, verifier)
        if kind == 'source':
            receipt = dict(result, includedEvidenceVersion=EVIDENCE,
                           sourceFingerprint=evidence['sourceFingerprint'], runtimeFileCount=len(evidence['files']),
                           excludes=['Library', 'Logs', 'Temp', 'obj', 'Builds', 'UserSettings', 'work', 'Docs/Evidence other than 066'],
                           sourceHashes={name[len('FrontierReckoning/'):]: value['sha256'] for name, _, value in entries})
            write_new_json(target.with_suffix('.manifest.json'), receipt)
        results.append(result)
    return {'action': 'packaged', 'packages': results, 'scope': 'Creation only; run the independent verifier next. No native or upload claim.'}


def release(args, verifier, evidence):
    embedded = verifier.parse_json((args.client_dir / 'release-manifest.json').read_bytes())
    expected = identity(args, verifier, evidence)
    for key in ('appVersion', 'runtimeCommit', 'sourceFingerprint', 'buildGuid'):
        need(embedded.get(key) == expected[key], 'Embedded manifest differs from final identity: ' + key)
    report = verifier.parse_json(args.verification.read_bytes())
    need(report.get('schema') == 'frontier-package-contents-verification-090'
         and report.get('release') == VERSION and report.get('passed') is True,
         'A complete successful independent 0.9 package verification is required')
    need(all(section.get('status') == 'passed' for section in report.get('sections', {}).values())
         and report.get('verifierSha256') == verifier.digest(args.output / 'verify-package-contents.py'),
         'Verification sections / verifier source changed after the successful run')
    packages = []
    for kind, name in ZIP_NAMES.items():
        path = args.output / name
        saved = report['sections'][kind]['archiveIdentity']
        current = {'name': name, 'bytes': path.stat().st_size, 'sha256': verifier.digest(path)}
        need(saved['file'] == name and saved['bytes'] == current['bytes'] and saved['sha256'] == current['sha256'],
             'ZIP changed after independent verification: ' + name)
        packages.append(current)
    result = dict(expected, packages=packages, createdUtc=datetime.now(timezone.utc).isoformat(),
                  verification={'file': args.verification.name, 'sha256': verifier.digest(args.verification),
                                'passed': True, 'script': 'verify-package-contents.py',
                                'scriptSha256': verifier.digest(args.output / 'verify-package-contents.py')},
                  clientGuide={'file': 'README-zh.md', 'sha256': verifier.digest(args.client_dir / 'README-zh.md')},
                  cloudVerificationScope='Local package bytes only. Upload privacy/parent/size and any remote content checksum need a later independent readback.')
    if args.docs_snapshot_commit:
        need(bool(re.fullmatch(r'[0-9a-f]{40}', args.docs_snapshot_commit)), 'Invalid docs snapshot commit')
        result['docsSnapshotCommit'] = args.docs_snapshot_commit
    result['scope'] = 'Outer delivery identity and verified local ZIP hashes. This file is excluded from all three ZIPs; no self-hash is recorded.'
    target = args.output / 'release-manifest.json'
    need(not target.exists(), 'Refusing to overwrite ' + str(target))
    if args.execute:
        write_new_json(target, result)
    return {'action': 'release-manifest', 'execute': args.execute, 'target': str(target), 'manifest': result}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--testdir', type=Path)
    parser.add_argument('--buildapp', type=Path)
    parser.add_argument('--client-receipt', type=Path)
    parser.add_argument('--client-dir', type=Path)
    parser.add_argument('--gallery-dir', type=Path)
    parser.add_argument('--verification', type=Path)
    parser.add_argument('--docs-snapshot-commit')
    parser.add_argument('--execute', action='store_true', help='Explicitly create staging/archive/manifest files. Omit for a read-only plan.')
    parser.add_argument('action', choices=('stage-client', 'package', 'release'))
    parser.add_argument('--kind', choices=('source', 'client', 'gallery', 'all'), default='all')
    args = parser.parse_args()
    args.project = args.project.resolve()
    args.output = args.output.resolve()
    args.testdir = (args.testdir or args.project / ('Docs/Evidence/' + EVIDENCE + '/Tests')).resolve()
    args.buildapp = (args.buildapp or args.project / 'Builds/macOS/Frontier Reckoning.app').resolve()
    args.client_receipt = (args.client_receipt or args.project / ('Docs/Evidence/' + EVIDENCE + '/Native/client-build.json')).resolve()
    args.client_dir = (args.client_dir or args.output / 'ClientPayload' / CLIENT_ROOT).resolve()
    args.gallery_dir = (args.gallery_dir or args.output / 'Gallery').resolve()
    args.verification = (args.verification or args.output / 'package-contents-verification.json').resolve()
    need(not args.output.is_relative_to(args.project), 'External output must stay outside the source checkout')
    need(args.client_dir.is_relative_to(args.output) and args.gallery_dir.is_relative_to(args.output),
         'Client/gallery staging must remain within the external output')
    verifier = load_verifier(args.output)
    evidence = frozen_evidence(args, verifier)
    actions = {'stage-client': stage_client, 'package': package, 'release': release}
    result = actions[args.action](args, verifier, evidence)
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0


if __name__ == '__main__':
    try:
        raise SystemExit(main())
    except (OSError, ValueError, KeyError, TypeError, zipfile.BadZipFile) as error:
        print('ERROR: ' + str(error), file=__import__('sys').stderr)
        raise SystemExit(1)
