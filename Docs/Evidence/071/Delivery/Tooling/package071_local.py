#!/usr/bin/env python3
"""Package an explicit Git snapshot, built app and evidence gallery; verify ZIP bytes locally."""
import argparse
import hashlib
import json
import os
import re
import stat
import subprocess
import sys
import zipfile
from datetime import datetime, timezone
from pathlib import Path

CHUNK = 1024 * 1024
EMPTY_SHA = hashlib.sha256(b'').hexdigest()


def digest_file(path):
    digest = hashlib.sha256()
    with Path(path).open('rb') as stream:
        while data := stream.read(CHUNK):
            digest.update(data)
    return digest.hexdigest()


def safe_member(name):
    parts = name.rstrip('/').split('/')
    if not name or name.startswith('/') or '\\' in name or '\x00' in name or any(part in ('', '.', '..') for part in parts) or re.match(r'^[A-Za-z]:', parts[0]):
        raise ValueError('Unsafe archive member: ' + repr(name))
    return name


def entry(name, kind, mode, size=0, digest=EMPTY_SHA):
    return {'path': safe_member(name), 'type': kind, 'sha256': digest, 'size': size,
            'mode': mode, 'modeOctal': format(mode, '06o')}


def zip_info(record, mtime):
    info = zipfile.ZipInfo(record['path'])
    stamp = datetime.fromtimestamp(mtime)
    if 1980 <= stamp.year <= 2107:
        info.date_time = (stamp.year, stamp.month, stamp.day, stamp.hour, stamp.minute, stamp.second)
    info.create_system = 3
    info.external_attr = record['mode'] << 16 | (0x10 if record['type'] == 'directory' else 0)
    info.compress_type = zipfile.ZIP_DEFLATED
    info.file_size = record['size']
    return info


def filesystem_members(root, archive=None):
    """Pin directory descriptors; never recurse into or read a symlink target."""
    root = Path(root)
    if not stat.S_ISDIR(os.lstat(root).st_mode):
        raise ValueError('Input root must be a real directory: ' + str(root))
    nofollow = os.O_NOFOLLOW
    records = {}

    def add(record):
        if record['path'] in records:
            raise ValueError('Duplicate input member: ' + record['path'])
        records[record['path']] = record

    def walk(fd, prefix):
        node = os.fstat(fd)
        record = entry(prefix + '/', 'directory', node.st_mode & 0xffff)
        add(record)
        if archive is not None:
            archive.writestr(zip_info(record, node.st_mtime), b'')
        with os.scandir(fd) as scan:
            names = sorted(item.name for item in scan)
        for name in names:
            member = safe_member(prefix + '/' + name)
            before = os.stat(name, dir_fd=fd, follow_symlinks=False)
            mode = before.st_mode & 0xffff
            if stat.S_ISDIR(mode):
                child = os.open(name, os.O_RDONLY | os.O_DIRECTORY | nofollow, dir_fd=fd)
                try:
                    walk(child, member)
                finally:
                    os.close(child)
            elif stat.S_ISLNK(mode):
                data = os.fsencode(os.readlink(name, dir_fd=fd))
                record = entry(member, 'symlink', mode, len(data), hashlib.sha256(data).hexdigest())
                record['symlinkTarget'] = os.fsdecode(data)
                add(record)
                if archive is not None:
                    archive.writestr(zip_info(record, before.st_mtime), data)
            elif stat.S_ISREG(mode):
                source = os.open(name, os.O_RDONLY | nofollow, dir_fd=fd)
                with os.fdopen(source, 'rb') as stream:
                    opened = os.fstat(stream.fileno())
                    if (before.st_dev, before.st_ino, before.st_mode, before.st_size, before.st_mtime_ns) != \
                       (opened.st_dev, opened.st_ino, opened.st_mode, opened.st_size, opened.st_mtime_ns):
                        raise ValueError('Input changed before reading: ' + member)
                    record = entry(member, 'file', mode, before.st_size)
                    target = archive.open(zip_info(record, before.st_mtime), 'w', force_zip64=True) if archive is not None else None
                    digest = hashlib.sha256()
                    size = 0
                    try:
                        while data := stream.read(CHUNK):
                            digest.update(data)
                            size += len(data)
                            if target is not None:
                                target.write(data)
                    finally:
                        if target is not None:
                            target.close()
                    after = os.fstat(stream.fileno())
                    if size != before.st_size or (after.st_size, after.st_mtime_ns) != (before.st_size, before.st_mtime_ns):
                        raise ValueError('Input changed while reading: ' + member)
                    record['sha256'] = digest.hexdigest()
                    add(record)
            else:
                raise ValueError('Unsupported input type, not silently omitted: ' + member)
    fd = os.open(root, os.O_RDONLY | os.O_DIRECTORY | nofollow)
    try:
        walk(fd, root.name)
    finally:
        os.close(fd)
    return records


