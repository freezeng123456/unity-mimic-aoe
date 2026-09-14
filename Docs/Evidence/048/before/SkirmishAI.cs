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
        private readonly Vector2 home,enemyStart;
        private readonly Dictionary<int,Vector2> memory=new Dictionary<int,Vector2>();
        private float thinkAt,armyAt;
        public AiDifficultyProfile Difficulty { get; }
        private bool expedition;
        private int expeditionSize;
        private readonly AiWorkforce workforce;
        private readonly AiAgePlan agePlan;
        private readonly AiConstruction construction;
        private int rotation,buildSerial;
        public SkirmishAI(World world,int player):this(world,player,AiDifficultyProfile.From(world.Rules,"standard")){}
        public SkirmishAI(World world,int player,AiDifficultyProfile difficulty)
        {this.world=world;this.player=player;Difficulty=difficulty??throw new ArgumentNullException(nameof(difficulty));workforce=new AiWorkforce(world,player);agePlan=new AiAgePlan(world,player);construction=new AiConstruction(world,player);home=player==0?new Vector2(24,28):new Vector2(104,100);enemyStart=new Vector2(128-home.x,128-home.y);}
        public void Tick(float dt)
        {
            if(world.Paused||world.Winner>=0)return;thinkAt-=dt;if(thinkAt>0)return;thinkAt=Difficulty.DecisionSeconds;
            var own=new List<EntityState>();var workers=new List<EntityState>();var army=new List<EntityState>();var enemies=new List<EntityState>();
            foreach(var e in world.Entities)
            {
                if(!e.Alive)continue;
                if(e.owner==player){own.Add(e);if(e.Worker)workers.Add(e);else if(e.def.kind==EntityKind.Unit)army.Add(e);}
                else if(e.owner>=0&&e.def.kind!=EntityKind.Resource&&world.IsVisible(player,e)){enemies.Add(e);memory[e.id]=e.position;}
            }
            int soldierCount=army.FindAll(e=>(e.def.tags&Tags.Explorer)==0).Count;
            var p=world.Players[player];var town=Find(own,"town");bool advancing=false;
            foreach(var e in own)foreach(var j in e.queue)if(j.kind=="Age")advancing=true;
            construction.ResumeOne(own,workers,enemies);
            if(town==null&&workers.Count>0)Build(own,workers,"town");
            int workerGoal=Math.Min(world.Rules.Civ(p.civilization).villagerLimit,p.age==1?12:p.age==2?24:p.age==3?34:42);
            bool underAttack=enemies.Exists(e=>e.Military&&(e.def.tags&Tags.Explorer)==0&&(e.position-home).sqrMagnitude<34*34);
            bool rangedRaid=enemies.Exists(e=>(e.def.tags&Tags.Infantry)!=0&&e.def.range>=16&&(e.position-home).sqrMagnitude<40*40);
            bool investWorkers=workers.Count<workerGoal&&(!underAttack||soldierCount>=6||workers.Count<12);
            int raiders=enemies.FindAll(e=>e.Military&&(e.def.tags&Tags.Explorer)==0&&(e.position-home).sqrMagnitude<34*34).Count;
            agePlan.Tick(town,workers.Count,soldierCount,raiders,advancing);
            bool saveForAge=agePlan.Saving;
            if(town!=null&&town.Complete&&town.queue.Count==0)
            {
                var next=world.Rules.Age(p.age+1);
                if(p.age<world.MaxAge&&next!=null&&world.CanAfford(player,next.cost)&&saveForAge)
                    Send(new GameCommand{kind=CommandKind.AdvanceAge,actors=new[]{town.id},choice=underAttack?"military":"economic"});
                else if(investWorkers&&!saveForAge)Send(new GameCommand{kind=CommandKind.Train,actors=new[]{town.id},item="villager"});
            }
            Cost workerReserve=town!=null&&town.Complete&&town.queue.Count==0&&investWorkers&&!saveForAge?world.UnitCost(player,world.Rules.Entity("villager")):new Cost();
            bool firstBank=p.age>=2&&world.CountingHouseLimit(player)>0&&Find(own,"bank")==null&&!saveForAge&&!underAttack;
            Cost bankReserve=firstBank?world.EntityCost(player,world.Rules.Entity("bank")):new Cost();
            if(p.populationCap-p.usedPopulation-p.reservedPopulation<5&&!Unfinished(own,"house")&&p.populationCap<world.Rules.populationLimit)Build(own,workers,"house");
            if(p.age>=2)
            {
                if(Find(own,"barracks")==null)Build(own,workers,"barracks");
                else if(rangedRaid&&Find(own,"stable")==null)Build(own,workers,"stable");
                else if(firstBank){if(CanSpend(bankReserve,workerReserve))Build(own,workers,"bank");}
                else if(Find(own,"market")==null)Build(own,workers,"market");
                else if(Find(own,"stable")==null&&workers.Count>=16)Build(own,workers,"stable");
                else if(world.CountingHouseLimit(player)>0&&!saveForAge&&!underAttack&&own.FindAll(x=>x.def.id=="bank").Count<Math.Min(world.CountingHouseLimit(player),p.age))Build(own,workers,"bank");
                if(p.age>=3&&Find(own,"foundry")==null)Build(own,workers,"foundry");
            }
            foreach(var e in own)if(e.def.garrison>0&&e.occupants.Count>0&&enemies.Find(x=>(x.position-e.position).sqrMagnitude<24*24)==null)Send(new GameCommand{kind=CommandKind.Ungarrison,actors=new[]{e.id}});
            workforce.Assign(workers,EconomyTarget(own,workers,enemies,saveForAge,investWorkers,firstBank),enemies);
            EnsureSustainableEconomy(own,workers,p);
            if(p.age>=2&&!underAttack&&!saveForAge&&workers.Count>=16)InvestInRoute(own,workers);
            foreach(var post in own)if(post.def.id=="trade_post"&&post.Complete&&p.technologies.Contains("cargo_ledgers"))
            {var wanted=p.resources.food<500?ResourceKind.Food:p.resources.coin<500?ResourceKind.Coin:ResourceKind.Wood;if(post.tradeMode!=wanted)Send(new GameCommand{kind=CommandKind.TradeMode,actors=new[]{post.id},index=(int)wanted});}
            if(p.shipmentTokens>0)
            {
                CardDef chosen=null;float best=-10000;
                foreach(string id in p.deck)
                {
                    if(world.ShipmentReason(player,id)!="")continue;var card=world.Rules.Card(id);
                    float score=card.repeatable?0:8;score+=card.age*2;
                    if(card.unit=="cannon"&&p.age>=3)score+=40;
                    else if(card.unit=="villager"&&workers.Count<workerGoal)score+=20;
                    else if(!string.IsNullOrEmpty(card.unit)&&soldierCount<12)score+=15;
                    if(card.tech=="bank_charter"&&world.CountingHouseLimit(player)>0)score+=15;
                    if(saveForAge){var need=world.Rules.Age(p.age+1).cost;score+=Math.Min(card.crates.food,Math.Max(0,need.food-p.resources.food))*.04f+Math.Min(card.crates.coin,Math.Max(0,need.coin-p.resources.coin))*.04f;}
                    if(score>best){best=score;chosen=card;}
                }
                if(chosen!=null)Send(new GameCommand{kind=CommandKind.Ship,item=chosen.id});
            }
            // Artillery pays first; other queues cannot repeatedly spend its saving target.
            int cannonCount=world.UnitCountIncludingProduction(player,"cannon");
            Cost siegeReserve=p.age>=3&&Find(own,"foundry")!=null&&cannonCount<2?world.UnitCost(player,world.Rules.Entity("cannon")):new Cost();
            int Priority(EntityState e)=>rangedRaid&&e.def.id=="stable"?0:e.def.id=="foundry"?1:2;
            own.Sort((a,b)=>Priority(a).CompareTo(Priority(b)));
            foreach(var producer in own)
            {
                if(!producer.Complete||producer.queue.Count>0)continue;
                bool artillery=producer.def.id=="foundry";
                var choices=new List<EntityDef>();foreach(var d in world.Rules.entities)
                    if(d.kind==EntityKind.Unit&&d.producer==producer.def.id&&!d.id.Equals("villager")&&(string.IsNullOrEmpty(d.civilization)||d.civilization==p.civilization)&&d.age<=p.age)choices.Add(d);
                if(artillery)choices.Sort((a,b)=>(a.id=="cannon"?0:1).CompareTo(b.id=="cannon"?0:1));
                else choices.Sort((a,b)=>CounterScore(b,enemies).CompareTo(CounterScore(a,enemies)));
                if(p.age>=2&&(!saveForAge||underAttack))for(int i=0;i<choices.Count;i++)
                {
                    var d=choices[i];if(world.ProductionReason(player,producer,d.id)!="")continue;
                    int batch=Math.Min(d.batch,p.age>=3?2:1);bool queued=false;
                    for(int n=batch;n>=1;n--)
                    {
                        var cost=world.UnitCost(player,d)*n;
                        if(!CanSpend(cost,workerReserve+bankReserve+agePlan.Reserve+(artillery?new Cost():siegeReserve)))continue;
                        var result=Send(new GameCommand{kind=CommandKind.Train,actors=new[]{producer.id},item=d.id,count=n});
                        if(result.accepted){rotation++;queued=true;break;}
                    }
                    if(queued)break;
                }
                if(producer.queue.Count>0||saveForAge||underAttack&&soldierCount<6)continue;
                foreach(var t in world.Rules.techs)
                {
                    if(world.ProductionReason(player,producer,t.id,true)!="")continue;
                    // Unit upgrades need a meaningful existing force; economy can invest sooner.
                    if(t.units!=null&&t.units.Length>0&&world.UnitCountIncludingProduction(player,t.units[0])<4)continue;
                    if(!CanSpend(t.cost,workerReserve+bankReserve+siegeReserve))continue;
                    Send(new GameCommand{kind=CommandKind.Research,actors=new[]{producer.id},item=t.id});break;
                }
            }
            foreach(var unit in army)if((unit.def.tags&Tags.Explorer)!=0)Explore(unit);
            if(world.Time<armyAt)return;armyAt=world.Time+Difficulty.ArmySeconds;
            EntityState threat=enemies.Find(x=>(x.position-home).sqrMagnitude<30*30);
            var soldiers=army.FindAll(x=>(x.def.tags&Tags.Explorer)==0);
            Vector2 destination=enemyStart;
            if(threat!=null)destination=threat.position;
            else
            {
                // A remembered location is retained, never refreshed from a hidden entity.
                float nearest=float.MaxValue;int removeId=0;
                foreach(var pair in memory)
                {
                    bool observedEmpty=world.Fog.At(player,pair.Value)==VisibilityState.Visible&&enemies.Find(x=>x.id==pair.Key)==null;
                    if(observedEmpty){removeId=pair.Key;continue;}
                    float dist=(pair.Value-home).sqrMagnitude;if(dist<nearest){nearest=dist;destination=pair.Value;}
                }
                if(removeId!=0)memory.Remove(removeId);
                int muster=p.age<=2?6:p.age==3?10:14;
                Vector2 rally=home+new Vector2(player==0?10:-10,player==0?10:-10);
                int assembled=soldiers.FindAll(e=>(e.position-rally).sqrMagnitude<22*22).Count;
                // A failed attack must rebuild a force; elapsed match time never
                // turns one replacement into an automatic suicide expedition.
                if(expedition&&soldiers.Count<Math.Max(3,expeditionSize/2))expedition=false;
                if(!expedition&&assembled>=muster){expedition=true;expeditionSize=soldiers.Count;}
                if(!expedition)destination=rally;
            }
            foreach(var unit in soldiers)
            {
                if(unit.hp<world.MaxHp(unit)*.22f&&enemies.Count>soldiers.Count){if(unit.orders.Count==0||unit.orders.Peek().kind!=OrderKind.Move)Send(new GameCommand{kind=CommandKind.Order,actors=new[]{unit.id},order=OrderKind.Move,point=home});continue;}
                bool newOrder=unit.orders.Count==0||unit.orders.Peek().kind!=OrderKind.AttackMove||(unit.orders.Peek().point-destination).sqrMagnitude>18*18;
                if(newOrder)Send(new GameCommand{kind=CommandKind.Order,actors=new[]{unit.id},order=OrderKind.AttackMove,point=destination+new Vector2((unit.id%5-2)*1.3f,((unit.id/5)%5-2)*1.3f)});
            }
        }
        private void Explore(EntityState unit)
        {
            if(unit.orders.Count>0&&unit.orders.Peek().kind!=OrderKind.Move)return;
            TreasureState chosen=null;float nearest=float.MaxValue;
            foreach(var state in world.Treasures)
            {
                var treasure=world.Get(state.entity);if(!world.IsVisible(player,treasure))continue;
                bool guarded=state.guards.Exists(id=>world.IsVisible(player,world.Get(id)));
                if(guarded&&unit.hp<world.MaxHp(unit)*.35f)continue;
                float d=(treasure.position-unit.position).sqrMagnitude;if(d<nearest){nearest=d;chosen=state;}
            }
            if(chosen!=null)
            {
                var treasure=world.Get(chosen.entity);var guard=chosen.guards.ConvertAll(world.Get).Find(e=>world.IsVisible(player,e));
                if(guard!=null){Send(new GameCommand{kind=CommandKind.Order,actors=new[]{unit.id},order=OrderKind.Attack,target=guard.id});return;}
                if(world.TreasureReason(player,treasure)==""){Send(new GameCommand{kind=CommandKind.Order,actors=new[]{unit.id},order=OrderKind.Claim,target=treasure.id});return;}
            }
            if(unit.orders.Count==0){Vector2 point=(unit.position-enemyStart).sqrMagnitude>200?enemyStart:new Vector2(64,64);Send(new GameCommand{kind=CommandKind.Order,actors=new[]{unit.id},order=OrderKind.Move,point=point});}
        }
        private void InvestInRoute(List<EntityState> own,List<EntityState> workers)
        {
            if(Find(own,"trade_post")!=null||world.Rules.landmarks==null)return;
            foreach(var site in world.TradeSockets)
            {
                var socket=world.Get(site.entity);if(!world.IsVisible(player,socket)||world.CanBuild(player,"trade_post",socket.position)!="")continue;
                EntityState builder=null;float nearest=float.MaxValue;
                foreach(var worker in workers){if(worker.garrisonedIn!=0||worker.orders.Count>0&&worker.orders.Peek().kind==OrderKind.Build)continue;float d=(worker.position-socket.position).sqrMagnitude;if(d<nearest){nearest=d;builder=worker;}}
                if(builder!=null){Send(new GameCommand{kind=CommandKind.Build,actors=new[]{builder.id},item="trade_post",point=socket.position});return;}
            }
        }
        // Savings are inferred from actual next commands and rule costs, never awarded.
        private Cost EconomyTarget(List<EntityState> own,List<EntityState> workers,List<EntityState> visible,bool save,bool recruit,bool firstBank)
        {
            var p=world.Players[player];Cost target=new Cost();
            var settler=world.Rules.Entity("villager");
            if(recruit)target+=world.UnitCost(player,settler)*(60/settler.time);
            if((save||p.age==1)&&p.age<world.MaxAge)target+=world.Rules.Age(p.age+1).cost;
            int free=p.populationCap-p.usedPopulation-p.reservedPopulation;
            if(free<12&&p.populationCap<world.Rules.populationLimit)target+=world.EntityCost(player,world.Rules.Entity("house"));
            if(Find(own,"barracks")==null)target+=world.EntityCost(player,world.Rules.Entity("barracks"));
            else if(firstBank)target+=world.EntityCost(player,world.Rules.Entity("bank"));
            else if(p.age>=2&&Find(own,"market")==null)target+=world.EntityCost(player,world.Rules.Entity("market"));
            else if(p.age>=2&&workers.Count>=16&&Find(own,"stable")==null)target+=world.EntityCost(player,world.Rules.Entity("stable"));
            if(p.age>=3&&Find(own,"foundry")==null)target+=world.EntityCost(player,world.Rules.Entity("foundry"));
            if(!save)foreach(var producer in own)
            {
                if(producer.def.kind!=EntityKind.Building||producer.def.id=="town")continue;
                EntityDef candidate=null;float score=float.MinValue;
                foreach(var d in world.Rules.entities)
                {
                    if(d.kind!=EntityKind.Unit||d.producer!=producer.def.id||d.age>p.age||!string.IsNullOrEmpty(d.civilization)&&d.civilization!=p.civilization)continue;
                    float value=CounterScore(d,visible);if(value>score){score=value;candidate=d;}
                }
                if(candidate!=null)target+=world.UnitCost(player,candidate)*(60/candidate.time);
            }
            if(Find(own,"town")==null)target+=world.EntityCost(player,world.Rules.Entity("town"));
            // Keep a modest infrastructure reserve when the next army is already funded.
            if(p.age>=2)target.wood+=world.EntityCost(player,world.Rules.Entity("house")).wood;
            return target;
        }
        private void EnsureSustainableEconomy(List<EntityState> ownEconomy,List<EntityState> workers,PlayerState p)
        {
            bool foodAvailable=workforce.HasSafeSource(ResourceKind.Food);
            int farmCount=ownEconomy.FindAll(e=>e.def.id=="farm").Count;
            if(p.age>=2&&(!foodAvailable||p.age>=3&&farmCount<Math.Max(1,workers.Count/16)))Build(ownEconomy,workers,"farm");
            bool coinAvailable=workforce.HasSafeSource(ResourceKind.Coin);
            if(p.age>=3&&!coinAvailable)Build(ownEconomy,workers,"estate");
        }
        private bool Build(List<EntityState> own,List<EntityState> workers,string id)
        {
            if(Unfinished(own,id)||workers.Count==0)return false;
            for(int ring=0;ring<4;ring++)for(int n=0;n<20;n++)
            {
                float angle=(n+buildSerial)*Mathf.PI*2/20;Vector2 point=home+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*(9+ring*5);point=new Vector2(Mathf.Round(point.x),Mathf.Round(point.y));
                if(world.CanBuild(player,id,point)!="")continue;
                EntityState worker=null;float best=float.MaxValue;foreach(var w in workers){if(w.garrisonedIn!=0||w.orders.Count>0&&w.orders.Peek().kind==OrderKind.Build)continue;float dist=(w.position-point).sqrMagnitude;if(dist<best){best=dist;worker=w;}}
                if(worker==null)return false;var r=Send(new GameCommand{kind=CommandKind.Build,actors=new[]{worker.id},item=id,point=point});if(r.accepted){buildSerial+=3;return true;}
            }
            return false;
        }
        private static EntityState Find(List<EntityState> list,string id)=>list.Find(e=>e.Alive&&e.def.id==id);
        private static bool Unfinished(List<EntityState> list,string id)=>list.Exists(e=>e.Alive&&e.def.id==id&&!e.Complete);
        private bool CanSpend(Cost cost,Cost reserve)
        {
            if(!world.CanAfford(player,cost))return false;var r=world.Players[player].resources;
            // Reserve available amounts in each currency independently. Missing food
            // for a settler must not block a cannon that spends only wood and coin.
            return r.food-cost.food>=Math.Min(r.food,reserve.food)&&r.wood-cost.wood>=Math.Min(r.wood,reserve.wood)&&r.coin-cost.coin>=Math.Min(r.coin,reserve.coin);
        }
        private float CounterScore(EntityDef unit,List<EntityState> visible)
        {
            float score=unit.id=="musketeer"?35:unit.id=="hussar"?25:20;
            foreach(var enemy in visible)
            {
                if(!enemy.Military||(enemy.def.tags&Tags.Explorer)!=0)continue;
                if(unit.id=="hussar"&&(enemy.def.tags&Tags.LightInfantry)!=0)score+=18;
                foreach(var bonus in unit.bonuses)if((bonus.tags&enemy.def.tags)!=0)score+=8*(bonus.multiplier-1);
            }
            return score-world.UnitCountIncludingProduction(player,unit.id)*5;
        }
        private CommandResult Send(GameCommand c){c.player=player;return world.Issue(c);}
    }
}
