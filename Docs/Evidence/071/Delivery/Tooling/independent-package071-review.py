#!/usr/bin/env python3
"""Independent read-only three-ZIP review. Never extract or follow symlink targets."""
import argparse
import hashlib
import io
import json
import os
import posixpath
import re
import stat
import subprocess
import sys
import unittest
import warnings
import zipfile
import zlib
from datetime import datetime, timezone
from pathlib import Path

CHUNK = 1024 * 1024
EMPTY_SHA = hashlib.sha256(b'').hexdigest()
DEFAULT_COMMIT = '47d7927d1cd7db7e9f96e876b779bdefa2ed9d6d'

def require(condition, message):
    if not condition: raise ValueError(message)

def pairs(rows):
    result = {}
    for key, value in rows:
        require(key not in result, 'Duplicate JSON key: ' + key)
        result[key] = value
    return result

def parse(raw):
    return json.loads(raw, object_pairs_hook=pairs,
        parse_constant=lambda value: (_ for _ in ()).throw(ValueError('Nonfinite JSON: ' + value)))

def sha(raw): return hashlib.sha256(raw).hexdigest()

def safe(name):
    require(isinstance(name, str) and name and not name.startswith('/') and '\\' not in name,
            'Unsafe member path: ' + repr(name))
    parts = name.rstrip('/').split('/')
    require(all(p and p not in ('.', '..') for p in parts) and
            not re.match(r'^[A-Za-z]:', parts[0]) and not any(ord(c) < 32 or ord(c) == 127 for c in name),
            'Unsafe member path: ' + repr(name))
    return name

def record(name, kind, mode, size=0, digest=EMPTY_SHA, **extra):
    return dict(path=safe(name), type=kind, mode=mode, size=size, sha256=digest, **extra)

def unchanged(a, b):
    return (a.st_dev, a.st_ino, a.st_mode, a.st_size, a.st_mtime_ns, a.st_ctime_ns) == \
           (b.st_dev, b.st_ino, b.st_mode, b.st_size, b.st_mtime_ns, b.st_ctime_ns)

def hash_regular(path):
    path = Path(path); before = path.lstat()
    require(stat.S_ISREG(before.st_mode), 'Expected original regular file, not link: ' + str(path))
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW)
    with os.fdopen(fd, 'rb') as stream:
        require(unchanged(before, os.fstat(stream.fileno())), 'File changed before reading: ' + str(path))
        digest, count = hashlib.sha256(), 0
        while True:
            data = stream.read(CHUNK)
            if not data: break
            digest.update(data); count += len(data)
        require(unchanged(before, os.fstat(stream.fileno())), 'File changed during reading: ' + str(path))
    require(unchanged(before, path.lstat()) and count == before.st_size, 'Original file changed: ' + str(path))
    return {'path': str(path), 'bytes': count, 'sha256': digest.hexdigest()}

def checked_json(path):
    before = hash_regular(path)
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW)
    with os.fdopen(fd, 'rb') as stream: raw = stream.read()
    require(sha(raw) == before['sha256'], 'JSON changed before decode')
    return parse(raw), before

def link_text_check(name, raw):
    text = os.fsdecode(raw)
    require(raw and len(raw) <= 65536 and not text.startswith('/') and '\\' not in text and
            not re.match(r'^[A-Za-z]:', text) and not any(ord(c) < 32 or ord(c) == 127 for c in text),
            'Unsafe symlink text: ' + name)
    destination = posixpath.normpath(posixpath.join(posixpath.dirname(name), text))
    root = name.split('/')[0]
    require(destination == root or destination.startswith(root + '/'), 'Symlink text escapes archive root: ' + name)
    return text