def git(repo, *args):
    return subprocess.run(['git', '-C', str(repo), *args], check=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE).stdout


def source_members(repo, commit):
    records = {'source/': entry('source/', 'directory', 0)}
    blob = subprocess.Popen(['git', '-C', str(repo), 'cat-file', '--batch'], stdin=subprocess.PIPE, stdout=subprocess.PIPE)
    try:
        for row in git(repo, 'ls-tree', '-r', '-z', '--full-tree', commit).split(b'\0'):
            if not row:
                continue
            header, raw_path = row.split(b'\t', 1)
            mode_raw, kind, oid = header.split(b' ')
            if kind != b'blob':
                raise ValueError('Snapshot contains a gitlink/non-blob; refusing to omit its contents')
            name = safe_member('source/' + raw_path.decode('utf-8'))
            segments = name.split('/')
            for i in range(1, len(segments)):
                directory = '/'.join(segments[:i]) + '/'
                records.setdefault(directory, entry(directory, 'directory', 0))
            blob.stdin.write(oid + b'\n')
            blob.stdin.flush()
            object_header = blob.stdout.readline().split()
            if len(object_header) != 3 or object_header[0] != oid or object_header[1] != b'blob':
                raise ValueError('Git blob identity mismatch')
            size = int(object_header[2])
            remaining, digest = size, hashlib.sha256()
            while remaining:
                data = blob.stdout.read(min(CHUNK, remaining))
                if not data:
                    raise ValueError('Git blob ended early')
                digest.update(data)
                remaining -= len(data)
            if blob.stdout.read(1) != b'\n':
                raise ValueError('Git blob boundary mismatch')
            git_mode = int(mode_raw, 8)
            if git_mode == 0o120000:
                record = entry(name, 'symlink', 0o120777, size, digest.hexdigest())
            elif git_mode in (0o100644, 0o100755):
                # Git ZIP uses implicit DOS metadata for ordinary 0644 files;
                # executable and symlink members carry explicit Unix modes.
                record = entry(name, 'file', 0 if git_mode == 0o100644 else git_mode, size, digest.hexdigest())
            else:
                raise ValueError('Unsupported tracked Git mode: ' + mode_raw.decode())
            record['gitTreeMode'] = format(git_mode, '06o')
            records[name] = record
    finally:
        blob.stdin.close()
        blob.stdout.close()
        if blob.wait() != 0:
            raise ValueError('Git cat-file failed')
    return records


def verify_zip(path, expected):
    actual = {}
    canonical = set()
    with zipfile.ZipFile(path, 'r') as archive:
        for info in archive.infolist():
            name = safe_member(info.filename)
            if name in actual or name.rstrip('/') in canonical:
                raise ValueError('Duplicate/aliased ZIP member: ' + name)
            canonical.add(name.rstrip('/'))
            mode = info.external_attr >> 16 & 0xffff
            kind = 'directory' if info.is_dir() else 'symlink' if stat.S_ISLNK(mode) else 'file'
            if info.flag_bits & 1:
                raise ValueError('Unexpected encrypted ZIP member')
            digest, size = hashlib.sha256(), 0
            target = bytearray() if kind == 'symlink' else None
            with archive.open(info, 'r') as stream:
                while data := stream.read(CHUNK):
                    digest.update(data)
                    size += len(data)
                    if target is not None:
                        target.extend(data)
            record = entry(name, kind, mode, size, digest.hexdigest())
            if target is not None:
                record['symlinkTarget'] = os.fsdecode(bytes(target))
            if info.file_size != size or kind == 'directory' and size != 0:
                raise ValueError('ZIP member payload size mismatch: ' + name)
            actual[name] = record
    if set(actual) != set(expected):
        raise ValueError('ZIP has missing/extra members: ' + repr(sorted(set(actual) ^ set(expected))))
    for name, wanted in expected.items():
        found = actual[name]
        for key in ('type', 'mode', 'size', 'sha256'):
            if found[key] != wanted[key]:
                raise ValueError('ZIP differs from original input (' + key + '): ' + name)
        if wanted['type'] == 'symlink' and wanted.get('symlinkTarget') is not None and found.get('symlinkTarget') != wanted['symlinkTarget']:
            raise ValueError('ZIP symlink text differs: ' + name)
        if wanted.get('gitTreeMode') is not None:
            found['gitTreeMode'] = wanted['gitTreeMode']
    return sorted(actual.values(), key=lambda item: item['path'])


