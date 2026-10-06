#!/usr/bin/env python3
"""Independent final1 parser review. In-memory fixture mutations, no real edits."""
import ast
import copy
import hashlib
import importlib.util
import json
from pathlib import Path
from datetime import datetime, timezone

D = Path(__file__).absolute().parent
C = D.parent
old_path = D / 'package072-prepared.py'
new_path = D / 'package072-final1.py'
journal_path = C / 'NativeObserved/FinalGuid072/native-journal072-final.json'
old_raw = old_path.read_bytes()
new_raw = new_path.read_bytes()
sha = lambda raw: hashlib.sha256(raw).hexdigest()
assert sha(old_raw) == '8b323316cf878b0be4e37233be49f5a5b9a233328de4ec1b3f82bd289ecd4801'
assert sha(journal_path.read_bytes()) == '4d98e829279aa259e1053cd87e8513c6a6fdd6c83769a38adeee4259c2564bc3'

def ref(path):
    raw = path.read_bytes()
    return {'path': str(path), 'bytes': len(raw), 'sha256': sha(raw)}

def load_module(path, name):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module

old = load_module(old_path, 'package072_prepared_review')
new = load_module(new_path, 'package072_final1_review')
assert new.MATRIX_CHECKER_SHA == old.MATRIX_CHECKER_SHA
assert new.MATRIX == old.MATRIX and new.PROFILE == old.PROFILE and new.VERSION == old.VERSION
assert new.ARCHIVES == old.ARCHIVES
old_functions = {f.name: f for f in ast.parse(old_raw).body if isinstance(f, (ast.FunctionDef, ast.AsyncFunctionDef, ast.ClassDef))}
new_functions = {f.name: f for f in ast.parse(new_raw).body if isinstance(f, (ast.FunctionDef, ast.AsyncFunctionDef, ast.ClassDef))}
unchanged = []
for name, node in old_functions.items():
    if name == 'check_gates':
        continue
    assert ast.dump(node, include_attributes=False) == ast.dump(new_functions[name], include_attributes=False), name
    unchanged.append(name)
old_text, new_text = old_raw.decode(), new_raw.decode()
start = old_text.index("    native = docs['nativeJournal']; native_identity(native, seal)\n")
matrix_start = old_text.index("    matrix, audit, declaration = docs['matrixManifest'], docs['matrixAudit'], docs['matrixDeclaration']\n", start)
next_function = old_text.index('\ndef zip_info(', matrix_start)
new_native_start = new_text.index("    native = docs['nativeJournal']; native_gate072_final1(native, seal, inputs)\n")
new_matrix_start = new_text.index("    matrix, audit, declaration = docs['matrixManifest'], docs['matrixAudit'], docs['matrixDeclaration']\n", new_native_start)
new_next_function = new_text.index('\ndef zip_info(', new_matrix_start)
assert old_text[matrix_start:next_function] == new_text[new_matrix_start:new_next_function]
old_gate_start = old_text.index('def check_gates(seal, inputs):')
new_gate_start = new_text.index('def check_gates(seal, inputs):')
assert old_text[old_gate_start:start] == new_text[new_gate_start:new_native_start]
inputs = new.Inputs()
native = inputs.ref(ref(journal_path))
seal = dict(native['identity'], nativeOriginalDirectory=native['nativeOriginalDirectory'])
actual = new.native_gate072_final1(native, seal, inputs)
assert actual['scopeCount'] == 9 and actual['processCount'] == 5 and actual['sourceAuditCount'] == 8
assert actual['rawOriginalFiles'] == 116 and actual['rawOriginalBytes'] == 269005909
assert actual['sourceAuditChecks'] == 3659 and actual['finalReleaseAccepted'] is False

# Logical schema fixtures use pinned, already-verified reference triples and
# cached read-only JSON values. Override values are in-memory only. This isolates
# semantic rejection from the actual 269MB original-byte positive check above.
pin_refs = {path: {key: item[key] for key in ('path', 'bytes', 'sha256')} for path, item in inputs.files.items()}
parsed_cache = {}

