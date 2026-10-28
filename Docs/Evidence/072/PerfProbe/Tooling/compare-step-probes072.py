#!/usr/bin/env python3
"""Independent byte/state/event audit of two closed isolated 250-step probes."""
import argparse, collections, datetime, hashlib, json, math, statistics
from pathlib import Path

parser=argparse.ArgumentParser()
parser.add_argument('--baseline',type=Path,required=True)
parser.add_argument('--optimized',type=Path,required=True)
parser.add_argument('--output-prefix',type=Path,required=True)
args=parser.parse_args()
outputs=[args.output_prefix.with_suffix('.json'),args.output_prefix.with_suffix('.md')]
if any(p.exists() for p in outputs):raise SystemExit('Exclusive new outputs required')

def hashfile(p):
 h=hashlib.sha256()
 with p.open('rb') as f:
  for b in iter(lambda:f.read(1024*1024),b''):h.update(b)
 return {'file':str(p.resolve()),'bytes':p.stat().st_size,'sha256':h.hexdigest()}

def inventory(root):return [hashfile(p) for p in sorted(root.rglob('*')) if p.is_file()]
checks=[]
def check(name,condition,actual=None):
 row={'name':name,'passed':bool(condition)}
 if actual is not None:row['actual']=actual
 checks.append(row)

before=inventory(args.baseline)+inventory(args.optimized)

def load(root):
 receipt=json.loads((root/'receipt.json').read_text())
 raw=(root/'steps.jsonl').read_bytes()
 check(str(root)+' complete JSONL newline',raw.endswith(b'\n'))
 rows=[json.loads(line) for line in raw.splitlines()]
 begins=[r for r in rows if r.get('phase')=='begin'];ends=[r for r in rows if r.get('phase')=='end'];failed=[r for r in rows if r.get('phase')=='failed']
 check(str(root)+' closed250',receipt['status']=='declared_continuation_completed' and receipt['completedSteps']==250 and receipt['declaredSteps']==250,receipt['status'])
 check(str(root)+' actual starting digest',receipt['startingStateDigestMatches'] and receipt['startingStateDigest']==receipt['expectedStartingStateDigest'])
 check(str(root)+' complete paired steps',len(begins)==250 and len(ends)==250 and not failed and [r['ordinal'] for r in begins]==list(range(1,251)) and [r['ordinal'] for r in ends]==list(range(1,251)))
 check(str(root)+' real forward steps',all(r['stepped'] and r['stepAfter']==r['stepBefore']+1 and not r['matchEnded'] for r in ends))
 check(str(root)+' event count forward',all(r['eventsAfter']>=r['eventsBefore'] for r in ends))
 events=(root/'new-events.jsonl').read_bytes();check(str(root)+' complete event JSONL',events.endswith(b'\n'))
 eventrows=[json.loads(line) for line in events.splitlines()]
 check(str(root)+' emitted event count',len(eventrows)==receipt['finalEvents']-receipt['initialEvents'],len(eventrows))
 return receipt,ends,eventrows

baseline,brows,bevents=load(args.baseline);optimized,orows,oevents=load(args.optimized)
for key in ['anchorSha256','prefixSha256','rulesHash','startingStateDigest','expectedStartingStateDigest','initialStep','initialEvents','initialWorldTime','declaredSteps','completedSteps','finalStep','finalEvents','finalWorldTime','finalStateDigest']:
 check('equal '+key,baseline[key]==optimized[key],{'baseline':baseline[key],'optimized':optimized[key]})
for name in ['final-world.json','final-simulation.json','new-events.jsonl']:
 a=(args.baseline/name).read_bytes();b=(args.optimized/name).read_bytes()
 check('byte-identical '+name,a==b,{'baseline':hashfile(args.baseline/name),'optimized':hashfile(args.optimized/name)})
check('equal emitted full event payloads',bevents==oevents)
for a,b in zip(brows,orows):
 check('identical step '+str(a['ordinal'])+' observable transition',all(a[k]==b[k] for k in ['ordinal','stepBefore','stepAfter','eventsBefore','eventsAfter','entities','timeBefore','timeAfter','stepped','matchEnded']))

