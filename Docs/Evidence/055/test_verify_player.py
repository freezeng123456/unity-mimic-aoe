"""Temporary synthetic parser fixtures; never native gameplay or gesture evidence."""
import base64
import copy
import hashlib
import json
from pathlib import Path
import tempfile
import unittest

import verify_player as verifier


class PlayerVerifierTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory(); self.addCleanup(self.tmp.cleanup)
        self.root=Path(self.tmp.name); self.repo=Path(__file__).resolve().parents[3]
        self.rules_path=self.repo/'Assets/Resources/Rules.json'; self.rules=json.loads(self.rules_path.read_text())
        self.definitions={d['id']:d for d in self.rules['entities']}; self.civs={c['id']:c for c in self.rules['civs']}
        self.guid='1'*32; self.match_id='2'*32
        self.path=self.root/(self.match_id+'-match_finished.json'); self.events=self.path.with_name(self.path.stem+'-events.jsonl')
        self.menu=self.root/'menu.json'; png=self.root/'menu.png'
        png.write_bytes(base64.b64decode('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jMScAAAAASUVORK5CYII='))
        self.menu.write_text(json.dumps(dict(pngFile=png.name,pngSha256=hashlib.sha256(png.read_bytes()).hexdigest(),width=1,height=1,
            buildGuid=self.guid,appVersion='0.2.31',inMenu=True,hasPlayer=False,matchId='',requestMatchId='')))

    def event(self,kind,owner=-1,entity=0,message='',amount=0,target=0,**extras):
        row=dict(schemaVersion=2,hasEconomy=False,time=self.time,kind=kind,player=owner,entity=entity,target=target,message=message,amount=amount)
        row.update(extras);self.rows.append(row);return row

    def spawn(self,item,owner):
        entity=self.next_id;self.next_id+=1;self.event('Spawn',owner,entity,self.definitions[item]['name']);self.born[entity]=(item,owner);return entity

    def pay(self,owner,entity,label,cost):
        self.event('Payment',owner,entity,label+': synthetic fixture',hasEconomy=True,economy={
            'requestedDelta':{r:-cost.get(r,0) for r in verifier.RESOURCES},'actualDelta':{r:-cost.get(r,0) for r in verifier.RESOURCES},
            'balanceBefore':{r:10000 for r in verifier.RESOURCES},'balanceAfter':{r:10000-cost.get(r,0) for r in verifier.RESOURCES}})

    def building(self,item):
        definition=self.definitions[item];self.pay(0,self.worker,'Foundation '+definition['name'],verifier.entity_cost(definition,self.civilization,self.civs))
        entity=self.spawn(item,0);self.time+=definition.get('time',1)+1;self.event('BuildingComplete',0,entity,definition['name']+' ready');return entity

    def train(self,item,producer):
        definition=self.definitions[item];self.pay(0,producer,definition['name']+' ×1',verifier.entity_cost(definition,self.civilization,self.civs))
        self.event('ProductionQueued',0,producer,item+' ×1',1);self.time+=definition['time']+1
        entity=self.spawn(item,0);self.event('Experience',0,producer,'Unit trained',definition.get('trainXp',1));return entity

    def make(self,river=False):
        self.civilization='river' if river else 'coast';self.rows=[];self.born={};self.time=0.;self.next_id=1;towns=[]
        for owner,civilization in enumerate((self.civilization,'coast')):
            resources=self.civs[civilization]['startingResources']
            self.event('StartingResources',owner,message='Configured opening: {} F / {} W / {} C'.format(*(resources[r] for r in verifier.RESOURCES)))
            towns.append(self.spawn('town',owner));workers=[self.spawn('villager',owner) for _ in range(6)]
            if owner==0:self.worker=workers[0]
            self.spawn('explorer',owner);self.spawn('crate_food',owner);self.spawn('crate_wood',owner)
        socket=self.spawn('trade_socket',-1)
        treasure=self.spawn('treasure_wood',-1)
        self.event('MatchStarted',message=f'Seed 314159; age cap {5 if river else 3}; {self.civilization} vs coast; project rules P v0.2')
        self.time=1.;self.event('Gather',0,self.worker,'Food',10,target=next(k for k,v in self.born.items() if v==('crate_food',0)))
        self.train('villager',towns[0])
        barracks=self.building('barracks');self.train('pike',barracks)
        for age in range(2,6 if river else 4):
            definition=next(a for a in self.rules['ages'] if a['age']==age)
            self.pay(0,towns[0],'Advance to age '+str(age),definition['cost']);self.time+=definition['time']+1
            self.event('AgeAdvanced',0,towns[0],'Reached age '+str(age))
        foundry=self.building('foundry');cannon=self.train('cannon',foundry)
        self.event('Damage',0,cannon,'Siege',100,target=towns[1])
        card=next(c for c in self.rules['cards'] if c['id']==('river_coin' if river else 'food_crates'))
        self.event('ShipmentSent',0,message=card['name']);self.time+=card['travel']+1;self.event('ShipmentArrived',0,towns[0],card['name']+' arrived')
        if river:
            bank=self.building('bank');self.event('BankIncome',0,bank,'Coin',5)
            stable=self.building('stable');self.train('carabineer',stable)
            explorer=next(k for k,v in self.born.items() if v==('explorer',0))
            self.event('TreasureClaimed',0,treasure,'Treasure secured; collect any reward crates',0,target=explorer)
            self.event('Death',-1,treasure,'Timber Cache lost')
            post=self.building('trade_post');self.event('Experience',0,post,'Trade route delivery',40)
            self.event('TradeDelivery',0,post,'XP',40,target=socket)
            tech=next(t for t in self.rules['techs'] if t['id']=='cargo_ledgers')
            self.pay(0,post,tech['name'],tech['cost']);self.time+=tech['time']+1
            self.event('Technology',0,message='cargo_ledgers applied once to matching current and future entities')
            self.event('ResearchComplete',0,post,tech['name'])
            self.event('TradeMode',0,post,'Food');self.event('TradeDelivery',0,post,'Food',75,target=socket)
        self.time+=1
        for entity,(item,owner) in self.born.items():
            if owner==1 and self.definitions[item]['kind']!=2:self.event('Death',owner,entity,self.definitions[item]['name']+' lost')
        self.event('MatchEnded',0,message='All opposing units and buildings eliminated')
        self.report=dict(matchId=self.match_id,buildGuid=self.guid,appVersion='0.2.31',platform='OSXPlayer',version='6000.6.3f1',eventSchemaVersion=2,
            autoplay=False,simulationSpeed=1,seed=314159,maxAge=5 if river else 3,time=self.time,winner=0,reason='match_finished',eventCount=len(self.rows),
            players=[dict(civilization=self.civilization,age=5 if river else 3),dict(civilization='coast',age=1)])
        self.save()

    def save(self):
        self.report['eventCount']=len(self.rows);self.path.write_text(json.dumps(self.report))
        self.events.write_text(''.join(json.dumps(row)+'\n' for row in self.rows))

    def audit(self,scenario=None,**kwargs):
        return verifier.audit_player(self.path,self.rules_path,self.guid,'0.2.31',self.match_id,scenario or ('river-v' if self.civilization=='river' else 'coast-iii'),'economy',self.menu,**kwargs)

    def test_coast_iii_paid_births_damage_and_natural_elimination_chain(self):
        self.make();result=self.audit();self.assertEqual('passed',result['status'],result['failedChecks'])
        self.assertEqual([],result['coverageGaps']);self.assertEqual(1,result['playerPaidCounts']['cannon'])
        cannon=next(x for x in result['lineage']['players'][0]['training'] if x['definition']=='cannon')
        self.assertLess(cannon['payment']['line'],cannon['queue']['line']);self.assertLess(cannon['queue']['line'],cannon['spawn']['line'])
        self.assertIn('externally declared UI',result['deck']['scope']);self.assertFalse(result['coverage']['paidTechnology'])

    def test_river_v_requires_one_paid_tech_and_one_resource_mode_not_all(self):
        self.make(True);result=self.audit();self.assertEqual('passed',result['status'],result['failedChecks'])
        self.assertTrue(all(result['coverage'][k] for k in verifier.RIVER_REQUIREMENTS));self.assertEqual(1,len(result['lineage']['players'][0]['technologies']))

    def test_wrong_guid_autoplay_speed_and_match_identity_are_rejected(self):
        self.make();original=copy.deepcopy(self.report)
        for key,value,check in [('buildGuid','3'*32,'exactNativeBuildVersionMatch'),('autoplay',True,'normalPlayerOneX'),('simulationSpeed',16,'normalPlayerOneX'),('matchId','3'*32,'explicitMatchIdentity')]:
            with self.subTest(key=key):
                self.report={**original,key:value};self.save();self.assertFalse(self.audit()['checks'][check])

    def test_fake_catalog_cost_consistent_balance_and_zero_debit_are_rejected(self):
        self.make();payment=next(x for x in self.rows if x['kind']=='Payment')
        payment['economy']['requestedDelta']['food']=-1;payment['economy']['actualDelta']['food']=-1;payment['economy']['balanceAfter']['food']=9999
        self.save();self.assertFalse(self.audit()['checks']['eventIdentitiesAndPaymentsValid'])
        for d in ('requestedDelta','actualDelta'):
            payment['economy'][d]={r:0 for r in verifier.RESOURCES}
        payment['economy']['balanceAfter']={r:10000 for r in verifier.RESOURCES}
        self.save();self.assertFalse(self.audit()['coverage']['paidWorkers'])

    def test_elapsed_production_cannot_be_faked_by_adjacent_spawn_and_xp(self):
        self.make();queue=next(x for x in self.rows if x['kind']=='ProductionQueued')
        birth_index=next(i for i,x in enumerate(self.rows) if x['kind']=='Experience' and x['message']=='Unit trained')-1
        self.rows[birth_index]['time']=self.rows[birth_index+1]['time']=queue['time']+.1
        self.save();result=self.audit();self.assertFalse(result['checks']['eventIdentitiesAndPaymentsValid']);self.assertFalse(result['coverage']['paidWorkers'])

    def test_time_zero_diagnostic_deaths_and_resign_cannot_pass_natural_game(self):
        self.make();death=next(x for x in self.rows if x['kind']=='Death');death['time']=0
        self.save();self.assertFalse(self.audit()['checks']['noOpeningDeaths'])
        self.make();self.rows.insert(-1,{**self.rows[-1],'kind':'Resign'});self.save()
        self.assertFalse(self.audit()['checks']['oneNaturalEndWithoutResign'])

    def test_old_third_native_match_remains_age_and_siege_negative(self):
        path=self.repo/'Docs/Evidence/055/NormalPlayer/attempt-003/20261002-103051-085-91311172a2df4d3987ff11860a1a08fc-match_finished.json'
        result=verifier.audit_player(path,self.rules_path,'21261224be4f40e692a3629fc4cc42fe','0.2.30','91311172a2df4d3987ff11860a1a08fc','coast-iii','economy')
        self.assertEqual('failed',result['status']);self.assertEqual(['ageIII','paidCannon','cannonEnemyBuildingDamage'],result['coverageGaps'])
        self.assertTrue(result['checks']['eventIdentitiesAndPaymentsValid']);self.assertTrue(result['checks']['naturalLoserHasZeroLivingUnitsOrBuildings'])

    def test_replaced_menu_bytes_and_nonmenu_capture_cannot_certify_ui_pair(self):
        self.make();data=json.loads(self.menu.read_text());data['inMenu']=False;self.menu.write_text(json.dumps(data))
        self.assertFalse(self.audit()['checks']['menuCapturePaired'])
        data['inMenu']=True;self.menu.write_text(json.dumps(data));self.menu.with_suffix('.png').write_bytes(b'not PNG')
        self.assertFalse(self.audit()['checks']['menuCapturePaired'])

    def test_schema_duplicate_nonfinite_and_boolean_ids_are_rejected(self):
        self.make();self.rows[0]['time']=float('nan');self.save()
        with self.assertRaises(ValueError):self.audit()
        self.make();self.rows[0]['entity']=True;self.save()
        with self.assertRaises(ValueError):self.audit()
        self.make();self.path.write_text('{"matchId":"a","matchId":"b"}')
        with self.assertRaises(ValueError):self.audit()


if __name__=='__main__':unittest.main()
