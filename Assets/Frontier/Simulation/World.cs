using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public partial class World
    {
        public readonly RuleCatalog Rules;
        public readonly MapLayout Layout;
        public readonly PlayerState[] Players;
        public readonly List<EntityState> Entities=new List<EntityState>();
        public readonly List<SimEvent> Events=new List<SimEvent>();
        public readonly List<ProjectileState> Projectiles=new List<ProjectileState>();
        public readonly List<Vector3> TerrainObstacles=new List<Vector3>();
        public readonly GridNavigation Navigation;
        public readonly Visibility Fog;
        public readonly int Seed,MaxAge;
        public int Winner=-1;
        public float Time;
        public bool Paused;
        public float LastNavigationMs,LastCombatMs,LastEconomyMs;
        private readonly Dictionary<int,EntityState> byId=new Dictionary<int,EntityState>();
        private readonly Dictionary<int,List<EntityState>> spatial=new Dictionary<int,List<EntityState>>();
        private int nextId=1;
        private float fogAt;
        public World(RuleCatalog rules,int seed=314159,string civ0="coast",string civ1="coast",int maxAge=3,string mapId="frontier")
            :this(rules,seed,new[]{civ0,civ1},new[]{0,1},maxAge,mapId){}
        public World(RuleCatalog rules,int seed,string[] civilizations,int[] teams,int maxAge=3,string mapId="frontier")
            :this(rules,seed,civilizations,teams,maxAge,mapId,null){}
        World(RuleCatalog rules,int seed,string[] civilizations,int[] teams,int maxAge,string mapId,ScenarioDocument scenario)
        {
            Rules=rules??throw new ArgumentNullException(nameof(rules));ValidateRoster(rules,civilizations,teams);
            Seed=seed;MaxAge=Mathf.Clamp(maxAge,1,rules.ages.Length);
            Players=new PlayerState[civilizations.Length];
            for(int i=0;i<Players.Length;i++)Players[i]=new PlayerState{id=i,team=teams[i],civilization=civilizations[i]};
            scenarioDocument=scenario==null?null:ScenarioCodec.Clone(scenario);scenarioHash=scenario==null?"":ScenarioCodec.ContentHash(scenarioDocument);
            MapId=mapId??MapTopology.Frontier;if(!HasScenario&&!MapTopology.IsValid(MapId))throw new ArgumentException("Unknown map: "+MapId);
            Layout=HasScenario?ScenarioCompiler.Compile(scenarioDocument,rules,Players.Length):MapLayout.Create(MapId,Seed,Players.Length);
            if(HasScenario)foreach(var start in scenarioDocument.starts)if(start.age>MaxAge)throw new ArgumentException("Scenario opening age exceeds the match age limit");
            Navigation=new GridNavigation();WaterNavigation=new GridNavigation();Fog=new Visibility(players:Players.Length,teams:teams,activePlayer:IsPlayerActive);
            TradeRoute=MapTradeRoute();InitializeNavigationTerrain();
            discoveredLandmarks=new HashSet<int>[Players.Length];for(int i=0;i<Players.Length;i++)discoveredLandmarks[i]=new HashSet<int>();
            InitializeEconomy();InitializeMap();InitializeLandmarks();InitializeNeutralAllies();Fog.Tick(Entities);RecordLandmarkDiscoveries();RebuildSpatial();
            if(HasScenario)scenarioRuntime=new ScenarioRuntime(this,scenarioDocument,initialScenarioBindings);
            Emit("MatchStarted",-1,0,$"Seed {Seed}; map {MapId}; age cap {MaxAge}; {string.Join(" vs ",civilizations)}; project rules P v0.2");
        }
        private void InitializeMap()
        {
            if(HasScenario){InitializeScenarioMap();return;}
            if(Layout.Generated||Players.Length>2){InitializeGeneratedMap();return;}
            EntityState Place(string definition,int owner,Vector2 position)=>Spawn(definition,owner,MapLandPosition(definition,position));
            // Mirrored resources, two outcrop walls with a narrow middle passage and wide flanks.
            for(int p=0;p<2;p++)
            {
                Vector2 b=p==0?new Vector2(24,28):new Vector2(104,100);float s=p==0?1:-1;
                Place("town",p,b);
                for(int i=0;i<Rules.Civ(Players[p].civilization).startingWorkers;i++)Place(WorkerDefinition(p).id,p,b+new Vector2(5+i%3*1.3f,-3+i/3*1.4f)*s);
                Place("explorer",p,b+new Vector2(7,4)*s);
                Place("berry",-1,b+new Vector2(8,-7)*s);Place("berry",-1,b+new Vector2(11,-7)*s);
                for(int i=0;i<5;i++)Place("deer",-1,b+new Vector2(-8+i*1.8f,9+i%2*1.8f)*s);
                Place("mine",-1,b+new Vector2(-10,-5)*s);
                for(int i=0;i<18;i++)Place("tree",-1,b+new Vector2(-14+i%6*2.2f,-12-i/6*2.2f)*s);
                Place("crate_food",p,b+new Vector2(5,1)*s);Place("crate_wood",p,b+new Vector2(5,3)*s);
                Place("mine",-1,b+new Vector2(22,7)*s);
                for(int i=0;i<9;i++)Place("tree",-1,b+new Vector2(17+i%3*2.1f,19+i/3*2.1f)*s);
                for(int i=0;i<5;i++)Place("deer",-1,b+new Vector2(9+i*2,25)*s);
            }
            if(MapId==MapTopology.Frontier)for(int y=42;y<=86;y+=4)
            {
                if(y>=58&&y<=70)continue;
                var o=new Vector3(64,y,1.8f);TerrainObstacles.Add(o);Navigation.SetBlocked(new Vector2(o.x,o.y),o.z,true);
            }
            Place("mine",-1,new Vector2(53,66));Place("mine",-1,new Vector2(75,62));
            for(int i=0;i<8;i++)Place("tree",-1,new Vector2(30+i*2.5f,73));
            if(Layout.HasWater)foreach(var point in Layout.FishPoints)Spawn("fish",-1,point);
        }
        public EntityState Get(int id)=>byId.TryGetValue(id,out var e)?e:null;
        public EntityState Spawn(string definition,int owner,Vector2 position,bool complete=true)
        {
            if(owner< -1||owner>=Players.Length)throw new ArgumentOutOfRangeException(nameof(owner));
            var d=Rules.Entity(definition);if(d==null)throw new ArgumentException("Undefined entity: "+definition);
            var e=new EntityState{id=nextId++,owner=owner,def=d,position=position,previousPosition=position,origin=position,
                autoHeal=d.healer,hp=complete?d.hp:Mathf.Max(1,d.hp*.05f),construction=complete?1:0,remaining=d.amount,rally=position+new Vector2(0,-d.depth*.5f-3)};
            Entities.Add(e);byId[e.id]=e;
            if(d.kind==EntityKind.Building)SetBuildingObstacle(e,true);
            OnEntitySpawned(e);e.hp=complete?MaxHp(e):Mathf.Max(1,MaxHp(e)*.05f);
            Emit("Spawn",owner,e.id,d.name);
            return e;
        }
        private void SetBuildingObstacle(EntityState e,bool blocked)
        {
            // Both channels consume the same rotated warehouse/quay footprint.
            // Their authored terrain layer remains separate when a dock is removed.
            foreach(var cell in BuildingFootprintCells(e.def,e.position))
            {Navigation.SetBlocked(cell,0,blocked);WaterNavigation.SetBlocked(cell,0,blocked);}
        }
        public void Remove(EntityState e,int killerOwner=-1)
        {
            if(e==null||!e.Alive)return;bool concealed=e.stealth;int observed=0;
            if(concealed)for(int player=0;player<Players.Length;player++)if(CanObserveStealth075(player,e))observed|=1<<player;
            e.stealth=false;e.dead=true;e.hp=0;ClearHealing076(e);e.autoHeal=false;ClearPatientHealingLinks076(e);
            if(e.def.kind==EntityKind.Building)SetBuildingObstacle(e,false);
            OnEntityRemoved(e);
            if(e.owner>=0&&e.def.kind!=EntityKind.Resource)Players[e.owner].losses++;
            if(killerOwner>=0&&killerOwner<Players.Length&&IsHostileTarget(killerOwner,e)&&e.def.kind!=EntityKind.Resource)
            {if(Guardian(e))Players[killerOwner].guardiansDefeated++;else Players[killerOwner].kills++;AwardXp(killerOwner,e.def.killXp,e.id,"Combat bounty");}
            bool removedTransport=HandleTransportRemoval(e,killerOwner);
            if(e.occupants.Count>0&&!removedTransport)EvacuateDestroyedBuilding(e);
            var death=Emit("Death",e.owner,e.id,e.def.name+" lost",0,killerOwner);if(concealed)death.visibilityMask&=observed;
        }
        public bool IsVisible(int player,EntityState e)=>e!=null&&e.Alive&&e.garrisonedIn==0&&Fog.IsVisible(player,e)&&CanObserveStealth075(player,e);
        public SimEvent Emit(string kind,int player,int entity,string message,float amount=0,int target=0,Vector2? position=null)
        {
            var e=Get(entity);Vector2 point=position??e?.position??Vector2.zero;int visible=0;
            for(int observer=0;observer<Players.Length;observer++)if(Fog.At(observer,point)==VisibilityState.Visible&&
                (e==null||CanObserveStealth075(observer,e)))visible|=1<<observer;
            var ev = new SimEvent{time=Time,kind=kind,player=player,entity=entity,target=target,message=message,amount=amount,position=point,facing=Get(entity)?.facing??Vector2.zero,visibilityMask=visible};
            if(RecordsPopulation(kind)&&player>=0&&player<Players.Length)
            {ev.hasPopulation=true;ev.population=PopulationSnapshot.From(Players[player]);}
            Events.Add(ev);return ev;
        }
        public CommandResult Issue(GameCommand c)
        {
            if(c==null||c.player<0||c.player>=Players.Length)return CommandResult.Fail("Invalid player");
            if(!System.Enum.IsDefined(typeof(CommandKind),c.kind)||!SnapshotCodec.Finite(c.point))return CommandResult.Fail("Invalid command or destination");
            c.actors??=Array.Empty<int>();
            if(IsMatchEnded)return CommandResult.Fail("Match has ended");
            if(!IsPlayerActive(c.player))return CommandResult.Fail("Player is eliminated or resigned");
            if(Paused)return CommandResult.Fail("Resume before issuing orders");
            if(c.kind==CommandKind.Resign)return ResignPlayer(c.player);
            if(c.kind==CommandKind.Disband)return DisbandLandUnits(c);
            if(c.kind==CommandKind.ToggleMode)
            {int n=0;foreach(int id in c.actors){var e=Get(id);if(e!=null&&e.Alive&&e.owner==c.player&&e.def.meleeDamage>0&&e.def.range>2){e.forceMelee=!e.forceMelee;n++;}}return n>0?CommandResult.Ok("Combat mode changed"):CommandResult.Fail("No mode-switching units selected");}
            if(c.kind==CommandKind.TradeMode)return SetTradeMode(c);
            if(c.kind==CommandKind.TradeRoute)return SetCaravanRoute(c);
            if(c.kind==CommandKind.Market)return ExchangeAtMarket(c);
            if(c.kind==CommandKind.Manufacture)return StartManufacture(c);
            if(c.kind==CommandKind.ToggleShield)return ToggleShield(c);
            if(c.kind==CommandKind.ToggleStealth)return ToggleStealth075(c);
            if(c.kind==CommandKind.ToggleAutoHeal)return ToggleAutoHeal076(c);
            if(TryHandleNavalTransportCommand(c,out var navalResult))return navalResult;
            if(c.kind==CommandKind.NeutralAlliance||c.kind==CommandKind.NeutralRecruit||c.kind==CommandKind.NeutralResearch)return IssueNeutralCommand(c);
            if(c.kind==CommandKind.Order)return IssueOrders(c);
            if(c.kind==CommandKind.Rally)return SetRally(c);
            if(c.kind==CommandKind.Ungarrison)
            {
                int n=0;foreach(int id in c.actors){var e=Get(id);if(e==null||!e.Alive||e.owner!=c.player||e.def.kind!=EntityKind.Building)continue;
                    if(!ReleaseGarrison(e,false))continue;n++;}
                return n>0?CommandResult.Ok():CommandResult.Fail("Garrison exit blocked or no valid building");
            }
            var economic=HandleEconomicCommand(c);
            if(economic.accepted&&c.kind==CommandKind.Build)foreach(int id in c.actors){var actor=Get(id);if(actor!=null&&actor.owner==c.player)BreakStealth075(actor);}
            return economic;
        }
        private CommandResult IssueOrders(GameCommand c)
        {
            if(!System.Enum.IsDefined(typeof(OrderKind),c.order))return CommandResult.Fail("Invalid order");
            if(c.order==OrderKind.Heal)return IssueHealOrders076(c);
            if(c.order==OrderKind.Scout&&c.target!=0)return CommandResult.Fail("Scout orders do not target an entity");
            if(c.order==OrderKind.Embark||c.order==OrderKind.Disembark||c.order==OrderKind.TransportBerth||c.order==OrderKind.DockRepair)return CommandResult.Fail("Use the transport or harbor command");
            var actors=new List<EntityState>();
            foreach(int id in c.actors){var e=Get(id);if(e!=null&&e.Alive&&e.owner==c.player&&e.def.kind==EntityKind.Unit&&e.garrisonedIn==0){actors.Add(e);}}
            if(actors.Count==0)return CommandResult.Fail("Select mobile units");
            var target=Get(c.target);
            bool targeted=c.order==OrderKind.Attack||c.order==OrderKind.Gather||c.order==OrderKind.Build||c.order==OrderKind.Repair||c.order==OrderKind.Garrison||c.order==OrderKind.Claim||c.order==OrderKind.Manufacture;
            if(targeted&&(target==null||!IsVisible(c.player,target)))return CommandResult.Fail("Target is not currently visible");
            if(c.order==OrderKind.Claim){string reason=TreasureReason(c.player,target);if(reason!="")return CommandResult.Fail(reason);}
            if(c.order==OrderKind.Attack&&(!IsHostileTarget(c.player,target)||target.def.kind==EntityKind.Resource))return CommandResult.Fail("Choose an enemy");
            if((c.order==OrderKind.Build||c.order==OrderKind.Repair||c.order==OrderKind.Garrison)&&target.owner!=c.player)return CommandResult.Fail("Choose your own building");
            if(c.order==OrderKind.Manufacture&&(target.owner!=c.player||target.def.id!="workshop"||!target.Complete||!Rules.Civ(Players[c.player].civilization).manufacturing))return CommandResult.Fail("Choose your completed workshop");
            if(c.order==OrderKind.Gather&&!(target.def.resource!=ResourceKind.None&&(target.def.kind==EntityKind.Resource||target.owner==c.player)))return CommandResult.Fail("Choose a resource");
            if(c.order==OrderKind.Garrison&&(target.def.kind!=EntityKind.Building||target.def.garrison<=0||!target.Complete))return CommandResult.Fail("This building cannot garrison units");
            bool needsSlots=!targeted&&(c.order==OrderKind.Move||c.order==OrderKind.AttackMove);
            // Resolve both channels before mutating any order. Mixed selections
            // keep independent spacing and cannot partially accept a failed move.
            var slots=needsSlots?new Vector2[actors.Count]:null;
            if(needsSlots)
            {
                for(int domain=0;domain<2;domain++)
                {
                    var indices=new List<int>();float domainSpacing=1.8f;
                    for(int i=0;i<actors.Count;i++)if(actors[i].def.waterborne==(domain==1))
                    {indices.Add(i);domainSpacing=Mathf.Max(domainSpacing,actors[i].def.radius*4+.3f);}
                    if(indices.Count==0)continue;
                    var navigation=domain==1?WaterNavigation:Navigation;
                    var points=navigation.FormationSlots(c.point,indices.Count,domainSpacing);
                    if(points.Count<indices.Count)return CommandResult.Fail("No room for this formation in its movement channel");
                    for(int i=0;i<indices.Count;i++)slots[indices[i]]=points[i];
                }
            }
            int accepted=0;
            for(int i=0;i<actors.Count;i++)
            {
                var e=actors[i];if(c.order==OrderKind.Claim&&(e.def.tags&Tags.Explorer)==0)continue;
                if(c.order==OrderKind.Scout&&!CanScout(e))continue;
                if(c.order==OrderKind.Gather&&!CanGather(e,target))continue;
                if(c.order==OrderKind.Build&&!CanConstruct(e,target.def.id))continue;
                if(c.order==OrderKind.Manufacture&&(!e.Worker||e.def.waterborne))continue;
                if(c.order==OrderKind.Build&&target.def.id=="factory")continue;
                if(c.order==OrderKind.Garrison&&e.def.waterborne)continue;
                if(c.order==OrderKind.Attack&&!CanAttackTarget(e,target))continue;
                if(c.order==OrderKind.Repair&&!(e.Worker&&target.def.kind==EntityKind.Building||e.def.repairArtillery&&(target.def.tags&Tags.Artillery)!=0))continue;
                if(e.def.civilian&&(c.order==OrderKind.Attack||c.order==OrderKind.AttackMove))continue;
                if(c.order==OrderKind.Attack||c.order==OrderKind.AttackMove||c.order==OrderKind.Build||c.order==OrderKind.Claim)BreakStealth075(e);
                CancelCaravan(e,"Manual order");
                if(e.shielded&&c.order!=OrderKind.Hold&&c.order!=OrderKind.Attack){e.shielded=false;e.shieldReadyAt=Time+2;}
                var previous=e.orders.Count>0?e.orders.Peek():null;
                var requested=targeted?target.position:needsSlots?slots[i]:c.point;
                bool append=c.append&&c.order!=OrderKind.Stop&&c.order!=OrderKind.Hold;
                bool sameTravel=!append&&SameTravelIntent(previous,c.order,requested,c.target);
                if(!append)
                {
                    ClearHealing076(e);
                    if(c.order==OrderKind.Stop)e.autoHeal=false;
                    bool keepPreparation=sameTravel&&(c.order==OrderKind.AttackMove||c.order==OrderKind.Attack&&e.attackTarget==c.target);
                    e.orders.Clear();
                    if(!sameTravel)InvalidateCommandPath(e);
                    if(!keepPreparation){e.fireAt=-1;e.attackTarget=0;}
                }
                if(c.order==OrderKind.Stop||c.order==OrderKind.Hold){e.hold=c.order==OrderKind.Hold;e.status=e.hold?"Holding":"Idle";}
                else {e.hold=false;e.orders.Enqueue(sameTravel?previous:new UnitOrder(c.order,requested,c.order==OrderKind.Scout?0:c.target));e.status=c.order.ToString();}
                accepted++;
            }
            if(accepted==0)return CommandResult.Fail("No selected unit can perform that order");
            Emit("Command",c.player,actors[0].id,c.order+" ×"+accepted,accepted,c.target);return CommandResult.Ok(c.order+" order accepted");
        }
        private static bool SameTravelIntent(UnitOrder previous,OrderKind kind,Vector2 point,int target)
        {
            if(previous==null||previous.kind!=kind)return false;
            if(kind==OrderKind.Attack)return previous.target==target;
            return (kind==OrderKind.Move||kind==OrderKind.AttackMove)&&(previous.point-point).sqrMagnitude<.0001f;
        }
        private void InvalidateCommandPath(EntityState unit)
        {
            // A new intention may need a nearby destination. It must not inherit
            // the failed/exhausted-route retry clock or an earlier recovery detour.
            unit.path.Clear();unit.pathIndex=0;unit.pathVersion=-1;unit.pathRetryAt=Time;
            unit.pathAvoidsAllies=false;unit.detourUntil=0;unit.progressDistance=float.MaxValue;unit.progressAt=Time;unit.stuckFor=0;
            unit.recoveryProgressPosition=unit.position;unit.recoveryProgressAt=Time;
        }
        public void Tick(float dt=.1f)
        {
            if(Paused||IsMatchEnded||dt<=0)return;
            Time+=dt;foreach(var e in Entities)e.previousPosition=e.position;TickEvacuations();
            RebuildSpatial();
            MaintainStealth075();
            var sw=System.Diagnostics.Stopwatch.StartNew();TickMovementCombat(dt);LastCombatMs=Mathf.Max(0,(float)sw.Elapsed.TotalMilliseconds-LastNavigationMs);
            TickTransportCargo();
            MaintainStealth075();
            TickHealing076(dt);
            sw.Restart();TickEconomy(dt);TickLandmarks(dt);TickNeutralAllies(dt);TickMapObjectives(dt);scenarioRuntime?.Tick(dt);LastEconomyMs=(float)sw.Elapsed.TotalMilliseconds;
            if(Time>=fogAt){fogAt=Time+.4f;Fog.Tick(Entities);RecordLandmarkDiscoveries();}
            if(!IsMatchEnded)EvaluateElimination();
        }
        private void RebuildSpatial()
        {
            foreach(var bucket in spatial.Values)bucket.Clear();
            foreach(var e in Entities)if(e.Alive&&e.garrisonedIn==0)
            {int key=Mathf.FloorToInt(e.position.x/8)+Mathf.FloorToInt(e.position.y/8)*32;if(!spatial.TryGetValue(key,out var bucket)){bucket=new List<EntityState>();spatial[key]=bucket;}bucket.Add(e);}
        }
        public IEnumerable<EntityState> Nearby(Vector2 center,float radius)
        {
            int minX=Mathf.FloorToInt((center.x-radius)/8),maxX=Mathf.FloorToInt((center.x+radius)/8),minY=Mathf.FloorToInt((center.y-radius)/8),maxY=Mathf.FloorToInt((center.y+radius)/8);
            for(int y=minY;y<=maxY;y++)for(int x=minX;x<=maxX;x++)if(spatial.TryGetValue(x+y*32,out var bucket))foreach(var e in bucket)if(e.Alive&&(e.position-center).sqrMagnitude<=radius*radius)yield return e;
        }
        public Vector2 WorkPoint(EntityState target,EntityState worker)
        {
            var delta=worker.position-target.position;if(delta.sqrMagnitude<.01f)delta=Vector2.right;
            float reach=target.def.kind==EntityKind.Building?Mathf.Max(target.def.width,target.def.depth)*.5f+worker.def.radius+.85f:target.def.radius+worker.def.radius+.45f;
            return NavigationFor(worker).NearestWalkable(target.position+delta.normalized*reach,worker.def.radius);
        }
        public float EdgeDistance(EntityState a,EntityState b)
        {
            if(b.def.id=="dock")return a.Worker&&!a.def.waterborne?DockLandEdgeDistance(a,b):Mathf.Max(0,BuildingFootprintDistance(b.def,b.position,a.position)-a.def.radius);
            if(b.def.kind==EntityKind.Building){Vector2 q=new Vector2(Mathf.Max(0,Mathf.Abs(a.position.x-b.position.x)-b.def.width*.5f),Mathf.Max(0,Mathf.Abs(a.position.y-b.position.y)-b.def.depth*.5f));return Mathf.Max(0,q.magnitude-a.def.radius);}
            return Mathf.Max(0,Vector2.Distance(a.position,b.position)-a.def.radius-b.def.radius);
        }
    }
}
