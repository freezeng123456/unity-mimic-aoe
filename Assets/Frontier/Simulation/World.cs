using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public partial class World
    {
        public readonly RuleCatalog Rules;
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
        public World(RuleCatalog rules,int seed=314159,string civ0="coast",string civ1="coast",int maxAge=3)
        {
            Rules=rules??throw new ArgumentNullException(nameof(rules));Seed=seed;MaxAge=Mathf.Clamp(maxAge,1,rules.ages.Length);
            Navigation=new GridNavigation();Fog=new Visibility();
            Players=new[]{new PlayerState{id=0,civilization=civ0},new PlayerState{id=1,civilization=civ1}};
            if(Rules.Civ(civ0)==null||Rules.Civ(civ1)==null)throw new ArgumentException("Unknown civilization");
            InitializeEconomy();InitializeMap();InitializeLandmarks();Fog.Tick(Entities);RebuildSpatial();
            Emit("MatchStarted",-1,0,$"Seed {Seed}; age cap {MaxAge}; {civ0} vs {civ1}; project rules P v0.2");
        }
        private void InitializeMap()
        {
            // Mirrored resources, two outcrop walls with a narrow middle passage and wide flanks.
            for(int p=0;p<2;p++)
            {
                Vector2 b=p==0?new Vector2(24,28):new Vector2(104,100);float s=p==0?1:-1;
                Spawn("town",p,b);
                for(int i=0;i<6;i++)Spawn("villager",p,b+new Vector2(5+i%3*1.3f,-3+i/3*1.4f)*s);
                Spawn("explorer",p,b+new Vector2(7,4)*s);
                Spawn("berry",-1,b+new Vector2(8,-7)*s);Spawn("berry",-1,b+new Vector2(11,-7)*s);
                for(int i=0;i<5;i++)Spawn("deer",-1,b+new Vector2(-8+i*1.8f,9+i%2*1.8f)*s);
                Spawn("mine",-1,b+new Vector2(-10,-5)*s);
                for(int i=0;i<18;i++)Spawn("tree",-1,b+new Vector2(-14+i%6*2.2f,-12-i/6*2.2f)*s);
                Spawn("crate_food",p,b+new Vector2(5,1)*s);Spawn("crate_wood",p,b+new Vector2(5,3)*s);
                Spawn("mine",-1,b+new Vector2(22,7)*s);
                for(int i=0;i<9;i++)Spawn("tree",-1,b+new Vector2(17+i%3*2.1f,19+i/3*2.1f)*s);
                for(int i=0;i<5;i++)Spawn("deer",-1,b+new Vector2(9+i*2,25)*s);
            }
            for(int y=42;y<=86;y+=4)
            {
                if(y>=58&&y<=70)continue;
                var o=new Vector3(64,y,1.8f);TerrainObstacles.Add(o);Navigation.SetBlocked(new Vector2(o.x,o.y),o.z,true);
            }
            Spawn("mine",-1,new Vector2(53,66));Spawn("mine",-1,new Vector2(75,62));
            for(int i=0;i<8;i++)Spawn("tree",-1,new Vector2(30+i*2.5f,73));
        }
        public EntityState Get(int id)=>byId.TryGetValue(id,out var e)?e:null;
        public EntityState Spawn(string definition,int owner,Vector2 position,bool complete=true)
        {
            var d=Rules.Entity(definition);if(d==null)throw new ArgumentException("Undefined entity: "+definition);
            var e=new EntityState{id=nextId++,owner=owner,def=d,position=position,previousPosition=position,origin=position,
                hp=complete?d.hp:Mathf.Max(1,d.hp*.05f),construction=complete?1:0,remaining=d.amount,rally=position+new Vector2(0,-d.depth*.5f-3)};
            Entities.Add(e);byId[e.id]=e;
            if(d.kind==EntityKind.Building)SetBuildingObstacle(e,true);
            OnEntitySpawned(e);e.hp=complete?MaxHp(e):Mathf.Max(1,MaxHp(e)*.05f);
            Emit("Spawn",owner,e.id,d.name);
            return e;
        }
        private void SetBuildingObstacle(EntityState e,bool blocked)
        {
            // Grid ownership is disjoint because construction validates the footprint.
            for(float x=e.position.x-e.def.width*.5f+.5f;x<e.position.x+e.def.width*.5f;x+=1)
                for(float z=e.position.y-e.def.depth*.5f+.5f;z<e.position.y+e.def.depth*.5f;z+=1)
                    Navigation.SetBlocked(new Vector2(x,z),.45f,blocked);
        }
        public void Remove(EntityState e,int killerOwner=-1)
        {
            if(e==null||!e.Alive)return;e.dead=true;e.hp=0;
            if(e.def.kind==EntityKind.Building)SetBuildingObstacle(e,false);
            OnEntityRemoved(e);
            if(e.owner>=0&&e.def.kind!=EntityKind.Resource)Players[e.owner].losses++;
            if(killerOwner>=0&&killerOwner<2&&e.owner!=killerOwner&&e.def.kind!=EntityKind.Resource)
            {if(Guardian(e))Players[killerOwner].guardiansDefeated++;else Players[killerOwner].kills++;AwardXp(killerOwner,e.def.killXp,e.id,"Combat bounty");}
            if(e.occupants.Count>0)EvacuateDestroyedBuilding(e);
            Emit("Death",e.owner,e.id,e.def.name+" lost",0,killerOwner);
        }
        public bool IsVisible(int player,EntityState e)=>e!=null&&e.Alive&&e.garrisonedIn==0&&Fog.IsVisible(player,e);
        public SimEvent Emit(string kind,int player,int entity,string message,float amount=0,int target=0,Vector2? position=null)
        {
            var e=Get(entity);Vector2 point=position??e?.position??Vector2.zero;int visible=0;
            for(int observer=0;observer<Players.Length;observer++)if(Fog.At(observer,point)==VisibilityState.Visible)visible|=1<<observer;
            var ev = new SimEvent{time=Time,kind=kind,player=player,entity=entity,target=target,message=message,amount=amount,position=point,facing=Get(entity)?.facing??Vector2.zero,visibilityMask=visible};
            if(RecordsPopulation(kind)&&player>=0&&player<Players.Length)
            {ev.hasPopulation=true;ev.population=PopulationSnapshot.From(Players[player]);}
            Events.Add(ev);return ev;
        }
        public CommandResult Issue(GameCommand c)
        {
            if(c==null||c.player<0||c.player>=2)return CommandResult.Fail("Invalid player");
            if(Winner>=0)return CommandResult.Fail("Match has ended");
            if(Paused)return CommandResult.Fail("Resume before issuing orders");
            if(c.kind==CommandKind.Resign){Players[c.player].resigned=true;Winner=1-c.player;Emit("Resign",c.player,0,"Player resigned");return CommandResult.Ok("Resigned");}
            if(c.kind==CommandKind.ToggleMode)
            {int n=0;foreach(int id in c.actors){var e=Get(id);if(e!=null&&e.Alive&&e.owner==c.player&&e.def.meleeDamage>0&&e.def.range>2){e.forceMelee=!e.forceMelee;n++;}}return n>0?CommandResult.Ok("Combat mode changed"):CommandResult.Fail("No mode-switching units selected");}
            if(c.kind==CommandKind.TradeMode)return SetTradeMode(c);
            if(c.kind==CommandKind.Order)return IssueOrders(c);
            if(c.kind==CommandKind.Rally)return SetRally(c);
            if(c.kind==CommandKind.Ungarrison)
            {
                int n=0;foreach(int id in c.actors){var e=Get(id);if(e==null||!e.Alive||e.owner!=c.player||e.def.kind!=EntityKind.Building)continue;
                    if(!ReleaseGarrison(e,false))continue;n++;}
                return n>0?CommandResult.Ok():CommandResult.Fail("Garrison exit blocked or no valid building");
            }
            return HandleEconomicCommand(c);
        }
        private CommandResult IssueOrders(GameCommand c)
        {
            var actors=new List<EntityState>();float spacing=1.8f;
            foreach(int id in c.actors){var e=Get(id);if(e!=null&&e.Alive&&e.owner==c.player&&e.def.kind==EntityKind.Unit&&e.garrisonedIn==0){actors.Add(e);spacing=Mathf.Max(spacing,e.def.radius*4+.3f);}}
            if(actors.Count==0)return CommandResult.Fail("Select mobile units");
            var target=Get(c.target);
            bool targeted=c.order==OrderKind.Attack||c.order==OrderKind.Gather||c.order==OrderKind.Build||c.order==OrderKind.Repair||c.order==OrderKind.Garrison||c.order==OrderKind.Claim;
            if(targeted&&(target==null||!IsVisible(c.player,target)))return CommandResult.Fail("Target is not currently visible");
            if(c.order==OrderKind.Claim){string reason=TreasureReason(c.player,target);if(reason!="")return CommandResult.Fail(reason);}
            if(c.order==OrderKind.Attack&&(target.owner==c.player||target.def.kind==EntityKind.Resource))return CommandResult.Fail("Choose an enemy");
            if((c.order==OrderKind.Build||c.order==OrderKind.Repair||c.order==OrderKind.Garrison)&&target.owner!=c.player)return CommandResult.Fail("Choose your own building");
            if(c.order==OrderKind.Gather&&!(target.def.resource!=ResourceKind.None&&(target.def.kind==EntityKind.Resource||target.owner==c.player)))return CommandResult.Fail("Choose a resource");
            if(c.order==OrderKind.Garrison&&(target.def.garrison<=0||!target.Complete))return CommandResult.Fail("This building cannot garrison units");
            bool needsSlots=!targeted&&(c.order==OrderKind.Move||c.order==OrderKind.AttackMove);
            var slots=needsSlots?Navigation.FormationSlots(c.point,actors.Count,spacing):null;if(needsSlots&&slots.Count<actors.Count)return CommandResult.Fail("No room for this formation");int accepted=0;
            for(int i=0;i<actors.Count;i++)
            {
                var e=actors[i];if(c.order==OrderKind.Claim&&(e.def.tags&Tags.Explorer)==0)continue;if((c.order==OrderKind.Gather||c.order==OrderKind.Build||c.order==OrderKind.Repair)&&!e.Worker)continue;
                var previous=e.orders.Count>0?e.orders.Peek():null;
                var requested=targeted?target.position:needsSlots?slots[i]:c.point;
                bool sameTravel=!c.append&&SameTravelIntent(previous,c.order,requested,c.target);
                if(!c.append)
                {
                    bool keepPreparation=sameTravel&&(c.order==OrderKind.AttackMove||c.order==OrderKind.Attack&&e.attackTarget==c.target);
                    e.orders.Clear();
                    if(!sameTravel)InvalidateCommandPath(e);
                    if(!keepPreparation){e.fireAt=-1;e.attackTarget=0;}
                }
                if(c.order==OrderKind.Stop||c.order==OrderKind.Hold){e.hold=c.order==OrderKind.Hold;e.status=e.hold?"Holding":"Idle";}
                else {e.hold=false;e.orders.Enqueue(sameTravel?previous:new UnitOrder(c.order,requested,c.target));e.status=c.order.ToString();}
                accepted++;
            }
            if(accepted==0)return CommandResult.Fail("That order requires villagers");
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
        }
        public void Tick(float dt=.1f)
        {
            if(Paused||Winner>=0||dt<=0)return;
            Time+=dt;foreach(var e in Entities)e.previousPosition=e.position;TickEvacuations();
            RebuildSpatial();
            var sw=System.Diagnostics.Stopwatch.StartNew();TickMovementCombat(dt);LastCombatMs=Mathf.Max(0,(float)sw.Elapsed.TotalMilliseconds-LastNavigationMs);
            sw.Restart();TickEconomy(dt);TickLandmarks(dt);LastEconomyMs=(float)sw.Elapsed.TotalMilliseconds;
            if(Time>=fogAt){fogAt=Time+.4f;Fog.Tick(Entities);}
            for(int p=0;p<2;p++)
            {
                bool remains=false;foreach(var e in Entities)if(e.Alive&&e.owner==p&&e.def.kind!=EntityKind.Resource){remains=true;break;}
                if(!remains){Winner=1-p;Emit("MatchEnded",Winner,0,"All opposing units and buildings eliminated");break;}
            }
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
            return Navigation.NearestWalkable(target.position+delta.normalized*reach,worker.def.radius);
        }
        public float EdgeDistance(EntityState a,EntityState b)
        {
            if(b.def.kind==EntityKind.Building){Vector2 q=new Vector2(Mathf.Max(0,Mathf.Abs(a.position.x-b.position.x)-b.def.width*.5f),Mathf.Max(0,Mathf.Abs(a.position.y-b.position.y)-b.def.depth*.5f));return Mathf.Max(0,q.magnitude-a.def.radius);}
            return Mathf.Max(0,Vector2.Distance(a.position,b.position)-a.def.radius-b.def.radius);
        }
    }
}
