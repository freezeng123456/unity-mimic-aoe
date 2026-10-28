from pathlib import Path
import datetime, hashlib, json, re, shutil

BASE=Path('/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.15.0/Candidate1/Diagnostics/PerfProbe072')
MAIN=Path('/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project')
COPY=BASE/'project'
PREP=BASE/'PatchedCopyPreparation1'
MANIFEST=BASE/'step-instrumentation-candidate1-manifest072.json'
if PREP.exists() or MANIFEST.exists():
    raise RuntimeError('Exclusive new preparation/manifest required; preserve previous evidence')
PREP.mkdir()

def stamp(): return datetime.datetime.now(datetime.timezone.utc).isoformat()
def info(p):
    b=p.read_bytes()
    return {'file':str(p),'bytes':len(b),'sha256':hashlib.sha256(b).hexdigest()}
def relative_info(p, root):
    value=info(p);value['path']=str(p.relative_to(root));return value

def write_json(path,data):
    with path.open('x') as f: json.dump(data,f,indent=2);f.write('\n')

modified=[
 'Assets/Frontier/Simulation/AiResourceContinuity072.cs',
 'Assets/Frontier/Simulation/GridNavigation.cs',
 'Assets/Frontier/Presentation/FrontierHud.cs',
 'Assets/Frontier/Presentation/GameController.cs',
 'Assets/Frontier/Tests/PlayMode/RevolutionHud071Tests.cs']
unchanged=[
 'Assets/Frontier/Simulation/SkirmishAI.cs',
 'Assets/Frontier/Simulation/World.cs',
 'Assets/Frontier/Simulation/AiArmyPlan072.cs',
 'Assets/Frontier/Simulation/PerfProbeDiagnostics072.cs',
 'Assets/Frontier/Editor/EditorProbe072.cs',
 'Assets/Frontier/Editor/EditorStepProbe072.cs']
prior_manifests=[BASE/'instrumentation-manifest072.json',BASE/'step-instrumentation-manifest072.json',BASE/'copy-receipt.json']
# Seal every replaced source and all retained instrumented execution code first.
old_sources=[]
for rel in modified+unchanged:
    original=COPY/rel;archived=PREP/'source-before'/rel
    archived.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(original,archived)
    old_sources.append({'original':info(original),'archive':info(archived)})
old_manifests=[]
for original in prior_manifests:
    if original.exists():
        archived=PREP/'manifests-before'/original.name
        archived.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(original,archived)
        old_manifests.append({'original':info(original),'archive':info(archived)})
baseline_files=[relative_info(p,BASE/'StepRun1') for p in sorted((BASE/'StepRun1').rglob('*')) if p.is_file()]
baseline=json.loads((BASE/'StepRun1'/'receipt.json').read_text())
if baseline['status']!='declared_continuation_completed' or baseline['completedSteps']!=250 or not baseline['startingStateDigestMatches']:
    raise RuntimeError('Require actually closed original 250-step baseline')
main_before={rel:info(MAIN/rel) for rel in modified}
receipt={'schema':'frontier-isolated-step-patched-preparation072-1','startedUtc':stamp(),
 'scope':'Only isolated copy sources. Original main files, source manifests and StepRun1 raw evidence retained byte-for-byte.',
 'beforeSources':old_sources,'priorManifests':old_manifests,'baselineFiles':baseline_files,'baselineReceipt':baseline,
 'mainInputsBefore':main_before,'retainedInstrumentation':unchanged}
write_json(PREP/'source-before-receipt.json',receipt)

# Current root-approved main decisions, plus the actual UI/glyph fix sources.
for rel in modified: shutil.copyfile(MAIN/rel,COPY/rel)

added=[]
def insert(s,needle,code):
    if s.count(needle)!=1: raise RuntimeError('Expected one instrumentation anchor: '+needle[:130])
    block='#if UNITY_EDITOR\n'+code+'\n#endif\n'
    added.append(needle.splitlines()[0].strip())
    return s.replace(needle,needle+block,1)
def timed(s,needle,name): return insert(s,needle,'            using var perfTimer072=PerfProbeDiagnostics072.Measure('+name+');')
def count(s,needle,counter): return insert(s,needle,'            if(PerfProbeDiagnostics072.Enabled)PerfProbeDiagnostics072.'+counter+'++;')