def summary(rows):
 wall=[r['wallMs'] for r in rows];metric=collections.defaultdict(lambda:{'calls':0,'inclusiveMs':0.,'maxMs':0.})
 for row in rows:
  for m in row['metrics']:
   value=metric[m['method']];value['calls']+=m['calls'];value['inclusiveMs']+=m['inclusiveMs'];value['maxMs']=max(value['maxMs'],m['maxMs'])
 return {'steps':len(rows),'grossStepWallMs':sum(wall),'meanStepMs':statistics.mean(wall),'medianStepMs':statistics.median(wall),'maxStepMs':max(wall),'stepsOver500Ms':sum(x>500 for x in wall),
 'counters':{key:sum(r[key] for r in rows) for key in ['safeCalls','segmentSafeCalls','economicThreatCalls','pathNodePops','pathClearLineCalls']},
 'metrics':dict(sorted(metric.items())),
 'largestSteps':[{'ordinal':r['ordinal'],'worldBefore':r['timeBefore'],'wallMs':r['wallMs']} for r in sorted(rows,key=lambda r:r['wallMs'],reverse=True)[:15]]}
a=summary(brows);b=summary(orows)
after=inventory(args.baseline)+inventory(args.optimized)
check('all original inputs hash-stable before/after',before==after)
result={'schema':'frontier-independent-isolated-step-comparison072-1','generatedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'passed':all(c['passed'] for c in checks),'checks':checks,'failures':[c for c in checks if not c['passed']],
 'inputs':before,'baselineReceipt':baseline,'optimizedReceipt':optimized,'baselineSummary':a,'optimizedSummary':b,
 'measuredGrossTimeRatio':a['grossStepWallMs']/b['grossStepWallMs'] if b['grossStepWallMs'] else None,
 'scope':'Two isolated Editor diagnostic continuations from one original NativeA anchor/event prefix; unchanged step harness. No GUI/native rerun, release FPS, complete regression or normal route matrix claim.',
 'timingLimits':['Counters include instrumentation overhead; inclusive nested timers overlap and must not be summed','Different compile/cache/concurrent CPU environment may affect wall time','Optimized SafeCalls also counts immutable current-visible circles; static Safe remains separately unchanged','Preflight negative-only graph must still pass full tests and actual candidate native observation']}
for p in outputs:p.parent.mkdir(parents=True,exist_ok=True)
with outputs[0].open('x') as f:json.dump(result,f,indent=2);f.write('\n')
text=f'''# Independent isolated step comparison 072\n\nStatus: {'passed' if result['passed'] else 'failed'}; {len(checks)} checks, {len(result['failures'])} failures.\n\nBoth runs must actually complete 250 steps from the same original anchor and recorded history. Final world/controller JSON, all emitted event JSONL bytes, final state digest, and per-step observable transitions were independently compared. No raw files were changed.\n\nBaseline gross step time: {a['grossStepWallMs']:.4f} ms; maximum {a['maxStepMs']:.4f} ms; {a['stepsOver500Ms']} steps over 500 ms. Optimized gross: {b['grossStepWallMs']:.4f} ms; maximum {b['maxStepMs']:.4f} ms; {b['stepsOver500Ms']} steps over 500 ms. Measured gross ratio: {result['measuredGrossTimeRatio']:.4f}. Inclusive method timers overlap.\n\nOriginal and optimized final state digest: `{baseline['finalStateDigest']}` / `{optimized['finalStateDigest']}`. Full event counts: {baseline['finalEvents']} / {optimized['finalEvents']}.\n\nThis is isolated instrumented Editor evidence. Compile/cache state and concurrent CPU affect wall time; it does not certify release frame rate, full regressions, native GUI or all normal routes. See the JSON for every source input hash, check, actual metric and failed assertion.\n'''
with outputs[1].open('x') as f:f.write(text)
print(json.dumps({'passed':result['passed'],'checks':len(checks),'failures':len(result['failures']),'grossRatio':result['measuredGrossTimeRatio'],'outputs':[hashfile(p) for p in outputs]},indent=2))
raise SystemExit(0 if result['passed'] else 1)