def write_directory_zip(root, path):
    expected = filesystem_members(root)
    with zipfile.ZipFile(path, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=6, allowZip64=True) as archive:
        written = filesystem_members(root, archive)
    if expected != written:
        raise ValueError('Input tree changed between inventory and archive: ' + str(root))
    members = verify_zip(path, expected)
    if filesystem_members(root) != expected:
        raise ValueError('Original input changed during verification: ' + str(root))
    return artifact(path, members, 'lstat original tree, descriptor-safe file reads; symlink text only; independent ZIP reread')


def artifact(path, members, method):
    return {'path': str(path), 'fileName': path.name, 'sha256': digest_file(path), 'bytes': path.stat().st_size,
            'memberCount': len(members), 'payloadBytes': sum(item['size'] for item in members), 'members': members,
            'verification': {'passed': True, 'method': method, 'noDuplicates': True, 'safeMemberPaths': True,
                             'exactMemberSet': True, 'allPayloadShaAndSizesEqual': True, 'allModesAndSymlinkTextsEqual': True}}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-snapshot-commit', required=True)
    parser.add_argument('--repo', type=Path, required=True)
    parser.add_argument('--app-directory', type=Path, required=True)
    parser.add_argument('--gallery-directory', type=Path, required=True)
    parser.add_argument('--empty-output-directory', type=Path, required=True)
    args = parser.parse_args()
    commit = args.source_snapshot_commit.lower()
    if not re.fullmatch(r'(?:[0-9a-f]{40}|[0-9a-f]{64})', commit):
        parser.error('Use the final full Git commit hash, never HEAD/branch/tag shorthand')
    args.repo = args.repo.absolute()
    for root in (args.repo, args.app_directory, args.gallery_directory):
        if not stat.S_ISDIR(os.lstat(root).st_mode):
            parser.error('Each input root must be a real directory, not a symlink')
    if git(args.repo, 'rev-parse', '--verify', commit + '^{commit}').decode().strip() != commit:
        parser.error('Requested object does not resolve to exactly the supplied commit')
    output = args.empty_output_directory.absolute()
    for root in (args.repo, args.app_directory, args.gallery_directory):
        if output.resolve().is_relative_to(root.resolve()):
            parser.error('Output must be outside all original input roots')
    if output.exists() or output.is_symlink():
        if not stat.S_ISDIR(os.lstat(output).st_mode) or any(output.iterdir()):
            parser.error('Output directory must be real and empty; existing bytes are preserved')
    else:
        output.mkdir(parents=True)
    manifest = {'schema': 1, 'createdAtUtc': datetime.now(timezone.utc).isoformat(),
                'sourceSnapshotCommit': commit, 'repo': str(args.repo), 'appDirectory': str(args.app_directory.absolute()),
                'galleryDirectory': str(args.gallery_directory.absolute()), 'passed': False,
                'scope': 'Local archive member content/size/type/mode and archive bytes only; not native quality, build identity or Drive remote byte verification.',
                'metadataLimit': 'Preserves files, directories, symlink payloads and Unix mode fields. Git normal-file ZIP modes are implicit; gitTreeMode is explicit. ACLs/xattrs/resource forks are not claimed.',
                'artifacts': []}
    try:
        expected = source_members(args.repo, commit)
        path = output / 'source.zip'
        git(args.repo, '-c', 'archive.umask=0022', 'archive', '--format=zip', '--prefix=source/', '--output=' + str(path), commit)
        members = verify_zip(path, expected)
        manifest['artifacts'].append(artifact(path, members, 'Exact git archive commit; independent tracked blob SHA/type/mode inventory and ZIP reread'))
        manifest['artifacts'].append(write_directory_zip(args.app_directory, output / 'client.zip'))
        manifest['artifacts'].append(write_directory_zip(args.gallery_directory, output / 'gallery.zip'))
        manifest['passed'] = all(item['verification']['passed'] for item in manifest['artifacts'])
    except Exception as error:
        manifest['error'] = str(error)
    manifest['finishedAtUtc'] = datetime.now(timezone.utc).isoformat()
    destination = output / 'package-manifest.json'
    destination.write_text(json.dumps(manifest, ensure_ascii=True, indent=2) + '\n')
    print(json.dumps({'passed': manifest['passed'], 'sourceSnapshotCommit': commit, 'manifest': str(destination),
                      'artifacts': [{key: item[key] for key in ('fileName', 'bytes', 'sha256', 'memberCount')} for item in manifest['artifacts']],
                      'error': manifest.get('error')}, ensure_ascii=True))
    return 0 if manifest['passed'] else 1


if __name__ == '__main__':
    sys.exit(main())
