import hashlib, importlib.util, json, plistlib, re, stat, subprocess
from datetime import datetime, timezone
from pathlib import Path
ROOT=Path('/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project')
BASE=Path(__file__).resolve().parent
BUILD=BASE/'Build1'
TARGET=BUILD/'actual-app-manifest078.json'
assert not TARGET.exists(), 'Fresh manifest required'
spec=importlib.util.spec_from_file_location('ev', ROOT/'Tools/verification_evidence.py')
ev=importlib.util.module_from_spec(spec);spec.loader.exec_module(ev)
rp=BUILD/'runner-receipt078.json';receipt=json.loads(rp.read_text())
assert receipt['status']=='closed' and receipt['exitCode']==0 and receipt['sourceStable']
assert receipt['kind']=='build'
files=ev.source_files(ROOT)
assert files==receipt['sourcesBefore']
assert ev.fingerprint(files)==receipt['sourceFingerprintAfter']=='05dc944960c2724e08f2d6eeb02df282710d099ae36cf35bb7923d8918166217'
assert len(files)==788
app=BUILD/'Frontier Reckoning.app';plist=plistlib.loads((app/'Contents/Info.plist').read_bytes())
assert plist['CFBundleIdentifier']=='com.frontierworkshop.reckoning'
assert plist['CFBundleShortVersionString']=='0.21.0'
boot=(app/'Contents/Resources/Data/boot.config').read_text()
guid=re.search(r'^build-guid=([0-9a-f]{32})$',boot,re.M).group(1)
assert guid not in ['00000000000000000000000000000000','6d3fd495bd8f48be852c1e6c62bcb02f','a86c515f3ab040649ea505e507193ff3','ed29410c2b27484abeac486c8f57b81b']
assert 'FRONTIER_BUILD Succeeded' in (BUILD/'build.log').read_text(errors='replace')
entries=[];symlinks=[]
for p in sorted(app.rglob('*')):
 if p.is_symlink():
  symlinks.append(dict(path=p.relative_to(app).as_posix(),target=str(p.readlink())))
 elif p.is_file():
  entries.append(dict(path=p.relative_to(app).as_posix(),bytes=p.stat().st_size,sha256=ev.file_hash(p),mode=oct(stat.S_IMODE(p.stat().st_mode))))
exe=app/'Contents/MacOS'/plist['CFBundleExecutable']
assert exe.is_file() and exe.stat().st_mode&stat.S_IXUSR
architecture=subprocess.check_output(['/usr/bin/file',str(exe)],text=True).strip()
assert 'arm64' in architecture
sig=subprocess.run(['/usr/bin/codesign','--verify','--deep','--strict',str(app)],capture_output=True,text=True)
assert sig.returncode==0, sig.stderr
manifest=dict(schema=1,scope='Actual 078 built files and build/source identity only; no native GUI acceptance',createdUtc=datetime.now(timezone.utc).isoformat(),appPath=str(app),version=plist['CFBundleShortVersionString'],bundleId=plist['CFBundleIdentifier'],buildGuid=guid,sourceFingerprint=receipt['sourceFingerprintAfter'],sourceHead=receipt['gitHeadBefore'],observedHead=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),sourceInputCount=len(files),runnerReceiptSha256=ev.file_hash(rp),runnerScriptSha256=ev.file_hash(BASE/'run078.py'),executableSha256=ev.file_hash(exe),architecture=architecture,codesignVerification=dict(exitCode=sig.returncode,stdout=sig.stdout,stderr=sig.stderr),fileCount=len(entries),totalFileBytes=sum(x['bytes'] for x in entries),symlinks=symlinks,files=entries)
TARGET.write_text(json.dumps(manifest,indent=2)+'\n')
print(json.dumps({k:manifest[k] for k in ['appPath','version','buildGuid','sourceFingerprint','fileCount','totalFileBytes','executableSha256']}))
print('Manifest sha256:',ev.file_hash(TARGET))
