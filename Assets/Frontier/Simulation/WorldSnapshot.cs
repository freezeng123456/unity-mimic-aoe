using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Frontier
{
    [Serializable] public sealed class NavigationSnapshot
    { public int width,height,version; public bool[] blocked,terrain; }
    [Serializable] public sealed class IntPointSnapshot
    { public int id; public Vector2 point; }
    [Serializable] public sealed class IncomeSnapshot
    { public int source,target;public float amount;public string kind; }
    [Serializable] public sealed class PlayerSnapshot
    {
        public PlayerState state;public string[] technologies,sentCards,deck,ageHistory;
        public RewardJob[] rewards;public ShipmentJob[] shipments;public IntPointSnapshot[] enemyMemory;
    }
    [Serializable] public sealed class EntitySnapshot
    {
        public string definition;public EntityState state;
        public UnitOrder[] orders;public Vector2[] path;public ProductionJob[] queue;public int[] occupants;
    }
    [Serializable] public sealed class TreasureSnapshot
    { public int entity,definition;public int[] guards; }
    [Serializable] public sealed class SocketSnapshot {public int entity,waypoint;}
    [Serializable] public sealed class LandmarkDiscoverySnapshot {public int[] entities;}
    [Serializable] public sealed class WorldSnapshot
    {
        public const int CurrentSchema=4;
        public int schema=CurrentSchema,seed,maxAge,winner,nextId,caravanWaypoint,caravanDirection;
        public string rulesHash,mapId;public float time,fogAt,nextEvacuation;public bool paused;
        public PlayerSnapshot[] players;public EntitySnapshot[] entities;
        public SimEvent[] events;public ProjectileState[] projectiles;public Vector3[] terrainObstacles;
        public NavigationSnapshot landNavigation,waterNavigation;public int[] visibility;
        public SocketSnapshot[] sockets;public TreasureSnapshot[] treasures;
        public Vector2 caravanPosition;public int[] discoveries0,discoveries1,pendingEvacuations,fineRecoveryQueue;
        public IncomeSnapshot[] incomes;
        public NeutralAllianceState[] neutralAllies;public MapObjectiveState mapObjective;
        public int[] teams;
        public LandmarkDiscoverySnapshot[] discoveries;
        public bool hasScenarioDocument;
        public string scenarioHash="";
        public ScenarioDocument scenarioDocument;
        public ScenarioRuntimeState scenarioState;
    }
    public static class SnapshotCodec
    {
        public static string Hash(string text)
        {using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();}
        public static string RulesHash(RuleCatalog rules)=>Hash(JsonUtility.ToJson(rules));
        public static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        public static bool Finite(Vector2 value)=>Finite(value.x)&&Finite(value.y);
        public static bool Finite(Cost value)=>Finite(value.food)&&Finite(value.wood)&&Finite(value.coin);
        public static IntPointSnapshot[] Points(Dictionary<int,Vector2> values)
        {
            var result=new List<IntPointSnapshot>();foreach(var item in values)result.Add(new IntPointSnapshot{id=item.Key,point=item.Value});
            result.Sort((a,b)=>a.id.CompareTo(b.id));return result.ToArray();
        }
        public static T Clone<T>(T value)=>JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
    }
    public partial class World
    {
        public WorldSnapshot CaptureState(bool includeEvents=true)
        {
            var snapshot=new WorldSnapshot{rulesHash=SnapshotCodec.RulesHash(Rules),seed=Seed,maxAge=MaxAge,mapId=MapId,
                hasScenarioDocument=HasScenario,scenarioHash=ScenarioHash,scenarioDocument=HasScenario?scenarioDocument:null,scenarioState=ScenarioState,
                winner=Winner,time=Time,paused=Paused,nextId=nextId,fogAt=fogAt,nextEvacuation=nextEvacuation,
                players=new PlayerSnapshot[Players.Length],entities=new EntitySnapshot[Entities.Count],events=includeEvents?Events.ToArray():Array.Empty<SimEvent>(),
                projectiles=Projectiles.ToArray(),terrainObstacles=TerrainObstacles.ToArray(),landNavigation=Navigation.CaptureState(),
                waterNavigation=WaterNavigation.CaptureState(),visibility=Fog.CaptureState(),caravanPosition=CaravanPosition,
                caravanWaypoint=caravanWaypoint,caravanDirection=caravanDirection,neutralAllies=CaptureNeutralAllies(),mapObjective=CaptureMapObjective()};
            for(int i=0;i<Players.Length;i++)
            {
                var p=Players[i];snapshot.players[i]=new PlayerSnapshot{state=p,technologies=new List<string>(p.technologies).ToArray(),sentCards=new List<string>(p.sentCards).ToArray(),
                    deck=p.deck.ToArray(),ageHistory=p.ageHistory.ToArray(),rewards=p.rewards.ToArray(),shipments=p.shipments.ToArray(),enemyMemory=SnapshotCodec.Points(p.enemyMemory)};
            }
            for(int i=0;i<Entities.Count;i++)
            {
                var e=Entities[i];snapshot.entities[i]=new EntitySnapshot{definition=e.def.id,state=e,orders=e.orders.ToArray(),path=e.path.ToArray(),queue=e.queue.ToArray(),occupants=e.occupants.ToArray()};
            }
            var sockets=new List<SocketSnapshot>();foreach(var s in TradeSockets)sockets.Add(new SocketSnapshot{entity=s.entity,waypoint=s.waypoint});snapshot.sockets=sockets.ToArray();
            var treasures=new List<TreasureSnapshot>();foreach(var t in Treasures)treasures.Add(new TreasureSnapshot{entity=t.entity,definition=Array.IndexOf(Rules.landmarks.treasures,t.definition),guards=t.guards.ToArray()});snapshot.treasures=treasures.ToArray();
            snapshot.discoveries0=Sorted(discoveredLandmarks[0]);snapshot.discoveries1=Sorted(discoveredLandmarks[1]);
            snapshot.teams=new int[Players.Length];snapshot.discoveries=new LandmarkDiscoverySnapshot[Players.Length];
            for(int i=0;i<Players.Length;i++){snapshot.teams[i]=Players[i].team;snapshot.discoveries[i]=new LandmarkDiscoverySnapshot{entities=Sorted(discoveredLandmarks[i])};}
            var evacuations=new List<int>();foreach(var e in pendingEvacuations)evacuations.Add(e.id);snapshot.pendingEvacuations=evacuations.ToArray();
            var recovery=new List<int>();foreach(var request in pendingFineRecoveryQueue)
                if(pendingFineRecoveries.TryGetValue(request.unit.id,out var current)&&ReferenceEquals(current,request)&&
                    ReferenceEquals(request.order,request.unit.orders.Count>0?request.unit.orders.Peek():null))recovery.Add(request.unit.id);
            snapshot.fineRecoveryQueue=recovery.ToArray();
            var incomes=new List<IncomeSnapshot>();foreach(var pair in gatherLog)
            {gatherLogTarget.TryGetValue(pair.Key,out int target);incomeKinds.TryGetValue(pair.Key,out string kind);incomes.Add(new IncomeSnapshot{source=pair.Key,target=target,amount=pair.Value,kind=kind});}
            incomes.Sort((a,b)=>a.source.CompareTo(b.source));snapshot.incomes=incomes.ToArray();
            // The returned snapshot owns its data; later ticks cannot mutate a pending save.
            return SnapshotCodec.Clone(snapshot);
        }
        private static string[] Sorted(HashSet<string> set){var list=new List<string>(set);list.Sort(StringComparer.Ordinal);return list.ToArray();}
        private static int[] Sorted(HashSet<int> set){var list=new List<int>(set);list.Sort();return list.ToArray();}
        public static World Restore(RuleCatalog rules,WorldSnapshot source)
        {
            ValidateSnapshot(rules,source);return RestoreOwned(rules,SnapshotCodec.Clone(source));
        }
        // Replay history already owns plain DTO copies. Clone only the eventless
        // state projection; repeated JSON serialization of the ledger would make
        // otherwise sparse seeks grow with the entire match history.
        internal static World RestoreReplay(RuleCatalog rules,WorldSnapshot projection,SimEvent[] ownedEvents)
        {
            ValidateSnapshot(rules,projection);
            if(projection.events.Length!=0||ownedEvents==null)throw new ArgumentException("Invalid replay projection");
            var s=SnapshotCodec.Clone(projection);s.events=ownedEvents;return RestoreOwned(rules,s);
        }
        static World RestoreOwned(RuleCatalog rules,WorldSnapshot s)
        {
            if(s.schema==2)
            {
                for(int i=0;i<2;i++)s.players[i].state.eliminated=s.players[i].state.resigned||s.winner>=0&&s.mapObjective.mode=="elimination"&&i!=s.winner;
                s.mapObjective.enemyPlayer=s.mapObjective.enemyProgress>0?1-s.mapObjective.player:-1;
            }
            var civilizations=new string[s.players.Length];var teams=new int[s.players.Length];
            for(int i=0;i<s.players.Length;i++){civilizations[i]=s.players[i].state.civilization;teams[i]=s.schema==2?i:s.teams[i];s.players[i].state.team=teams[i];}
            var world=s.hasScenarioDocument?new World(rules,s.scenarioDocument,civilizations,teams,s.maxAge):new World(rules,s.seed,civilizations,teams,s.maxAge,s.mapId);
            world.Entities.Clear();world.byId.Clear();world.Events.Clear();world.Projectiles.Clear();world.TradeSockets.Clear();world.Treasures.Clear();
            world.TerrainObstacles.Clear();world.TerrainObstacles.AddRange(s.terrainObstacles);
            for(int i=0;i<s.players.Length;i++)
            {
                var saved=s.players[i];var p=saved.state;
                p.technologies.UnionWith(saved.technologies);p.sentCards.UnionWith(saved.sentCards);p.deck.AddRange(saved.deck);p.ageHistory.AddRange(saved.ageHistory);
                p.rewards.AddRange(saved.rewards);p.shipments.AddRange(saved.shipments);foreach(var item in saved.enemyMemory)p.enemyMemory.Add(item.id,item.point);world.Players[i]=p;
            }
            foreach(var saved in s.entities)
            {
                var e=saved.state;e.def=rules.Entity(saved.definition);e.orders.Clear();foreach(var order in saved.orders)e.orders.Enqueue(order);
                e.path.AddRange(saved.path);e.queue.AddRange(saved.queue);e.occupants.AddRange(saved.occupants);world.Entities.Add(e);world.byId.Add(e.id,e);
            }
            world.Navigation.RestoreState(s.landNavigation);world.WaterNavigation.RestoreState(s.waterNavigation);world.Fog.RestoreState(s.visibility);
            world.Time=s.time;world.Paused=s.paused;world.Winner=s.winner;world.nextId=s.nextId;world.fogAt=s.fogAt;
            world.Events.AddRange(s.events);world.Projectiles.AddRange(s.projectiles);
            foreach(var item in s.sockets)world.TradeSockets.Add(new TradeSocketState{entity=item.entity,waypoint=item.waypoint});
            foreach(var item in s.treasures)
            {var treasure=new TreasureState{entity=item.entity,definition=rules.landmarks.treasures[item.definition]};treasure.guards.AddRange(item.guards);world.Treasures.Add(treasure);}
            world.CaravanPosition=s.caravanPosition;world.caravanWaypoint=s.caravanWaypoint;world.caravanDirection=s.caravanDirection;
            for(int i=0;i<s.players.Length;i++)
            {world.discoveredLandmarks[i].Clear();world.discoveredLandmarks[i].UnionWith(s.schema==2?(i==0?s.discoveries0:s.discoveries1):s.discoveries[i].entities);}
            world.pendingEvacuations.Clear();foreach(int id in s.pendingEvacuations)world.pendingEvacuations.Add(world.Get(id));world.nextEvacuation=s.nextEvacuation;
            foreach(int id in s.fineRecoveryQueue)
            {var e=world.Get(id);var request=new FineRecoveryRequest{unit=e,order=e.orders.Count>0?e.orders.Peek():null};world.pendingFineRecoveries.Add(id,request);world.pendingFineRecoveryQueue.Enqueue(request);}
            foreach(var item in s.incomes){world.gatherLog[item.source]=item.amount;world.gatherLogTarget[item.source]=item.target;world.incomeKinds[item.source]=item.kind;}
            world.RestoreNeutralAllies(s.neutralAllies);world.RestoreMapObjective(s.mapObjective);
            if(world.HasScenario)world.RestoreScenarioState(s.scenarioState);
            world.RebuildSpatial();return world;
        }
        public static void ValidateSnapshot(RuleCatalog rules,WorldSnapshot s)
        {
            void Require(bool valid,string message){if(!valid)throw new ArgumentException("Invalid saved match: "+message);}
            Require(rules!=null&&s!=null,"missing state");Require(s.schema==2||s.schema==3||s.schema==WorldSnapshot.CurrentSchema,"unsupported schema");
            Require(s.rulesHash==SnapshotCodec.RulesHash(rules),"rules changed; this save needs its original game version");
            Require((s.hasScenarioDocument?s.schema==4&&s.mapId==MapTopology.Scenario:MapTopology.IsValid(s.mapId))&&s.maxAge>=1&&s.maxAge<=rules.ages.Length,"map or age cap");
            if(s.hasScenarioDocument)
            {
                Require(ScenarioCodec.Validate(s.scenarioDocument,rules)==""&&s.scenarioHash==ScenarioCodec.ContentHash(s.scenarioDocument)&&s.seed==s.scenarioDocument.seed,"scenario document identity");
            }
            else Require(string.IsNullOrEmpty(s.scenarioHash)&&(s.scenarioDocument==null||string.IsNullOrEmpty(s.scenarioDocument.id))&&(s.scenarioState==null||string.IsNullOrEmpty(s.scenarioState.documentHash)),"unexpected scenario identity");
            Require(SnapshotCodec.Finite(s.time)&&s.time>=0&&SnapshotCodec.Finite(s.fogAt)&&SnapshotCodec.Finite(s.nextEvacuation),"clock");
            int count=s.players?.Length??0;
            Require(count>=2&&count<=4&&(s.schema!=2||count==2)&&s.winner>=-2&&s.winner<count&&s.entities!=null&&s.entities.Length<100000,"players or entities");
            MapLayout savedLayout=s.hasScenarioDocument?ScenarioCompiler.Compile(s.scenarioDocument,rules,count):MapLayout.Create(s.mapId,s.seed,count);
            int[] teams=s.schema==2?new[]{0,1}:s.teams;
            Require(teams!=null&&teams.Length==count,"team roster");var factions=new HashSet<int>();
            for(int i=0;i<count;i++){Require(teams[i]>=0&&teams[i]<=3,"team identity");factions.Add(teams[i]);}
            Require(factions.Count>=2,"at least two factions");
            var ids=new HashSet<int>();int max=0;
            for(int i=0;i<count;i++)
            {
                var p=s.players[i];Require(p?.state!=null&&p.state.id==i&&rules.Civ(p.state.civilization)!=null,"civilization");
                Require(s.schema==2||p.state.team==teams[i],"player team mismatch");
                Require(s.schema==2||!p.state.resigned||p.state.eliminated,"resigned player state");
                Require(p.state.age>=1&&p.state.age<=s.maxAge&&SnapshotCodec.Finite(p.state.resources)&&SnapshotCodec.Finite(p.state.xp),"player economy");
                Require(p.technologies!=null&&p.sentCards!=null&&p.deck!=null&&p.ageHistory!=null&&p.rewards!=null&&p.shipments!=null&&p.enemyMemory!=null,"player collections");
                foreach(string id in p.technologies)Require(rules.Tech(id)!=null,"unknown technology");foreach(string id in p.deck)Require(rules.Card(id)!=null,"unknown card");
                foreach(var shipment in p.shipments)Require(shipment!=null&&rules.Card(shipment.card)!=null&&SnapshotCodec.Finite(shipment.elapsed),"shipment");
            }
            foreach(var saved in s.entities)
            {
                var e=saved?.state;Require(e!=null&&e.id>0&&ids.Add(e.id)&&e.owner>=-1&&e.owner<count&&rules.Entity(saved.definition)!=null,"entity identity");max=Math.Max(max,e.id);
                Require(SnapshotCodec.Finite(e.position)&&SnapshotCodec.Finite(e.hp)&&SnapshotCodec.Finite(e.construction)&&SnapshotCodec.Finite(e.remaining),"entity values");
                Require(SnapshotCodec.Finite(e.factoryElapsed)&&e.factoryElapsed>=0,"factory progress");
                if(saved.definition=="factory")Require(e.factoryMode=="food"||e.factoryMode=="wood"||e.factoryMode=="coin"||e.factoryMode=="artillery","factory mode");
                // Clearing a completed/working path deliberately retains its old
                // cursor; it is reset when a new path is assigned. This is a legal
                // boundary state and must be preserved by an exact checkpoint.
                Require(saved.orders!=null&&saved.path!=null&&saved.queue!=null&&saved.occupants!=null&&e.pathIndex>=0&&(saved.path.Length==0||e.pathIndex<=saved.path.Length),"entity collections");
                foreach(var order in saved.orders)Require(order!=null&&Enum.IsDefined(typeof(OrderKind),order.kind)&&SnapshotCodec.Finite(order.point)&&SnapshotCodec.Finite(order.elapsed)&&(!order.hasResolvedPoint||SnapshotCodec.Finite(order.resolvedPoint)),"order");
                foreach(var job in saved.queue)
                {
                    Require(job!=null&&SnapshotCodec.Finite(job.elapsed)&&job.elapsed>=0&&SnapshotCodec.Finite(job.duration)&&job.duration>=0&&SnapshotCodec.Finite(job.paid)&&job.count>0,"production");
                    bool known=false;
                    if(job.kind=="Train"||job.kind=="Gift")known=rules.Entity(job.item)?.kind==EntityKind.Unit;
                    else if(job.kind=="Research")known=rules.Tech(job.item)!=null;
                    else if(job.kind=="Manufacture")known=Array.Exists(rules.manufactureRecipes,r=>r.id==job.item);
                    else if(job.kind=="Age")known=int.TryParse(job.item,out int age)&&age>=2&&age<=s.maxAge&&rules.Age(age)!=null;
                    Require(known,"unknown production kind or item");
                }
            }
            Require(s.nextId>max&&s.nextId<int.MaxValue,"next entity id");
            if(s.hasScenarioDocument)
            {
                ScenarioRuntime.Validate(s.scenarioDocument,s.scenarioHash,s.scenarioState,Array.ConvertAll(s.entities,e=>e.state),s.winner,s.time);
                foreach(var nav in new[]{s.landNavigation,s.waterNavigation})Require(nav!=null&&nav.width==128&&nav.height==128&&nav.terrain?.Length==16384,"scenario navigation");
                for(int y=0;y<128;y++)for(int x=0;x<128;x++)
                {
                    var p=new Vector2(x,y);int index=x+y*128;
                    Require(s.landNavigation.terrain[index]==(savedLayout.IsWater(p)&&!savedLayout.IsBridge(p)||savedLayout.IsBridgeSideBarrierCell(p)||savedLayout.IsTerrainBlocked(p)),"scenario land terrain mismatch");
                    Require(s.waterNavigation.terrain[index]==!savedLayout.IsWater(p),"scenario water terrain mismatch");
                }
            }
            ValidateTransportSnapshot(rules,s.entities);
            ValidateNeutralAlliesSnapshot(rules,s.neutralAllies,s.entities,s.players,s.hasScenarioDocument?s.scenarioDocument:null,s.hasScenarioDocument?s.scenarioState:null);
            var objective=s.mapObjective;
            if(s.schema==2&&objective!=null){objective=SnapshotCodec.Clone(objective);objective.enemyPlayer=objective.enemyProgress>0?1-objective.player:-1;}
            ValidateMapObjectiveSnapshot(rules,objective,s.entities,s.mapId,s.winner,s.seed,teams,savedLayout);
            Require(s.events!=null&&s.projectiles!=null&&s.terrainObstacles!=null&&s.visibility?.Length==count*16384&&s.sockets!=null&&s.treasures!=null&&s.incomes!=null,"world collections");
            Require(s.discoveries0!=null&&s.discoveries1!=null&&s.pendingEvacuations!=null&&s.fineRecoveryQueue!=null,"world queues");
            if(s.schema!=2)
            {
                Require(s.discoveries!=null&&s.discoveries.Length==count,"landmark discovery roster");
                for(int player=0;player<count;player++)
                {
                    Require(s.discoveries[player]?.entities!=null,"landmark discoveries");var seen=new HashSet<int>();
                    foreach(int id in s.discoveries[player].entities)Require(ids.Contains(id)&&seen.Add(id),"landmark discovery identity");
                    if(s.players[player].state.eliminated&&!s.players[player].state.resigned)
                        Require(!Array.Exists(s.entities,e=>e.state.Alive&&e.state.owner==player&&rules.Entity(e.definition).kind!=EntityKind.Resource),"eliminated player has actors");
                }
                var surviving=new HashSet<int>();for(int i=0;i<count;i++)if(!s.players[i].state.eliminated&&!s.players[i].state.resigned)surviving.Add(teams[i]);
                bool scenarioResult=s.hasScenarioDocument&&s.scenarioState.scenarioVictory;
                if(s.winner==-2&&s.mapObjective.mode=="elimination"&&!scenarioResult)Require(surviving.Count==0,"draw still has a surviving faction");
                if(s.winner>=0)
                {
                    Require(!s.players[s.winner].state.eliminated&&!s.players[s.winner].state.resigned,"winner is inactive");
                    if(s.mapObjective.mode=="elimination"&&!scenarioResult)Require(surviving.Count==1&&surviving.Contains(teams[s.winner]),"elimination winner factions");
                }
                if(s.winner==-1&&(!s.hasScenarioDocument||s.scenarioDocument.enableElimination))Require(surviving.Count>=2,"active match has fewer than two factions");
                for(int player=0;player<count;player++)for(int other=0;other<player;other++)if(teams[player]==teams[other])
                    for(int cell=0;cell<16384;cell++)Require(s.visibility[player*16384+cell]==s.visibility[other*16384+cell],"allied visibility mismatch");
            }
            foreach(int value in s.visibility)Require(value>=0&&value<=2,"visibility cell");
            foreach(var projectile in s.projectiles)Require(projectile!=null&&projectile.owner>=-1&&projectile.owner<count&&ids.Contains(projectile.target)&&ids.Contains(projectile.source)&&SnapshotCodec.Finite(projectile.hitAt)&&SnapshotCodec.Finite(projectile.damage)&&projectile.damage>=0,"projectile state");
            foreach(var ev in s.events)Require(ev!=null&&ev.player>=-2&&ev.player<count&&(ev.visibilityMask&~((1<<count)-1))==0&&SnapshotCodec.Finite(ev.time)&&SnapshotCodec.Finite(ev.position),"event roster");
            foreach(var saved in s.entities){var e=saved.state;Require(e.garrisonedIn==0||ids.Contains(e.garrisonedIn),"garrison reference");foreach(int id in saved.occupants)Require(ids.Contains(id),"occupant reference");}
            foreach(int id in s.pendingEvacuations)Require(ids.Contains(id),"evacuation reference");
            var recoveryIds=new HashSet<int>();foreach(int id in s.fineRecoveryQueue)Require(ids.Contains(id)&&recoveryIds.Add(id),"recovery queue");
            foreach(var t in s.treasures)Require(t!=null&&ids.Contains(t.entity)&&t.definition>=0&&t.definition<(rules.landmarks?.treasures?.Length??0)&&t.guards!=null,"treasure");
            foreach(var socket in s.sockets)Require(socket!=null&&ids.Contains(socket.entity)&&socket.waypoint>=0&&socket.waypoint<(s.hasScenarioDocument||savedLayout.HasWater?savedLayout.TradeRoute.Length:rules.landmarks.route.Length),"trade socket");
            Require(SnapshotCodec.Finite(s.caravanPosition)&&(s.caravanDirection==1||s.caravanDirection==-1),"caravan");
            int routeLength=s.hasScenarioDocument||savedLayout.HasWater?savedLayout.TradeRoute.Length:rules.landmarks?.route?.Length??0;
            Require(routeLength<2||s.caravanWaypoint>=0&&s.caravanWaypoint<routeLength,"caravan waypoint");
        }
    }
}
