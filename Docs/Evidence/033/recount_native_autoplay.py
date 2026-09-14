import argparse
import json
from pathlib import Path

parser = argparse.ArgumentParser(description='Recount artillery events by source and target identity.')
parser.add_argument('--rules', required=True, type=Path, help='Path to the matching Assets/Resources/Rules.json')
parser.add_argument('--evidence', type=Path, default=Path(__file__).resolve().parent)
args = parser.parse_args()
REPORT=args.evidence/'native-autoplay.json'
EVENTS=args.evidence/'native-autoplay-events.jsonl'
RULES=args.rules
events=[json.loads(line) for line in EVENTS.read_text().splitlines()]
assert len(events) == json.loads(REPORT.read_text())['eventCount']
spawn={e['entity']:e for e in events if e['kind']=='Spawn'}
defs={e['id']:e for e in json.loads(RULES.read_text())['entities']}
name_to_kind={e['name']:e['kind'] for e in defs.values()}
def is_kind(entity, kind):
    s=spawn.get(entity); return bool(s and name_to_kind.get(s['message'])==kind)
art={i for i,e in spawn.items() if e['player']==0 and e['message'] in ('Field Cannon','Mortar')}
build={i for i in spawn if is_kind(i,1)}
def selected(k): return [e for e in events if e['kind']==k and e.get('entity') in art]
def selected_build_damage(): return [e for e in events if e['kind']=='Damage' and e.get('entity') in art and e.get('target') in build]
result={'artillery':sorted(art),'buildingEntities':sorted(build),'fire':len(selected('Fire')),'impact':len(selected('Impact')),'damage':len(selected('Damage')),'attackCancelled':len(selected('AttackCancelled')),'buildingFire':len([e for e in selected('Fire') if e.get('target') in build]),'buildingImpact':len([e for e in selected('Impact') if e.get('target') in build]),'buildingDamage':len(selected_build_damage()),'buildingDamageTargets':sorted({e['target'] for e in selected_build_damage()}),'cancelled':selected('AttackCancelled')}
print(json.dumps(result,indent=2))