path=COPY/modified[0];s=path.read_text()
s=timed(s,'        SourceSurvey Survey(List<EntityState> workers,List<EntityState> visible,bool allowScope)\n        {\n','"SourceSurvey"')
s=timed(s,'        Plan EvaluatePlan(List<EntityState> own,List<EntityState> workers,List<EntityState> visible,SourceSurvey surveyed)\n        {\n','"EvaluatePlan"')
s=timed(s,'        float SourceAmountInSurvey(ResourceKind kind,List<EntityState> workers,SurveySafety safety,bool threatenedPaths,out bool available,bool firstOnly=false)\n        {\n','"SourceAmount"')
s=timed(s,'        bool ReachableSource(EntityState source,List<EntityState> workers,SurveySafety safety,bool threatenedPaths)\n        {\n','"ReachableSource"')
s=count(s,'            public bool Safe(Vector2 point)\n            {\n','SafeCalls')
s=count(s,'            public bool SegmentSafe(Vector2 from,Vector2 to)\n            {\n','SegmentSafeCalls')
s=timed(s,'            public bool MayConnect(Vector2 from,Vector2 to,float radius)\n            {\n','"SourceMayConnect"')
s=count(s,'        public static bool Safe(World world,int player,Vector2 point,List<EntityState> visible)\n        {\n','SafeCalls')
s=count(s,'        public static bool SegmentSafe(World world,int player,Vector2 from,Vector2 to,List<EntityState> visible)\n        {\n','SegmentSafeCalls')
s=timed(s,'        public static bool TryConstructionWorker(World world,int player,EntityDef definition,Vector2 point,\n            List<EntityState> workers,List<EntityState> visible,out EntityState selected,int foundation=0)\n        {\n','"TryConstructionWorker"')
# Expression conversion is purely for adding a counter and is explicitly undone
# by the token-identity audit below.
pattern=r'(        public static bool EconomicThreat\(World world,int player,EntityState e\))=>(.*?);\n'
match=re.search(pattern,s,re.S)
if not match: raise RuntimeError('EconomicThreat expression anchor missing')
body=match.group(1)+'\n        {\n#if UNITY_EDITOR\n            if(PerfProbeDiagnostics072.Enabled)PerfProbeDiagnostics072.EconomicThreatCalls++;\n#endif\n            return '+match.group(2)+';\n        }\n'
s=s[:match.start()]+body+s[match.end():]
path.write_text(s)

path=COPY/modified[1];s=path.read_text()
s=timed(s,'        public List<Vector2> FindPath(Vector2 start,Vector2 end,float radius=.4f,Func<Vector2,bool> extraWalkable=null,Func<Vector2,Vector2,bool> extraSegmentWalkable=null)\n        {\n','extraWalkable!=null||extraSegmentWalkable!=null?"FindPathFiltered":"FindPathOrdinary"')
s=count(s,'                while(heap.Count>0)\n                {\n','PathNodePops')
s=count(s,'        private bool PathClearLine(Vector2 start,Vector2 end,float radius,Func<Vector2,bool> extraWalkable,Func<Vector2,Vector2,bool> extraSegmentWalkable)\n        {\n','PathClearLineCalls')
s=timed(s,'            public bool MayConnect(Vector2 start,Vector2 end)\n            {\n','"FilteredMayConnect"')
s=timed(s,'            void BuildComponents()\n            {\n','"FilteredBuildComponents"')
path.write_text(s)

# Remove only added editor counter/timer blocks. Convert the single instrumented
# expression back before comparing lexical tokens; no decision changes allowed.
def strip_instrumentation(s):
    s=re.sub(r'^[ \t]*#if UNITY_EDITOR\n.*?^[ \t]*#endif\n','',s,flags=re.M|re.S)
    s=re.sub(r'(public static bool EconomicThreat\(World world,int player,EntityState e\))\s*\{\s*return\s+(.*?);\s*\}',lambda m:m.group(1)+'=>'+m.group(2)+';',s,flags=re.S)
    return s

def tokens(s):
    pattern=r'//[^\n]*|/\*.*?\*/|@"(?:[^"]|"")*"|"(?:\\.|[^"\\])*"|\w+|[^\s]'
    return [m.group() for m in re.finditer(pattern,s,re.S) if not m.group().startswith(('//','/*'))]