class FixtureInputs:
    def __init__(self, overrides=None, stable=True):
        self.overrides = overrides or {}
        self.stable = stable

    def ref(self, pinned, document=True):
        path = pinned.get('path') if isinstance(pinned, dict) else None
        if pin_refs.get(path) != pinned:
            raise ValueError('Fixture reference differs from real pinned original')
        if not document:
            return copy.deepcopy(pin_refs[path])
        if path in self.overrides:
            return copy.deepcopy(self.overrides[path])
        if path not in parsed_cache:
            parsed_cache[path] = json.loads(Path(path).read_bytes())
        return copy.deepcopy(parsed_cache[path])

    def unchanged(self):
        return self.stable

fixture_results = []
assert new.native_gate072_final1(copy.deepcopy(native), seal, FixtureInputs()) == actual
fixture_results.append({'name': 'actual schema with pinned logical snapshot accepted only bounded scopes', 'expected': 'accept', 'passed': True})

def reject(label, mutate=None, override=None, stable=True):
    document = copy.deepcopy(native)
    if mutate:
        mutate(document)
    overrides = {}
    if override:
        path, modifier = override
        if path not in parsed_cache:
            parsed_cache[path] = json.loads(Path(path).read_bytes())
        value = copy.deepcopy(parsed_cache[path])
        modifier(value)
        overrides[path] = value
    try:
        new.native_gate072_final1(document, seal, FixtureInputs(overrides, stable))
    except (ValueError, KeyError, TypeError, OSError) as error:
        fixture_results.append({'name': label, 'expected': 'reject', 'passed': True,
                                'actualErrorType': type(error).__name__, 'actualError': str(error)})
        return
    raise AssertionError('Fixture should have been rejected: ' + label)

reject('missing required scope', lambda d: d['scopes'].pop())
reject('duplicate required scope ID', lambda d: d['scopes'][1].update(id=d['scopes'][0]['id']))
reject('unknown required scope ID', lambda d: d['scopes'][0].update(id='unrequested_scope'))
reject('reordered required scope IDs', lambda d: d['scopes'].reverse())
reject('pending native assertion', lambda d: d['pending'].append('cold still missing'))
reject('pending scope assertion', lambda d: d['scopes'][0]['pendingAssertions'].append('not observed'))
reject('scope failed source audit flag', lambda d: d['scopes'][1].update(allSourceAuditChecksPassed=False))
reject('scope limits not explicit', lambda d: d['scopes'][1].update(scopeLimitsExplicit=False))
for field in ['nativeAllPassed', 'allRequiredProcessesClosed', 'rawNativeOriginalTreeFrozen',
              'originalInputsStableBeforeAndAfter', 'scopePlanOriginalRemainsImmutable', 'selfReferenceExcluded']:
    reject('false actual acceptance flag ' + field, lambda d, field=field: d.update({field: False}))
reject('whole release overclaim', lambda d: d.update(finalReleaseAccepted=True))
reject('old journal schema', lambda d: d.update(schema='old_observed_native'))
reject('invented global manual acceptance', lambda d: d.update(manualObservationStatus='observed_selected_native_scope'))
reject('wrong fixed plan hash', lambda d: d['scopePlan'].update(sha256='0' * 64))
reject('scope assertion not bound to original plan', lambda d: d['scopes'][0]['scopePlanAssertions'].pop())
reject('scope description not bound to original plan', lambda d: d['scopes'][0].update(description='new unrelated scope'))
reject('missing actual scope evidence', lambda d: d['scopes'][3].update(evidenceReferences=[]))
reject('wrong scope audit set', lambda d: d['scopes'][3].update(evidenceReferences=d['scopes'][2]['evidenceReferences']))
reject('missing outside audit original', lambda d: d['outsideAudits'].pop())
reject('source check sum differs', lambda d: d.update(sourceAuditCheckCountSum=3658))
reject('source failure count nonzero', lambda d: d.update(sourceAuditFailureCount=1))
reject('native process still running', lambda d: d['processes'][0].update(status='running'))
reject('missing actual process', lambda d: d['processes'].pop())
reject('duplicate actual PID', lambda d: d['processes'][1].update(pid=d['processes'][0]['pid']))
reject('journal PID disagrees with actual process metadata', lambda d: d['processes'][0].update(pid=1234567))
process_path = native['processes'][0]['processMetadata']['path']
reject('actual process metadata still running', override=(process_path, lambda d: d.update(status='running')))
reject('actual process metadata exit nonzero', override=(process_path, lambda d: d.update(exitCode=1)))
reject('unknown pixel record status', lambda d: d['manualObservations'][0].update(status='observed_all_pixels'))
reject('old broad observed pixel status not promoted', lambda d: d['manualObservations'][0].update(status='observed'))
reject('pixel record not actually observed', lambda d: d['manualObservations'][0].update(actuallyObserved=False))
reject('pixel record missing description', lambda d: d['manualObservations'][0].update(description=''))
reject('pixel record refers to nonexistent capture', lambda d: d['manualObservations'][0].update(captureIds=['not-a-real-capture']))
reject('capture reference hash differs', lambda d: d['captures'][0]['png'].update(sha256='0' * 64))
capture_path = native['captures'][0]['json']['path']
reject('actual capture wrong build GUID', override=(capture_path, lambda d: d.update(buildGuid='0' * 32)))
reject('actual capture wrong paired PNG SHA', override=(capture_path, lambda d: d.update(pngSha256='0' * 64)))
reject('raw original wrong relative path', lambda d: d['originalFiles'][0].update(relativePath='Other/file.json'))
def omit_original(document):
    item = document['originalFiles'].pop()
    document['originalFileCount'] -= 1
    document['originalBytes'] -= item['bytes']
