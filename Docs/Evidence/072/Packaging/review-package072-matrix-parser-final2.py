#!/usr/bin/env python3
"""Independent final2 AST/schema review; real bounded-scope acceptance, fixture mutations only in memory."""
import ast,copy,difflib,hashlib,importlib.util,json
from pathlib import Path
from datetime import datetime,timezone
D=Path(__file__).absolute().parent;C=D.parent;R=Path('/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project')
oldp=D/'package072-final1.py';newp=D/'package072-final2.py';auditp=D/'independent-normal072-final1.json';normal=C/'NormalRoutesFinal1'
sha=lambda b:hashlib.sha256(b).hexdigest()
def ref(p):b=p.read_bytes();return dict(path=str(p),bytes=len(b),sha256=sha(b))
def module(p,name):
 s=importlib.util.spec_from_file_location(name,p);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
oldraw=oldp.read_bytes();newraw=newp.read_bytes();assert sha(oldraw)=='8144e8f5bb75c88e6f77dfedb15fb7f52a7384a27fe4f5845e5655da67b53f4c'
old=module(oldp,'package072_final1_matrix_review');new=module(newp,'package072_final2_matrix_review')
oldnodes={x.name:x for x in ast.parse(oldraw).body if isinstance(x,(ast.FunctionDef,ast.ClassDef))};newnodes={x.name:x for x in ast.parse(newraw).body if isinstance(x,(ast.FunctionDef,ast.ClassDef))}
unchanged=[]
for name,node in oldnodes.items():
 if name in ['check_gates','verify_audit_coverage']:continue
 assert ast.dump(node,include_attributes=False)==ast.dump(newnodes[name],include_attributes=False),name
 unchanged.append(name)
assert set(newnodes)-set(oldnodes)=={'matrix_audited_refs_gate072_final2'}
class ReverseAdapter(ast.NodeTransformer):
 def visit_Call(self,node):
  node=self.generic_visit(node)
  if isinstance(node.func,ast.Name) and node.func.id=='matrix_audited_refs_gate072_final2':
   first=node.args[0]
   if isinstance(first,ast.Name) and first.id=='audit':return ast.parse("audit.get('filesUnchangedDuringAudit') is True",mode='eval').body
   assert isinstance(first,ast.Name) and first.id=='actual'
   return ast.parse("actual['filesUnchangedDuringAudit'] is True",mode='eval').body
  return node
for name in ['check_gates','verify_audit_coverage']:
 reverted=ReverseAdapter().visit(copy.deepcopy(newnodes[name]));assert ast.dump(reverted,include_attributes=False)==ast.dump(oldnodes[name],include_attributes=False),name
calls=[x for x in ast.walk(ast.parse(newraw)) if isinstance(x,ast.Call) and isinstance(x.func,ast.Name) and x.func.id=='matrix_audited_refs_gate072_final2'];assert len(calls)==2
for name in ['MATRIX','MATRIX_CHECKER_SHA','PROFILE','VERSION','ARCHIVES']:assert getattr(old,name)==getattr(new,name)
checker=D/'independent-normal072.py';assert sha(checker.read_bytes())==old.MATRIX_CHECKER_SHA
# Confirm the same complete semantic comparison key list and subprocess argv.
def matrix_compare_parts(node):
 keys=[];processes=[]
 for x in ast.walk(node):
  if isinstance(x,ast.For) and isinstance(x.iter,ast.Tuple) and all(isinstance(y,ast.Constant) and isinstance(y.value,str) for y in x.iter.elts):keys.append(tuple(y.value for y in x.iter.elts))
  if isinstance(x,ast.Call) and isinstance(x.func,ast.Attribute) and isinstance(x.func.value,ast.Name) and x.func.value.id=='subprocess':processes.append(ast.dump(x,include_attributes=False))
 return keys,processes
assert matrix_compare_parts(oldnodes['verify_audit_coverage'])==matrix_compare_parts(newnodes['verify_audit_coverage'])
selftest=new.self_test();assert selftest['count']==27 and selftest['status']=='fixture_self_tests_passed'
selfp=D/'package072-final2-selftest.json';assert not selfp.exists()
with selfp.open('xb') as f:f.write(new.encoded(selftest))
audit=json.loads(auditp.read_bytes());matrix=json.loads((normal/'matrix/manifest.json').read_bytes());runtime=json.loads((normal/'source-before.json').read_bytes())['files']
seal=dict(trees={'normal':{'directory':str(normal)}},matrixChecker=ref(checker),inputs={k:ref(p) for k,p in [('matrixDeclaration',D/'predeclared-observation072-final1.json'),('matrixManifest',normal/'matrix/manifest.json'),('matrixRunner',normal/'runner-receipt.json'),('matrixSourceBefore',normal/'source-before.json'),('matrixSourceAfter',normal/'source-after.json')]})
inputs=new.Inputs();before={x['path']:x.copy() for x in audit['filesUnchangedDuringAudit']};actual=new.matrix_audited_refs_gate072_final2(audit,seal,runtime,R,matrix,inputs);assert actual is True and len(inputs.files)==844
assert inputs.files==before
fixtures=[]
class FixtureInputs:
 def __init__(self,stable=True,override=None):self.stable=stable;self.override=override or {}
 def read(self,path):
  if path in self.override:return copy.deepcopy(self.override[path])
  if path not in before:raise FileNotFoundError(path)
  return copy.deepcopy(before[path])
 def unchanged(self):return self.stable

