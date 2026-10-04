using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    [Serializable] public sealed class MapObjectiveDef
    {
        public string id,name,escortUnit="objective_cart";
        public float holdSeconds=90,deadline=600,radius=9,failureSeconds=15;
    }
    [Serializable] public sealed class MapObjectiveState
    {
        public string mode="elimination",status="inactive",reason="";
        public int player,target,escort,waypoint;
        public Vector2 center;
        public Vector2[] route=Array.Empty<Vector2>();
        public float elapsed,progress,enemyProgress,nextProgressAt;
        public bool contested,moving;
    }
    public partial class World
    {
        public MapObjectiveState MapObjective {get;private set;}=new MapObjectiveState();
        public MapObjectiveDef ObjectiveDefinition(string id)
        {foreach(var d in Rules.mapObjectives??Array.Empty<MapObjectiveDef>())if(d.id==id)return d;return null;}
        private EntityState ObjectiveTown(int player)
        {foreach(var e in Entities)if(e.Alive&&e.owner==player&&e.Complete&&e.def.id=="town")return e;return null;}
        private bool ObjectiveLandPath(Vector2 from,Vector2 to,float radius)
        {
            if(!Navigation.IsWalkable(from,radius)||!Navigation.IsWalkable(to,radius))return false;
            var path=Navigation.FindPath(from,to,radius);
            return path.Count>0&&(path[path.Count-1]-to).sqrMagnitude<.01f;
        }
        private bool ObjectiveLandReach(int player,Vector2 center,float radius)
        {
            var town=ObjectiveTown(player);if(town==null||!FindSpawnPositions(town,1,.4f,out var starts))return false;
            // An occupied building centre is deliberately not a destination.
            for(int i=0;i<12;i++)
            {
                float angle=i*Mathf.PI/6;Vector2 end=center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*Mathf.Max(3,radius*.65f);
                if(ObjectiveLandPath(starts[0],end,.4f))return true;
            }
            return false;
        }
        private bool ObjectiveHarborReach(int player,Vector2 harbor,float radius)
        {
            if(ObjectiveLandReach(player,harbor,radius))return true;
            // A disconnected shore is usable only if a player can reach a
            // legal embarking shore and its water channel reaches the harbor.
            var ship=Rules.Entity("gunboat");if(ship==null||!ship.waterborne)return false;float clearance=ship.radius;
            Vector2 goal=WaterNavigation.NearestWalkable(harbor+DockWaterDirection(harbor)*7,clearance);
            if((goal-harbor).sqrMagnitude>radius*radius||!WaterNavigation.IsWalkable(goal,clearance))return false;
            foreach(var bank in Layout.DockSites)foreach(var shore in bank)
            {
                if(DockPlacementReason(shore)!=""||DockFootprintReason(shore)!=""||!ObjectiveLandReach(player,shore,8))continue;
                Vector2 start=WaterNavigation.NearestWalkable(shore+DockWaterDirection(shore)*7,clearance);
                if((start-shore).sqrMagnitude>100||!WaterNavigation.IsWalkable(start,clearance))continue;
                var path=WaterNavigation.FindPath(start,goal,clearance);
                if(path.Count>0&&(path[path.Count-1]-goal).sqrMagnitude<.01f)return true;
            }
            return false;
        }
        private bool TryObjectiveHarbor(MapObjectiveDef definition,out Vector2 point)
        {
            point=Vector2.zero;float best=float.MaxValue;
            foreach(var bank in Layout.DockSites)foreach(var candidate in bank)
            {
                if(DockPlacementReason(candidate)!=""||DockFootprintReason(candidate)!="")continue;
                bool occupied=false;var dock=Rules.Entity("dock");
                foreach(var e in Entities)if(e.Alive&&e.garrisonedIn==0&&BuildingFootprintContains(dock,candidate,e.position,e.def.radius+1)){occupied=true;break;}
                if(occupied||!ObjectiveHarborReach(0,candidate,definition.radius)||!ObjectiveHarborReach(1,candidate,definition.radius))continue;
                float score=(candidate-new Vector2(64,64)).sqrMagnitude;
                if(score>=best)continue;point=candidate;best=score;
            }
            return best<float.MaxValue;
        }
        private bool TryObjectiveBridge(MapObjectiveDef definition,out Vector2 point)
        {
            point=Vector2.zero;
            foreach(var bridge in Layout.Bridges)
                if(ObjectiveLandReach(0,bridge.Center,definition.radius)&&ObjectiveLandReach(1,bridge.Center,definition.radius))
                {point=bridge.Center;return true;}
            return false;
        }
        private bool TryObjectiveEscort(int player,MapObjectiveDef definition,out Vector2 start,out Vector2[] route)
        {
            start=Vector2.zero;route=Array.Empty<Vector2>();var unit=Rules.Entity(definition.escortUnit);var town=ObjectiveTown(player);
            if(unit==null||unit.kind!=EntityKind.Unit||unit.waterborne||town==null||TradeRoute.Length<2||!FindSpawnPositions(town,1,unit,out var exits))return false;
            start=exits[0];var points=new List<Vector2>();bool forward=(start-TradeRoute[0]).sqrMagnitude<=(start-TradeRoute[TradeRoute.Length-1]).sqrMagnitude;Vector2 previous=start;
            for(int step=0;step<TradeRoute.Length;step++)
            {
                Vector2 authored=TradeRoute[forward?step:TradeRoute.Length-1-step];Vector2 next=Navigation.NearestWalkable(authored,unit.radius);
                if((next-authored).sqrMagnitude>16||!ObjectiveLandPath(previous,next,unit.radius))return false;
                if((next-previous).sqrMagnitude>4){points.Add(next);previous=next;}
            }
            if(points.Count<2||Vector2.Distance(start,points[points.Count-1])<20)return false;
            route=points.ToArray();return true;
        }
        public string MapObjectiveAvailability(string id,int player=0)
        {
            if(player<0||player>=Players.Length)return "Invalid objective player";
            if(id=="elimination")return "";
            var definition=ObjectiveDefinition(id);if(definition==null)return "Unknown map objective";
            if(!SnapshotCodec.Finite(definition.holdSeconds)||definition.holdSeconds<=0||!SnapshotCodec.Finite(definition.deadline)||definition.deadline<=0||
                !SnapshotCodec.Finite(definition.radius)||definition.radius<=0||!SnapshotCodec.Finite(definition.failureSeconds)||definition.failureSeconds<=0)return "Invalid objective timing or radius";
            if(id=="capture_harbor")return Layout.HasWater&&Rules.Entity("dock")!=null&&TryObjectiveHarbor(definition,out _)?"":"No legal harbor reachable by both players";
            if(id=="defend_bridge")return Layout.Bridges.Length>0&&TryObjectiveBridge(definition,out _)?"":"This map has no bridge reachable by both players";
            if(id=="escort_caravan")return TryObjectiveEscort(player,definition,out _,out _)?"":"This map has no continuous land escort route";
            return "Unknown map objective";
        }
        public CommandResult ConfigureMapObjective(string id,int player=0)
        {
            if(Time!=0||Winner>=0||MapObjective.mode!="elimination")return CommandResult.Fail("Choose the objective once before the match starts");
            string reason=MapObjectiveAvailability(id,player);if(reason!="")return CommandResult.Fail(reason);
            if(id=="elimination")return CommandResult.Ok("Elimination victory");
            var definition=ObjectiveDefinition(id);var state=new MapObjectiveState{mode=id,status="active",player=player,nextProgressAt=5};
            if(id=="capture_harbor")
            {TryObjectiveHarbor(definition,out state.center);state.target=Spawn("dock",-1,state.center).id;}
            else if(id=="defend_bridge")TryObjectiveBridge(definition,out state.center);
            else
            {
                TryObjectiveEscort(player,definition,out var start,out state.route);
                state.escort=Spawn(definition.escortUnit,player,start).id;state.target=state.escort;state.center=state.route[state.route.Length-1];
            }
            MapObjective=state;PublicObjectiveEvent("MapObjectiveStarted",player,definition.name);return CommandResult.Ok(definition.name);
        }
        private void PublicObjectiveEvent(string kind,int player,string message,float amount=0)
        {
            // Objective position/progress is an announced game rule, shared by
            // both players. No enemy entity list or hidden positions is exposed.
            var e=Emit(kind,player,MapObjective.target,message,amount,0,MapObjective.center);e.visibilityMask=3;
        }
        private bool ObjectivePresence(int player,Vector2 center,float radius,int except=0,bool militaryOnly=true,bool landOnly=false)
        {
            foreach(var e in Entities)
                if(e.id!=except&&e.Alive&&e.owner==player&&e.garrisonedIn==0&&e.def.kind==EntityKind.Unit&&
                    (!militaryOnly||e.Military)&&(!landOnly||!e.def.waterborne)&&(e.position-center).sqrMagnitude<=radius*radius)return true;
            return false;
        }
        private void FinishMapObjective(int winner,string reason)
        {
            MapObjective.status=winner==MapObjective.player?"won":"failed";MapObjective.reason=reason;MapObjective.moving=false;
            Winner=winner;PublicObjectiveEvent(winner==MapObjective.player?"MapObjectiveWon":"MapObjectiveFailed",winner,reason);
            PublicObjectiveEvent("MatchEnded",winner,reason);
        }
        public void FinalizeMapObjectiveForMatchEnd()
        {
            if(Winner<0||MapObjective.mode=="elimination"||MapObjective.status!="active")return;
            MapObjective.status=Winner==MapObjective.player?"ended":"failed";MapObjective.reason="Match ended before objective completion";MapObjective.moving=false;
            PublicObjectiveEvent(Winner==MapObjective.player?"MapObjectiveEnded":"MapObjectiveFailed",Winner,MapObjective.reason);
        }
        public bool TryAiObjectiveDestination(int player,out Vector2 destination)
        {
            destination=Vector2.zero;var state=MapObjective;
            if(player<0||player>=Players.Length||Winner>=0||state.mode=="elimination"||state.status!="active")return false;
            if(state.mode!="escort_caravan"){destination=state.center;return true;}
            var cart=Get(state.escort);if(cart==null||!cart.Alive)return false;
            if(player==state.player)
            {
                destination=cart.position;
                if(state.waypoint<state.route.Length)destination+=Vector2.ClampMagnitude(state.route[state.waypoint]-cart.position,3);
            }
            else if(IsVisible(player,cart))destination=cart.position;
            else destination=state.route[Mathf.Min(state.waypoint,state.route.Length-1)];
            return true;
        }
        private void TickMapObjectives(float dt)
        {
            var state=MapObjective;if(state.mode=="elimination"||state.status!="active"||Winner>=0)return;
            var definition=ObjectiveDefinition(state.mode);state.elapsed+=dt;
            if(state.mode=="escort_caravan")TickEscortObjective(definition,dt);
            else TickControlObjective(definition,dt);
            if(state.status!="active")return;
            if(state.elapsed+.0001f>=definition.deadline){FinishMapObjective(1-state.player,"Objective deadline expired");return;}
            if(state.elapsed+.0001f>=state.nextProgressAt)
            {state.nextProgressAt=state.elapsed+5;PublicObjectiveEvent("MapObjectiveProgress",state.player,state.mode+": "+state.progress.ToString("0.0")+(state.contested?" contested":""),state.progress);}
        }
        private void TickControlObjective(MapObjectiveDef definition,float dt)
        {
            var state=MapObjective;
            if(state.mode=="capture_harbor"&&(Get(state.target)==null||!Get(state.target).Alive))
            {FinishMapObjective(1-state.player,"The objective harbor was destroyed");return;}
            bool landOnly=state.mode=="defend_bridge";
            bool friendly=ObjectivePresence(state.player,state.center,definition.radius,landOnly:landOnly),hostile=ObjectivePresence(1-state.player,state.center,definition.radius,landOnly:landOnly);
            bool contested=friendly&&hostile;if(state.contested!=contested){state.contested=contested;PublicObjectiveEvent("MapObjectiveContest",state.player,contested?"Objective contested":"Objective uncontested");}
            if(contested)return;
            if(friendly){state.progress=Mathf.Min(definition.holdSeconds,state.progress+dt);state.enemyProgress=0;}
            else if(hostile){state.enemyProgress+=dt;state.progress=0;}
            else {state.progress=0;state.enemyProgress=0;}
            float enemyRequired=state.mode=="defend_bridge"?definition.failureSeconds:definition.holdSeconds;
            if(state.progress+.0001f>=definition.holdSeconds||state.enemyProgress+.0001f>=enemyRequired)
            {
                int winner=state.progress+.0001f>=definition.holdSeconds?state.player:1-state.player;
                if(state.mode=="capture_harbor")
                {var harbor=Get(state.target);harbor.owner=winner;RecomputePopulationCap(winner);PublicObjectiveEvent("MapHarborCaptured",winner,"Harbor captured",definition.holdSeconds);}
                FinishMapObjective(winner,state.mode=="defend_bridge"?(winner==state.player?"Bridge defended":"Bridge overrun"):"Harbor secured");
            }
        }
        private void TickEscortObjective(MapObjectiveDef definition,float dt)
        {
            var state=MapObjective;var cart=Get(state.escort);
            if(cart==null||!cart.Alive){FinishMapObjective(1-state.player,"Escort caravan destroyed");return;}
            bool escorted=ObjectivePresence(state.player,cart.position,definition.radius,cart.id);
            bool threatened=ObjectivePresence(1-state.player,cart.position,definition.radius,cart.id);state.contested=threatened;
            if(!escorted||cart.garrisonedIn!=0)
            {
                if(cart.orders.Count>0){cart.orders.Clear();InvalidateCommandPath(cart);cart.hold=false;}
                if(state.moving)PublicObjectiveEvent("MapEscortWaiting",state.player,"Caravan waits for nearby military escort",state.waypoint);
                state.moving=false;return;
            }
            // A nearby escort may occupy the exact route point. Normal movement
            // then chooses a separated destination. Recognise a real arrival
            // within three metres without requiring overlapping bodies;
            // a wall between the cart and checkpoint must still prevent credit.
            while(state.waypoint<state.route.Length&&
                (cart.position-state.route[state.waypoint]).sqrMagnitude<=9&&
                Navigation.ClearLine(cart.position,state.route[state.waypoint],cart.def.radius))
            {
                state.waypoint++;state.progress=state.waypoint;
                PublicObjectiveEvent("MapEscortCheckpoint",state.player,"Escort checkpoint reached",state.waypoint);
            }
            if(state.waypoint>=state.route.Length){FinishMapObjective(state.player,"Caravan delivered along its complete route");return;}
            Vector2 next=state.route[state.waypoint];var order=cart.orders.Count>0?cart.orders.Peek():null;
            if(order==null||order.kind!=OrderKind.Move||(order.point-next).sqrMagnitude>.0001f||cart.orders.Count>1)
            {
                cart.orders.Clear();InvalidateCommandPath(cart);cart.hold=false;cart.orders.Enqueue(new UnitOrder(OrderKind.Move,next));cart.status="Objective escort";
                if(!state.moving)PublicObjectiveEvent("MapEscortMoving",state.player,"Caravan moving with its escort",state.waypoint);
            }
            state.moving=true;
        }
        public MapObjectiveState CaptureMapObjective()=>SnapshotCodec.Clone(MapObjective);
        public void RestoreMapObjective(MapObjectiveState state){MapObjective=SnapshotCodec.Clone(state);}
        public static void ValidateMapObjectiveSnapshot(RuleCatalog rules,MapObjectiveState state,EntitySnapshot[] entities,string mapId,int winner,int seed)
        {
            void Require(bool valid){if(!valid)throw new ArgumentException("Invalid saved map objective");}
            Require(state!=null&&state.route!=null&&state.player>=0&&state.player<2);
            Require(SnapshotCodec.Finite(state.center)&&SnapshotCodec.Finite(state.elapsed)&&state.elapsed>=0&&SnapshotCodec.Finite(state.progress)&&state.progress>=0&&SnapshotCodec.Finite(state.enemyProgress)&&state.enemyProgress>=0&&SnapshotCodec.Finite(state.nextProgressAt)&&state.nextProgressAt>=0);
            foreach(var point in state.route)Require(SnapshotCodec.Finite(point)&&point.x>=0&&point.x<=128&&point.y>=0&&point.y<=128);
            if(state.mode=="elimination"){Require(state.status=="inactive"&&state.target==0&&state.escort==0&&state.route.Length==0);return;}
            var definition=Array.Find(rules.mapObjectives??Array.Empty<MapObjectiveDef>(),d=>d.id==state.mode);
            Require(definition!=null&&(state.status=="active"||state.status=="won"||state.status=="failed"||state.status=="ended"));
            Require(state.status!="won"||winner==state.player);Require(state.status!="failed"||winner==1-state.player);
            Require(state.status!="ended"||winner==state.player);
            if(state.mode=="escort_caravan")
            {Require(state.route.Length>=2&&state.waypoint>=0&&state.waypoint<=state.route.Length&&state.target==state.escort&&state.progress==state.waypoint&&(state.center-state.route[state.route.Length-1]).sqrMagnitude<.0001f);Require(Array.Exists(entities,e=>e.state.id==state.escort&&e.definition==definition.escortUnit&&e.state.owner==state.player));}
            else if(state.mode=="capture_harbor")Require(MapLayout.Create(mapId,seed).HasWater&&Array.Exists(entities,e=>e.state.id==state.target&&e.definition=="dock"&&(e.state.position-state.center).sqrMagnitude<.0001f)&&state.route.Length==0&&state.escort==0);
            else Require(state.mode=="defend_bridge"&&Array.Exists(MapLayout.Create(mapId,seed).Bridges,b=>(b.Center-state.center).sqrMagnitude<.0001f)&&state.target==0&&state.route.Length==0&&state.escort==0);
        }
    }
}
