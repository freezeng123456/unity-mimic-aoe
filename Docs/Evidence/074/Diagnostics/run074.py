"""Run fresh, sequential Unity validation and retain actual completion receipts."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
import os
from pathlib import Path
import re
import subprocess
import sys
from xml.etree import ElementTree as ET

ROOT = Path('/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project')
spec = importlib.util.spec_from_file_location('verification_evidence', ROOT / 'Tools/verification_evidence.py')
evidence = importlib.util.module_from_spec(spec)
spec.loader.exec_module(evidence)
parser = argparse.ArgumentParser()
parser.add_argument('kind', choices=('focused', 'full', 'build', 'normal'))
parser.add_argument('--output', type=Path, required=True)
parser.add_argument('--filter', default='')
args = parser.parse_args()
output = args.output.resolve()
output.mkdir(parents=True, exist_ok=False)
before = evidence.source_files(ROOT)
fingerprint = evidence.fingerprint(before)
version = re.search(r'^m_EditorVersion: (.+)$', (ROOT / 'ProjectSettings/ProjectVersion.txt').read_text(), re.M).group(1)
editor = Path('/Applications/Unity/Hub/Editor') / version / 'Unity.app/Contents/MacOS/Unity'
assert editor.is_file()
receipt = {'schema': 'frontier074-unity-runner-v1', 'kind': args.kind, 'status': 'running',
           'startedUtc': datetime.now(timezone.utc).isoformat(), 'sourceFingerprintBefore': fingerprint,
           'sourceFilesBefore': before, 'gitHeadBefore': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
           'preparationRun': False, 'caseInterventions': False, 'unityVersion': version}
target = output / 'runner-receipt074.json'
def save():
    target.write_text(json.dumps(receipt, indent=2) + '\n')
save()
env = dict(os.environ)
env['FRONTIER_SOURCE_FINGERPRINT'] = fingerprint
if args.kind == 'full':
    cmd = ['bash', str(ROOT / 'Tools/test.sh')]
elif args.kind == 'focused':
    assert args.filter
    cmd = [str(editor), '-batchmode', '-nographics', '-projectPath', str(ROOT), '-runTests',
           '-testPlatform', 'EditMode', '-testFilter', args.filter, '-testResults', str(output / 'EditMode.xml'),
           '-logFile', str(output / 'EditMode.log')]
elif args.kind == 'build':
    env['FRONTIER_BUILD_PATH'] = str(output / 'Frontier Reckoning.app')
    cmd = [str(editor), '-batchmode', '-nographics', '-quit', '-projectPath', str(ROOT),
           '-executeMethod', 'Frontier.Editor.BuildTools.BuildMac', '-logFile', str(output / 'build.log')]
else:
    env['FRONTIER_REPORT_PATH'] = str(output / 'matrix')
    cmd = [str(editor), '-batchmode', '-nographics', '-quit', '-projectPath', str(ROOT),
           '-executeMethod', 'Frontier.Editor.GatherObservation074.RunNormal', '-logFile', str(output / 'normal.log')]
receipt['command'] = cmd
save()
with (output / 'wrapper-output.log').open('w') as stream:
    completed = subprocess.run(cmd, cwd=ROOT, env=env, stdout=stream, stderr=subprocess.STDOUT)
after = evidence.source_files(ROOT)
receipt.update(exitCode=completed.returncode, finishedUtc=datetime.now(timezone.utc).isoformat(),
               sourceFingerprintAfter=evidence.fingerprint(after), sourceStable=before == after,
               changedSources=[p for p in sorted(before.keys() | after.keys()) if before.get(p) != after.get(p)])
if args.kind == 'full' and completed.returncode == 0:
    transcript = (output / 'wrapper-output.log').read_text()
    matches = re.findall(r'This run writes fresh results to: (.+)', transcript)
    assert len(matches) == 1
    reports = Path(matches[0])
    receipt['reportsDirectory'] = str(reports)
    receipt['reportsFingerprint'] = evidence.verify(ROOT, reports)
    receipt['tests'] = {mode: ET.parse(reports / (mode + '.xml')).getroot().attrib for mode in ('EditMode', 'PlayMode')}
elif args.kind == 'focused' and (output / 'EditMode.xml').exists():
    receipt['tests'] = {'EditMode': ET.parse(output / 'EditMode.xml').getroot().attrib}
elif args.kind == 'normal' and (output / 'matrix/manifest.json').exists():
    manifest = json.loads((output / 'matrix/manifest.json').read_text())
    receipt['manifestStatus'] = manifest['status']
    receipt['manifestSourceFingerprint'] = manifest['sourceFingerprint']
elif args.kind == 'build' and completed.returncode == 0:
    app = output / 'Frontier Reckoning.app'
    import plistlib
    info = plistlib.loads((app / 'Contents/Info.plist').read_bytes())
    receipt['buildVersion'] = info['CFBundleShortVersionString']
    receipt['bootConfig'] = (app / 'Contents/Resources/Data/boot.config').read_text()
    receipt['mainExecutableMode'] = oct((app / 'Contents/MacOS/Frontier Reckoning').stat().st_mode & 0o777)
receipt['status'] = 'completed_source_stable' if completed.returncode == 0 and before == after else 'failed_or_source_changed'
save()
print(json.dumps({k: receipt.get(k) for k in ('kind', 'status', 'exitCode', 'sourceStable', 'sourceFingerprintBefore', 'sourceFingerprintAfter', 'reportsDirectory', 'manifestStatus', 'buildVersion')}), flush=True)
sys.exit(completed.returncode if completed.returncode else (0 if before == after else 3))
