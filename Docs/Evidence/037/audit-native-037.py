#!/usr/bin/env python3
"""Read-only source evidence audit; writes derived JSON beside this script. No Unity launch."""
import collections, hashlib, json, math, pathlib, re, struct, sys
ROOT=pathlib.Path(__file__).resolve().parent
GUIDS={'0.2.15':'28e06a3b43184bdeaa73fdf70f10d6c2','0.2.16':'af1e524273704830bbd0445a24446435'}
def read(path):return json.loads(path.read_text())
def jsonl(path):return [json.loads(s) for s in path.read_text().splitlines() if s.strip()]
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def only(pattern):
    paths=list(ROOT.glob(pattern));assert len(paths)==1,(pattern,paths);return paths[0]
def match(mid,reason,version):
    p=only('*'+mid+'-'+reason+'.json');report=read(p);assert report['matchId']==mid and report['appVersion']==version and report['buildGuid']==GUIDS[version]
    ep=p.with_name(p.stem+'-events.jsonl');events=jsonl(ep);assert len(events)==report['eventCount']
    return report,events,ep

def captures():
    groups={};rows=jsonl(ROOT/'capture-index.jsonl');assert len({r['label'] for r in rows})==len(rows)
    for r in rows:
        s=read(ROOT/(r['label']+'.json'));p=ROOT/(r['label']+'.png');h=sha(p);size=struct.unpack('>II',p.read_bytes()[16:24]);v=s['appVersion']
        assert h==s['pngSha256']==r['sha256'] and s['pngFile']==r['sourcePng']
        assert s['buildGuid']==GUIDS[v] and size==(2560,1440)==(s['width'],s['height'])
        groups.setdefault(v,[]).append({'label':r['label'],'pngSha256':h,'sourceJson':r['sourceJson'],'sourcePng':r['sourcePng'],'buildGuid':s['buildGuid'],'matchId':s['matchId'],'size':size,'worldTime':s['worldTime'],'paused':s['paused'],'status':'PASS'})
    return {'indexEntries':len(rows),'byAppVersion':groups,'allChecksPass':True}

def damage():
    report,ev,ep=match('36ea1d82568a47ccb8578a525d1d8994','match_finished','0.2.15')
    spawn={e['entity']:e for e in ev if e['kind']=='Spawn'}
    ds=[e for e in ev if e['kind']=='Damage' and e['player']==0]
    missing=[e['target'] for e in ds if e['target'] not in spawn]
    # Event message labels actually present are retained for review. No unknown labels are silently treated as buildings.
    bytarget=collections.Counter((spawn[e['target']]['player'],spawn[e['target']]['message']) for e in ds if e['target'] in spawn)
    buildings={'Town Center','Manor','House','Market','Barracks','Stable','Foundry','Tower','Farm','Bank','Trading Post'}
    bd=[e for e in ds if e['target'] in spawn and spawn[e['target']]['player']==1 and spawn[e['target']]['message'] in buildings]
    resign=[e for e in ev if e['kind']=='Resign' and e['player']==0]
    assert len(ds)==204 and not missing and not bd and not resign
    return {'eventFile':ep.name,'report':report,'player0DamageEvents':len(ds),'damageTargetsWithoutSpawn':len(missing),'damageEventsWithSpawn':len(ds)-len(missing),'mappedEnemyBuildingDamageEvents':len(bd),'targetOwnerAndSpawnMessages':[{'owner':k[0],'spawnMessage':k[1],'damageEvents':n} for k,n in bytarget.items()],'player0ResignEvents':len(resign)}