def inventory(root):
    root = Path(root); before_root = root.lstat()
    require(stat.S_ISDIR(before_root.st_mode), 'Original directory must not be a symlink: ' + str(root))
    result = {}
    def add(item):
        require(item['path'] not in result, 'Repeated original path'); result[item['path']] = item
    def walk(fd, prefix):
        before = os.fstat(fd)
        add(record(prefix + '/', 'directory', before.st_mode & 0xffff))
        names = sorted(os.listdir(fd))
        for name in names:
            member = safe(prefix + '/' + name)
            node = os.stat(name, dir_fd=fd, follow_symlinks=False); mode = node.st_mode & 0xffff
            if stat.S_ISDIR(mode):
                child = os.open(name, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW, dir_fd=fd)
                try:
                    require(unchanged(node, os.fstat(child)), 'Directory replaced before opening')
                    walk(child, member)
                finally: os.close(child)
                require(unchanged(node, os.stat(name, dir_fd=fd, follow_symlinks=False)), 'Directory replaced during read')
            elif stat.S_ISLNK(mode):
                raw = os.fsencode(os.readlink(name, dir_fd=fd))
                require(unchanged(node, os.stat(name, dir_fd=fd, follow_symlinks=False)), 'Link changed: ' + member)
                add(record(member, 'symlink', mode, len(raw), sha(raw), symlinkTarget=link_text_check(member, raw)))
            elif stat.S_ISREG(mode):
                child = os.open(name, os.O_RDONLY | os.O_NOFOLLOW, dir_fd=fd)
                with os.fdopen(child, 'rb') as stream:
                    require(unchanged(node, os.fstat(stream.fileno())), 'Original changed before file read')
                    digest, count = hashlib.sha256(), 0
                    while True:
                        data = stream.read(CHUNK)
                        if not data: break
                        digest.update(data); count += len(data)
                    require(unchanged(node, os.fstat(stream.fileno())) and count == node.st_size,
                            'Original changed during file read')
                require(unchanged(node, os.stat(name, dir_fd=fd, follow_symlinks=False)), 'Original entry replaced')
                add(record(member, 'file', mode, count, digest.hexdigest()))
            else: raise ValueError('Unsupported original filesystem type: ' + member)
        require(names == sorted(os.listdir(fd)) and unchanged(before, os.fstat(fd)), 'Original directory changed')
    fd = os.open(root, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
    try: walk(fd, root.name)
    finally: os.close(fd)
    require(unchanged(before_root, root.lstat()), 'Original root changed')
    return result

def git(repo, *args):
    env = dict(os.environ); env['GIT_OPTIONAL_LOCKS'] = '0'
    return subprocess.run(['git', '--no-replace-objects', '-C', str(repo), *args],
        check=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE, env=env).stdout

def source_inventory(repo, commit):
    require(re.fullmatch(r'[0-9a-f]{40}|[0-9a-f]{64}', commit), 'Full source commit required')
    require(git(repo, 'rev-parse', '--verify', commit + '^{commit}').decode().strip() == commit,
            'Exact supplied commit must resolve without ref substitution')
    algorithm = git(repo, 'rev-parse', '--show-object-format').decode().strip()
    require(algorithm in ('sha1', 'sha256'), 'Unsupported Git object format')
    tree = git(repo, 'ls-tree', '-r', '-z', '--full-tree', commit)
    tree_rows = [row for row in tree.split(b'\0') if row]
    link_oids = {row.split(b'\t', 1)[0].split()[2] for row in tree_rows
                 if row.split(b'\t', 1)[0].split()[0] == b'120000'}
    result = {'source/': record('source/', 'directory', 0)}; cache = {}; blobs = 0
    env = dict(os.environ); env['GIT_OPTIONAL_LOCKS'] = '0'
    process = subprocess.Popen(['git', '--no-replace-objects', '-C', str(repo), 'cat-file', '--batch'],
        stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE, env=env)
    try:
        for row in tree_rows:
            header, path = row.split(b'\t', 1); mode_text, kind, oid = header.split()
            require(kind == b'blob', 'Gitlinks/non-blobs cannot be silently omitted')
            name = safe('source/' + path.decode('utf-8')); mode = int(mode_text, 8)
            require(mode in (0o100644, 0o100755, 0o120000), 'Unsupported Git tree mode')
            require(name not in result, 'Duplicate tracked Git path')
            parts = name.split('/')
            for i in range(1, len(parts)):
                directory = '/'.join(parts[:i]) + '/'
                result.setdefault(directory, record(directory, 'directory', 0))
            if oid not in cache:
                process.stdin.write(oid + b'\n'); process.stdin.flush()
                response = process.stdout.readline().split()
                require(len(response) == 3 and response[:2] == [oid, b'blob'], 'Git object header mismatch')
                size = int(response[2]); require(size >= 0, 'Negative blob size')
                digest = hashlib.sha256(); object_digest = hashlib.new(algorithm)
                object_digest.update(b'blob ' + str(size).encode() + b'\0')
                remaining = size; linkraw = bytearray() if oid in link_oids else None
                while remaining:
                    data = process.stdout.read(min(CHUNK, remaining)); require(data, 'Truncated Git blob')
                    digest.update(data); object_digest.update(data); remaining -= len(data)
                    if linkraw is not None:
                        require(len(linkraw) + len(data) <= 65536, 'Oversized Git symlink blob'); linkraw.extend(data)
                require(process.stdout.read(1) == b'\n' and object_digest.hexdigest().encode() == oid,
                        'Git blob boundary/object-address mismatch')
                cache[oid] = (size, digest.hexdigest(), bytes(linkraw) if linkraw is not None else None)
            size, digest, linkraw = cache[oid]
            if mode == 0o120000:
                require(linkraw is not None, 'Blob reused with incompatible regular/link type')
                item = record(name, 'symlink', 0o120777, size, digest, symlinkTarget=link_text_check(name, linkraw))
            else: item = record(name, 'file', 0 if mode == 0o100644 else mode, size, digest)
            item['gitTreeMode'] = format(mode, '06o'); item['gitObjectId'] = oid.decode()
            result[name] = item; blobs += 1
        process.stdin.close(); process.stdout.close()
        require(process.wait() == 0, 'Git batch read failed')
    finally:
        if process.poll() is None: process.terminate(); process.wait()
        if process.stderr: process.stderr.close()
    return result, {'commit': commit, 'treeSha256': sha(tree), 'trackedPaths': blobs,
                    'uniqueBlobObjectsChecked': len(cache), 'gitObjectFormat': algorithm}

def compare(actual, expected, context, manifest=False):
    require(set(actual) == set(expected), context + ': member set differs (' +
            str(len(set(actual) - set(expected))) + ' extra, ' + str(len(set(expected) - set(actual))) + ' missing)')
    for name, desired in expected.items():
        found = actual[name]
        for key in ('type', 'mode', 'size', 'sha256'):
            require(found[key] == desired[key], context + ': ' + key + ' differs: ' + name)
        if desired['type'] == 'symlink' and desired.get('symlinkTarget') is not None:
            require(found.get('symlinkTarget') == desired['symlinkTarget'], context + ': symlink text differs: ' + name)
        if manifest and desired.get('gitTreeMode') is not None:
            require(found.get('gitTreeMode') == desired['gitTreeMode'], context + ': Git mode differs: ' + name)

def zip_inventory(source, expected, git_comment=None):
    result = {}; aliases = set(); details = []
    with zipfile.ZipFile(source, 'r') as archive:
        if git_comment is not None:
            require(archive.comment == git_comment.encode('ascii'), 'Source ZIP Git commit comment differs')
        for info in archive.infolist():
            name = safe(info.filename); canonical = name.rstrip('/')
            require(name not in result and canonical not in aliases, 'Duplicate/aliased archive path: ' + name)
            aliases.add(canonical); require(name in expected, 'Extra ZIP entry: ' + name)
            mode = (info.external_attr >> 16) & 0xffff; filetype = stat.S_IFMT(mode)
            require(filetype in (0, stat.S_IFREG, stat.S_IFDIR, stat.S_IFLNK), 'Special ZIP Unix type rejected')
            kind = 'directory' if info.is_dir() else 'symlink' if stat.S_ISLNK(mode) else 'file'
            require(not stat.S_ISDIR(mode) or kind == 'directory', 'Directory mode/name mismatch')
            require(not info.flag_bits & 1 and info.compress_type in (zipfile.ZIP_STORED, zipfile.ZIP_DEFLATED),
                    'Encrypted/unsupported compression')
            desired = expected[name]
            require(kind == desired['type'] and mode == desired['mode'] and info.file_size == desired['size'],
                    'ZIP declared type/mode/size differs: ' + name)
            if mode: require(info.create_system == 3, 'Explicit Unix mode without Unix creator')
            digest, crc, count = hashlib.sha256(), 0, 0
            target = bytearray() if kind == 'symlink' else None
            with archive.open(info, 'r') as stream:
                while True:
                    data = stream.read(CHUNK)
                    if not data: break
                    count += len(data); require(count <= desired['size'], 'Inflated member larger than original')
                    digest.update(data); crc = zlib.crc32(data, crc)
                    if target is not None:
                        require(count <= 65536, 'Oversized ZIP symlink'); target.extend(data)
            require(count == info.file_size and (crc & 0xffffffff) == info.CRC, 'CRC/size differs: ' + name)
            require(kind != 'directory' or count == 0, 'Nonempty directory entry')
            item = record(name, kind, mode, count, digest.hexdigest())
            if target is not None: item['symlinkTarget'] = link_text_check(name, bytes(target))
            if 'gitTreeMode' in desired: item['gitTreeMode'] = desired['gitTreeMode']
            result[name] = item
            details.append({'path': name, 'type': kind, 'unixMode': mode, 'size': count,
                'sha256': item['sha256'], 'crc32': format(crc & 0xffffffff, '08x'),
                'symlinkTarget': item.get('symlinkTarget')})
    compare(result, expected, 'ZIP independent original comparison')
    return result, details

def manifest_inventory(rows):
    result = {}
    for item in rows:
        name = safe(item['path']); require(name not in result, 'Duplicate manifest path')
        require(type(item['size']) is int and item['size'] >= 0 and type(item['mode']) is int,
                'Manifest size/mode must be integers')
        require(re.fullmatch(r'[0-9a-f]{64}', item['sha256']), 'Manifest member SHA invalid')
        require(item.get('modeOctal') == format(item['mode'], '06o'), 'Manifest octal mode differs')
        result[name] = item
    return result

def self_test():
    class BoundaryTests(unittest.TestCase):
        def test_paths(self):
            for bad in ('/abs', '../bad', 'a/../bad', 'a\\bad', 'C:bad', 'a//bad', 'a/./bad', 'a\0bad', 'a\nbad'):
                with self.assertRaises(ValueError): safe(bad)
        def test_duplicate_json(self):
            with self.assertRaises(ValueError): parse('{"x":1,"x":2}')
        def test_nonfinite_json(self):
            with self.assertRaises(ValueError): parse('{"x":NaN}')
        def test_link_relative(self): self.assertEqual(link_text_check('app/dir/link', b'../file'), '../file')
        def test_link_escapes(self):
            for bad in (b'/tmp/a', b'../../file', b'C:foo', b'..\\foo', b''):
                with self.assertRaises(ValueError): link_text_check('app/link', bad)
        def sample(self, names=('root/file',), mode=0o100644, raw=b'abc'):
            data = io.BytesIO()
            with warnings.catch_warnings():
                warnings.simplefilter('ignore', UserWarning)
                with zipfile.ZipFile(data, 'w', compression=zipfile.ZIP_STORED) as output:
                    for name in names:
                        item = zipfile.ZipInfo(name); item.create_system = 3; item.external_attr = mode << 16
                        output.writestr(item, raw)
            data.seek(0); return data
        def test_good_zip(self):
            expected={'root/file':record('root/file','file',0o100644,3,sha(b'abc'))}
            actual,_=zip_inventory(self.sample(),expected);self.assertEqual(actual,expected)
        def test_duplicate_members(self):
            expected={'root/file':record('root/file','file',0o100644,3,sha(b'abc'))}
            with self.assertRaises(ValueError):zip_inventory(self.sample(('root/file','root/file')),expected)
        def test_corrupt_crc(self):
            data=self.sample().getvalue();offset=data.index(b'abc');bad=io.BytesIO(data[:offset]+b'xbc'+data[offset+3:])
            expected={'root/file':record('root/file','file',0o100644,3,sha(b'abc'))}
            with self.assertRaises((ValueError,zipfile.BadZipFile)):zip_inventory(bad,expected)
        def test_same_size_wrong_content(self):
            expected={'root/file':record('root/file','file',0o100644,3,sha(b'def'))}
            with self.assertRaises(ValueError):zip_inventory(self.sample(),expected)
        def test_wrong_mode(self):
            expected={'root/file':record('root/file','file',0o100755,3,sha(b'abc'))}
            with self.assertRaises(ValueError):zip_inventory(self.sample(),expected)
        def test_extra_entry(self):
            with self.assertRaises(ValueError):zip_inventory(self.sample(),{})
        def test_missing_entry(self):
            expected={'root/file':record('root/file','file',0o100644,3,sha(b'abc')),
                      'root/missing':record('root/missing','file',0o100644)}
            with self.assertRaises(ValueError):zip_inventory(self.sample(),expected)
        def test_symlink_text(self):
            expected={'root/link':record('root/link','symlink',0o120777,4,sha(b'file'),symlinkTarget='file')}
            zip_inventory(self.sample(('root/link',),0o120777,b'file'),expected)
        def test_symlink_target_mismatch(self):
            expected={'root/link':record('root/link','symlink',0o120777,4,sha(b'file'),symlinkTarget='else')}
            with self.assertRaises(ValueError):zip_inventory(self.sample(('root/link',),0o120777,b'file'),expected)
    result=unittest.TextTestRunner(verbosity=1).run(unittest.defaultTestLoader.loadTestsFromTestCase(BoundaryTests))
    print(json.dumps({'selfTestOnly':True,'tests':result.testsRun,'passed':result.wasSuccessful(),
                      'releaseArtifactVerification':'not_run','temporaryPackageFilesWritten':False}))
    return 0 if result.wasSuccessful() else 1

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--manifest',type=Path)
    parser.add_argument('--repo',type=Path)
    parser.add_argument('--expected-source-commit',default=DEFAULT_COMMIT)
    parser.add_argument('--artifacts', nargs='+', choices=('source.zip','client.zip','gallery.zip'),
                        default=['source.zip','client.zip','gallery.zip'])
    parser.add_argument('--output-json',type=Path)
    parser.add_argument('--output-md',type=Path)
    parser.add_argument('--self-test',action='store_true')
    args=parser.parse_args()
    if args.self_test:return self_test()
    require(all((args.manifest,args.repo,args.output_json,args.output_md)), 'Manifest, repo and two new output paths required')
    require(not args.output_json.exists() and not args.output_json.is_symlink() and
            not args.output_md.exists() and not args.output_md.is_symlink(), 'Preserve existing review outputs; select new names')
    manifest, manifest_ref = checked_json(args.manifest)
    require(manifest.get('schema')==1 and manifest.get('passed') is True and not manifest.get('error'),
            'Completed successful real package manifest required')
    require(manifest['sourceSnapshotCommit']==args.expected_source_commit, 'Unexpected source snapshot commit')
    require(Path(manifest['repo']).resolve()==args.repo.resolve(), 'Repository does not match explicit manifest origin')
    artifacts = manifest['artifacts']
    require(len(artifacts)==3 and {a['fileName']for a in artifacts}=={'source.zip','client.zip','gallery.zip'},
            'Exactly the declared source/client/gallery artifacts required')
    require(len(set(args.artifacts)) == len(args.artifacts), 'Duplicate requested artifact scope')
    artifacts = [artifact for artifact in artifacts if artifact['fileName'] in args.artifacts]
    roots={name: Path(manifest[field]) for name,field in
           (('client.zip','appDirectory'),('gallery.zip','galleryDirectory')) if name in args.artifacts}
    for root in roots.values():
        require(not args.output_json.resolve().is_relative_to(root.resolve()) and
                not args.output_md.resolve().is_relative_to(root.resolve()), 'Review output must be outside originals')
    source_ref = {'commit':args.expected_source_commit, 'fullGitBlobAudit':'not_requested_in_this_partial_scope'}
    expected = {}
    if 'source.zip' in args.artifacts:
        source,source_ref=source_inventory(args.repo,args.expected_source_commit)
        expected['source.zip'] = source
    expected.update({name:inventory(root)for name,root in roots.items()})
    results=[]
    original_zips={a['fileName']:hash_regular(Path(a['path']))for a in artifacts}
    for artifact in artifacts:
        name=artifact['fileName'];path=Path(artifact['path'])
        require(path.name==name and path.parent.resolve()==args.manifest.parent.resolve(), 'ZIP path/name differs from manifest location')
        original=original_zips[name]
        require(original['sha256']==artifact['sha256'] and original['bytes']==artifact['bytes'], 'ZIP container SHA/bytes differs')
        declared=manifest_inventory(artifact['members'])
        require(artifact['memberCount']==len(declared) and artifact['payloadBytes']==sum(r['size']for r in declared.values()),
                'Manifest artifact counts differ')
        compare(expected[name],declared,'Manifest independent original inventory',manifest=True)
        found,members=zip_inventory(path,expected[name],args.expected_source_commit if name=='source.zip' else None)
        compare(found,declared,'ZIP manifest comparison',manifest=True)
        results.append({'artifact':name,'zip':original,'memberCount':len(found),'payloadBytes':sum(r['size']for r in found.values()),
            'fileCount':sum(r['type']=='file'for r in found.values()),'directoryCount':sum(r['type']=='directory'for r in found.values()),
            'symlinkCount':sum(r['type']=='symlink'for r in found.values()),'allMembersCrcShaBytesModeAndTextVerified':True,
            'sourceZipGitCommentVerified': name=='source.zip',
            'exactOriginalMemberSetAndManifestMatch':True,'duplicateOrUnsafePaths':False,'members':members})
    for name,root in roots.items():compare(inventory(root),expected[name],'Original tree after ZIP review')
    if 'source.zip' in args.artifacts:
        require(source_ref['treeSha256']==sha(git(args.repo,'ls-tree','-r','-z','--full-tree',args.expected_source_commit)), 'Git tree changed')
    require(manifest_ref==hash_regular(args.manifest),'Package manifest changed during independent review')
    for artifact in artifacts:require(original_zips[artifact['fileName']]==hash_regular(Path(artifact['path'])),'ZIP changed during review')
    report={'schema':1,'reviewedUtc':datetime.now(timezone.utc).isoformat(),
        'status':'passed_exact_local_three_package_scope' if len(args.artifacts)==3 else 'passed_exact_local_selected_package_scope',
        'artifactScope': args.artifacts,
        'manifest':manifest_ref,'checker':hash_regular(Path(__file__)),'source':source_ref,'artifacts':results,
        'originalInputsStableBeforeAfter':True,'symlinkTargetsFollowed':False,'archivesExtracted':False,
        'limitations':['Local byte/type/mode and explicit original snapshot integrity only; no new build/Unity/native quality or remote Drive verification.',
            'Git100644 ZIP modes are implicit0; exact Git tree mode100644 separately checked. Executable100755 and symlink120777 modes explicit.',
            'ACLs/xattrs/resource forks/signing/notarization are not asserted. Symlink payloads are read as text; targets are never followed.',
            'Symlink safety is lexical containment within archive root; no OS target resolution or extraction performed.']}
    with args.output_json.open('x',encoding='utf-8')as f:json.dump(report,f,ensure_ascii=True,indent=2);f.write('\n')
    text='Independent final three-package review\n\nPassed exact local archive scope. Source commit: '+args.expected_source_commit+'.\n\n'
    for r in results:
        text+=r['artifact']+': '+str(r['zip']['bytes'])+' bytes, '+str(r['memberCount'])+' members, SHA '+r['zip']['sha256']+'.\n'
    text+='\nEvery ZIP member independently checked against full Git blobs/tree or original lstat directory and the manifest: CRC, SHA, bytes, Unix mode, paths and symlink text. No duplicate/missing/extra paths; originals/manifest/ZIP bytes unchanged before and after. No symlink target followed; no extraction, build, Unity, GUI, Git writes or network.\n\nLocal packaging integrity only; native quality, remote delivery, ACLs/xattrs and signing are outside this review.\n'
    with args.output_md.open('x',encoding='utf-8')as f:f.write(text)
    print(json.dumps({'status':report['status'],'sourceCommit':args.expected_source_commit,
        'artifacts':[{k:r[k]for k in ('artifact','zip','memberCount','symlinkCount')}for r in results],
        'outputs':[str(args.output_json),str(args.output_md)]}))
    return 0

if __name__=='__main__':
    try:sys.exit(main())
    except (ValueError,OSError,KeyError,TypeError,zipfile.BadZipFile,subprocess.CalledProcessError)as error:
        print(json.dumps({'status':'not_passed','error':str(error),'noReleasePassGenerated':True}));sys.exit(2)
