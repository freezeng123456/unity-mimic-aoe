using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public partial class World
    {
        readonly ScenarioDocument scenarioDocument;
        readonly string scenarioHash;
        ScenarioRuntime scenarioRuntime;
        readonly List<ScenarioBindingState> initialScenarioBindings=new List<ScenarioBindingState>();
        public bool HasScenario=>scenarioDocument!=null;
        public ScenarioDocument ScenarioDocument=>HasScenario?ScenarioCodec.Clone(scenarioDocument):null;
        public string ScenarioHash=>scenarioHash??"";
        public ScenarioRuntimeState ScenarioState=>scenarioRuntime?.Capture();
        public ScenarioObjectiveState[] ScenarioObjectiveStates=>scenarioRuntime?.Capture().objectiveStates??Array.Empty<ScenarioObjectiveState>();
        public EntityState ScenarioEntity(string id)
        {var ids=scenarioRuntime?.BoundEntities(id);return ids!=null&&ids.Length>0?Get(ids[0]):null;}
        public int[] ScenarioEntities(string id)=>scenarioRuntime?.BoundEntities(id)??Array.Empty<int>();
        public World(RuleCatalog rules,ScenarioDocument document,string[] civilizations,int[] teams,int maxAge=5)
            :this(rules,document?.seed??314159,civilizations,teams,maxAge,MapTopology.Scenario,document??throw new ArgumentNullException(nameof(document))){}
        void BindScenario(string id,int entity)
        {
            initialScenarioBindings.Add(new ScenarioBindingState{objectId=id,entityIds=new[]{entity}});
        }
        void InitializeScenarioMap()
        {
            for(int player=0;player<Players.Length;player++)
            {
                var start=scenarioDocument.starts[player];
                BindScenario("start-"+player+"-town",PlaceScenario("town",player,start.town,true).id);
                BindScenario("start-"+player+"-explorer",PlaceScenario("explorer",player,start.explorer,true).id);
                for(int i=0;i<start.workers.Length;i++)BindScenario("start-"+player+"-worker-"+i,
                    PlaceScenario(WorkerDefinition(player).id,player,start.workers[i],true).id);
            }
            foreach(var item in scenarioDocument.resources)
            {
                var entity=PlaceScenario(item.definition,item.owner,item.position,item.complete);
                if(item.remaining>=0)entity.remaining=item.remaining;BindScenario(item.id,entity.id);
            }
            foreach(var item in scenarioDocument.objects)
            {var entity=PlaceScenario(item.definition,item.owner,item.position,item.complete);BindScenario(item.id,entity.id);}
        }
        void InitializeScenarioLandmarks()
        {
            foreach(var socket in scenarioDocument.tradeSockets)
            {
                var e=PlaceScenario("trade_socket",-1,socket.position,true);
                TradeSockets.Add(new TradeSocketState{entity=e.id,waypoint=socket.waypoint});BindScenario(socket.id,e.id);
            }
            foreach(var camp in scenarioDocument.treasures)
            {
                var definition=Rules.landmarks.treasures[camp.definition];var e=PlaceScenario(definition.entity,-1,camp.position,true);
                var treasure=new TreasureState{entity=e.id,definition=definition};Treasures.Add(treasure);BindScenario(camp.id,e.id);
                for(int i=0;i<camp.guardPositions.Length;i++)
                {var guard=PlaceScenario("guardian",-1,camp.guardPositions[i],true);treasure.guards.Add(guard.id);BindScenario(camp.id+"-guard-"+i,guard.id);}
            }
            if(TradeRoute.Length>0)CaravanPosition=TradeRoute[0];RecordOpeningLandmarkReachability();
        }
        void InitializeScenarioNeutralAllies()
        {
            foreach(var binding in scenarioDocument.neutralBindings)
            {
                var definition=Array.Find(Rules.neutralAllies,d=>d.id==binding.definition);
                var stable=initialScenarioBindings.Find(b=>b.objectId==binding.socketId);
                if(definition==null||stable==null)throw new ArgumentException("Invalid scenario neutral settlement binding");
                NeutralAllies.Add(new NeutralAllianceState{id=definition.id,site=stable.entityIds[0]});
            }
        }
        EntityState PlaceScenario(string definition,int owner,Vector2 p,bool complete)
        {
            string reason=ScenarioPlacementReason(definition,p);
            if(reason!="")throw new ArgumentException("Scenario "+definition+" at "+p+": "+reason);
            return Spawn(definition,owner,p,complete);
        }
        public string ScenarioPlacementReason(string definition,Vector2 p)
        {
            var d=Rules.Entity(definition);if(d==null||!SnapshotCodec.Finite(p))return "Unknown object or invalid position";
            if(d.id=="dock")
            {string bank=DockPlacementReason(p);if(bank!="")return bank;string quay=DockFootprintReason(p);if(quay!="")return quay;if(Layout.FoundationHeightRange(p,d.width,d.depth,Layout.WaterDirection(p))>.5f)return "Dock needs a level warehouse foundation";}
            else if(d.waterborne)
            {if(!IsWater(p)||!WaterNavigation.IsWalkable(p,d.radius))return "Water channel is blocked or too narrow";}
            else if(d.kind==EntityKind.Building)
            {
                if(!Navigation.IsAreaFree(p,d.width,d.depth)||BridgeFootprintOverlaps(p,d.width,d.depth))return "Blocked building footprint";
                if(Layout.FoundationHeightRange(p,d.width,d.depth)>.25f)return "Building requires a level foundation";
            }
            else if(!Navigation.IsWalkable(p,d.radius))return "Blocked, steep or submerged ground";
            foreach(var e in Entities)
            {
                if(!e.Alive||e.garrisonedIn!=0||d.waterborne!=e.def.waterborne&&d.id!="dock"&&e.def.id!="dock")continue;
                if(d.id=="trade_socket"||e.def.id=="trade_socket")continue;
                if(d.kind!=EntityKind.Building&&e.def.kind!=EntityKind.Building&&(d.kind!=EntityKind.Unit||e.def.kind!=EntityKind.Unit))continue;
                if(e.def.kind==EntityKind.Building)
                {if(BuildingFootprintDistance(e.def,e.position,p)<d.radius+.08f)return "Object overlaps a building";}
                else if(d.kind==EntityKind.Building)
                {if(BuildingFootprintContains(d,p,e.position,e.def.radius+.08f))return "Building overlaps another object";}
                else if((e.position-p).sqrMagnitude<Mathf.Pow(d.radius+e.def.radius,2)-.00001f)return "Objects overlap";
            }
            return "";
        }
        internal bool TryScenarioSpawnGroup(ScenarioAction action,out int[] ids,out string error)
        {
            ids=Array.Empty<int>();if(!PlanScenarioSpawnGroup(action,null,out var points,out error))return false;
            ids=SpawnScenarioGroup(action,points);return true;
        }
        internal bool PlanScenarioSpawnGroup(ScenarioAction action,List<Tuple<EntityDef,Vector2>> reserved,out Vector2[] planned,out string error)
        {
            planned=Array.Empty<Vector2>();error="";var d=Rules.Entity(action.definition);
            if(d==null||action.count<1||action.count>64){error="Invalid scenario spawn definition or count";return false;}
            if(!IsPlayerActive(action.player)){error="Scenario spawn player is inactive";return false;}
            var points=new List<Vector2>();
            for(int ring=0;ring<=8&&points.Count<action.count;ring++)for(int y=-ring;y<=ring&&points.Count<action.count;y++)for(int x=-ring;x<=ring&&points.Count<action.count;x++)
            {
                if(Mathf.Max(Mathf.Abs(x),Mathf.Abs(y))!=ring)continue;
                var p=action.point+new Vector2(x,y)*Mathf.Max(1,d.radius*2+.25f);
                if(ScenarioPlacementReason(d.id,p)!="")continue;
                bool overlaps=false;foreach(var previous in points)if((previous-p).sqrMagnitude<Mathf.Pow(d.radius*2+.08f,2)){overlaps=true;break;}
                if(reserved!=null)foreach(var previous in reserved)
                    if(previous.Item1.waterborne==d.waterborne&&(previous.Item2-p).sqrMagnitude<Mathf.Pow(previous.Item1.radius+d.radius+.08f,2)){overlaps=true;break;}
                if(!overlaps)points.Add(p);
            }
            if(points.Count!=action.count){error="Scenario spawn has no room for the complete group";return false;}
            planned=points.ToArray();return true;
        }
        internal int[] SpawnScenarioGroup(ScenarioAction action,Vector2[] points)
        {
            var ids=new int[points.Length];for(int i=0;i<ids.Length;i++)ids[i]=Spawn(action.definition,action.player,points[i]).id;return ids;
        }
        internal void GrantScenarioResource(int player,ResourceKind kind,float amount)
        {
            var p=Players[player];var before=p.resources;var delta=ResourceCost(kind,amount);p.resources+=delta;
            var ev=RecordTransaction("ScenarioResourceGranted",p,0,0,kind+" +"+amount,delta,before);ev.visibilityMask=1<<player;
        }
        internal void FinishScenario(int winner,string reason)
        {
            if(IsMatchEnded)return;
            Winner=winner;scenarioRuntime.MarkResult(winner,reason,true);
            var ev=Emit("ScenarioEnded",winner,0,reason);ev.visibilityMask=(1<<Players.Length)-1;
            ev=Emit("MatchEnded",winner,0,reason);ev.visibilityMask=(1<<Players.Length)-1;
        }
        internal int ScenarioFailureWinner(int player)
        {
            int first=-1;for(int other=0;other<Players.Length;other++)if(IsPlayerActive(other)&&AreEnemies(player,other))
            {if(first<0)first=other;else if(!AreAllies(first,other))return -2;}return first<0?-2:first;
        }
        internal void RestoreScenarioState(ScenarioRuntimeState state)
        {if(HasScenario)scenarioRuntime.Restore(state);}
    }
}