def v16(caps):
    report,ev,ep=match('41627f03e2544493ab4a81b4621b5780','match_finished','0.2.16')
    menu,mev,mep=match('d5f9665061f6467996c313e5314fc92f','menu_exit','0.2.16')
    p0=[e for e in ev if e['player']==0];commands=[e for e in p0 if e['kind']=='Command'];stops=[e for e in commands if e['message'].startswith('Stop ')]
    assert len(stops)==1 and stops[0]['entity']==2
    assert sum(e['kind']=='Resign' for e in p0)==1 and report['autoplay'] is False and report['simulationSpeed']==1
    rows={r['label']:r for r in jsonl(ROOT/'capture-index.jsonl')};lines=(ROOT/'v16-native-Player.log').read_text().splitlines()
    pattern=re.compile(r'^FRONTIER_INPUT (\w+) (\w+) (down|up) p=\(([\d.-]+), ([\d.-]+)\) shift=(True|False) save=(True|False) t=([\d.]+)$')
    def segment(before,after):
        def end(label):return next(i for i,l in enumerate(lines) if l.startswith('FRONTIER_CAPTURE_OK ') and l.endswith('/'+rows[label]['sourceJson']))
        return [(i+1,lines[i]) for i in range(end(before)+1,end(after))]
    def parse(seq):
        parsed=[]
        for n,l in seq:
            m=pattern.match(l)
            if m:parsed.append({'line':n,'kind':m[1],'key':m[2],'state':m[3],'position':[float(m[4]),float(m[5])],'shift':m[6]=='True','save':m[7]=='True','time':float(m[8])})
        return parsed
    cases={}
    for name,before,after,counts in [('far','v16-opening','v16-separated-clicks',[1,1]),('near','v16-separated-clicks','v16-near-double-click',[1,6])]:
        seg=segment(before,after);events=parse(seg);ups=[e for e in events if e['kind']=='Left' and e['state']=='up'];assert len(ups)==2
        actual=[int(m[1]) for _,l in seg if (m:=re.search(r'^FRONTIER_SELECTION .* selected=(\d+)$',l))];assert actual==counts
        delta=ups[1]['time']-ups[0]['time'];dist=math.dist(ups[0]['position'],ups[1]['position']);assert 0<=delta<.3
        assert (dist==76 if name=='far' else dist==0)
        cases[name]={'leftReleaseEvents':ups,'releaseDeltaSeconds':delta,'inputPixelDistance':dist,'selectionCounts':actual,'contextLines':[{'line':n,'text':l} for n,l in seg if l.startswith('FRONTIER_UI context ')]}
    inputs=parse(list(enumerate(lines,1)));digits=[e for e in inputs if e['kind']=='Key' and e['key'] in ('Digit1','Digit2')]
    assert digits[0]['key']=='Digit1' and not digits[0]['save'] and digits[1]['key']=='Digit2' and not digits[1]['save']
    save=[{'line':n,'text':l} for n,l in enumerate(lines,1) if l.startswith('FRONTIER_UI action=SAVE 1 ')];assert len(save)==1
    pauses=[e for e in mev if e['kind']=='PauseChanged' and e['player']==0];assert len(pauses)==1 and pauses[0]['message']=='focus_lost'
    return {'captureCount':len(caps['byAppVersion']['0.2.16']),'allCaptureChecksPass':True,'report':report,'matchEventFile':ep.name,'player0Commands':commands,'player0Stop':len(stops),'player0Gather':sum(e['kind']=='Gather' for e in p0),'pauseEvents':[e for e in p0 if e['kind']=='PauseChanged'],'inputCases':cases,'digitKeyEvents':digits,'mouseSaveLines':save,'menuExitReport':menu,'menuExitEventFile':mep.name,'menuExitPlayerPauseEvents':pauses,'limitations':['Sky calls associated first Digit1 with ctrl+1 and first Digit2 with super+2; log only proves received key/save state, not physical hardware failure.','Right sequence has 299.9ms rounded release delta and 76px distance; does not isolate right-reset cause.','STOP-to-click about448ms is not a sub0.3s test.','First group-recall PNG retains old selection, confirmed capture later shows6; no same-frame UI claim.','No sound/performance or complete economic-siege-match claim.']}
if __name__=='__main__':
    c=captures();old={'schemaVersion':3,'captures':c,'damage':damage()};new=v16(c)
    (ROOT/'native-evidence-check.json').write_text(json.dumps(old,indent=2,ensure_ascii=False)+'\n')
    (ROOT/'v16-native-audit.json').write_text(json.dumps(new,indent=2,ensure_ascii=False)+'\n')
    print(json.dumps({'captures':{v:len(xs) for v,xs in c['byAppVersion'].items()},'far':new['inputCases']['far'],'near':new['inputCases']['near'],'stopCommands':new['player0Stop'],'allChecksPass':True},indent=2))
