using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    // A decision-local plan derived from owned state and current vision. No new
    // ledger, hidden resource inventory, mutable commitment or save state.
    public sealed class AiResourceContinuity072
    {
        readonly World world;readonly int player;readonly Func<Cost> obligations;
        bool decisionActive,surveyCached;float surveyTime;int surveyEvents,surveyNavigation;
        SourceSurvey cachedSurvey;
        readonly struct SourceSurvey
        {
            public readonly float WoodAmount;public readonly bool Wood,Coin;
            public SourceSurvey(float amount,bool wood,bool coin){WoodAmount=amount;Wood=wood;Coin=coin;}
        }
        public AiResourceContinuity072(World world,int player,Func<Cost> obligations=null){this.world=world;this.player=player;this.obligations=obligations;}
        // This temporary survey never crosses a synchronous strategy review and
        // has no save DTO. Plans, wallets and obligations are never cached.
        public void BeginDecision(){decisionActive=true;InvalidateSourceSurvey();}
        public void EndDecision(){decisionActive=false;InvalidateSourceSurvey();surveyTime=0;surveyEvents=surveyNavigation=0;}
        public void InvalidateSourceSurvey(){surveyCached=false;cachedSurvey=default;}
        public sealed class Plan
        {
            public Cost Reserve,ResourceNeed;
            public bool TownRecovery,NeedsWood,NeedsCoin,CoinChannelNeeded,TradeBootstrap,LedgerBootstrap,HasReceiver,NearWoodDepletion,CoreDeferredForWood;
            public float VisibleWoodRemaining,CoinForecast60,CoinDemand;
            public string CoreBuilding="";
            public Cost ProtectedReserve(Cost other,string consumer="")
            {
                var core=Reserve;
                if(CoreBuilding!=""&&consumer==CoreBuilding||consumer=="trade_post"&&TradeBootstrap||consumer=="cargo_ledgers"&&LedgerBootstrap)core=new Cost();
                return other+core;
            }
            public bool IsCoreBuilding(string id)=>id==CoreBuilding||id=="trade_post"&&TradeBootstrap;
        }
        public Plan Evaluate()
        {
            var own=new List<EntityState>();var workers=new List<EntityState>();var visible=new List<EntityState>();
            foreach(var e in world.Entities)
            {
                if(!e.Alive)continue;
                if(e.owner==player){own.Add(e);if(e.Worker&&!e.def.waterborne)workers.Add(e);}
                else if(EconomicThreat(world,player,e))visible.Add(e);
            }
            return EvaluatePlan(own,workers,visible,Survey(workers,visible,true));
        }
        public Plan Evaluate(List<EntityState> own,List<EntityState> workers,List<EntityState> visible)
            =>EvaluatePlan(own,workers,visible,Survey(workers,visible,false));
        SourceSurvey Survey(List<EntityState> workers,List<EntityState> visible,bool allowScope)
        {
            if(allowScope&&decisionActive&&surveyCached&&surveyTime==world.Time&&surveyEvents==world.Events.Count&&surveyNavigation==world.Navigation.Version)return cachedSurvey;
            var routes=new AiGatherRoutes074(world,player,visible);
            float amount=SourceAmountInSurvey(ResourceKind.Wood,workers,routes,out bool wood);
            SourceAmountInSurvey(ResourceKind.Coin,workers,routes,out bool coin,true);
            var surveyed=new SourceSurvey(amount,wood,coin);
            if(allowScope&&decisionActive)
            {cachedSurvey=surveyed;surveyCached=true;surveyTime=world.Time;surveyEvents=world.Events.Count;surveyNavigation=world.Navigation.Version;}
            return surveyed;
        }
        Plan EvaluatePlan(List<EntityState> own,List<EntityState> workers,List<EntityState> visible,SourceSurvey surveyed)
        {
            var result=new Plan();var p=world.Players[player];var committed=obligations?.Invoke()??new Cost();
            float woodAmount=surveyed.WoodAmount;bool wood=surveyed.Wood,coin=surveyed.Coin;
            bool landWorkers=workers.Exists(e=>e.Alive&&e.owner==player&&e.Worker&&!e.def.waterborne);
            bool town=own.Exists(e=>e.Alive&&e.def.id=="town");
            result.HasReceiver=own.Exists(e=>e.Alive&&e.Complete&&(e.def.id=="town"||e.def.id=="tower"));
            result.TownRecovery=!town&&landWorkers;result.VisibleWoodRemaining=woodAmount;
            float coinNeed=0;
            foreach(var producer in own)if(producer.Alive&&producer.Complete)
            {
                if(producer.def.id=="bank")result.CoinForecast60+=world.CountingHouseRate(player)*60;
                if(producer.def.id=="factory"&&producer.queue.Count==0&&producer.factoryMode=="coin")result.CoinForecast60+=world.FactoryResourceRate(player,ResourceKind.Coin)*60;
                if(producer.def.id=="trade_post"&&producer.tradeMode==ResourceKind.Coin&&p.technologies.Contains("cargo_ledgers"))
                    for(int i=world.Events.Count-1;i>=0&&world.Events[i].time>=world.Time-60;i--)
                    {var e=world.Events[i];if(e.player==player&&e.entity==producer.id&&e.kind=="TradeDelivery"&&e.message=="Coin")result.CoinForecast60+=e.amount;}
                float production=0;
                foreach(var unit in world.Rules.entities)if(unit.kind==EntityKind.Unit&&!unit.healer&&unit.producer==producer.def.id&&
                    world.EntityAvailable(player,unit)&&world.UnitAge(player,unit)<=p.age)production=Mathf.Max(production,world.UnitCost(player,unit).coin);
                coinNeed+=production*2;
            }
            int ageWorkers=Math.Min(world.WorkerLimit(player),p.age==1?12:p.age==2?20:p.age==3?30:38);
            int soldiers=own.FindAll(e=>e.Alive&&e.Military&&!e.def.waterborne&&(e.def.tags&Tags.Explorer)==0).Count;
            if(p.age<world.MaxAge&&workers.Count>=ageWorkers&&(p.age==1||soldiers>=8)&&!world.WorkerSourcesClosed(player))coinNeed+=world.Rules.Age(p.age+1)?.cost.coin??0;
            coinNeed=Mathf.Max(coinNeed,committed.coin);
            result.CoinDemand=coinNeed;
            // A funded wallet bridges production; it does not replace a source.
            // Keep a real trade startup protected until its dependency is paid.
            result.CoinChannelNeeded=landWorkers&&!coin&&coinNeed>0&&result.CoinForecast60+.001f<coinNeed;
            bool coinGap=!coin&&p.resources.coin+result.CoinForecast60+.001f<coinNeed;
            if(result.TownRecovery)result.CoreBuilding="town";
            else if(landWorkers&&p.age>=3&&coinGap&&!own.Exists(e=>e.Alive&&e.def.id=="estate"))result.CoreBuilding="estate";
            else if(landWorkers&&p.age>=2&&coinGap&&world.CountingHouseLimit(player)>0&&
                own.FindAll(e=>e.Alive&&e.def.id=="bank").Count<Math.Min(world.CountingHouseLimit(player),p.age))result.CoreBuilding="bank";
            if(result.CoreBuilding!="")result.Reserve=world.EntityCost(player,world.Rules.Entity(result.CoreBuilding));
            result.ResourceNeed=result.Reserve;
            result.NeedsCoin=coinGap&&landWorkers;
            if(result.NeedsCoin)result.ResourceNeed.coin=Mathf.Max(result.ResourceNeed.coin,coinNeed);
            // Even after the core is funded, a modest real house/infrastructure
            // horizon keeps an already-paid wood channel useful without locking it.
            float infrastructure=world.EntityCost(player,world.Rules.Entity("house")).wood;
            if(p.age>=2&&!own.Exists(e=>e.Alive&&e.def.id=="farm"))infrastructure+=world.EntityCost(player,world.Rules.Entity("farm")).wood;
            float woodHorizon=Mathf.Max(result.Reserve.wood,committed.wood+infrastructure);
            result.NearWoodDepletion=wood&&woodAmount<=woodHorizon+.001f;
            result.NeedsWood=(!wood||result.NearWoodDepletion)&&p.resources.wood+.001f<woodHorizon;
            if(result.NeedsWood)result.ResourceNeed.wood=Mathf.Max(result.ResourceNeed.wood,woodHorizon);
            bool industry=own.Exists(e=>e.Alive&&(e.def.id=="factory"||e.def.id=="factory_wagon"))||
                p.shipments.Exists(s=>world.Rules.Card(s.card)?.unit=="factory_wagon");
            var post=own.Find(e=>e.Alive&&e.def.id=="trade_post");
            bool ledger=p.technologies.Contains("cargo_ledgers");
            bool ledgerPending=own.Exists(e=>e.queue.Exists(j=>j.kind=="Research"&&j.item=="cargo_ledgers"))||
                p.shipments.Exists(s=>world.Rules.Card(s.card)?.tech=="cargo_ledgers");
            bool ledgerCard=false;
            if(result.HasReceiver)foreach(string id in world.ActiveShipmentCards(player))
                if(world.Rules.Card(id)?.tech=="cargo_ledgers"&&world.ShipmentReason(player,id)==""){ledgerCard=true;break;}
            var research=world.Rules.Tech("cargo_ledgers");
            var startup=(post==null?world.EntityCost(player,world.Rules.Entity("trade_post")):new Cost())+
                (ledger||ledgerPending||ledgerCard?new Cost():research.cost);
            float startupWood=startup.wood;
            // The competing coin facility must not consume an individually
            // funded wood startup. Count both still-unpaid expenses, including
            // the exact-wallet case and a Post whose Ledger is not paid yet.
            if(result.CoreBuilding!=""&&!result.TownRecovery&&landWorkers&&p.age>=2&&!industry)
                woodHorizon=Mathf.Max(woodHorizon,world.EntityCost(player,world.Rules.Entity(result.CoreBuilding)).wood+startupWood);
            if(!wood||woodAmount<=Mathf.Max(woodHorizon,startupWood)+.001f)
            {result.NearWoodDepletion=true;result.NeedsWood=p.resources.wood+.001f<Mathf.Max(woodHorizon,startupWood);}
            if((result.NeedsWood||result.CoinChannelNeeded)&&landWorkers&&p.age>=2&&!industry&&!(result.TownRecovery&&world.CanAfford(player,result.Reserve)))
            {
                result.CoreDeferredForWood=startupWood>0&&result.CoreBuilding!=""&&!result.TownRecovery&&world.CanAfford(player,startup)&&
                    p.resources.wood+.001f<world.EntityCost(player,world.Rules.Entity(result.CoreBuilding)).wood+startupWood;
                if(post==null)
                {
                    var cost=startup;
                    // Authorize a bounded, payable startup rather than sinking
                    // the last 200 wood into an unfinishable 400-wood dependency.
                    result.TradeBootstrap=world.CanAfford(player,cost);
                    if(result.CoreBuilding=="")result.Reserve=cost;
                    else if(!ledger&&!ledgerPending&&!ledgerCard)
                    {
                        // A replacement Town protects wood, but an approved
                        // trade startup also needs its still-unpaid ledger fee.
                        // Military queues cannot erode that coin while the
                        // actually paid Post is under construction.
                        result.Reserve.food=Mathf.Max(result.Reserve.food,cost.food);
                        result.Reserve.wood=Mathf.Max(result.Reserve.wood,cost.wood);
                        result.Reserve.coin=Mathf.Max(result.Reserve.coin,cost.coin);
                    }
                    if(result.TradeBootstrap)result.ResourceNeed+=cost;
                }
                else if(!ledger&&!ledgerPending)
                {
                    result.LedgerBootstrap=true;result.ResourceNeed+=ledgerCard?new Cost():research.cost;
                    if(result.CoreBuilding==""&&!ledgerCard)result.Reserve=research.cost;
                    else if(!ledgerCard)
                    {result.Reserve.food=Mathf.Max(result.Reserve.food,research.cost.food);result.Reserve.wood=Mathf.Max(result.Reserve.wood,research.cost.wood);result.Reserve.coin=Mathf.Max(result.Reserve.coin,research.cost.coin);}
                }
            }
            if(result.NeedsWood)result.ResourceNeed.wood=Mathf.Max(result.ResourceNeed.wood,Mathf.Max(woodHorizon,startupWood));
            return result;
        }
        float SourceAmountInSurvey(ResourceKind kind,List<EntityState> workers,AiGatherRoutes074 routes,out bool available,bool firstOnly=false)
        {
            available=false;float amount=0;
            foreach(var source in world.Entities)
                if(source.def.resource==kind&&!source.def.waterborne&&!source.def.id.StartsWith("crate_")&&
                    world.GatherableResource(player,source)&&routes.PointSafe(source.position)&&ReachableSource(source,workers,routes))
                {available=true;if(World.Sustainable(source))return float.PositiveInfinity;if(firstOnly)return source.remaining;amount+=source.remaining;}
            return amount;
        }
        bool ReachableSource(EntityState source,List<EntityState> workers,AiGatherRoutes074 routes)
        {
            foreach(var worker in workers)
            {
                if(worker.owner==player&&AiCivilizationEconomy.CanReassign(worker)&&routes.CanGather(worker,source))return true;
            }
            return false;
        }
        public static bool CanSpend(World world,int player,Cost cost,Cost reserve)
        {
            if(!world.CanAfford(player,cost))return false;var r=world.Players[player].resources;
            return r.food-cost.food+.001f>=Math.Min(r.food,reserve.food)&&r.wood-cost.wood+.001f>=Math.Min(r.wood,reserve.wood)&&r.coin-cost.coin+.001f>=Math.Min(r.coin,reserve.coin);
        }
        public Cost EnsureProtectedReserve(Cost supplied)
        {
            var core=Evaluate().Reserve;
            // Integrated callers have already added the core obligation. Direct
            // component reviews still cannot omit it, or add it a second time.
            return new Cost(Mathf.Max(supplied.food,core.food),Mathf.Max(supplied.wood,core.wood),Mathf.Max(supplied.coin,core.coin));
        }
        public static bool MayRepair(World world,int player,EntityState target=null)
        {
            bool town=false,workers=false;
            foreach(var e in world.Entities)if(e.Alive&&e.owner==player){if(e.def.id=="town")town=true;if(e.Worker&&!e.def.waterborne)workers=true;}
            if(!town&&workers)return false;
            if(target==null)return true;
            var value=target.def.repairCost;
            if(value.food<=0&&value.wood<=0&&value.coin<=0)value=world.EntityCost(player,target.def);
            float rate=target.def.waterborne?world.NavalRepairRate(target):18;
            float heal=Mathf.Min(rate*MatchSimulation.StepSeconds,Mathf.Max(0,world.MaxHp(target)-target.hp));
            Cost cost=value*(heal/world.MaxHp(target)*.5f);
            if(target.def.waterborne)cost.food=0;
            if(!world.CanAfford(player,cost))return false;
            var reserve=new AiResourceContinuity072(world,player).Evaluate().Reserve;
            return CanSpend(world,player,cost,reserve);
        }
        public void StopRecoveryRepairs(List<EntityState> own)
        {
            // No active repairs means no expensive source/path scan between
            // reviews. With repairs, inspect a fresh core budget once and sum
            // all next fixed-step costs against the same wallet.
            var repairs=own.FindAll(e=>e.Alive&&e.owner==player&&e.orders.Count>0&&
                (e.orders.Peek().kind==OrderKind.Repair||e.orders.Peek().kind==OrderKind.DockRepair));
            if(repairs.Count==0)return;
            var plan=Evaluate();Cost committed=new Cost();
            foreach(var actor in repairs)
            {
                var target=actor.orders.Peek().kind==OrderKind.DockRepair?actor:world.Get(actor.orders.Peek().target);
                Cost cost=new Cost();
                if(target!=null&&target.Alive)
                {
                    var value=target.def.repairCost;if(value.food<=0&&value.wood<=0&&value.coin<=0)value=world.EntityCost(player,target.def);
                    float rate=target.def.waterborne?world.NavalRepairRate(target):18;
                    cost=value*(Mathf.Min(rate*MatchSimulation.StepSeconds,Mathf.Max(0,world.MaxHp(target)-target.hp))/world.MaxHp(target)*.5f);
                    if(target.def.waterborne)cost.food=0;
                }
                if(plan.TownRecovery||!CanSpend(world,player,committed+cost,plan.Reserve))
                    world.Issue(new GameCommand{player=player,kind=CommandKind.Order,actors=new[]{actor.id},order=OrderKind.Stop});
                else committed+=cost;
            }
        }
        // Scout is a support/cohort role, not immunity from enemy safety checks.
        // Its actual visible weapon still endangers gathering and construction.
        public static bool EconomicThreat(World world,int player,EntityState e)=>e!=null&&e.Alive&&
            (world.AreEnemies(player,e.owner)&&(e.Military||(e.Scout||e.def.healer)&&(e.def.damage>0||e.def.meleeDamage>0||e.def.siegeDamage>0)||e.Complete&&e.def.kind==EntityKind.Building&&(e.def.damage>0||e.def.siegeDamage>0))||
             e.owner<0&&World.Guardian(e)&&(e.def.damage>0||e.def.meleeDamage>0||e.def.siegeDamage>0))&&
            (e.def.tags&Tags.Explorer)==0&&world.IsVisible(player,e);
        static bool HasRelevantThreat(World world,int player,List<EntityState> visible)
        {
            for(int i=0;i<visible.Count;i++)if(EconomicThreat(world,player,visible[i]))return true;
            return false;
        }
        public static bool Safe(World world,int player,Vector2 point,List<EntityState> visible)
        {
            // This is also used per navigation cell. Avoid a captured predicate
            // allocation while retaining the same weapon, visibility and range.
            for(int i=0;i<visible.Count;i++)
            {
                var e=visible[i];if(!EconomicThreat(world,player,e))continue;
                float reach=Mathf.Max(12,e.def.range+4);
                if((e.position-point).sqrMagnitude<reach*reach)return false;
            }
            return true;
        }
        public static bool SegmentSafe(World world,int player,Vector2 from,Vector2 to,List<EntityState> visible)
        {
            Vector2 segment=to-from;float length=segment.sqrMagnitude;
            for(int i=0;i<visible.Count;i++)
            {
                var threat=visible[i];if(!EconomicThreat(world,player,threat))continue;
                float t=length>0?Mathf.Clamp01(Vector2.Dot(threat.position-from,segment)/length):0;
                float reach=Mathf.Max(12,threat.def.range+4);
                if((threat.position-(from+segment*t)).sqrMagnitude<reach*reach)return false;
            }
            return true;
        }
        public static bool TryConstructionWorker(World world,int player,EntityDef definition,Vector2 point,
            List<EntityState> workers,List<EntityState> visible,out EntityState selected,int foundation=0)
        {
            var existing=foundation>0?world.Get(foundation):null;
            if(foundation>0&&(existing==null||!existing.Alive||existing.owner!=player||existing.def.id!=definition.id))
            {selected=null;return false;}
            point=existing!=null?existing.position:world.BuildPoint(player,definition.id,point);
            selected=null;if(!Safe(world,player,point,visible))return false;
            var candidates=workers.FindAll(e=>e.owner==player&&e.Alive&&e.Worker&&e.garrisonedIn==0&&!e.hold&&!e.def.waterborne&&
                (AiCivilizationEconomy.CanReassign(e)||foundation>0&&e.orders.Count==1&&e.orders.Peek().kind==OrderKind.Build&&e.orders.Peek().target==foundation)&&Safe(world,player,e.position,visible));
            candidates.Sort((a,b)=>(a.position-point).sqrMagnitude.CompareTo((b.position-point).sqrMagnitude));
            bool threatenedPaths=HasRelevantThreat(world,player,visible);GridNavigation prospective=null;
            var target=existing??new EntityState{def=definition,position=point,owner=player};
            for(int candidate=0;candidate<Math.Min(6,candidates.Count);candidate++)
            {
                var worker=candidates[candidate];
                if(threatenedPaths)
                {
                    // A filtered alternate route is not the route commanded by
                    // Build. Preview the exact post-foundation ordinary route
                    // locally, without changing the live navigation or accounts.
                    if(prospective==null)
                    {
                        if(existing!=null)prospective=world.Navigation;
                        else
                        {
                            var state=world.Navigation.CaptureState();prospective=new GridNavigation(state.width,state.height);prospective.RestoreState(state);
                            foreach(var cell in world.BuildingFootprintCells(definition,point))prospective.SetBlocked(cell,0,true);
                        }
                    }
                    if(!prospective.IsWalkable(worker.position,worker.def.radius))continue;
                    // Movement already stops at this real edge distance. Do not
                    // invent an approach trip for an eligible nearby builder.
                    if(world.EdgeDistance(worker,target)<=1.35f){selected=worker;return true;}
                    var delta=worker.position-point;if(delta.sqrMagnitude<.01f)delta=Vector2.right;
                    float actualReach=Mathf.Max(definition.width,definition.depth)*.5f+worker.def.radius+.85f;
                    var goal=prospective.NearestWalkable(point+delta.normalized*actualReach,worker.def.radius);
                    if(!Safe(world,player,goal,visible))continue;
                    var arrival=new EntityState{def=worker.def,position=goal,owner=player};
                    if(world.EdgeDistance(arrival,target)>1.5f)continue;
                    bool ordinarySafe;
                    if(prospective.ClearLine(worker.position,goal,worker.def.radius))ordinarySafe=SegmentSafe(world,player,worker.position,goal,visible);
                    else
                    {
                        var ordinary=prospective.FindPath(worker.position,goal,worker.def.radius);
                        ordinarySafe=ordinary.Count>=2&&(ordinary[0]-worker.position).sqrMagnitude<.01f&&(ordinary[ordinary.Count-1]-goal).sqrMagnitude<.01f;
                        for(int i=1;i<ordinary.Count&&ordinarySafe;i++)ordinarySafe=SegmentSafe(world,player,ordinary[i-1],ordinary[i],visible);
                    }
                    if(!ordinarySafe)continue;
                }
                // Check approaches outside the future footprint. Path filters
                // prevent a prospective foundation from severing its own builder.
                float reach=Mathf.Max(definition.width,definition.depth)*.5f+worker.def.radius+1.1f;
                float initial=Mathf.Atan2(worker.position.y-point.y,worker.position.x-point.x);
                for(int n=0;n<8;n++)
                {
                    float angle=initial+n*Mathf.PI/4;Vector2 approach=point+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*reach;
                    if(!world.Navigation.IsWalkable(approach,worker.def.radius)||!Safe(world,player,approach,visible)||
                        world.BuildingFootprintDistance(definition,point,approach)>worker.def.radius+1.5f)continue;
                    bool Clear(Vector2 p)=>world.BuildingFootprintDistance(definition,point,p)>worker.def.radius+.15f&&Safe(world,player,p,visible);
                    bool Segment(Vector2 a,Vector2 b)
                    {int count=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)*2));for(int i=0;i<=count;i++)if(!Clear(Vector2.Lerp(a,b,(float)i/count)))return false;return true;}
                    if(world.Navigation.ClearLine(worker.position,approach,worker.def.radius)&&Segment(worker.position,approach))
                    {selected=worker;return true;}
                    var path=world.Navigation.FindPath(worker.position,approach,worker.def.radius,Clear,Segment);
                    if(path.Count>=2&&(path[path.Count-1]-approach).sqrMagnitude<.01f){selected=worker;return true;}
                }
            }
            return false;
        }
    }
}
