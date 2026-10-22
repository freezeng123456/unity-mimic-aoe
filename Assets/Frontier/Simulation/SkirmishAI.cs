using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    // Commands share exactly the human command authority. No privileged economy or hidden targets.
    public sealed class SkirmishAI
    {
        private readonly World world;
        private readonly int player;
        private Vector2 home=>armyPlan.Home;
        private Vector2 enemyStart=>world.OpeningTownPosition(world.FirstEnemyPlayer(player));
        private readonly Dictionary<int,Vector2> memory=new Dictionary<int,Vector2>();
        private float thinkAt,armyAt;
        public AiDifficultyProfile Difficulty { get; }
        private bool expedition;
        private int expeditionSize;
        private readonly AiWorkforce workforce;
        private readonly AiResourceContinuity072 continuity;
        private readonly AiArmyPlan072 armyPlan;
        private readonly AiAgePlan agePlan;
        private readonly AiRevolution071 revolution;
        private readonly AiConstruction construction;
        private readonly AiSearchPlan search;
        private readonly AiCivilizationEconomy civilizationEconomy;
        private readonly AiMedicalSupport076 medicalSupport;
        private readonly AiWaterEconomy waterEconomy;
        private bool searchingLastReview;
        private int rotation,buildSerial;
        public SkirmishAI(World world,int player):this(world,player,AiDifficultyProfile.From(world.Rules,"standard")){}
        public SkirmishAI(World world,int player,AiDifficultyProfile difficulty)
        {this.world=world??throw new ArgumentNullException(nameof(world));if(player<0||player>=world.Players.Length)throw new ArgumentOutOfRangeException(nameof(player));this.player=player;Difficulty=difficulty??throw new ArgumentNullException(nameof(difficulty));workforce=new AiWorkforce(world,player);agePlan=new AiAgePlan(world,player);revolution=new AiRevolution071(world,player,Difficulty);construction=new AiConstruction(world,player);continuity=new AiResourceContinuity072(world,player,()=>agePlan.Reserve+revolution.Reserve);civilizationEconomy=new AiCivilizationEconomy(world,player,continuity);medicalSupport=new AiMedicalSupport076(world,player,continuity);waterEconomy=new AiWaterEconomy(world,player,continuity);armyPlan=new AiArmyPlan072(world,player,Difficulty);search=new AiSearchPlan(world,player,enemyStart);waterEconomy.BindBridgeArmy(armyPlan,revolution,medicalSupport);}
        public AiSnapshot CaptureState()
        {
            var remembered=new List<IntPointSnapshot>();
            // Equal-distance targets and removal of stale sightings depend on this original order.
            foreach(var item in memory)remembered.Add(new IntPointSnapshot{id=item.Key,point=item.Value});
            return new AiSnapshot{player=player,thinkAt=thinkAt,armyAt=armyAt,expedition=expedition,expeditionSize=expeditionSize,
                searchingLastReview=searchingLastReview,rotation=rotation,buildSerial=buildSerial,memory=remembered.ToArray(),
                armyPlan=armyPlan.CaptureState(),agePlan=agePlan.CaptureState(),workforce=workforce.CaptureState(),search=search.CaptureState(),
                waterEconomy=waterEconomy.CaptureState(),civilizationEconomy=civilizationEconomy.CaptureState(),medicalSupport=medicalSupport.CaptureState()};
        }
        internal static SkirmishAI Restore(World world,AiSnapshot state,AiDifficultyProfile difficulty)
        {
            AiSaveValidation.Require(world!=null&&state!=null&&difficulty!=null,"missing controller");
            AiSaveValidation.Require(state.player>=0&&state.player<world.Players.Length,"controller player");
            AiSaveValidation.Require(AiSaveValidation.Clock(state.thinkAt)&&state.thinkAt<=difficulty.DecisionSeconds+MatchSimulation.StepSeconds&&
                AiSaveValidation.Clock(state.armyAt)&&state.expeditionSize>=0&&state.rotation>=0&&state.buildSerial>=0,"controller clocks or army state");
            AiSaveValidation.Require(state.memory!=null&&state.memory.Length<100000,"enemy memory");
            var result=new SkirmishAI(world,state.player,difficulty);var ids=new HashSet<int>();
            foreach(var item in state.memory)
            {
                AiSaveValidation.Require(item!=null&&item.id>0&&ids.Add(item.id)&&SnapshotCodec.Finite(item.point),"enemy memory entry");
                var remembered=world.Get(item.id);AiSaveValidation.Require(remembered==null||world.AreEnemies(state.player,remembered.owner),"allied enemy memory");
                result.memory.Add(item.id,item.point);
            }
            result.armyPlan.RestoreState(state.armyPlan);result.agePlan.RestoreState(state.agePlan);result.workforce.RestoreState(state.workforce);result.search.RestoreState(state.search);
            result.waterEconomy.RestoreState(state.waterEconomy);result.civilizationEconomy.RestoreState(state.civilizationEconomy);
            result.medicalSupport.RestoreState(state.medicalSupport);
            result.waterEconomy.ValidateBridgeState();
            foreach(int id in result.medicalSupport.RecoveryMembers)
                AiSaveValidation.Require(Array.IndexOf(result.armyPlan.WaveMembers,id)<0&&Array.IndexOf(result.armyPlan.GuardMembers,id)<0,"medical and army cohort overlap");
            result.thinkAt=state.thinkAt;result.armyAt=state.armyAt;result.expedition=state.expedition;result.expeditionSize=state.expeditionSize;
            result.searchingLastReview=state.searchingLastReview;result.rotation=state.rotation;result.buildSerial=state.buildSerial;return result;
        }
        public void Tick(float dt)
        {
            if(world.Paused||world.IsMatchEnded||!world.IsPlayerActive(player))return;
            // Existing paid repairs tick between full reviews. Recovery stops
            // only this AI's orders at every fixed-step boundary.
            continuity.StopRecoveryRepairs(world.Entities);thinkAt-=dt;if(thinkAt>0)return;thinkAt=Difficulty.DecisionSeconds;
            continuity.BeginDecision();
            try{ReviewDecision(dt);}
            finally{continuity.EndDecision();}
        }
        void ReviewDecision(float dt)
        {
            var own=new List<EntityState>();var workers=new List<EntityState>();var army=new List<EntityState>();var enemies=new List<EntityState>();var economicThreats=new List<EntityState>();
            foreach(var e in world.Entities)
            {
                if(!e.Alive)continue;
                if(e.owner==player){own.Add(e);if(e.Worker)workers.Add(e);else if(e.Military&&!e.def.waterborne)army.Add(e);}
                else if(world.AreEnemies(player,e.owner)&&e.def.kind!=EntityKind.Resource&&world.IsVisible(player,e)){enemies.Add(e);economicThreats.Add(e);memory[e.id]=e.position;}
                else if(AiResourceContinuity072.EconomicThreat(world,player,e))economicThreats.Add(e);
            }
            // Neutral camp weapons affect civilian safety, never enemy-faction
            // memory, counter selection or the army's strategic destination.
            DevelopIndustry(own,economicThreats);
            int soldierCount=army.FindAll(e=>(e.def.tags&Tags.Explorer)==0).Count;
            var p=world.Players[player];var town=Find(own,"town");bool advancing=false;
            foreach(var e in own)foreach(var j in e.queue)if(j.kind=="Age"||j.kind=="Revolution")advancing=true;
            construction.ResumeOne(own,workers,economicThreats);
            if(town==null&&workers.Count>0)Build(own,workers,"town");
            int workerGoal=Math.Min(world.WorkerLimit(player),p.age==1?12:p.age==2?24:p.age==3?34:42);
            var workerDefinition=world.WorkerDefinition(player);bool paidWorkers=world.AutoRecruitSeconds(player)<=0;
            bool underAttack=armyPlan.HasImmediateThreat(own,enemies);
            bool rangedRaid=enemies.Exists(e=>(e.def.tags&Tags.Infantry)!=0&&e.def.range>=16&&(e.position-home).sqrMagnitude<40*40);
            bool investWorkers=!world.WorkerSourcesClosed(player)&&world.CivilianWorkersEnabled(player)&&workers.Count<workerGoal&&(!underAttack||soldierCount>=6||workers.Count<12);
            int raiders=enemies.FindAll(e=>(e.Military||(e.Scout||e.def.healer)&&(e.def.damage>0||e.def.meleeDamage>0||e.def.siegeDamage>0))&&(e.def.tags&Tags.Explorer)==0&&(e.position-home).sqrMagnitude<34*34).Count;
            agePlan.Tick(town,workers.Count,soldierCount,raiders,advancing,revolution.AllowsAgeAdvance);
            var politicalReserve=revolution.Reserve;
            bool saveForAge=agePlan.Saving||politicalReserve.food+politicalReserve.wood+politicalReserve.coin>0;
            if(town!=null&&town.Complete&&town.queue.Count==0)
            {
                var next=world.Rules.Age(p.age+1);
                if(revolution.TryFreeCivilianSeat(town,enemies))
                {own.RemoveAll(e=>!e.Alive);army.RemoveAll(e=>!e.Alive);workers.RemoveAll(e=>!e.Alive);}
                if(revolution.NeedsFirstPaidWorker&&paidWorkers&&investWorkers&&!world.WorkerSourcesClosed(player)&&
                    world.ProductionReason(player,town,workerDefinition.id)==""&&CanSpend(world.UnitCost(player,workerDefinition),new Cost())&&
                    Send(new GameCommand{kind=CommandKind.Train,actors=new[]{town.id},item=workerDefinition.id}).accepted)
                {politicalReserve=revolution.Reserve;saveForAge=agePlan.Saving||politicalReserve.food+politicalReserve.wood+politicalReserve.coin>0;}
                else if(revolution.TryCommit(town))
                {politicalReserve=revolution.Reserve;saveForAge=agePlan.Saving||politicalReserve.food+politicalReserve.wood+politicalReserve.coin>0;}
                else if(p.age<world.MaxAge&&next!=null&&CanSpend(next.cost,new Cost())&&agePlan.Saving&&revolution.AllowsAgeAdvance)
                    Send(new GameCommand{kind=CommandKind.AdvanceAge,actors=new[]{town.id},choice=agePlan.ChooseBranch(underAttack)});
                else if(paidWorkers&&investWorkers&&!world.WorkerSourcesClosed(player)&&!saveForAge&&CanSpend(world.UnitCost(player,workerDefinition),new Cost()))Send(new GameCommand{kind=CommandKind.Train,actors=new[]{town.id},item=workerDefinition.id});
            }
            Cost progressReserve=agePlan.Reserve+politicalReserve;
            Cost workerReserve=!world.WorkerSourcesClosed(player)&&paidWorkers&&town!=null&&town.Complete&&town.queue.Count==0&&investWorkers&&!saveForAge?world.UnitCost(player,workerDefinition):new Cost();
            bool firstBank=p.age>=2&&world.CountingHouseLimit(player)>0&&Find(own,"bank")==null&&!saveForAge&&!underAttack;
            Cost bankReserve=firstBank?world.EntityCost(player,world.Rules.Entity("bank")):new Cost();
            string specialBuilding=civilizationEconomy.Building;
            bool firstSpecial=specialBuilding!=""&&p.age>=world.UnitAge(player,world.Rules.Entity(specialBuilding))&&Find(own,specialBuilding)==null&&!saveForAge&&!underAttack;
            Cost specialReserve=firstSpecial?world.EntityCost(player,world.Rules.Entity(specialBuilding)):new Cost();
            var factoryArtillery=world.FactoryArtilleryDefinition(player);
            int housingMargin=factoryArtillery!=null&&own.Exists(e=>e.def.id=="factory")?Math.Max(5,factoryArtillery.population):5;
            var recovery=continuity.Evaluate();
            if(AiMarket073.TryRelieve(world,player,own,recovery,progressReserve+workerReserve+bankReserve+specialReserve))
            {continuity.InvalidateSourceSurvey();recovery=continuity.Evaluate();}
            if(recovery.CoreBuilding!=""&&recovery.CoreBuilding!="town")Build(own,workers,recovery.CoreBuilding);
            if(p.populationCap-p.usedPopulation-p.reservedPopulation<housingMargin&&!Unfinished(own,"house")&&p.populationCap<world.Rules.populationLimit)Build(own,workers,"house");
            if(firstSpecial&&p.age<2&&CanSpend(specialReserve,workerReserve))Build(own,workers,specialBuilding);
            if(p.age>=2)
            {
                if(Find(own,"barracks")==null)Build(own,workers,"barracks");
                else if(rangedRaid&&Find(own,"stable")==null)Build(own,workers,"stable");
                else if(firstSpecial){if(CanSpend(specialReserve,workerReserve))Build(own,workers,specialBuilding);}
                else if(firstBank){if(CanSpend(bankReserve,workerReserve))Build(own,workers,"bank");}
                else if(Find(own,"market")==null)Build(own,workers,"market");
                else if(Find(own,"stable")==null&&workers.Count>=16)Build(own,workers,"stable");
                else if(world.CountingHouseLimit(player)>0&&!saveForAge&&!underAttack&&own.FindAll(x=>x.def.id=="bank").Count<Math.Min(world.CountingHouseLimit(player),p.age))Build(own,workers,"bank");
                if(p.age>=world.UnitAge(player,world.Rules.Entity("foundry"))&&Find(own,"foundry")==null)Build(own,workers,"foundry");
            }
            foreach(var e in own)if(e.def.garrison>0&&e.occupants.Count>0&&economicThreats.Find(x=>(x.position-e.position).sqrMagnitude<24*24)==null)Send(new GameCommand{kind=CommandKind.Ungarrison,actors=new[]{e.id}});
            bool medicalPriority=!saveForAge&&!underAttack&&medicalSupport.YieldNoncriticalMosqueResearch(own,workers.Count,soldierCount);
            Cost civilizationResearchReserve=civilizationEconomy.TechnologyReserve(own,saveForAge,underAttack,medicalPriority);
            civilizationEconomy.Tick(own,workers,economicThreats,continuity.Evaluate().ProtectedReserve(workerReserve+bankReserve+progressReserve),saveForAge,underAttack,medicalPriority);
            bool riverInvestment=workers.Count>=12&&soldierCount>=4&&own.Exists(e=>e.def.id=="barracks"&&e.Complete);
            waterEconomy.Tick(own,workers,enemies,continuity.Evaluate().ProtectedReserve(workerReserve+bankReserve+specialReserve+progressReserve),saveForAge||!riverInvestment,underAttack,economicThreats);
            var economyTarget=EconomyTarget(own,workers,enemies,saveForAge,investWorkers,firstBank,politicalReserve)+continuity.Evaluate().ResourceNeed;
            ChooseFactoryProduction(own,army,economyTarget,dt);
            workforce.Assign(workers,economyTarget,economicThreats);
            revolution.CollectCrates(army,economicThreats);
            EnsureSustainableEconomy(own,workers,p);
            if(continuity.Evaluate().TradeBootstrap||p.age>=2&&!underAttack&&!saveForAge&&workers.Count>=16)InvestInRoute(own,workers,economicThreats);
            foreach(var post in own)if(post.def.id=="trade_post"&&post.Complete&&p.technologies.Contains("cargo_ledgers"))
            {var supply=continuity.Evaluate();var wanted=supply.NeedsWood?ResourceKind.Wood:supply.CoinChannelNeeded?ResourceKind.Coin:p.resources.food<500?ResourceKind.Food:p.resources.coin<500?ResourceKind.Coin:ResourceKind.Wood;if(post.tradeMode!=wanted)Send(new GameCommand{kind=CommandKind.TradeMode,actors=new[]{post.id},index=(int)wanted});}
            if(p.age>=2&&!underAttack&&!saveForAge)UseNeutralSettlement(own,workerReserve+bankReserve+specialReserve+civilizationResearchReserve+progressReserve);
            if(p.shipmentTokens>0)
            {
                CardDef chosen=null;float best=-10000;var supply=continuity.Evaluate();
                foreach(string id in world.ActiveShipmentCards(player))
                {
                    if(world.ShipmentReason(player,id)!="")continue;var card=world.Rules.Card(id);
                    float score=card.repeatable?0:8;score+=card.age*2;
                    if(card.unit=="cannon"&&p.age>=3)score+=40;
                    else if(!string.IsNullOrEmpty(card.unit)&&(world.Rules.Entity(card.unit).tags&Tags.Villager)!=0&&workers.Count<workerGoal)score+=20;
                    else if(!string.IsNullOrEmpty(card.unit)&&soldierCount<12)score+=15;
                    if(card.tech=="bank_charter"&&world.CountingHouseLimit(player)>0)score+=15;
                    if(card.unit=="factory_wagon"&&p.age>=4&&world.ClassicIndustryAvailable(player))
                    {
                        int industry=own.FindAll(e=>e.Alive&&(e.def.id=="factory"||e.def.id=="factory_wagon")).Count;
                        foreach(var pending in p.shipments)if(world.Rules.Card(pending.card)?.unit=="factory_wagon")industry++;
                        score+=underAttack?(industry==0?20:10):(industry==0?70:35);
                        if(supply.NeedsWood&&industry==0)score+=180;
                    }
                    if(supply.NeedsWood&&supply.HasReceiver&&card.crates.wood>0)score+=200+Math.Min(card.crates.wood,Math.Max(0,supply.ResourceNeed.wood-p.resources.wood))*.1f;
                    if(supply.NeedsWood&&supply.HasReceiver&&card.tech=="cargo_ledgers"&&own.Exists(e=>e.Alive&&e.def.id=="trade_post"))score+=240;
                    if(saveForAge)
                    {
                        var need=progressReserve;
                        float useful=Math.Min(card.crates.food,Math.Max(0,need.food-p.resources.food))+Math.Min(card.crates.wood,Math.Max(0,need.wood-p.resources.wood))+Math.Min(card.crates.coin,Math.Max(0,need.coin-p.resources.coin));
                        score+=useful*.04f;
                        if(politicalReserve.food+politicalReserve.wood+politicalReserve.coin>0&&useful>0)score+=80;
                    }
                    if(score>best){best=score;chosen=card;}
                }
                if(chosen!=null)Send(new GameCommand{kind=CommandKind.Ship,item=chosen.id});
            }
            // Artillery pays first; other queues cannot repeatedly spend its saving target.
            int cannonCount=world.UnitCountIncludingProduction(player,"cannon");
            Cost siegeReserve=p.age>=3&&Find(own,"foundry")!=null&&cannonCount<2?world.UnitCost(player,world.Rules.Entity("cannon")):new Cost();
            // This approved income startup remains eligible while age saving;
            // ordinary research below keeps its existing saving/defense gate.
            var ledgerPlan=continuity.Evaluate();
            var ledgerPost=own.Find(e=>e.Alive&&e.Complete&&e.def.id=="trade_post"&&e.queue.Count==0);
            var ledgerTech=world.Rules.Tech("cargo_ledgers");
            if(ledgerPlan.LedgerBootstrap&&ledgerPost!=null&&world.ProductionReason(player,ledgerPost,ledgerTech.id,true)==""&&
                CanSpend(ledgerTech.cost,new Cost(),ledgerTech.id))
                Send(new GameCommand{kind=CommandKind.Research,actors=new[]{ledgerPost.id},item=ledgerTech.id});
            int Priority(EntityState e)=>rangedRaid&&e.def.id=="stable"?0:e.def.id=="foundry"?1:2;
            own.Sort((a,b)=>Priority(a).CompareTo(Priority(b)));
            foreach(var producer in own)
            {
                if(!producer.Complete||producer.queue.Count>0)continue;
                bool artillery=producer.def.id=="foundry";
                var choices=new List<EntityDef>();foreach(var d in world.Rules.entities)
                    if(d.kind==EntityKind.Unit&&!d.civilian&&!d.scout&&!d.healer&&!d.waterborne&&d.producer==producer.def.id&&(d.tags&Tags.Villager)==0&&world.EntityAvailable(player,d)&&world.UnitAge(player,d)<=p.age)choices.Add(d);
                if(artillery)choices.Sort((a,b)=>(a.id=="cannon"?0:1).CompareTo(b.id=="cannon"?0:1));
                else choices.Sort((a,b)=>CounterScore(b,enemies).CompareTo(CounterScore(a,enemies)));
                if(p.age>=2&&(!saveForAge||underAttack))for(int i=0;i<choices.Count;i++)
                {
                    var d=choices[i];if(world.ProductionReason(player,producer,d.id)!="")continue;
                    int batch=Math.Min(d.batch,p.age>=3?2:1);bool queued=false;
                    for(int n=batch;n>=1;n--)
                    {
                        var cost=world.UnitCost(player,d)*n;
                        bool urgentCounter=underAttack&&rangedRaid&&soldierCount<6&&producer.def.id=="stable";
                        if(!CanSpend(cost,workerReserve+bankReserve+specialReserve+civilizationResearchReserve+progressReserve+(artillery?new Cost():siegeReserve),urgentDefense:urgentCounter))continue;
                        var result=Send(new GameCommand{kind=CommandKind.Train,actors=new[]{producer.id},item=d.id,count=n});
                        if(result.accepted){rotation++;queued=true;break;}
                    }
                    if(queued)break;
                }
                if(producer.queue.Count>0||saveForAge||underAttack&&soldierCount<6||medicalPriority&&producer.def.id=="mosque")continue;
                foreach(var t in world.Rules.techs)
                {
                    if(t.healing>0||t.healerTrainSpeed>0)continue; // Medical investment owns this optional support budget.
                    if(world.ProductionReason(player,producer,t.id,true)!="")continue;
                    // Unit upgrades need a meaningful existing force; economy can invest sooner.
                    if(t.units!=null&&t.units.Length>0&&world.UnitCountIncludingProduction(player,t.units[0])<4)continue;
                    var techReserve=workerReserve+bankReserve+specialReserve+civilizationResearchReserve+progressReserve+siegeReserve;
                    if(t.id=="cargo_ledgers"&&continuity.Evaluate().LedgerBootstrap)techReserve=new Cost();
                    if(!CanSpend(t.cost,techReserve,t.id))continue;
                    Send(new GameCommand{kind=CommandKind.Research,actors=new[]{producer.id},item=t.id});break;
                }
            }
            // Medical spending follows ordinary military queues and protects
            // all economic, political, civilization and siege commitments.
            var supportReserve=progressReserve+workerReserve+bankReserve+specialReserve+civilizationResearchReserve+siegeReserve;
            bool investMedical=!saveForAge&&!underAttack;
            if(investMedical&&medicalSupport.NeedsBuilding(own,workers.Count,soldierCount))
            {
                var medicalBuilding=world.Rules.Entity(medicalSupport.Building);
                if(medicalBuilding!=null&&CanSpend(world.EntityCost(player,medicalBuilding),supportReserve))Build(own,workers,medicalBuilding.id);
            }
            medicalSupport.TryInvest(own,workers.Count,soldierCount,supportReserve,investMedical);
            medicalSupport.ReviewSupport(own,army,economicThreats,home);
            // Support scouting spends only after military queues and research,
            // so an Envoy cannot consume the first available skirmisher budget.
            ReviewScouts075(own,workers,economicThreats,progressReserve+workerReserve+bankReserve+specialReserve+civilizationResearchReserve+siegeReserve,!saveForAge&&!underAttack&&workers.Count>=12);
            foreach(var unit in army)if((unit.def.tags&Tags.Explorer)!=0)Explore(unit);
            // Memory remains observations at their original position; the army
            // planner owns cohorts, defense, rally and transport-compatible dispatch.
            Vector2 destination=enemyStart;float nearest=float.MaxValue;int removeId=0;bool knownTarget=false;
            foreach(var pair in memory)
            {
                bool empty=world.Fog.At(player,pair.Value)==VisibilityState.Visible&&enemies.Find(x=>x.id==pair.Key)==null;
                if(empty){removeId=pair.Key;continue;}
                float distance=(pair.Value-home).sqrMagnitude;
                if(distance<nearest){nearest=distance;destination=pair.Value;knownTarget=true;}
            }
            if(removeId!=0)ForgetMemory(removeId);
            armyPlan.Review(own,army,enemies,destination,knownTarget,search,waterEconomy,revolution,civilizationEconomy,medicalSupport);
        }
        private void UseNeutralSettlement(List<EntityState> own,Cost reserve)
        {
            foreach(var state in world.NeutralAllies)
            {
                var site=world.Get(state.site);if(!world.IsVisible(player,site))continue;
                var definition=world.NeutralDefinition(state.id);
                if(world.NeutralCommandReason(player,state.site,CommandKind.NeutralAlliance)==""&&CanSpend(definition.allianceCost,reserve))
                {Send(new GameCommand{kind=CommandKind.NeutralAlliance,target=state.site});return;}
                if(state.owner!=player)continue;
                int units=own.FindAll(e=>e.Alive&&e.def.id==definition.unit).Count;
                var tech=world.Rules.Tech(definition.tech);
                if(units>=3&&world.NeutralCommandReason(player,state.site,CommandKind.NeutralResearch)==""&&CanSpend(tech.cost,reserve))
                {Send(new GameCommand{kind=CommandKind.NeutralResearch,target=state.site});return;}
                var unit=world.Rules.Entity(definition.unit);
                if(units<5&&world.NeutralCommandReason(player,state.site,CommandKind.NeutralRecruit)==""&&CanSpend(world.UnitCost(player,unit),reserve))
                {Send(new GameCommand{kind=CommandKind.NeutralRecruit,target=state.site});return;}
            }
        }
        private void ForgetMemory(int id)
        {
            if(!memory.Remove(id))return;
            // A checkpoint retains entries, not Dictionary's deleted slots.
            // Compact after removal so later sightings append in the same order
            // in both the live controller and a controller restored from JSON.
            var retained=new List<KeyValuePair<int,Vector2>>(memory);memory.Clear();
            foreach(var item in retained)memory.Add(item.Key,item.Value);
        }
        private void DevelopIndustry(List<EntityState> own,List<EntityState> visibleEnemies)
        {
            if(!world.ClassicIndustryAvailable(player)||world.Players[player].age<4)return;
            foreach(var wagon in own)
            {
                if(!wagon.Alive||wagon.def.id!="factory_wagon"||wagon.garrisonedIn!=0||wagon.orders.Count>0)continue;
                bool built=false;
                // Search current vision around the home, then the delivered wagon;
                // CanBuild still owns terrain, foundations, limits and fog authority.
                for(int center=0;center<2&&!built;center++)for(int ring=0;ring<5&&!built;ring++)for(int n=0;n<20;n++)
                {
                    float angle=(n+buildSerial)*Mathf.PI*2/20;
                    Vector2 point=(center==0?home:wagon.position)+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*(10+ring*5);
                    point=new Vector2(Mathf.Round(point.x),Mathf.Round(point.y));
                    if(!AiResourceContinuity072.Safe(world,player,point,visibleEnemies))continue;
                    if(world.CanBuild(player,"factory",point)!=""||world.IndustrialDeploymentReason(player,point,wagon.id)!="")continue;
                    if(!Send(new GameCommand{kind=CommandKind.Build,actors=new[]{wagon.id},item="factory",point=point}).accepted)continue;
                    buildSerial+=3;built=true;break;
                }
            }
        }
        private void ChooseFactoryProduction(List<EntityState> own,List<EntityState> army,Cost target,float dt)
        {
            var artillery=world.FactoryArtilleryDefinition(player);if(artillery==null||world.Players[player].age<4)return;
            var factories=own.FindAll(e=>e.def.id=="factory"&&e.Complete&&e.garrisonedIn==0);
            factories.Sort((a,b)=>a.id.CompareTo(b.id));if(factories.Count==0)return;
            var p=world.Players[player];var resources=p.resources;
            string resource="food";float deficit=target.food-resources.food;
            if(target.wood-resources.wood>deficit){resource="wood";deficit=target.wood-resources.wood;}
            if(target.coin-resources.coin>deficit)resource="coin";
            // Housing must not wait behind food or the next age's savings while a ready gun holds its progress.
            if(continuity.Evaluate().NeedsWood)resource="wood";
            if(p.populationCap-p.usedPopulation-p.reservedPopulation<artillery.population&&p.populationCap<world.Rules.populationLimit&&
                resources.wood<world.EntityCost(player,world.Rules.Entity("house")).wood)resource="wood";
            int guards=army.FindAll(e=>e.Complete&&e.garrisonedIn==0&&!e.def.civilian&&(e.def.tags&(Tags.Explorer|Tags.Artillery))==0).Count;
            int guns=own.FindAll(e=>e.def.id==artillery.id).Count;
            Vector2 rally=world.Navigation.NearestWalkable(armyPlan.RallyPoint,artillery.radius);
            for(int i=0;i<factories.Count;i++)
            {
                var factory=factories[i];if(factory.queue.Count>0)continue;
                string wanted=i>0&&guards>=6&&guns<2?"artillery":resource;
                if(factory.factoryMode=="artillery"&&wanted!="artillery"&&factory.factoryElapsed>0)
                {
                    bool justProduced=false;
                    // A gun can already have started its next cycle before this scheduled review.
                    // Only a real output since the preceding review releases that commitment.
                    float since=world.Time-Mathf.Max(Difficulty.DecisionSeconds,dt)-MatchSimulation.StepSeconds-.001f;
                    for(int n=world.Events.Count-1;n>=0&&world.Events[n].time>=since;n--)
                        if(world.Events[n].kind=="FactoryArtilleryProduced"&&world.Events[n].entity==factory.id){justProduced=true;break;}
                    if(!justProduced)continue;
                }
                if(factory.factoryMode!=wanted)Send(new GameCommand{kind=CommandKind.FactoryMode,actors=new[]{factory.id},item=wanted});
                if(wanted=="artillery"&&(factory.rallyTarget!=0||(factory.rally-rally).sqrMagnitude>.01f))
                    Send(new GameCommand{kind=CommandKind.Rally,actors=new[]{factory.id},point=rally});
            }
        }
        private void Explore(EntityState unit)
        {
            if(unit.orders.Count>0&&unit.orders.Peek().kind!=OrderKind.Move)return;
            TreasureState chosen=null;float nearest=float.MaxValue;
            foreach(var state in world.Treasures)
            {
                var treasure=world.Get(state.entity);if(!world.IsVisible(player,treasure))continue;
                if(world.Layout.RequiresTransport&&world.Layout.LandRegion(unit.position)!=world.Layout.LandRegion(treasure.position))continue;
                bool guarded=state.guards.Exists(id=>world.IsVisible(player,world.Get(id)));
                if(guarded&&unit.hp<world.MaxHp(unit)*.35f)continue;
                float d=(treasure.position-unit.position).sqrMagnitude;if(d<nearest){nearest=d;chosen=state;}
            }
            if(chosen!=null)
            {
                var treasure=world.Get(chosen.entity);var guard=chosen.guards.ConvertAll(world.Get).Find(e=>world.IsVisible(player,e)&&(!world.Layout.RequiresTransport||world.Layout.LandRegion(unit.position)==world.Layout.LandRegion(e.position)));
                if(guard!=null){Send(new GameCommand{kind=CommandKind.Order,actors=new[]{unit.id},order=OrderKind.Attack,target=guard.id});return;}
                if(world.TreasureReason(player,treasure)==""){Send(new GameCommand{kind=CommandKind.Order,actors=new[]{unit.id},order=OrderKind.Claim,target=treasure.id});return;}
            }
            if(unit.orders.Count==0){Vector2 point=(unit.position-enemyStart).sqrMagnitude>200?enemyStart:new Vector2(64,64);point=waterEconomy.LandArmyDestination(unit,point);Send(new GameCommand{kind=CommandKind.Order,actors=new[]{unit.id},order=OrderKind.Move,point=point});}
        }
        private void ReviewScouts075(List<EntityState> own,List<EntityState> workers,List<EntityState> visible,Cost reserve,bool recruit)
        {
            foreach(var scout in own)if(scout.Scout&&world.CanScout(scout)&&!scout.hold)
            {
                if(scout.orders.Count==0)Send(new GameCommand{kind=CommandKind.Order,actors=new[]{scout.id},order=OrderKind.Scout,point=scout.position});
                if(scout.orders.Count==1&&scout.orders.Peek().kind==OrderKind.Scout&&!scout.stealth&&world.StealthReason(player,scout)=="")
                    Send(new GameCommand{kind=CommandKind.ToggleStealth,actors=new[]{scout.id}});
            }
            if(!recruit)return;
            // One dedicated scout, counting the paid queue, is enough. These
            // support units never enter counter selection or army strength.
            foreach(var definition in world.Rules.entities)if(definition.scout&&world.EntityAvailable(player,definition))
            {
                if(world.UnitCountIncludingProduction(player,definition.id)>0)continue;
                var producer=own.Find(e=>e.Alive&&e.Complete&&e.garrisonedIn==0&&e.def.id==definition.producer&&e.queue.Count==0);
                if(producer!=null&&world.ProductionReason(player,producer,definition.id)==""&&CanSpend(world.UnitCost(player,definition),reserve))
                {Send(new GameCommand{kind=CommandKind.Train,actors=new[]{producer.id},item=definition.id});return;}
            }
        }
        private void InvestInRoute(List<EntityState> own,List<EntityState> workers,List<EntityState> visible)
        {
            if(Find(own,"trade_post")!=null||world.Rules.landmarks==null)return;
            var definition=world.Rules.Entity("trade_post");var cost=world.EntityCost(player,definition);
            if(!world.CanAfford(player,cost))return;
            foreach(var site in world.TradeSockets)
            {
                var socket=world.Get(site.entity);if(!world.IsVisible(player,socket)||world.CanBuild(player,"trade_post",socket.position)!="")continue;
                var plan=continuity.Evaluate();
                var reserve=plan.TradeBootstrap?new Cost():agePlan.Reserve+revolution.Reserve;
                if(!CanSpend(cost,reserve,"trade_post"))continue;
                if(!AiResourceContinuity072.TryConstructionWorker(world,player,definition,socket.position,workers,visible,out var builder))
                {
                    var routes=new AiGatherRoutes074(world,player,visible);
                    var preview=new EntityState{def=definition,position=socket.position};
                    builder=own.Find(e=>e.Scout&&!e.hold&&world.CanConstruct(e,definition.id)&&e.orders.Count<=1&&
                        (e.orders.Count==0||e.orders.Peek().kind==OrderKind.Scout)&&routes.OrdinaryPathSafe(e,world.WorkPoint(preview,e)));
                    if(builder==null)continue;
                }
                if(Send(new GameCommand{kind=CommandKind.Build,actors=new[]{builder.id},item="trade_post",point=socket.position}).accepted){own.Add(world.Entities.Find(e=>e.Alive&&e.owner==player&&e.def.id=="trade_post"));return;}
            }
        }
        // Savings are inferred from actual next commands and rule costs, never awarded.
        private Cost EconomyTarget(List<EntityState> own,List<EntityState> workers,List<EntityState> visible,bool save,bool recruit,bool firstBank,Cost politicalReserve)
        {
            var p=world.Players[player];Cost target=new Cost();
            var settler=world.WorkerDefinition(player);
            if(recruit&&world.AutoRecruitSeconds(player)<=0)target+=world.UnitCost(player,settler)*(60/settler.time);
            if(agePlan.Saving)target+=agePlan.Reserve;
            else if(p.age==1&&revolution.AllowsAgeAdvance&&p.age<world.MaxAge)target+=world.Rules.Age(p.age+1).cost;
            target+=politicalReserve;
            int free=p.populationCap-p.usedPopulation-p.reservedPopulation;
            if(free<12&&p.populationCap<world.Rules.populationLimit)target+=world.EntityCost(player,world.Rules.Entity("house"));
            if(Find(own,"barracks")==null)target+=world.EntityCost(player,world.Rules.Entity("barracks"));
            else if(civilizationEconomy.Building!=""&&p.age>=world.UnitAge(player,world.Rules.Entity(civilizationEconomy.Building))&&Find(own,civilizationEconomy.Building)==null)target+=world.EntityCost(player,world.Rules.Entity(civilizationEconomy.Building));
            else if(firstBank)target+=world.EntityCost(player,world.Rules.Entity("bank"));
            else if(p.age>=2&&Find(own,"market")==null)target+=world.EntityCost(player,world.Rules.Entity("market"));
            else if(p.age>=2&&workers.Count>=16&&Find(own,"stable")==null)target+=world.EntityCost(player,world.Rules.Entity("stable"));
            if(p.age>=world.UnitAge(player,world.Rules.Entity("foundry"))&&Find(own,"foundry")==null)target+=world.EntityCost(player,world.Rules.Entity("foundry"));
            if(!save)foreach(var producer in own)
            {
                if(producer.def.kind!=EntityKind.Building||producer.def.id=="town")continue;
                EntityDef candidate=null;float score=float.MinValue;
                foreach(var d in world.Rules.entities)
                {
                    if(d.kind!=EntityKind.Unit||d.civilian||d.scout||d.healer||d.waterborne||(d.tags&Tags.Villager)!=0||d.producer!=producer.def.id||world.UnitAge(player,d)>p.age||!world.EntityAvailable(player,d))continue;
                    float value=CounterScore(d,visible);if(value>score){score=value;candidate=d;}
                }
                if(candidate!=null)target+=world.UnitCost(player,candidate)*(60/candidate.time);
            }
            if(Find(own,"town")==null)target+=world.EntityCost(player,world.Rules.Entity("town"));
            // Keep a modest infrastructure reserve when the next army is already funded.
            if(p.age>=2)target.wood+=world.EntityCost(player,world.Rules.Entity("house")).wood;
            return target+civilizationEconomy.Forecast(own,save)+waterEconomy.Forecast(own,save)+(save?new Cost():medicalSupport.Forecast(own,workers.Count,own.FindAll(e=>e.Military&&!e.def.waterborne&&(e.def.tags&Tags.Explorer)==0).Count));
        }
        private void EnsureSustainableEconomy(List<EntityState> ownEconomy,List<EntityState> workers,PlayerState p)
        {
            bool foodAvailable=workforce.HasSafeSource(ResourceKind.Food);
            int farmCount=ownEconomy.FindAll(e=>e.def.id=="farm").Count;
            if(p.age>=2&&(!foodAvailable||p.age>=3&&farmCount<Math.Max(1,workers.Count/16)))Build(ownEconomy,workers,"farm");
            bool coinAvailable=workforce.HasSafeSource(ResourceKind.Coin);
            if(p.age>=3&&!coinAvailable&&world.CanAfford(player,world.EntityCost(player,world.Rules.Entity("estate")))&&
                continuity.Evaluate().NeedsCoin)Build(ownEconomy,workers,"estate");
        }
        private bool Build(List<EntityState> own,List<EntityState> workers,string id)
        {
            if(Unfinished(own,id)||workers.Count==0)return false;
            var definition=world.Rules.Entity(id);var cost=world.EntityCost(player,definition);
            if(!world.CanAfford(player,cost))return false;
            var plan=continuity.Evaluate();
            if(plan.CoreDeferredForWood&&id==plan.CoreBuilding)return false;
            // A legal core income facility or replacement Town must be able to
            // fund the economy on which an unpaid age/political goal depends.
            var reserve=plan.IsCoreBuilding(id)?new Cost():agePlan.Reserve+revolution.Reserve;
            if(!CanSpend(cost,reserve,id))return false;
            var visible=new List<EntityState>();foreach(var enemy in world.Entities)
                if(AiResourceContinuity072.EconomicThreat(world,player,enemy))visible.Add(enemy);
            for(int ring=0;ring<4;ring++)for(int n=0;n<20;n++)
            {
                float angle=(n+buildSerial)*Mathf.PI*2/20;Vector2 point=home+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*(9+ring*5);point=new Vector2(Mathf.Round(point.x),Mathf.Round(point.y));
                if(id=="waystation"&&(point-home).sqrMagnitude<18*18)continue;
                if(world.CanBuild(player,id,point)!="")continue;
                if(!AiResourceContinuity072.TryConstructionWorker(world,player,definition,point,workers,visible,out var worker))continue;
                var r=Send(new GameCommand{kind=CommandKind.Build,actors=new[]{worker.id},item=id,point=point});
                if(r.accepted){var foundation=world.Entities.FindLast(e=>e.Alive&&e.owner==player&&e.def.id==id);if(foundation!=null&&!own.Contains(foundation))own.Add(foundation);buildSerial+=3;return true;}
            }
            return false;
        }
        private static EntityState Find(List<EntityState> list,string id)=>list.Find(e=>e.Alive&&e.def.id==id);
        private static bool Unfinished(List<EntityState> list,string id)=>list.Exists(e=>e.Alive&&e.def.id==id&&!e.Complete);
        private bool CanSpend(Cost cost,Cost reserve,string consumer="",bool urgentDefense=false)
        {
            if(!world.CanAfford(player,cost))return false;
            var plan=continuity.Evaluate();
            // A visible ranged raid and a weak home army may use food/coin
            // for its cavalry counter. Recovery wood remains protected, and
            // this exception never applies to construction or research.
            if(urgentDefense&&!plan.TownRecovery)
            {
                plan.Reserve.food=0;
                plan.Reserve.coin=plan.TradeBootstrap||plan.LedgerBootstrap?
                    Math.Min(plan.Reserve.coin,world.Rules.Tech("cargo_ledgers").cost.coin):0;
            }
            return AiResourceContinuity072.CanSpend(world,player,cost,plan.ProtectedReserve(reserve,consumer));
        }
        private float CounterScore(EntityDef unit,List<EntityState> visible)
        {
            float score=unit.id=="musketeer"?35:unit.id=="hussar"?25:20;
            if(unit.id=="escort"||unit.id=="shield_guard"||unit.id=="janissary")score=38;
            if(unit.id=="cuirassier")score=38;
            if(unit.id=="abus")score=36;
            if(unit.id=="convoy_gunner")score=36;
            if(unit.id=="outrider")score=30;
            if(unit.repairArtillery)score=world.UnitCountIncludingProduction(player,"cannon")+world.UnitCountIncludingProduction(player,"culverin")>0?40:8;
            foreach(var enemy in visible)
            {
                if(!enemy.Military||(enemy.def.tags&Tags.Explorer)!=0)continue;
                if((unit.id=="hussar"||unit.id=="cuirassier")&&(enemy.def.tags&Tags.LightInfantry)!=0)score+=18;
                foreach(var bonus in unit.bonuses)if((bonus.tags&enemy.def.tags)!=0)score+=8*(bonus.multiplier-1);
            }
            return score-world.UnitCountIncludingProduction(player,unit.id)*5;
        }
        private CommandResult Send(GameCommand c)
        {c.player=player;var result=world.Issue(c);if(result.accepted)continuity.InvalidateSourceSurvey();return result;}
    }
}
