#!/usr/bin/env python3
"""Place this script, the manifest and all3parts together; run python3 assemble-normal072.py."""
from pathlib import Path
import hashlib,json,os,zipfile
root=Path(__file__).resolve().parent
m=json.loads((root/'normal072-transport-manifest.json').read_text())
assert m['schema']=='frontier072-byte-split-transport-1'
assert m['original']=={'file':'normal-routes-originals072.zip','bytes':209058503,'sha256':'036fde3891836a12f25a98f8af1bf3c0fa5974711580ffed72864b82d3c9c33a'}
assert len(m['parts'])==3
for i,p in enumerate(m['parts'],1):
    assert p['file']=='normal-routes-originals072.zip.part'+str(i).zfill(3)
    path=root/p['file']; h=hashlib.sha256(); total=0
    with path.open('rb') as f:
        for b in iter(lambda:f.read(1048576),b''): h.update(b); total+=len(b)
    assert total==p['bytes'] and h.hexdigest()==p['sha256'], 'Part checksum mismatch: '+p['file']
target=root/m['original']['file']; temp=target.with_name(target.name+'.assembling')
assert not target.exists(), 'Output already exists; move it aside before assembling.'
whole=hashlib.sha256(); total=0
with temp.open('xb') as sink:
    for p in m['parts']:
        with (root/p['file']).open('rb') as source:
            for b in iter(lambda:source.read(1048576),b''): sink.write(b); whole.update(b); total+=len(b)
    sink.flush(); os.fsync(sink.fileno())
assert total==m['original']['bytes'] and whole.hexdigest()==m['original']['sha256'], 'Full ZIP checksum mismatch; temporary output retained.'
with zipfile.ZipFile(temp) as z: assert z.testzip() is None, 'ZIP CRC check failed'
os.link(temp,target)
temp.unlink()
print('Created verified ZIP: '+str(target))