reject('incomplete raw native tree even with adjusted summary', omit_original)
audit_path = native['scopes'][3]['evidenceReferences'][0]['path']
reject('actual source audit has failed check', override=(audit_path, lambda d: d['checks'][0].update(passed=False)))
reject('actual source audit unknown pass status', override=(audit_path, lambda d: d.update(passed=False)))
reject('actual original snapshot becomes unstable', stable=False)
matrix_runner = json.loads((C / 'NormalRoutesFinal1/runner-receipt.json').read_bytes())
try:
    new.runner(matrix_runner, 'routes', native['identity']['sourceFingerprint'])
except ValueError as error:
    matrix_block = {'status': 'current_running_matrix_still_rejected', 'actualError': str(error)}
else:
    # A concurrently finished real runner is allowed to close; never simulate it.
    assert matrix_runner.get('finishedAtUtc') and matrix_runner.get('allProcessesSucceeded') is True
    matrix_block = {'status': 'actual_runner_has_now_closed_no_full_package_claim'}
synthetic = new.self_test()
assert synthetic['count'] == 27 and synthetic['actualReleasePackaged'] is False
assert inputs.unchanged()
assert old_path.read_bytes() == old_raw and new_path.read_bytes() == new_raw
assert sha(journal_path.read_bytes()) == '4d98e829279aa259e1053cd87e8513c6a6fdd6c83769a38adeee4259c2564bc3'
checker = ref(D / 'independent-normal072.py')
assert checker['sha256'] == old.MATRIX_CHECKER_SHA
self_test_record = dict(synthetic, tool=ref(new_path), createdUtc=datetime.now(timezone.utc).isoformat())
review = {
    'schema': 'frontier-package072-final1-native-parser-review-1',
    'status': 'passed_actual_bounded_schema_and_adversarial_fixtures',
    'createdUtc': datetime.now(timezone.utc).isoformat(),
    'baseTool': ref(old_path), 'derivedTool': ref(new_path), 'reviewTool': ref(Path(__file__).absolute()),
    'actualNativeJournal': ref(journal_path), 'actualNativeGateResult': actual,
    'actualOriginalStable': True, 'oldToolAndJournalUnmodified': True,
    'unchangedOriginalFunctionOrClassCount': len(unchanged), 'unchangedOriginalFunctionsOrClasses': unchanged,
    'preNativeSourceFullTestsBuildGateBytesUnchanged': True,
    'matrixGateBytesUnchanged': True,
    'matrixChecker': checker, 'matrixCheckerUnmodified': True,
    'remainingRunnerGateObserved': matrix_block,
    'sameOriginalSynthetic27': self_test_record,
    'newSchemaFixtureCount': len(fixture_results), 'newSchemaFixtures': fixture_results,
    'allSchemaFixturesPassed': True,
    'scopeNote': 'Nine declared bounded native scopes only. Partial and selected pixel records remain facts, not independent whole-native acceptance. Eight JSON audits plus eight MD refs, not nine unique source audit files.',
    'actualNativeProjection': 'Original refs carrying relativePath are validated against exact full raw tree and projected to path/bytes/SHA for the unchanged reader. Original journal bytes are never rewritten.',
    'limits': [
        'Parser structural and file binding review; does not re-execute GUI/combat/cold/replay or inspect new pixels.',
        'Logical negative fixtures are in-memory copies with a pinned read-only reference snapshot. Actual positive read checks original native bytes and stable lstat independently.',
        'No final seal, full package --check, release ZIP, upload, commit or push performed.',
        'All existing source/full1567/build/inventory/archive/CRC gates and fixed16 matrix requirements remain unchanged.'
    ],
    'notPerformed': ['Unity', 'GUI', 'source or Docs edits', 'raw original mutation', 'final seal', 'ZIP package', 'network', 'upload', 'commit', 'push'],
}
outputs = [
    (D / 'package072-final1-selftest.json', self_test_record),
    (D / '072-package-native-parser-final1-review.json', review),
]
for path, document in outputs:
    raw = (json.dumps(document, ensure_ascii=False, sort_keys=True, indent=2) + '\n').encode()
    with path.open('xb') as stream:
        stream.write(raw)
    print(json.dumps({'path': str(path), 'bytes': len(raw), 'sha256': sha(raw)}))
