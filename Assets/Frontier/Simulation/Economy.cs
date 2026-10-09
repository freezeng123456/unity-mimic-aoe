using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public partial class World
    {
        private readonly Dictionary<int,float> gatherLog=new Dictionary<int,float>();
        private readonly Dictionary<int,int> gatherLogTarget=new Dictionary<int,int>();
        private readonly Dictionary<int,string> incomeKinds=new Dictionary<int,string>();
        private readonly Dictionary<int,float> builders=new Dictionary<int,float>();
        private readonly List<int> buildWorkers=new List<int>();
        private PlayerState P(int id)=>id>=0&&id<Players.Length?Players[id]:null;
        private static Cost Sub(Cost a,Cost b)=>new Cost(a.food-b.food,a.wood-b.wood,a.coin-b.coin);
        private static Cost ResourceCost(ResourceKind k,float amount)=>k==ResourceKind.Food?new Cost(amount,0,0):k==ResourceKind.Wood?new Cost(0,amount,0):new Cost(0,0,amount);
        public void InitializeEconomy()
        {
            foreach(var p in Players)
            {
                p.resources=HasScenario?scenarioDocument.starts[p.id].initialResources:Rules.Civ(p.civilization).startingResources;p.populationCap=0;
                if(HasScenario)p.age=scenarioDocument.starts[p.id].age;
                p.deck.AddRange(DeckLibrary.Preset(Rules,p.civilization,"economy"));
                p.ageHistory.Add(HasScenario?"Age "+p.age+" @ 0s (authored scenario)":"I @ 0s");Emit("StartingResources",p.id,0,(HasScenario?"Authored scenario opening: ":"Configured opening: ")+p.resources);
            }
        }
        public bool CanAfford(int player,Cost cost)
        {
            var p=P(player);return p!=null&&IsPlayerActive(player)&&cost.food>=0&&cost.wood>=0&&cost.coin>=0&&p.resources.food+.001f>=cost.food&&p.resources.wood+.001f>=cost.wood&&p.resources.coin+.001f>=cost.coin;
        }
        public Cost UnitCost(int player,EntityDef d)=>EntityCost(player,d);
        public string ValidateDeck(int player,IList<string> deck)
        {
            return DeckLibrary.Validate(Rules,Players[player].civilization,deck);
        }
        public bool CanConstruct(EntityState actor,string buildingId)
        {
            var building=Rules.Entity(buildingId);
            return actor!=null&&actor.Alive&&actor.Complete&&actor.garrisonedIn==0&&actor.def.kind==EntityKind.Unit&&!actor.def.waterborne&&
                P(actor.owner)!=null&&IsPlayerActive(actor.owner)&&EntityAvailable(actor.owner,actor.def)&&building!=null&&
                building.kind==EntityKind.Building&&building.id!="factory"&&EntityAvailable(actor.owner,building)&&
                (actor.Worker||Array.IndexOf(actor.def.constructs??Array.Empty<string>(),buildingId)>=0);
        }
        public string CanBuild(int player,string id,Vector2 point)
        {
            var p=P(player);var d=Rules.Entity(id);
            if(p==null||d==null||d.kind!=EntityKind.Building)return "Unknown building";
            if(!EntityAvailable(player,d))return "Unavailable to this civilization";
            if(p.age<UnitAge(player,d))return "Requires age "+UnitAge(player,d);
            var industry=IndustrialBuildingReason(player,id);if(industry!="")return industry;
            if(!CanAfford(player,EntityCost(player,d)))return "Need "+EntityCost(player,d);
            if(id=="trade_post"&&(SocketAt(point)==null||TradingPostAt(SocketAt(point).id)!=null))return "Place on an empty trade route site";
            if(Fog.At(player,point)!=VisibilityState.Visible)return "Build inside current vision";
            if(id=="dock")
            {
                var shore=DockPlacementReason(point);if(shore!="")return shore;
                var footprint=DockFootprintReason(point);if(footprint!="")return footprint;
            }
            else
            {
                if(BridgeFootprintOverlaps(point,d.width,d.depth))return "Keep bridge decks and approaches clear";
                if(!Navigation.IsAreaFree(point,d.width+1,d.depth+1))return "Blocked footprint or map boundary";
            }
            if(HasScenario&&(id=="dock"?Layout.FoundationHeightRange(point,d.width,d.depth,Layout.WaterDirection(point)):Layout.FoundationHeightRange(point,d.width,d.depth))>(id=="dock"?.5f:.25f))return "Building requires a level foundation";
            var footprintCells=new List<Vector2>(BuildingFootprintCells(d,point));int banks=0;
            foreach(var e in Entities)
            {
                if(!e.Alive||e.garrisonedIn!=0)continue;
                if(e.def.id=="trade_socket"&&id=="trade_post"&&(e.position-point).sqrMagnitude<.01f)continue;
                if(e.owner==player&&e.def.id=="bank")banks++;
                if(e.def.kind==EntityKind.Building)
                {if(BuildingFootprintsOverlap(d,point,e.def,e.position,1))return "Too close to another building";}
                else
                {
                    if(BuildingFootprintContains(d,point,e.position,e.def.radius))return "Footprint occupied by units or resources";
                    // A coarse navigation cell can project slightly beyond a
                    // rotated wall. A foundation must not imprison an actor
                    // that clears the mesh but overlaps its occupied cell.
                    foreach(var cell in footprintCells)
                    {
                        Vector2 distance=new Vector2(Mathf.Max(0,Mathf.Abs(e.position.x-cell.x)-.5f),Mathf.Max(0,Mathf.Abs(e.position.y-cell.y)-.5f));
                        if(distance.sqrMagnitude<e.def.radius*e.def.radius+.00001f)return "Footprint occupied by units or resources";
                    }
                }
            }
            if(id=="bank"&&banks>=CountingHouseLimit(player))return "Counting house limit reached";
            return "";
        }
        public string ProductionReason(int player,EntityState b,string item,bool research=false,int count=1)
        {
            if(P(player)==null||!IsPlayerActive(player)||b==null||!b.Alive||b.owner!=player||!b.Complete||b.garrisonedIn!=0)return "Select your completed, available producer";
            // The sole mobile producer is the Dutch Pathfinder training an
            // Envoy. A unit must never become an arbitrary research facility.
            bool mobileProducer=b.def.kind==EntityKind.Unit&&b.def.id=="explorer"&&!research&&item=="envoy";
            if(b.def.kind!=EntityKind.Building&&!mobileProducer)return "Select a completed production building";
            var p=P(player);
            if(!research&&b.def.id=="factory")return "Use factory artillery mode";
            if(b.queue.Count>=6)return "Production queue full";
            if(research)
            {
                var t=Rules.Tech(item);if(t==null)return "Unknown technology";
                if(t.building!=b.def.id)return "Requires "+t.building;
                if(t.requiresRevolution&&p.politicalPhase!=PoliticalPhase.Revolutionary)return "Requires completed revolution; civilian rebuilding is once-only";
                if(p.age<t.age)return "Requires age "+t.age;
                if(!string.IsNullOrEmpty(t.civilization)&&t.civilization!=p.civilization)return "Civilization requirement";
                if(t.units!=null&&t.units.Length>0&&!Array.Exists(t.units,id=>EntityAvailable(player,Rules.Entity(id))))return "No available units use this technology";
                if(p.technologies.Contains(item)||TechnologyQueued(player,item))return "Already completed or queued";
                if(!string.IsNullOrEmpty(t.requires)&&!p.technologies.Contains(t.requires))return "Requires "+t.requires;
                if(!CanAfford(player,t.cost))return "Need "+t.cost;
            }
            else
            {
                var d=Rules.Entity(item);if(d==null||d.kind!=EntityKind.Unit)return "Unknown unit";
                if(d.producer!=b.def.id)return "Requires "+d.producer;
                if(p.age<UnitAge(player,d))return "Requires age "+UnitAge(player,d);
                if(!EntityAvailable(player,d))return "Civilization requirement";
                if((d.tags&Tags.Villager)!=0&&WorkerSourcesClosed(player))return "Settler training closed during revolution; rebuild civilian government";
                if(d.waterborne&&!Layout.HasWater)return "River vessels require a water map";
                if((d.tags&Tags.Villager)!=0&&AutoRecruitSeconds(player)>0)return "Settlers are recruited automatically by town centers";
                count=Mathf.Clamp(count,1,d.batch);
                if(UnitLimitReason(player,d,count)!="")return UnitLimitReason(player,d,count);
                if(!CanAfford(player,UnitCost(player,d)*count))return "Need "+(UnitCost(player,d)*count);
                if(p.usedPopulation+p.reservedPopulation+d.population*count>Mathf.Min(p.populationCap,Rules.populationLimit))return "Need more housing";
            }
            return "";
        }
        private bool TechnologyQueued(int player,string id)
        {
            foreach(var e in Entities)if(e.Alive&&e.owner==player)foreach(var j in e.queue)if(j.kind=="Research"&&j.item==id)return true;
            foreach(var s in Players[player].shipments)if(Rules.Card(s.card)?.tech==id)return true;
            return false;
        }
        public CommandResult HandleEconomicCommand(GameCommand c)
        {
            var p=P(c.player);if(p==null)return CommandResult.Fail("Invalid player");
            if(TryHandleIndustrialCommand(c,out var industrialResult))return industrialResult;
            var b=c.actors!=null&&c.actors.Length>0?Get(c.actors[0]):null;
            if(c.kind==CommandKind.Revolution)return StartRevolution(c,b);
            if(c.kind==CommandKind.ToggleRecruitment)
            {
                if(b==null||!b.Alive||b.owner!=c.player||!b.Complete||b.def.id!="town"||AutoRecruitSeconds(c.player)<=0)return CommandResult.Fail("Select your completed automatic recruiting town center");
                b.autoRecruitPaused=!b.autoRecruitPaused;
                Emit("AutoRecruitmentToggled",c.player,b.id,b.autoRecruitPaused?"Paused":"Recruiting");
                return CommandResult.Ok(b.autoRecruitPaused?"Automatic settlers paused":"Automatic settlers resumed");
            }
            if(c.kind==CommandKind.Build)
            {
                c.point=BuildPoint(c.player,c.item,c.point);
                var reason=CanBuild(c.player,c.item,c.point);if(reason!="")return CommandResult.Fail(reason);
                var workers=new List<EntityState>();foreach(int id in c.actors??Array.Empty<int>()){var w=Get(id);if(w!=null&&w.owner==c.player&&CanConstruct(w,c.item)&&!workers.Contains(w))workers.Add(w);}
                if(workers.Count==0)return CommandResult.Fail("Select an eligible constructor");
                var d=Rules.Entity(c.item);var cost=EntityCost(c.player,d);Pay(c.player,cost,workers[0].id,"Foundation "+d.name);
                var placed=Spawn(c.item,c.player,c.point,false);placed.paidConstruction=cost;ApplyPrefab(placed);
                foreach(var w in workers){if(!c.append){w.orders.Clear();w.path.Clear();w.fireAt=-1;}w.orders.Enqueue(new UnitOrder(OrderKind.Build,c.point,placed.id));}
                return CommandResult.Ok("Construction started: "+d.name);
            }
            if(c.kind==CommandKind.Demolish)
            {
                if(b==null||!b.Alive||b.owner!=c.player||b.def.kind!=EntityKind.Building||!b.Complete)return CommandResult.Fail("Select your completed building");
                Remove(b);return CommandResult.Ok("Building demolished without refund");
            }
            if(c.kind==CommandKind.CancelBuilding)
            {
                if(b==null||!b.Alive||b.owner!=c.player||b.def.kind!=EntityKind.Building)return CommandResult.Fail("Select your building");
                if(b.Complete)return CommandResult.Fail("Only unfinished construction can be refunded");
                Refund(c.player,b.paidConstruction*Rules.constructionRefund,b.id,"Construction cancellation");Remove(b);return CommandResult.Ok("Construction cancelled; 75% refunded");
            }
            if(c.kind==CommandKind.CancelQueue)
            {
                if(b==null||!b.Alive||b.owner!=c.player||c.index<0||c.index>=b.queue.Count)return CommandResult.Fail("No such queue entry");
                var j=b.queue[c.index];b.queue.RemoveAt(c.index);p.reservedPopulation=Mathf.Max(0,p.reservedPopulation-j.population);Refund(c.player,j.paid,b.id,"Cancelled "+j.item);
                if(j.kind=="Revolution")Emit("RevolutionCancelled",c.player,b.id,j.item);
                return CommandResult.Ok("Unfinished order refunded");
            }
            if(c.kind==CommandKind.Train||c.kind==CommandKind.Research)
            {
                bool research=c.kind==CommandKind.Research;string reason=ProductionReason(c.player,b,c.item,research,c.count);if(reason!="")return CommandResult.Fail(reason);
                if(research)
                {var t=Rules.Tech(c.item);Pay(c.player,t.cost,b.id,t.name);b.queue.Add(new ProductionJob{item=t.id,kind="Research",paid=t.cost,duration=t.time});}
                else
                {
                    var d=Rules.Entity(c.item);int count=Mathf.Clamp(c.count,1,d.batch);Cost cost=UnitCost(c.player,d)*count;int pop=d.population*count;
                    Pay(c.player,cost,b.id,d.name+" ×"+count);p.reservedPopulation+=pop;b.queue.Add(new ProductionJob{item=d.id,kind="Train",paid=cost,duration=d.time,count=count,population=pop});
                    Emit("ProductionQueued",c.player,b.id,d.id+" ×"+count,count);
                }
                return CommandResult.Ok("Added to production queue");
            }
            if(c.kind==CommandKind.AdvanceAge)
            {
                string ageReason=AgeReason(c.player,b);if(ageReason!="")return CommandResult.Fail(ageReason);
                if(ResolveAgeChoice(c.player,p.age+1,c.choice)==null)return CommandResult.Fail("Choose a valid civilization age route");
                var a=Rules.Age(p.age+1);if(a==null)return CommandResult.Fail("Age undefined");if(!CanAfford(c.player,a.cost))return CommandResult.Fail("Need "+a.cost);
                Pay(c.player,a.cost,b.id,"Advance to age "+a.age);b.queue.Add(new ProductionJob{item=a.age.ToString(),kind="Age",paid=a.cost,duration=a.time,choice=c.choice});return CommandResult.Ok("Age advancement queued");
            }
            if(c.kind==CommandKind.Ship)
            {
                string reason=ShipmentReason(c.player,c.item);if(reason!="")return CommandResult.Fail(reason);
                var card=Rules.Card(c.item);
                p.shipmentTokens--;p.shipsSent++;p.sentCards.Add(card.id);p.shipments.Add(new ShipmentJob{card=card.id});Emit("ShipmentSent",c.player,0,card.name);return CommandResult.Ok("Shipment requested: "+card.name);
            }
            return CommandResult.Fail("Unsupported command");
        }
        public string ShipmentReason(int player,string id)
        {
            var p=P(player);if(p==null)return "Invalid player";
                var card=Rules.Card(id);if(card==null||!ActiveShipmentCards(player).Contains(id))return ("Card not in this active deck");
                if(ValidateDeck(player,p.deck)!="")return ("Invalid deck");
                var shippedUnit=Rules.Entity(card.unit);
                if(shippedUnit!=null&&(shippedUnit.tags&Tags.Villager)!=0&&!shippedUnit.waterborne&&WorkerSourcesClosed(player))return "New settler shipments closed during revolution; accepted shipments still arrive";
                if(card.age>p.age)return ("Card requires age "+card.age);
                if(!card.repeatable&&p.sentCards.Contains(card.id))return ("Already sent this match");
                if(p.shipmentTokens<=0)return ("Earn XP for a shipment opportunity");
                if(!string.IsNullOrEmpty(card.tech)&&(p.technologies.Contains(card.tech)||TechnologyQueued(player,card.tech)))return ("This technology is already active or pending");
            if(Rules.Entity(card.unit)?.waterborne==true)
            {if(!Layout.HasWater)return "Naval shipment needs water";if(FindNavalReceiver(player)==null)return "Build a completed harbor for this shipment";}
            return IndustrialShipmentReason(player,card);
        }
        public void TickEconomy(float dt)
        {
            builders.Clear();buildWorkers.Clear();int originalCount=Entities.Count;
            for(int i=0;i<originalCount;i++)
            {
                var e=Entities[i];if(!e.Alive||e.garrisonedIn!=0||e.owner>=0&&!IsPlayerActive(e.owner))continue;
                if((e.Worker||e.def.repairArtillery||e.def.fishingBoat||e.def.revolutionary||e.def.constructs!=null&&e.def.constructs.Length>0)&&e.orders.Count>0)
                {
                    var o=e.orders.Peek();var target=Get(o.target);
                    if(o.kind==OrderKind.Gather&&(e.Worker||e.def.fishingBoat||e.def.revolutionary))Gather(e,o,dt);
                    else if(o.kind==OrderKind.Manufacture&&e.Worker)
                    {
                        if(target==null||!target.Alive||!target.Complete){e.orders.Dequeue();e.status="Workshop lost";continue;}
                        if(EdgeDistance(e,target)<=1.5f){e.status=target.queue.Count>0?"Manufacturing":"Workshop awaiting orders";if(dt>0&&target.queue.Count>0)e.work=new WorkActivity{kind=WorkKind.Manufacture,target=target.id,time=Time};}
                    }
                    else if(o.kind==OrderKind.Build&&(e.Worker||e.def.constructs!=null&&e.def.constructs.Length>0)||o.kind==OrderKind.Repair&&(e.Worker||e.def.repairArtillery))
                    {
                        if(target==null||!target.Alive||target.garrisonedIn!=0||target.owner!=e.owner){e.orders.Dequeue();e.status="Target lost";continue;}
                        if(EdgeDistance(e,target)>1.5f)continue;
                        if(o.kind==OrderKind.Build)
                        {if(target.Complete||!CanConstruct(e,target.def.id)){e.orders.Dequeue();e.status="Idle";continue;}builders.TryGetValue(target.id,out float n);builders[target.id]=n+1;e.status="Constructing";buildWorkers.Add(e.id);if(dt>0)e.work=new WorkActivity{kind=WorkKind.Build,target=target.id,time=Time};}
                        else Repair(e,target,dt);
                    }
                }
                TickAutoRecruitment(e,dt);
                if(e.Complete&&e.queue.Count>0)TickProduction(e,dt);
                if(e.Complete&&e.def.id=="bank")
                {float amount=CountingHouseRate(e.owner)*dt;Credit(e.owner,ResourceKind.Coin,amount,e.id,e.id,"BankIncome");}
            }
            foreach(var pair in builders)
            {
                var b=Get(pair.Key);if(b==null||!b.Alive||b.Complete)continue;
                float speed=(1+.5f*(Mathf.Min(8,pair.Value)-1))*CivilizationFactor(b.owner,t=>t.buildSpeed),delta=dt*speed/Mathf.Max(1,b.def.time);
                b.construction=Mathf.Min(1,b.construction+delta);b.hp=Mathf.Min(MaxHp(b),b.hp+delta*MaxHp(b)*.95f);
                if(b.Complete)
                {
                    RecomputePopulationCap(b.owner);AwardXp(b.owner,b.def.buildXp,b.id,"Building completed");Emit("BuildingComplete",b.owner,b.id,b.def.name+" ready");
                    if(b.def.id=="house"&&CivilianWorkersEnabled(b.owner)&&Rules.Civ(Players[b.owner].civilization).houseVillager)b.queue.Add(new ProductionJob{kind="Gift",item=WorkerDefinition(b.owner).id,count=1,duration=0});
                }
            }
            TickIndustrialEconomy(dt);
            foreach(var p in Players)if(IsPlayerActive(p.id)&&p.shipments.Count>0)
                TickPoliticalShipments(p,dt);
            foreach(var p in Players)if(IsPlayerActive(p.id)&&p.rewards.Count>0)TickPoliticalRewards(p);
        }
        private void Gather(EntityState worker,UnitOrder order,float dt)
        {
            var r=Get(order.target);
            if(r==null||!r.Alive||(r.remaining<=0&&!Sustainable(r)))
            {
                FlushGather(worker.id);worker.orders.Dequeue();worker.status="Resource depleted";
                EntityState next=null;float best=18*18;
                foreach(var candidate in Entities)if(candidate.def.resource==(r?.def.resource??ResourceKind.None)&&GatherableResource(worker.owner,candidate)&&CanGather(worker,candidate))
                {float dist=(candidate.position-worker.position).sqrMagnitude;if(dist<best){best=dist;next=candidate;}}
                if(worker.orders.Count==0&&next!=null)worker.orders.Enqueue(new UnitOrder(OrderKind.Gather,next.position,next.id));return;
            }
            if(!GatheringDomainAllowed(worker,r)){FlushGather(worker.id);worker.orders.Dequeue();worker.status="Resource unavailable";return;}
            if(!r.Complete){worker.status="Waiting for completion";return;}
            if(EdgeDistance(worker,r)>1.5f)return;
            int atPoint=0;foreach(var e in Nearby(r.position,3.2f))if((e.Worker||e.def.fishingBoat||e.def.revolutionary)&&e.orders.Count>0&&e.orders.Peek().kind==OrderKind.Gather&&e.orders.Peek().target==r.id&&e.id<worker.id)atPoint++;
            if(atPoint>=8){worker.status="Waiting for a work position";return;}
            float amount=GatherRate(worker,r)*dt;if(!Sustainable(r))amount=Mathf.Min(r.remaining,amount);
            if(amount<=0)return;
            if(!Sustainable(r))r.remaining=Mathf.Max(0,r.remaining-amount);
            Credit(worker.owner,r.def.resource,amount,worker.id,r.id,"Gather");AwardXp(worker.owner,amount*.1f,worker.id,"Gathering");worker.status="Gathering "+r.def.resource;
            worker.work=new WorkActivity{kind=WorkKind.Gather,target=r.id,resource=r.def.resource,time=Time};
            if(!Sustainable(r)&&r.remaining<=.0001f){FlushGather(worker.id);Emit("ResourceDepleted",worker.owner,r.id,r.def.name+" exhausted");Remove(r);}
        }
        private void Credit(int player,ResourceKind resource,float amount,int source,int target,string kind)
        {
            var p=P(player);Cost delta=ResourceCost(resource,amount);p.resources+=delta;if(kind=="Gather")p.gathered+=delta;else p.passiveIncome+=delta;
            incomeKinds[source]=kind;
            if(gatherLogTarget.TryGetValue(source,out int previous)&&previous!=target)FlushGather(source);
            gatherLogTarget[source]=target;gatherLog.TryGetValue(source,out float current);gatherLog[source]=current+amount;
            var e=Get(source);if(e!=null&&Time>=e.nextEconomyLog){e.nextEconomyLog=Time+1;FlushGather(source);}
        }
        private void FlushGather(int source)
        {
            if(!gatherLog.TryGetValue(source,out float amount)||amount<=0)return;
            var e=Get(source);gatherLogTarget.TryGetValue(source,out int target);
            Emit(incomeKinds.TryGetValue(source,out string incomeKind)?incomeKind:"Gather",e?.owner??-1,source,incomeKind=="BankIncome"?"Coin":incomeKind=="FactoryIncome"?e?.factoryMode??"Unknown":Get(target)?.def.resource.ToString()??"Coin",amount,target);gatherLog[source]=0;
        }
        public void FlushEconomyLog()
        {
            // A cold checkpoint reconstructs the income buckets by source ID.
            // Explicit report flushes use the same order regardless of when a
            // worker first started gathering in the live session.
            var sources=new List<int>(gatherLog.Keys);sources.Sort();foreach(int id in sources)FlushGather(id);
        }
        private void Repair(EntityState worker,EntityState target,float dt)
        {
            if(!target.Complete||target.hp>=MaxHp(target)-.01f){worker.orders.Dequeue();worker.status="Idle";return;}
            float heal=Mathf.Min(18*dt,MaxHp(target)-target.hp);var repairValue=target.def.repairCost;
            if(repairValue.food<=0&&repairValue.wood<=0&&repairValue.coin<=0)repairValue=EntityCost(worker.owner,target.def);
            Cost cost=repairValue*(heal/MaxHp(target)*.5f);
            if(!CanAfford(worker.owner,cost)){worker.status="Repair needs resources";return;}
            float hpBefore=target.hp;
            var payment=Pay(worker.owner,cost,worker.id,"Repair "+target.id,target.id);
            target.hp+=heal;worker.status="Repairing";
            if(heal>0)worker.work=new WorkActivity{kind=WorkKind.Repair,target=target.id,time=Time};
            payment.economy.hasRepair=true;payment.economy.healthTarget=target.id;
            payment.economy.hpBefore=hpBefore;payment.economy.hpAfter=target.hp;
        }
        private void TickProduction(EntityState b,float dt)
        {
            var j=b.queue[0];
            if(b.def.id=="workshop"&&(j.kind=="Train"||j.kind=="Manufacture"))
            {
                int artisans=AssignedArtisans(b);if(artisans==0){b.status="Assign settlers to manufacture";return;}
                dt*=(.5f+.5f*artisans)*CivilizationFactor(b.owner,t=>t.manufactureSpeed);
            }
            if(j.kind=="Train")
            {
                dt*=CivilizationFactor(b.owner,t=>t.trainSpeed);
                var trained=Rules.Entity(j.item);
                if(trained!=null&&trained.healer)dt*=HealerTrainingSpeed076(b.owner,trained);
            }
            j.elapsed=Mathf.Min(j.duration,j.elapsed+dt);if(j.elapsed<j.duration)return;
            if(j.kind=="Manufacture"){Players[b.owner].prefabCharges+=j.count;b.queue.RemoveAt(0);Emit("ManufactureComplete",b.owner,b.id,j.item,j.count);b.status="Prefabrication kit ready";return;}
            if(j.kind=="Research"){ApplyTechnology(b.owner,j.item);if(j.item==CivilianRebuildingTechnology)CompleteCivilianRebuilding(b.owner,b.id);b.queue.RemoveAt(0);Emit("ResearchComplete",b.owner,b.id,Rules.Tech(j.item).name);return;}
            if(j.kind=="Revolution"){CompleteRevolution(b,j);return;}
            if(j.kind=="Age")
            {
                int age=int.Parse(j.item);var a=Rules.Age(age);var p=P(b.owner);p.age=age;p.ageHistory.Add(age+" @ "+Time.ToString("0.0")+"s");b.queue.RemoveAt(0);AwardXp(p.id,100,b.id,"Age advancement");Emit("AgeAdvanced",p.id,b.id,"Reached age "+age);
                var choice=ResolveAgeChoice(p.id,age,j.choice);
                if(choice!=null)
                {
                    if(!string.IsNullOrEmpty(choice.tech))ApplyTechnology(p.id,choice.tech);
                    if(choice.crates.food+choice.crates.wood+choice.crates.coin>0)p.rewards.Add(new RewardJob{crates=choice.crates});
                    if(!string.IsNullOrEmpty(choice.unit)&&EntityAvailable(p.id,Rules.Entity(choice.unit)))p.rewards.Add(new RewardJob{unit=choice.unit,count=choice.count});
                    Emit("AgeRouteChosen",p.id,b.id,choice.id);
                }return;
            }
            var d=RevolutionOutputDefinition(b.owner,Rules.Entity(j.item));if(d==null){b.queue.RemoveAt(0);return;}
            var owner=P(b.owner);
            if(j.kind=="Gift"&&UnitLimitReason(b.owner,d,j.count)!=""){b.status=UnitLimitReason(b.owner,d,j.count);return;}
            if(j.kind=="Gift"&&owner.usedPopulation+owner.reservedPopulation+d.population*j.count>Mathf.Min(owner.populationCap,Rules.populationLimit)){b.status="Reward waiting for housing";return;}
            if(!FindSpawnPositions(b,j.count,d,out var points)){b.status="Production exit blocked";return;}
            b.queue.RemoveAt(0);owner.reservedPopulation=Mathf.Max(0,owner.reservedPopulation-j.population);
            var produced=new List<EntityState>();
            foreach(var point in points)
            {
                var unit=Spawn(d.id,b.owner,point);produced.Add(unit);
                if(j.kind=="Train"){owner.trained++;AwardXp(b.owner,d.trainXp,b.id,"Unit trained");Emit("UnitTrained",b.owner,b.id,d.id,1,unit.id);}
            }
            AssignProducerRally(b,produced);
            b.status="Ready";
        }
        public bool FindSpawnPositions(EntityState receiver,int count,float radius,out List<Vector2> positions)
        {
            positions=new List<Vector2>();
            for(int ring=0;ring<5&&positions.Count<count;ring++)for(int k=0;k<24&&positions.Count<count;k++)
            {
                float angle=k*Mathf.PI*2/24;Vector2 point=receiver.position+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*(Mathf.Max(receiver.def.width,receiver.def.depth)*.5f+2+ring*1.5f);
                if(!Navigation.IsWalkable(point,radius))continue;bool blocked=false;
                // A moving land producer cannot eject its unit across water,
                // a wall or an enclosed obstacle just because an outer ring is free.
                if(receiver.id>0&&receiver.def.kind==EntityKind.Unit&&!Navigation.ClearLine(receiver.position,point,radius))continue;
                foreach(var e in Entities)if(e.Alive&&e.garrisonedIn==0&&e.def.kind!=EntityKind.Building&&(e.position-point).sqrMagnitude<Mathf.Pow(radius+e.def.radius+.15f,2)){blocked=true;break;}
                foreach(var p in positions)if((p-point).sqrMagnitude<Mathf.Pow(radius*2+.3f,2))blocked=true;
                if(!blocked)positions.Add(point);
            }
            return positions.Count==count;
        }
        private EntityState FindReceiver(int player)
        {
            EntityState receiver=null;foreach(var e in Entities)if(e.Alive&&e.owner==player&&e.Complete&&(e.def.id=="town"||e.def.id=="tower")){receiver=e;if(e.def.id=="town")break;}return receiver;
        }
        private EntityState FindNavalReceiver(int player)
        {foreach(var e in Entities)if(e.Alive&&e.Complete&&e.owner==player&&e.def.id=="dock")return e;return null;}
        private void DeliverReward(PlayerState p,RewardJob reward=null)
        {
            reward??=p.rewards[0];var d=RevolutionOutputDefinition(p.id,Rules.Entity(reward.unit));
            var receiver=d?.waterborne==true?FindNavalReceiver(p.id):FindReceiver(p.id);
            if(receiver==null){reward.waiting=d?.waterborne==true?"Naval reward needs a completed harbor":"Age reward needs a town center or watchtower";return;}
            if(d!=null)
            {
                if(UnitLimitReason(p.id,d,reward.count)!=""){reward.waiting=UnitLimitReason(p.id,d,reward.count);return;}
                if(p.usedPopulation+p.reservedPopulation+d.population*reward.count>Mathf.Min(p.populationCap,Rules.populationLimit)){reward.waiting="Age reward needs housing";return;}
                if(d.waterborne){receiver=FindNavalReceiver(p.id);if(receiver==null){reward.waiting="Naval reward needs a completed harbor";return;}}
                if(!FindSpawnPositions(receiver,reward.count,d,out var positions)){reward.waiting="Age reward arrival blocked";return;}
                foreach(var point in positions)Spawn(d.id,p.id,point);
            }
            else if(!SpawnCrates(p.id,receiver.position,reward.crates)){reward.waiting="Age reward crate landing blocked";return;}
            p.rewards.Remove(reward);Emit("AgeRewardArrived",p.id,receiver.id,"Governor reward arrived");
        }
        private void Deliver(PlayerState p,ShipmentJob shipment,CardDef card)
        {
            var industry=IndustrialShipmentReason(p.id,card,true);if(industry!=""){shipment.waiting=industry;return;}
            var d=string.IsNullOrEmpty(card.unit)?null:RevolutionOutputDefinition(p.id,Rules.Entity(card.unit));
            EntityState receiver=d?.waterborne==true?FindNavalReceiver(p.id):FindReceiver(p.id);
            if(receiver==null){shipment.waiting=d?.waterborne==true?"Naval shipment needs a completed harbor":"Need a town center or watchtower";return;}
            if(d!=null)
            {
                if(UnitLimitReason(p.id,d,card.count)!=""){shipment.waiting=UnitLimitReason(p.id,d,card.count);return;}
                if(p.usedPopulation+p.reservedPopulation+d.population*card.count>Mathf.Min(p.populationCap,Rules.populationLimit)){shipment.waiting="Waiting for population capacity";return;}
                if(d.waterborne){receiver=FindNavalReceiver(p.id);if(receiver==null){shipment.waiting="Naval shipment needs a completed harbor";return;}}
                if(!FindSpawnPositions(receiver,card.count,d,out var positions)){shipment.waiting="Arrival area blocked";return;}
                foreach(var point in positions)Spawn(d.id,p.id,point);
            }
            else if(!SpawnCrates(p.id,receiver.position,card.crates)){shipment.waiting="Crate landing area blocked";return;}
            if(!string.IsNullOrEmpty(card.tech))ApplyTechnology(p.id,card.tech);
            p.shipments.Remove(shipment);Emit("ShipmentArrived",p.id,receiver.id,card.name+" arrived");
        }
        private bool SpawnCrates(int player,Vector2 point,Cost crates)
        {
            var definitions=new List<string>();var amounts=new List<float>();
            if(crates.food>0){definitions.Add("crate_food");amounts.Add(crates.food);}if(crates.wood>0){definitions.Add("crate_wood");amounts.Add(crates.wood);}if(crates.coin>0){definitions.Add("crate_coin");amounts.Add(crates.coin);}
            if(definitions.Count==0)return true;
            var receiver=new EntityState{position=point,def=new EntityDef{width=5,depth=5}};
            if(!FindSpawnPositions(receiver,definitions.Count,.5f,out var positions))return false;
            for(int i=0;i<definitions.Count;i++){var crate=Spawn(definitions[i],player,positions[i]);crate.remaining=amounts[i];}return true;
        }
        public void AwardXp(int player,float amount,int entity,string reason)
        {
            var p=P(player);if(p==null||amount<=0)return;p.xp+=amount;
            if(reason!="Gathering")Emit("Experience",player,entity,reason,amount);
            while(p.xp+1e-4f>=Rules.initialXp+Rules.xpIncrease*p.xpLevels)
            {p.xp-=Rules.initialXp+Rules.xpIncrease*p.xpLevels;p.xpLevels++;p.shipmentTokens++;Emit("ShipmentEarned",player,entity,"Shipment opportunity earned");}
        }
        private void RecomputePopulationCap(int player)
        {int cap=0;foreach(var e in Entities)if(e.Alive&&e.owner==player&&e.Complete)cap+=e.def.capacity;Players[player].populationCap=Mathf.Min(Rules.populationLimit,cap);}
        public void OnEntitySpawned(EntityState e)
        {var p=P(e.owner);if(p==null)return;if(e.def.kind==EntityKind.Unit)p.usedPopulation+=e.def.population;if(e.Complete)RecomputePopulationCap(e.owner);}
        public void OnEntityRemoved(EntityState e)
        {
            CancelCaravan(e,"Caravan destroyed");FlushGather(e.id);var p=P(e.owner);if(p==null)return;
            if(e.def.kind==EntityKind.Unit)p.usedPopulation=Mathf.Max(0,p.usedPopulation-e.def.population);
            foreach(var j in e.queue)
            {p.reservedPopulation=Mathf.Max(0,p.reservedPopulation-j.population);if(j.kind=="Revolution")Emit("RevolutionLost",e.owner,e.id,j.item+"; paid commitment lost with the town center");}
            e.queue.Clear();RecomputePopulationCap(e.owner);
        }
        public void ApplyTechnology(int player,string id)
        {
            var p=P(player);if(p.technologies.Contains(id)||Rules.Tech(id)==null)return;
            var ratios=new Dictionary<int,float>();foreach(var e in Entities)if(e.Alive&&e.owner==player)ratios[e.id]=e.hp/MaxHp(e);
            p.technologies.Add(id);foreach(var e in Entities)if(ratios.TryGetValue(e.id,out float ratio))e.hp=MaxHp(e)*ratio;
            Emit("Technology",player,0,id+" applied once to matching current and future entities");
        }
        private bool Applies(TechDef t,EntityState e)=>(t.units==null||t.units.Length==0||Array.IndexOf(t.units,e.def.id)>=0)&&(t.tags==Tags.None||(t.tags&e.def.tags)!=0);
        public float MaxHp(EntityState e)
        {float factor=1;var p=P(e.owner);if(p!=null)foreach(var id in p.technologies){var t=Rules.Tech(id);if(t!=null&&Applies(t,e))factor+=t.health;}return e.def.hp*factor;}
        public int GarrisonFirepowerCount(EntityState building)
        {
            int count=0;
            for(int i=0;i<building.occupants.Count&&count<building.def.garrisonAttackLimit;i++)
            {
                int id=building.occupants[i];var unit=Get(id);
                if(unit!=null&&unit.Alive&&unit.owner==building.owner&&unit.garrisonedIn==building.id&&unit.def.kind==EntityKind.Unit&&
                    (unit.def.tags&building.def.garrisonAttackTags)!=0&&building.occupants.IndexOf(id)==i)count++;
            }
            return count;
        }
        public float Attack(EntityState e)
        {float factor=1;var p=P(e.owner);if(p!=null)foreach(var id in p.technologies){var t=Rules.Tech(id);if(t!=null&&Applies(t,e))factor+=t.attack;}return (e.forceMelee&&e.def.meleeDamage>0?e.def.meleeDamage:e.def.damage)*factor*(e.def.garrisonAttackLimit>0?GarrisonFirepowerCount(e):1);}
        public float Speed(EntityState e)
        {float factor=1;var p=P(e.owner);if(p!=null)foreach(var id in p.technologies){var t=Rules.Tech(id);if(t!=null&&Applies(t,e))factor+=t.speed;}return e.def.speed*factor*(e.stealth&&e.def.canStealth?e.def.stealthSpeedMultiplier:1);}
        public float GatherRate(EntityState worker,EntityState resource)
        {float factor=1;foreach(var id in Players[worker.owner].technologies){var t=Rules.Tech(id);if(t!=null&&Applies(t,worker)&&(t.resource==ResourceKind.None||t.resource==resource.def.resource))factor+=t.gather;}float mine=resource.def.id=="mine"?Rules.Civ(Players[worker.owner].civilization).mineGatherMultiplier:1;return resource.def.gatherRate*factor*worker.def.gatherMultiplier*mine;}
    }
}
