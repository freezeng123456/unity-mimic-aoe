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
    [Serializable] public sealed class WorldSnapshot
    {
        public const int CurrentSchema=1;
        public int schema=CurrentSchema,seed,maxAge,winner,nextId,caravanWaypoint,caravanDirection;
        public string rulesHash,mapId;public float time,fogAt,nextEvacuation;public bool paused;
        public PlayerSnapshot[] players;public EntitySnapshot[] entities;
        public SimEvent[] events;public ProjectileState[] projectiles;public Vector3[] terrainObstacles;
        public NavigationSnapshot landNavigation,waterNavigation;public int[] visibility;
        public SocketSnapshot[] sockets;public TreasureSnapshot[] treasures;
        public Vector2 caravanPosition;public int[] discoveries0,discoveries1,pendingEvacuations,fineRecoveryQueue;
        public IncomeSnapshot[] incomes;
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
        public WorldSnapshot CaptureState()
        {
            var snapshot=new WorldSnapshot{rulesHash=SnapshotCodec.RulesHash(Rules),seed=Seed,maxAge=MaxAge,mapId=MapId,
                winner=Winner,time=Time,paused=Paused,nextId=nextId,fogAt=fogAt,nextEvacuation=nextEvacuation,
                players=new PlayerSnapshot[Players.Length],entities=new EntitySnapshot[Entities.Count],events=Events.ToArray(),
                projectiles=Projectiles.ToArray(),terrainObstacles=TerrainObstacles.ToArray(),landNavigation=Navigation.CaptureState(),
                waterNavigation=WaterNavigation.CaptureState(),visibility=Fog.CaptureState(),caravanPosition=CaravanPosition,
                caravanWaypoint=caravanWaypoint,caravanDirection=caravanDirection};
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
            ValidateSnapshot(rules,source);var s=SnapshotCodec.Clone(source);
            var world=new World(rules,s.seed,s.players[0].state.civilization,s.players[1].state.civilization,s.maxAge,s.mapId);
            world.Entities.Clear();world.byId.Clear();world.Events.Clear();world.Projectiles.Clear();world.TradeSockets.Clear();world.Treasures.Clear();
            world.TerrainObstacles.Clear();world.TerrainObstacles.AddRange(s.terrainObstacles);
            for(int i=0;i<2;i++)
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
            world.discoveredLandmarks[0].Clear();world.discoveredLandmarks[0].UnionWith(s.discoveries0);world.discoveredLandmarks[1].Clear();world.discoveredLandmarks[1].UnionWith(s.discoveries1);
            world.pendingEvacuations.Clear();foreach(int id in s.pendingEvacuations)world.pendingEvacuations.Add(world.Get(id));world.nextEvacuation=s.nextEvacuation;
            foreach(int id in s.fineRecoveryQueue)
            {var e=world.Get(id);var request=new FineRecoveryRequest{unit=e,order=e.orders.Count>0?e.orders.Peek():null};world.pendingFineRecoveries.Add(id,request);world.pendingFineRecoveryQueue.Enqueue(request);}
            foreach(var item in s.incomes){world.gatherLog[item.source]=item.amount;world.gatherLogTarget[item.source]=item.target;world.incomeKinds[item.source]=item.kind;}
            world.RebuildSpatial();return world;
        }
        public static void ValidateSnapshot(RuleCatalog rules,WorldSnapshot s)
        {
            void Require(bool valid,string message){if(!valid)throw new ArgumentException("Invalid saved match: "+message);}
            Require(rules!=null&&s!=null,"missing state");Require(s.schema==WorldSnapshot.CurrentSchema,"unsupported schema");
            Require(s.rulesHash==SnapshotCodec.RulesHash(rules),"rules changed; this save needs its original game version");
            Require(MapTopology.IsValid(s.mapId)&&s.maxAge>=1&&s.maxAge<=rules.ages.Length,"map or age cap");
            Require(SnapshotCodec.Finite(s.time)&&s.time>=0&&SnapshotCodec.Finite(s.fogAt)&&SnapshotCodec.Finite(s.nextEvacuation),"clock");
            Require(s.winner>=-1&&s.winner<2&&s.players?.Length==2&&s.entities!=null&&s.entities.Length<100000,"players or entities");
            var ids=new HashSet<int>();int max=0;
            for(int i=0;i<2;i++)
            {
                var p=s.players[i];Require(p?.state!=null&&p.state.id==i&&rules.Civ(p.state.civilization)!=null,"civilization");
                Require(p.state.age>=1&&p.state.age<=s.maxAge&&SnapshotCodec.Finite(p.state.resources)&&SnapshotCodec.Finite(p.state.xp),"player economy");
                Require(p.technologies!=null&&p.sentCards!=null&&p.deck!=null&&p.ageHistory!=null&&p.rewards!=null&&p.shipments!=null&&p.enemyMemory!=null,"player collections");
                foreach(string id in p.technologies)Require(rules.Tech(id)!=null,"unknown technology");foreach(string id in p.deck)Require(rules.Card(id)!=null,"unknown card");
                foreach(var shipment in p.shipments)Require(shipment!=null&&rules.Card(shipment.card)!=null&&SnapshotCodec.Finite(shipment.elapsed),"shipment");
            }
            foreach(var saved in s.entities)
            {
                var e=saved?.state;Require(e!=null&&e.id>0&&ids.Add(e.id)&&e.owner>=-1&&e.owner<2&&rules.Entity(saved.definition)!=null,"entity identity");max=Math.Max(max,e.id);
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
            Require(s.events!=null&&s.projectiles!=null&&s.terrainObstacles!=null&&s.visibility?.Length==32768&&s.sockets!=null&&s.treasures!=null&&s.incomes!=null,"world collections");
            Require(s.discoveries0!=null&&s.discoveries1!=null&&s.pendingEvacuations!=null&&s.fineRecoveryQueue!=null,"world queues");
            foreach(var saved in s.entities){var e=saved.state;Require(e.garrisonedIn==0||ids.Contains(e.garrisonedIn),"garrison reference");foreach(int id in saved.occupants)Require(ids.Contains(id),"occupant reference");}
            foreach(int id in s.pendingEvacuations)Require(ids.Contains(id),"evacuation reference");
            var recoveryIds=new HashSet<int>();foreach(int id in s.fineRecoveryQueue)Require(ids.Contains(id)&&recoveryIds.Add(id),"recovery queue");
            foreach(var t in s.treasures)Require(t!=null&&ids.Contains(t.entity)&&t.definition>=0&&t.definition<(rules.landmarks?.treasures?.Length??0)&&t.guards!=null,"treasure");
            foreach(var socket in s.sockets)Require(socket!=null&&ids.Contains(socket.entity)&&socket.waypoint>=0&&socket.waypoint<(MapLayout.Create(s.mapId).HasWater?MapLayout.Create(s.mapId).TradeRoute.Length:rules.landmarks.route.Length),"trade socket");
            Require(SnapshotCodec.Finite(s.caravanPosition)&&(s.caravanDirection==1||s.caravanDirection==-1),"caravan");
            int routeLength=MapLayout.Create(s.mapId).HasWater?MapLayout.Create(s.mapId).TradeRoute.Length:rules.landmarks?.route?.Length??0;
            Require(routeLength<2||s.caravanWaypoint>=0&&s.caravanWaypoint<routeLength,"caravan waypoint");
        }
    }
}