md = [
    '# 072 final1 native parser review', '',
    'Status: passed actual bounded schema and adversarial fixtures. No final package or release approval.', '',
    'The derived tool is package072-final1.py; package072-prepared.py remains byte-identical. The actual final journal SHA remains ' + ref(journal_path)['sha256'] + '.', '',
    'Actual positive input: nine scopes, five exited native processes/PIDs, 116 originals (269005909 bytes), 16 outside originals (eight JSON audits plus eight MD files), 3659 passed audit checks, 40 F9 pairs and 15 concretely identified pixel records.', '',
    'The original scope plan is pinned by its exact path/bytes/SHA and its nine ordered IDs/assertions/descriptions. Each scope must retain passed_declared_bounded_native_scope, empty pendingAssertions, true allSourceAuditChecksPassed/scopeLimitsExplicit and the exact correct audit refs.', '',
    'Actual process metadata/PID/exit0/start/end/GUID/command/profile are matched. Capture IDs point to hashed real F9 JSON+PNG originals and the matching GUID/PNG SHA/frame/worldTime/dimensions. Pixel labels partial_observation_recorded_not_acceptance and observed_selected_pixels are retained as actual pixel facts, never renamed broad observed acceptance.', '',
    'The extended original refs keep their validated relativePath. Only an exact triple projection is supplied to the unchanged hash reader. Full raw-tree coverage excludes exactly the final/running journals for self-reference; no other file may be omitted.', '',
    'Original self-test: 27/27. Additional logical schema fixtures: ' + str(len(fixture_results)) + '/' + str(len(fixture_results)) + '. Negative cases cover missing/duplicate scopes, pending/false flags, wrong plan bindings/audit sets, actual partial process, PID mismatch, unknown/unobserved pixels, unknown capture/PNG identity, raw-tree omission and unstable inputs.', '',
    'Independent comparison: all ' + str(len(unchanged)) + ' original non-check_gates functions/classes have identical ASTs. The exact source/full-tests/build prefix and full matrix gate bytes are unchanged. The fixed independent matrix checker SHA is unchanged. Current matrix runner result: ' + matrix_block['status'] + '.', '',
    'finalReleaseAccepted remains false, releaseAcceptance remains accepted_declared_native_scopes_only. This schema adaptation closes only the real bounded native prerequisite; it does not let an unfinished matrix through or make whole-replay/FPS/all-pixels/LAN claims.', '',
    'No Unity/GUI/source/Docs/original edits, final seal, full package --check, release ZIP, upload, commit or push. Fixtures alter only in-memory copies; the synthetic temporary ZIPs are the original self-test fixtures and were removed by that self-test.', '',
    'Exact tool/reference hashes and all fixture outcomes are in 072-package-native-parser-final1-review.json.',
]
path = D / '072-package-native-parser-final1-review.md'
raw = ('\n'.join(md) + '\n').encode()
with path.open('xb') as stream:
    stream.write(raw)
print(json.dumps({'path': str(path), 'bytes': len(raw), 'sha256': sha(raw)}))
print(json.dumps({'nativeSchema': 'passed_bounded_only', 'original27': 27, 'newSchemaFixtures': len(fixture_results),
                  'unchangedFunctionsOrClasses': len(unchanged), 'matrix': matrix_block,
                  'finalReleaseAccepted': False, 'packageCreated': False}))
