from pathlib import Path
from collections import Counter
import hashlib,json,struct
w=Path(__file__).resolve().parent
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
rows=[json.loads(x) for x in (w/'capture-index.jsonl').read_text().splitlines()]
assert len({r['label'] for r in rows})==len(rows)
captures=[]
for row in rows:
 m=json.loads((w/(row['label']+'.json')).read_text()); p=w/(row['label']+'.png')
 assert sha(p)==m['pngSha256']==row['sha256']
 assert struct.unpack('>II',p.read_bytes()[16:24])==(m['width'],m['height'])==(2560,1440)
 assert m['requestMatchId']==m['matchId']
 if m['hasPlayer']:
  pop=m['population'];assert f"POP  {pop['used']}+{pop['reserved']}/{pop['capacity']}" in m['hudResources']
 captures.append({'label':row['label'],'version':m['appVersion'],'buildGuid':m['buildGuid'],'matchId':m['matchId'],'worldTime':m['worldTime'],'paused':m['paused'],'pngSha256':sha(p)})
rp=next(w.glob('*014316*match_finished.json')); report=json.loads(rp.read_text());ep=rp.with_name(rp.stem+'-events.jsonl');events=[json.loads(x) for x in ep.read_text().splitlines()]
assert len(events)==report['eventCount']==1386
player=[e for e in events if e['player']==0];payments=[e for e in player if e['kind']=='Payment'];refunds=[e for e in player if e['kind']=='Refund'];rallies=[e for e in player if e['kind']=='RallySet'];commands=[e for e in player if e['kind']=='Command'];pauses=[e for e in player if e['kind']=='PauseChanged']
assert len(payments)==3 and len(refunds)==1 and len(rallies)==2
assert [e['economy']['actualDelta']['food'] for e in payments]==[-100,-100,0]
assert [e['economy']['actualDelta']['wood'] for e in payments]==[0,0,-135]
assert refunds[0]['economy']['actualDelta']['food']==100
assert len(commands)==1 and commands[0]['message']=='AttackMove ×1'
assert report['autoplay'] is False and report['simulationSpeed']==1 and report['players'][0]['gathered']==dict(food=0,wood=0,coin=0)
assert any(e['kind']=='Resign' for e in player) and report['winner']==1
rr=next(w.glob('*483220*menu_exit.json'));restart=json.loads(rr.read_text())
assert restart['winner']==-1 and restart['players'][0]['population']==6 and restart['players'][0]['resources']==dict(food=300,wood=300,coin=100)
out={'scope':'Targeted native input check, ended by resignation; not a normal complete gameplay acceptance. OS focus recovery not proven. Native action intervals are not measured below 220ms.','buildGuid':report['buildGuid'],'appVersion':report['appVersion'],'captures':captures,'captureCounts':dict(Counter(x['version'] for x in captures)),'match':{'report':rp.name,'reportSha256':sha(rp),'events':ep.name,'eventsSha256':sha(ep),'matchId':report['matchId'],'seconds':report['time'],'winner':report['winner'],'resigned':True,'autoplay':False,'speed':1,'eventCount':len(events),'player0TrainingPayments':2,'player0BuildPayments':1,'player0Refunds':1,'player0Rallies':[(e['time'],e['target']) for e in rallies],'player0Commands':[(e['time'],e['message']) for e in commands],'player0PauseEvents':[(e['time'],e['message']) for e in pauses],'player0Gathered':report['players'][0]['gathered']},'restart':{'report':rr.name,'sha256':sha(rr),'matchId':restart['matchId'],'seconds':restart['time'],'resources':restart['players'][0]['resources'],'population':restart['players'][0]['population']}}
(w/'native-audit.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({k:v for k,v in out.items() if k!='captures'},ensure_ascii=False,indent=2))
