using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public sealed class TradeSocketState { public int entity,waypoint; }
    public sealed class TreasureState { public int entity; public TreasureDef definition; public readonly List<int> guards=new List<int>(); }
    [Serializable] public sealed class LandmarkReachabilityObservation
    {
        public int seed,player,entity,openingTown,routeSocket,nearestSocket,camp,waypoint=-1,pathNodes;
        public string objectType,origin="First legal Town Center villager production exit",socketAssociation="Nearest route socket by authored position; observation only";
        public Vector2 from,position;public float radius;public bool openingExitAvailable,startWalkable,destinationWalkable,reachable;
    }
    [Serializable] public sealed class LandmarkDiscoveryObservation
    {public int seed,player,entity;public string objectType;public Vector2 position;}
    public partial class World
    {
        public readonly List<TradeSocketState> TradeSockets=new List<TradeSocketState>();
        public readonly List<TreasureState> Treasures=new List<TreasureState>();
        public Vector2 CaravanPosition {get;private set;}
        private int caravanWaypoint=1,caravanDirection=1;
        private readonly HashSet<int>[] discoveredLandmarks={new HashSet<int>(),new HashSet<int>()};
        public static bool Guardian(EntityState e)=>e!=null&&(e.def.tags&Tags.Guardian)!=0;
        public static bool Treasure(EntityState e)=>e!=null&&e.def.id.StartsWith("treasure_");
        private void InitializeLandmarks()
        {
            var rules=Rules.landmarks;if(rules==null)return;
            foreach(var socket in rules.sockets)
            {
                var e=Spawn("trade_socket",-1,socket.position);TradeSockets.Add(new TradeSocketState{entity=e.id,waypoint=socket.waypoint});
                Emit("TradeSocket",-1,e.id,"Route waypoint "+socket.waypoint);
            }
            foreach(var definition in rules.treasures)
            {
                var e=Spawn(definition.entity,-1,definition.position);var treasure=new TreasureState{entity=e.id,definition=definition};Treasures.Add(treasure);
                for(int i=0;i<definition.guards;i++)
                {
                    var offset=new Vector2(i%2==0?-2.5f:2.5f,2.5f+i/2*1.5f);
                    var guard=Spawn("guardian",-1,Navigation.NearestWalkable(e.position+offset,.4f));treasure.guards.Add(guard.id);
                }
                Emit("TreasurePlaced",-1,e.id,"Guarded camp: "+definition.guards);
            }
            if(rules.route.Length>0)CaravanPosition=rules.route[0];
            RecordOpeningLandmarkReachability();
        }
        private void RecordOpeningLandmarkReachability()
        {
            // These are actual navigation queries on the authored opening, not
            // rewards, visibility grants or a claim of randomized map coverage.
            float radius=Rules.Entity("villager").radius;
            for(int player=0;player<Players.Length;player++)
            {
                var town=Entities.Find(e=>e.Alive&&e.owner==player&&e.def.id=="town");
                var points=new List<Vector2>();bool exit=town!=null&&FindSpawnPositions(town,1,radius,out points);
                Vector2 from=exit?points[0]:town?.position??Vector2.zero;
                foreach(var site in TradeSockets)Observe(Get(site.entity),site.entity,0,site.waypoint);
                foreach(var treasure in Treasures)
                {Observe(Get(treasure.entity),0,treasure.entity,-1);foreach(int guard in treasure.guards)Observe(Get(guard),0,treasure.entity,-1);}
                void Observe(EntityState entity,int socket,int camp,int waypoint)
                {
                    int nearest=0;float distance=float.MaxValue;
                    foreach(var site in TradeSockets){var candidate=Get(site.entity);float delta=(candidate.position-entity.position).sqrMagnitude;if(delta<distance){distance=delta;nearest=candidate.id;}}
                    bool startWalkable=exit&&Navigation.IsWalkable(from,radius),destinationWalkable=Navigation.IsWalkable(entity.position,radius);
                    var path=startWalkable&&destinationWalkable?Navigation.FindPath(from,entity.position,radius):new List<Vector2>();
                    bool reachable=path.Count>0&&(path[path.Count-1]-entity.position).sqrMagnitude<.0001f;
                    var observation=new LandmarkReachabilityObservation{seed=Seed,player=player,entity=entity.id,openingTown=town?.id??0,
                        routeSocket=socket,nearestSocket=nearest,camp=camp,waypoint=waypoint,objectType=entity.def.id,
                        from=from,position=entity.position,radius=radius,openingExitAvailable=exit,startWalkable=startWalkable,
                        destinationWalkable=destinationWalkable,reachable=reachable,pathNodes=path.Count};
                    Emit("LandmarkReachability",player,entity.id,JsonUtility.ToJson(observation),reachable?1:0,socket,entity.position);
                }
            }
        }
        private void RecordLandmarkDiscoveries()
        {
            if(Rules.landmarks==null)return;
            foreach(var entity in Entities)
            {
                if(!entity.Alive||(entity.def.id!="trade_socket"&&entity.def.id!="trade_post"&&!Treasure(entity)&&!Guardian(entity)))continue;
                for(int player=0;player<Players.Length;player++)
                {
                    // Own-entity shortcuts in IsVisible are not discovery. The
                    // fog cell itself must be currently visible to this player.
                    if(Fog.At(player,entity.position)!=VisibilityState.Visible||!discoveredLandmarks[player].Add(entity.id))continue;
                    var observation=new LandmarkDiscoveryObservation{seed=Seed,player=player,entity=entity.id,objectType=entity.def.id,position=entity.position};
                    Emit("LandmarkDiscovered",player,entity.id,JsonUtility.ToJson(observation),0,0,entity.position);
                }
            }
        }
        public EntityState SocketAt(Vector2 point)
        {foreach(var socket in TradeSockets){var e=Get(socket.entity);if(e!=null&&(e.position-point).sqrMagnitude<.01f)return e;}return null;}
        public EntityState TradingPostAt(int socketId)
        {
            var socket=Get(socketId);if(socket==null)return null;
            return Entities.Find(e=>e.Alive&&e.def.id=="trade_post"&&(e.position-socket.position).sqrMagnitude<.01f);
        }
        public Vector2 BuildPoint(int player,string id,Vector2 point)
        {
            if(id=="trade_post")
            {
                EntityState best=null;float distance=6*6;
                foreach(var socket in TradeSockets){var e=Get(socket.entity);if(!IsVisible(player,e))continue;float d=(e.position-point).sqrMagnitude;if(d<distance){distance=d;best=e;}}
                if(best!=null)return best.position;
            }
            return new Vector2(Mathf.Round(point.x),Mathf.Round(point.y));
        }
        public string TreasureReason(int player,EntityState treasure)
        {
            if(!Treasure(treasure)||!IsVisible(player,treasure))return "Choose a visible treasure";
            var state=Treasures.Find(t=>t.entity==treasure.id);if(state==null)return "No treasure reward";
            if(state.guards.Exists(id=>Get(id)?.Alive==true))return "Defeat this treasure's guardians first";
            return "";
        }
        public string LandmarkDescription(int player,EntityState e)
        {
            if(Treasure(e)){var state=Treasures.Find(t=>t.entity==e.id);return state==null?e.def.description:e.def.description+"\nReward: "+state.definition.crates+" in crates / "+state.definition.xp+" XP. Pathfinder: right click to claim.";}
            if(e.def.id=="trade_socket")return "Trading post site. Select settlers, open Build, place Trading Post here.";
            if(e.def.id=="trade_post")return "Caravan delivery: "+(e.tradeMode==ResourceKind.None?"XP":e.tradeMode.ToString())+". Upgrade Cargo Ledgers to choose resources.";
            return e.def.description;
        }
        private CommandResult SetTradeMode(GameCommand c)
        {
            var e=c.actors.Length>0?Get(c.actors[0]):null;
            if(e==null||!e.Alive||e.owner!=c.player||e.def.id!="trade_post"||!e.Complete)return CommandResult.Fail("Select your completed trading post");
            if(c.index<0||c.index>3)return CommandResult.Fail("Unknown trade resource");
            if(c.index>0&&!Players[c.player].technologies.Contains("cargo_ledgers"))return CommandResult.Fail("Research Cargo Ledgers first");
            e.tradeMode=(ResourceKind)c.index;Emit("TradeMode",c.player,e.id,e.tradeMode==ResourceKind.None?"XP":e.tradeMode.ToString());return CommandResult.Ok("Trade delivery mode changed");
        }
        private void TickLandmarks(float dt)
        {
            if(Rules.landmarks==null)return;
            // Claims are resolved in entity order. Removing a claimed object makes all
            // other claims fail before any reward can be applied a second time.
            int count=Entities.Count;
            for(int i=0;i<count;i++)
            {
                var explorer=Entities[i];if(!explorer.Alive||explorer.garrisonedIn!=0||explorer.orders.Count==0)continue;
                var order=explorer.orders.Peek();if(order.kind!=OrderKind.Claim)continue;
                var treasure=Get(order.target);string reason=TreasureReason(explorer.owner,treasure);
                if(reason!=""){explorer.orders.Dequeue();explorer.status=reason;continue;}
                if(EdgeDistance(explorer,treasure)>1.5f){order.elapsed=0;continue;}
                order.elapsed+=dt;explorer.status="Claiming treasure";if(order.elapsed<Rules.landmarks.claimTime)continue;
                var state=Treasures.Find(t=>t.entity==treasure.id);
                if(!SpawnCrates(explorer.owner,treasure.position,state.definition.crates)){explorer.status="Treasure reward landing blocked";continue;}
                AwardXp(explorer.owner,state.definition.xp,treasure.id,"Treasure reward");Players[explorer.owner].treasuresClaimed++;
                Emit("TreasureClaimed",explorer.owner,treasure.id,"Treasure secured; collect any reward crates",state.definition.xp,explorer.id);
                Remove(treasure);explorer.orders.Dequeue();explorer.status="Idle";
            }
            var route=Rules.landmarks.route;if(route.Length<2)return;
            float remaining=Mathf.Max(0,Rules.landmarks.caravanSpeed)*dt;int safety=0;
            while(remaining>0&&safety++<64)
            {
                var next=route[caravanWaypoint];float distance=Vector2.Distance(CaravanPosition,next);
                if(distance>remaining){CaravanPosition=Vector2.MoveTowards(CaravanPosition,next,remaining);break;}
                CaravanPosition=next;remaining-=distance;DeliverTrade(caravanWaypoint);
                if(caravanWaypoint==route.Length-1)caravanDirection=-1;else if(caravanWaypoint==0)caravanDirection=1;
                caravanWaypoint+=caravanDirection;
            }
        }
        private void DeliverTrade(int waypoint)
        {
            foreach(var socket in TradeSockets)
            {
                if(socket.waypoint!=waypoint)continue;var post=TradingPostAt(socket.entity);
                if(post==null||!post.Complete)continue;
                var p=Players[post.owner];float factor=p.technologies.Contains("cargo_ledgers")?Rules.landmarks.tradeUpgradeMultiplier:1;
                if(post.tradeMode==ResourceKind.None){float xp=Rules.landmarks.tradeXp*factor;AwardXp(post.owner,xp,post.id,"Trade route delivery");Emit("TradeDelivery",post.owner,post.id,"XP",xp,socket.entity);}
                else
                {
                    var values=Rules.landmarks.tradeResources;float amount=(post.tradeMode==ResourceKind.Food?values.food:post.tradeMode==ResourceKind.Wood?values.wood:values.coin)*factor;
                    var credit=ResourceCost(post.tradeMode,amount);p.resources+=credit;p.tradeIncome+=credit;
                    Emit("TradeDelivery",post.owner,post.id,post.tradeMode.ToString(),amount,socket.entity);
                }
            }
        }
        private bool Observes(EntityState observer,EntityState target)
        {return observer.owner>=0?IsVisible(observer.owner,target):target!=null&&target.Alive&&target.garrisonedIn==0&&Vector2.Distance(observer.position,target.position)<=observer.def.vision;}
    }
}