def good(label,a=None,s=None,rt=None,m=None,fi=None):
 assert new.matrix_audited_refs_gate072_final2(a or audit,s or seal,rt or runtime,R,m or matrix,fi or FixtureInputs()) is True
 fixtures.append(dict(name=label,expected='accepted',actual='accepted'))
def bad(label,edit=None,editseal=None,editruntime=None,editmatrix=None,fi=None,drop=False):
 a=copy.deepcopy(audit);s=copy.deepcopy(seal);rt=copy.deepcopy(runtime);m=copy.deepcopy(matrix)
 if edit:edit(a)
 if editseal:editseal(s)
 if editruntime:editruntime(rt)
 if editmatrix:editmatrix(m)
 try:new.matrix_audited_refs_gate072_final2(a,s,rt,R,m,fi or FixtureInputs())
 except (ValueError,OSError,KeyError,TypeError) as e:fixtures.append(dict(name=label,expected='rejected',actual='rejected',errorType=type(e).__name__,error=str(e)[:240]));return
 raise AssertionError('Expected rejection: '+label)
good('actual 844 frozen ref list in cached semantic fixture')
for value,label in [(True,'boolean true insufficient'),(False,'boolean false'),(None,'missing value'),({},'object instead of list'),([],'empty list')]:bad(label,lambda a,v=value:a.update(filesUnchangedDuringAudit=v))
bad('missing property',lambda a:a.pop('filesUnchangedDuringAudit'))
bad('843 refs missing a source',lambda a:a['filesUnchangedDuringAudit'].pop(10))
bad('845 refs surplus duplicate',lambda a:a['filesUnchangedDuringAudit'].append(copy.deepcopy(a['filesUnchangedDuringAudit'][0])))
bad('844 refs duplicate replacing required path',lambda a:a['filesUnchangedDuringAudit'].__setitem__(10,copy.deepcopy(a['filesUnchangedDuringAudit'][9])))
idx=next(i for i,x in enumerate(audit['filesUnchangedDuringAudit']) if x['path'].endswith('/winding-ottoman-44/events.jsonl'))
for key,value,label in [('sha256','f'*64,'tampered artifact hash'),('bytes',64259622,'tampered artifact bytes'),('path','/tmp/foreign.json','unrelated absolute path'),('path','relative.json','relative path'),('identity',[1,2],'truncated identity'),('identity',[1,2,33188,64259623,1,True],'boolean identity integer'),('sha256','G'*64,'malformed SHA'),('bytes',True,'boolean size'),('bytes',-1,'negative size')]:bad(label,lambda a,k=key,v=value:a['filesUnchangedDuringAudit'][idx].__setitem__(k,v))
bad('missing identity',lambda a:a['filesUnchangedDuringAudit'][idx].pop('identity'))
bad('extra ref property',lambda a:a['filesUnchangedDuringAudit'][idx].update(extra='not allowed'))
bad('same bytes/hash but changed filesystem inode',lambda a:a['filesUnchangedDuringAudit'][idx]['identity'].__setitem__(1,a['filesUnchangedDuringAudit'][idx]['identity'][1]+1))
bad('same bytes/hash but changed filesystem ctime',lambda a:a['filesUnchangedDuringAudit'][idx]['identity'].__setitem__(5,a['filesUnchangedDuringAudit'][idx]['identity'][5]+1))
bad('directory mode instead of regular',lambda a:a['filesUnchangedDuringAudit'][idx]['identity'].__setitem__(2,0o40755))
bad('false unchanged stability',fi=FixtureInputs(stable=False))
row=copy.deepcopy(audit['filesUnchangedDuringAudit'][idx]);row['sha256']='f'*64
bad('current read differs from stored audit row',fi=FixtureInputs(override={row['path']:row}))
bad('changed frozen runtime SHA',editruntime=lambda rt:rt.__setitem__('Assets/Resources/Rules.json','f'*64))
bad('missing frozen runtime input',editruntime=lambda rt:rt.pop('Assets/Resources/Rules.json'))
bad('extra frozen runtime input',editruntime=lambda rt:rt.update({'Assets/extra.cs':'f'*64}))
bad('tampered checker hash',editseal=lambda s:s['matrixChecker'].__setitem__('sha256','f'*64))
bad('changed checker path',editseal=lambda s:s['matrixChecker'].__setitem__('path',str(D/'substitute.py')))
bad('sealed manifest bytes differ',editseal=lambda s:s['inputs']['matrixManifest'].__setitem__('bytes',0))
bad('sealed declaration hash differs',editseal=lambda s:s['inputs']['matrixDeclaration'].__setitem__('sha256','f'*64))
bad('projection branch coverage differs',editmatrix=lambda m:m['cases'][0].__setitem__('comparisonReached',True))
bad('matrix case missing',editmatrix=lambda m:m['cases'].pop())
bad('matrix case identity differs',editmatrix=lambda m:m['cases'][0].__setitem__('id','foreign'))
# The audit result/closed-process/source gates outside this adapter stay byte-for-byte identical after inverse replacement.
actual_refset=set(before);runtime_paths={str(R/p) for p in runtime};normal_paths={x['path'] for x in audit['filesUnchangedDuringAudit'] if x['path'].startswith(str(normal)+'/')}
assert len(runtime_paths)==718 and len(normal_paths)==124 and len(actual_refset)==844
review=dict(schema='package072-final2-matrix-schema-adapter-review-1',status='passed_exact_matrix_ref_schema_review',createdUtc=datetime.now(timezone.utc).isoformat(),oldTool=ref(oldp),newTool=ref(newp),fixedChecker=ref(checker),actualMatrixAudit=ref(auditp),actualNormalInventory=ref(D/'normal072-final1-inventory.json'),actualPositive=dict(status='passed_real_844_refs_bytes_sha_identity_and_stability',auditedRefs=844,runtimeRefs=718,normalAuditedRefs=124,checkerAndDeclarationRefs=2,originalFileBytesRead=sum(x['bytes'] for x in before.values()),allReadRecordsEqualActualFrozenAuditor=True,actualObservationStatus=audit['observationStatus'],actualRoutesReached=audit['observedRoutesReached'],actualRoutesTotal=audit['observedRoutesTotal'],finalReleaseAccepted=False),astReview=dict(unchangedTopLevelFunctionsClasses=unchanged,modifiedFunctions=['check_gates','verify_audit_coverage'],exactlyTwoBooleanExpectationSitesReplaced=True,functionsRestoredExactlyByInverseAdapter=True,addedFunctions=['matrix_audited_refs_gate072_final2'],nativeGateAstUnchanged=True,sourceFullTestsBuildInventoryZipAstUnchanged=True,fixedCheckerSemanticRereadArgvAndComparisonKeysUnchanged=True),syntheticSelfTest=ref(selfp),semanticFixtures=fixtures,semanticFixtureCount=len(fixtures),oldOriginalsPreserved=True,actualReleasePackaged=False,scope='Exact existing844-reference schema adapter only. No raw auditor/matrix/source/native changes;16 consistency does not mean16routes reached or balance accepted. Historical final1 review remains unchanged.')
raw=new.encoded(review);jp=D/'072-package-matrix-parser-final2-review.json';mp=jp.with_suffix('.md');diffp=D/'package072-final1-to-final2.diff';assert all(not x.exists() for x in [jp,mp,diffp])
with jp.open('xb') as f:f.write(raw)
lines=['# package072 final2 matrix reference schema adapter review','','Status: passed exact matrix reference schema review. No release packaged.','','Actual fixed audit: Consistent;16closed cases;9routes reached; observation=observed_routes_incomplete; balance Not assessed.','','Only two boolean expectation sites were replaced by a new strict helper. Every other existing AST function/class is identical; inverse adapter reconstructs check_gates and verify_audit_coverage exactly. Native/source/full1567/build/tree/ZIP gates and pinned auditor subprocess/semantic comparison keys remain unchanged.','','The helper requires exactly844 unique canonical ref paths:718source files +124audited Normal files +checker/declaration. It validates strict ref fields, actual size/SHA256/all6filesystem identity fields, regular modes, exact sealed input refs, frozen runtime SHA map, complete source/case/projection namespaces and actual before/after stability. Boolean true is rejected.','', 'Actual positive: all844 real source/raw refs match the fixed auditor and remain stable. Existing27 synthetic self-tests passed; %d additional semantic fixtures passed.'%len(fixtures),'','Limits:','', '- Evidence consistency does not imply16route successes, balanced civilizations, continuous movement quality or whole-release acceptance.','- The unchanged old verify_audit_coverage comment about missing exported hashes is historical; final2 helper binds the auditor\'s actual exported844refs and retains the same semantic reread comparison.','- Final1/prepared originals and historical reviews remain untouched.','', 'New tool SHA256: '+ref(newp)['sha256'],'Review JSON SHA256: '+sha(raw),'']
with mp.open('xb') as f:f.write('\n'.join(lines).encode())
with diffp.open('xb') as f:f.write(''.join(difflib.unified_diff(oldraw.decode().splitlines(keepends=True),newraw.decode().splitlines(keepends=True),fromfile='package072-final1.py',tofile='package072-final2.py')).encode())
print(json.dumps(dict(status=review['status'],fixtures=len(fixtures),selftests=selftest['count'],outputs=[ref(x) for x in [newp,selfp,jp,mp,diffp]],unchangedAstCount=len(unchanged),actualRefs=844),indent=2))