comparisons=[]
for rel in modified:
    same=tokens(strip_instrumentation((COPY/rel).read_text()))==tokens((MAIN/rel).read_text())
    comparisons.append({'path':rel,'main':info(MAIN/rel),'copy':info(COPY/rel),'strippedDecisionTokensIdentical':same})
    if not same: raise RuntimeError('Decision tokens changed: '+rel)
for rel in unchanged[:3]:
    same=tokens(strip_instrumentation((COPY/rel).read_text()))==tokens((MAIN/rel).read_text())
    comparisons.append({'path':rel,'main':info(MAIN/rel),'copy':info(COPY/rel),'strippedDecisionTokensIdentical':same,'retainedOriginalInstrumentation':True})
    if not same: raise RuntimeError('Retained runtime decisions drifted: '+rel)
for rel in modified:
    if info(MAIN/rel)!=main_before[rel]: raise RuntimeError('Main input changed during preparation: '+rel)
for item in old_manifests:
    if info(Path(item['original']['file']))!=item['original']: raise RuntimeError('Prior manifest changed')
for item in baseline_files:
    p=BASE/'StepRun1'/item['path']
    if info(p)['sha256']!=item['sha256']: raise RuntimeError('Baseline raw changed')
for rel in unchanged:
    initial=next(row['original'] for row in old_sources if row['original']['file']==str(COPY/rel))
    if info(COPY/rel)!=initial: raise RuntimeError('Retained execution code changed: '+rel)

manifest={'schema':'frontier-isolated-step-instrumentation-candidate1-072-1','status':'sealed_not_unity_compiled_or_run',
 'finishedPreparationUtc':stamp(),'executeMethod':'Frontier.Editor.EditorStepProbe072.Run','declaredFixedSteps':250,
 'baselineStatus':baseline['status'],'baselineFinalStateDigest':baseline['finalStateDigest'],
 'beforeReceipt':info(PREP/'source-before-receipt.json'),'originalManifestsRetained':True,'originalStepRun1RawUnchanged':True,
 'mainInputs':comparisons,'retainedExecutionCode':[info(COPY/rel) for rel in unchanged],
 'newCountersAndTimers':['SourceSurvey','EvaluatePlan','SourceAmount (actual SourceAmountInSurvey)','ReachableSource',
   'SourceMayConnect','FilteredMayConnect','FilteredBuildComponents','FindPathFiltered/Ordinary','TryConstructionWorker',
   'SafeCalls counts both immutable circle Safe and original static Safe','SegmentSafeCalls includes immutable circle direct-line test',
   'EconomicThreatCalls','PathNodePops','PathClearLineCalls','retained Tick.P0/P1 and ReviewDecision.P0/P1, World.Tick and Home'],
 'scope':'Main runtime and UI decisions match current copied files after stripping Editor instrumentation. Retained SkirmishAI/World/AiArmy instrumentation matches current main tokens. No main source/old raw modified. Test copy only for compilation, not execution; new FilteredConnectivity test source remains main-only and does not run in this step probe.',
 'comparisonRequired':['identical exact anchor/prefix and starting digest','same 250 completed fixed steps','identical final world/controller JSON bytes','identical new event JSONL bytes','identical final state digest','recorded timings and counters, no release FPS claim'],
 'commandEnvironment':{'FRONTIER_PERF_ANCHOR':str(BASE/'NativeAnchor1470'/'anchor.json'),'FRONTIER_PERF_EVENTS_PREFIX':str(BASE/'NativeAnchor1470'/'recorded-events-prefix.json'),'FRONTIER_PERF_OUTPUT':str(BASE/'StepRun2')},
 'limits':['Editor diagnostic instrumentation and concurrent CPU influence wall time','No renderer or future external ReplayRecords applied','No runtime resources, HP, fog or winners injected','Existing copy package environment retained from actual root UPM repair','This is not final full/native/matrix acceptance']}
write_json(MANIFEST,manifest)
print(json.dumps({'manifest':info(MANIFEST),'beforeReceipt':info(PREP/'source-before-receipt.json'),'tokenChecks':len(comparisons),'executeMethod':manifest['executeMethod'],'environment':manifest['commandEnvironment']},indent=2))
