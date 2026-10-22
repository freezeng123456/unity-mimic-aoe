using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    // A derived decision, not a second economy or a reservation. A restored AI
    // recomputes it from owned assets, current observations and public terrain.
    public sealed class AiWaterInvestment084
    {
        public sealed class Plan
        {
            public bool survey,buildDock;
            public Vector2 dockPoint;
            public int dockId,fishingTarget;
            public float reachableFish,foodDeficit;
            public EntityState fish;
            public string nextShip="";
            public Cost cost;
        }
        const float FoodWindow=120,MinimumFishingSeconds=60;
        const int MaximumFishCandidates=6,MaximumPathQueries=4;
        readonly World world;readonly int player;
        public AiWaterInvestment084(World world,int player){this.world=world;this.player=player;}

        public Plan Evaluate(List<EntityState> own)
        {
            var result=new Plan();var p=world.Players[player];
            if(!world.Layout.HasWater||p.age<2||!world.IsPlayerActive(player))return result;
            var dangers=VisibleDangers();EntityState dock=null;
            foreach(var e in own)if(e.Alive&&e.owner==player&&e.def.id=="dock"&&(dock==null||e.id<dock.id))dock=e;
            var fishing=world.Rules.Entity("fishing_boat");var gunboat=world.Rules.Entity("gunboat");
            if(fishing==null||gunboat==null)return result;
            bool requiredTransport=world.Layout.RequiresTransport&&world.Rules.Entity("transport_ship")!=null;
            Vector2 origin=Vector2.zero;bool hasOrigin=false,hasFish=false;
            float available=0,trip=float.MaxValue;int budget=MaximumPathQueries;
            if(dock!=null)
            {
                result.dockId=dock.id;result.dockPoint=dock.position;hasOrigin=WaterExit(dock.position,fishing,out origin);
            }
            else
            {
                // Survey one authored shore at a time. Hidden fish depletion and
                // hidden enemy entities never decide which bank to inspect.
                bool unseen=false;
                foreach(var point in world.Layout.DockSites[player])
                {
                    if(world.DockPlacementReason(point)!=""||!PointSafe(point,dangers))continue;
                    if(!WaterExit(point,fishing,out var exit))continue;
                    if(!hasOrigin){result.dockPoint=point;origin=exit;hasOrigin=true;}
                    if(ActiveSurvey(own,point))
                    {result.survey=true;result.dockPoint=point;origin=exit;break;}
                    if(world.Fog.At(player,point)!=VisibilityState.Visible)
                    {
                        if(!unseen){unseen=true;result.survey=true;result.dockPoint=point;origin=exit;}
                        continue;
                    }
                    var probe=new EntityState{def=fishing,owner=player,hp=1,position=exit};
                    var fish=FindFish(probe,dangers,ref budget,out float amount,out float distance);
                    if(fish==null||amount<world.GatherRate(probe,fish)*MinimumFishingSeconds)continue;
                    result.dockPoint=point;origin=exit;result.fish=fish;available=amount;trip=distance;hasFish=true;break;
                }
            }
            if(!hasOrigin)return result;
            var vessel=new EntityState{def=fishing,owner=player,hp=1,position=origin};
            if(!hasFish)result.fish=FindFish(vessel,dangers,ref budget,out available,out trip);
            result.reachableFish=available;
            result.foodDeficit=Mathf.Max(0,FoodTarget(own)-p.resources.food);
            if(result.fish!=null)
            {
                float rate=world.GatherRate(vessel,result.fish);
                float supply=rate*Mathf.Max(0,FoodWindow-trip/Mathf.Max(.1f,world.Speed(vessel)));
                int finite=rate>0?Mathf.FloorToInt(available/(rate*MinimumFishingSeconds)):0;
                if(supply>0&&finite>0)
                    result.fishingTarget=Mathf.Min(4,finite,Math.Max(1,Mathf.CeilToInt(result.foodDeficit/supply)));
            }
            int hostileFleet=0;bool heavyFleet=false,shoreTarget=false;
            foreach(var enemy in world.Entities)
            {
                if(!world.AreEnemies(player,enemy.owner)||!world.IsVisible(player,enemy))continue;
                if(enemy.def.waterborne&&Armed(enemy)&&ThreatensWaterAsset(enemy,own,result.dockPoint))
                {hostileFleet++;heavyFleet|=enemy.def.id=="frigate"||enemy.def.id=="monitor";}
                if(enemy.def.kind==EntityKind.Building&&ReachableShoreTarget(origin,enemy,gunboat,ref budget))shoreTarget=true;
            }
            int defenseGunboats=Mathf.Clamp(hostileFleet,0,2);
            if(Count("gunboat")<defenseGunboats)result.nextShip="gunboat";
            else if((hostileFleet>=2||heavyFleet)&&Available("frigate")&&Count("frigate")<1)result.nextShip="frigate";
            else if(requiredTransport&&Count("transport_ship")==0)result.nextShip="transport_ship";
            else if(result.fishingTarget>Count("fishing_boat"))result.nextShip="fishing_boat";
            else if(shoreTarget&&Count("gunboat")<1)result.nextShip="gunboat";
            else if(shoreTarget&&Available("monitor")&&Count("monitor")<1&&
                own.Exists(e=>e.Alive&&e.owner==player&&e.Complete&&(e.def.id=="gunboat"||e.def.id=="frigate")))result.nextShip="monitor";
            bool purpose=requiredTransport||result.fishingTarget>0||hostileFleet>0||shoreTarget;
            result.buildDock=dock==null&&purpose&&world.Fog.At(player,result.dockPoint)==VisibilityState.Visible;
            result.survey=dock==null&&(result.survey||purpose);
            result.cost=dock==null?result.survey?world.EntityCost(player,world.Rules.Entity("dock")):new Cost():
                result.nextShip!=""?world.UnitCost(player,world.Rules.Entity(result.nextShip)):new Cost();
            return result;
        }

        // The food target describes two ordinary recruitment rounds at existing
        // producers. It excludes this water forecast, so there is no recursion.
        float FoodTarget(List<EntityState> own)
        {
            float food=0;var worker=world.WorkerDefinition(player);
            if(worker!=null&&!world.WorkerSourcesClosed(player)&&world.AutoRecruitSeconds(player)<=0&&
                own.Exists(e=>e.Alive&&e.Complete&&e.owner==player&&e.def.id=="town"))food+=world.UnitCost(player,worker).food*2;
            foreach(var producer in own)
            {
                if(!producer.Alive||!producer.Complete||producer.owner!=player||producer.def.kind!=EntityKind.Building||producer.def.id=="town")continue;
                float next=0;
                foreach(var d in world.Rules.entities)
                    if(d.kind==EntityKind.Unit&&!d.civilian&&!d.scout&&!d.healer&&!d.waterborne&&(d.tags&Tags.Villager)==0&&
                        d.producer==producer.def.id&&world.EntityAvailable(player,d)&&world.UnitAge(player,d)<=world.Players[player].age)
                        next=Mathf.Max(next,world.UnitCost(player,d).food);
                food+=next*2;
            }
            return food;
        }
        bool Available(string id){var d=world.Rules.Entity(id);return d!=null&&world.EntityAvailable(player,d)&&world.UnitAge(player,d)<=world.Players[player].age;}
        int Count(string id)=>world.UnitCountIncludingProduction(player,id);
        bool ActiveSurvey(List<EntityState> own,Vector2 shore)
        {
            foreach(var worker in own)
            {
                if(!worker.Alive||worker.owner!=player||!worker.Worker||worker.def.waterborne||worker.garrisonedIn!=0||worker.orders.Count!=1)continue;
                var order=worker.orders.Peek();if(order.kind!=OrderKind.Move)continue;
                var goal=world.Navigation.NearestWalkable(shore-world.DockWaterDirection(shore)*4,worker.def.radius);
                if((order.point-goal).sqrMagnitude<.01f&&(worker.position-goal).sqrMagnitude>1)return true;
            }
            return false;
        }
        bool WaterExit(Vector2 shore,EntityDef vessel,out Vector2 point)
        {
            var direction=world.DockWaterDirection(shore);point=shore+direction*(World.DockQuayLength+vessel.radius+1.5f);
            return direction.sqrMagnitude>.5f&&world.IsWater(point)&&world.WaterNavigation.IsWalkable(point,vessel.radius);
        }
        bool ThreatensWaterAsset(EntityState enemy,List<EntityState> own,Vector2 bank)
        {
            float reach=Mathf.Max(22,enemy.def.range+8);
            if((enemy.position-bank).sqrMagnitude<reach*reach)return true;
            return own.Exists(e=>e.Alive&&e.owner==player&&e.def.waterborne&&(e.position-enemy.position).sqrMagnitude<reach*reach);
        }
        bool ReachableShoreTarget(Vector2 origin,EntityState enemy,EntityDef vessel,ref int budget)
        {
            var water=world.WaterNavigation.NearestWalkable(enemy.position,vessel.radius);
            var arrival=new EntityState{def=vessel,position=water};
            if(!world.IsWater(water)||world.EdgeDistance(arrival,enemy)>vessel.range||world.EdgeDistance(arrival,enemy)<vessel.minRange)return false;
            return Route(origin,water,vessel.radius,null,ref budget,out _);
        }
        List<EntityState> VisibleDangers()
        {
            var result=new List<EntityState>();
            foreach(var e in world.Entities)if(world.IsVisible(player,e)&&e.owner!=player&&
                AiResourceContinuity072.EconomicThreat(world,player,e)&&Armed(e))result.Add(e);
            result.Sort((a,b)=>a.id.CompareTo(b.id));return result;
        }
        bool Armed(EntityState e)=>e!=null&&e.Alive&&e.Complete&&(e.def.damage>0||e.def.siegeDamage>0)&&
            (e.def.waterborne||!e.forceMelee&&e.def.range>2);
        public bool PointSafe(Vector2 point,List<EntityState> dangers)=>SegmentSafe(point,point,dangers);
        bool SegmentSafe(Vector2 from,Vector2 to,List<EntityState> dangers)
        {
            if(dangers==null)return true;var delta=to-from;float length=delta.sqrMagnitude;
            foreach(var e in dangers)
            {
                if(!world.IsVisible(player,e)||!Armed(e)||!AiResourceContinuity072.EconomicThreat(world,player,e))continue;
                float t=length>0?Mathf.Clamp01(Vector2.Dot(e.position-from,delta)/length):0;
                float reach=Mathf.Max(8,e.def.range+4)+e.def.radius;
                if((e.position-from-delta*t).sqrMagnitude<reach*reach)return false;
            }
            return true;
        }
        bool Route(Vector2 from,Vector2 goal,float radius,List<EntityState> dangers,ref int budget,out float length)
        {
            length=0;var nav=world.WaterNavigation;
            if(!nav.IsWalkable(from,radius)||!nav.IsWalkable(goal,radius)||!PointSafe(from,dangers)||!PointSafe(goal,dangers))return false;
            if(nav.ClearLine(from,goal,radius))
            {length=Vector2.Distance(from,goal);return SegmentSafe(from,goal,dangers);}
            if(budget<=0)return false;budget--;
            var path=nav.FindPath(from,goal,radius);
            if(path.Count<2||(path[0]-from).sqrMagnitude>=.01f||(path[path.Count-1]-goal).sqrMagnitude>=.01f)return false;
            for(int i=1;i<path.Count;i++)
            {
                if(!nav.ClearLine(path[i-1],path[i],radius)||!SegmentSafe(path[i-1],path[i],dangers))return false;
                length+=Vector2.Distance(path[i-1],path[i]);
            }
            return true;
        }
        public bool WaterRouteSafe(EntityState boat,Vector2 point,List<EntityState> dangers)
        {int budget=1;return Route(boat.position,point,boat.def.radius,dangers,ref budget,out _);}
        // Escape has a different admission contract from an investment/Gather:
        // a boat already under a known gun must be able to leave its coverage.
        // The goal is outside every weapon; all static geometry remains exact.
        // No ordinary fishing route calls this exception.
        public bool EscapeWaterRouteSafe(EntityState boat,Vector2 goal,List<EntityState> dangers)
        {
            if(boat==null||!boat.Alive||boat.owner!=player||!boat.def.waterborne||boat.garrisonedIn!=0)return false;
            var nav=world.WaterNavigation;float radius=boat.def.radius;Vector2 from=boat.position;
            List<Vector2> path=null;
            var order=boat.orders.Count>0?boat.orders.Peek():null;
            if(order!=null&&order.kind==OrderKind.Move&&(order.point-goal).sqrMagnitude<=1)
            {
                // Retreat's caller keeps this existing Move. Reissuing an equal
                // Move would also retain its path, so a safe fresh alternative
                // is not enough to authorize that saved corridor.
                if(boat.detourUntil>world.Time)return false;
                goal=order.hasResolvedPoint?order.resolvedPoint:order.point;
                if(boat.pathVersion==nav.Version&&(boat.pathGoal-goal).sqrMagnitude<=4&&boat.pathIndex<boat.path.Count)
                {
                    path=new List<Vector2>{from};
                    for(int i=boat.pathIndex;i<boat.path.Count;i++)path.Add(boat.path[i]);
                }
            }
            if(!nav.IsWalkable(from,radius)||!nav.IsWalkable(goal,radius)||!PointSafe(goal,dangers))return false;
            if(path==null)path=nav.ClearLine(from,goal,radius)?new List<Vector2>{from,goal}:nav.FindPath(from,goal,radius);
            if(path.Count<2||(path[0]-from).sqrMagnitude>=.01f||(path[path.Count-1]-goal).sqrMagnitude>=.01f)return false;
            for(int i=1;i<path.Count;i++)if(!nav.ClearLine(path[i-1],path[i],radius))return false;
            if(dangers==null)return true;
            for(int i=1;i<path.Count;i++)
            {
                // MoveCore may skip saved corners that are now visible from
                // this position. A departed ally does not change Nav.Version;
                // validating only the old adjacent legs would miss that shortcut.
                bool shortcut=i>1&&nav.ClearLine(from,path[i],radius);
                foreach(var enemy in dangers)
                {
                    if(!world.IsVisible(player,enemy)||!Armed(enemy)||!AiResourceContinuity072.EconomicThreat(world,player,enemy))continue;
                    double reach=Mathf.Max(8,enemy.def.range+4)+enemy.def.radius,limit=reach*reach;
                    if(!EscapeLegSafe(path[i-1],path[i],enemy.position,limit)||
                        shortcut&&!EscapeLegSafe(from,path[i],enemy.position,limit))return false;
                }
            }
            return true;
        }
        static bool EscapeLegSafe(Vector2 from,Vector2 to,Vector2 enemy,double limit)
        {
            double x=(double)from.x-enemy.x,y=(double)from.y-enemy.y;
            double dx=(double)to.x-from.x,dy=(double)to.y-from.y;
            double length=dx*dx+dy*dy;if(length==0)return true;
            double dot=x*dx+y*dy,start=x*x+y*y,end=(x+dx)*(x+dx)+(y+dy)*(y+dy);
            if(start>=limit)
            {
                double t=Math.Max(0,Math.Min(1,-dot/length));
                return (x+dx*t)*(x+dx*t)+(y+dy*t)*(y+dy*t)>=limit;
            }
            // Squared distance along a straight leg is convex. A nonnegative
            // initial derivative proves no closer point on this entire leg.
            return dot>=0&&end>start;
        }
        public bool FishingRouteSafe(EntityState boat,EntityState fish,List<EntityState> dangers)
        {
            if(boat==null||fish==null||!world.CanGather(boat,fish)||!world.GatherableResource(player,fish))return false;
            var goal=world.WorkPoint(fish,boat);var arrival=new EntityState{def=boat.def,position=goal};int budget=1;
            if(world.EdgeDistance(arrival,fish)>1.5f||!Route(boat.position,goal,boat.def.radius,dangers,ref budget,out _))return false;
            var order=boat.orders.Count>0?boat.orders.Peek():null;
            if(order!=null&&order.kind==OrderKind.Gather&&order.target==fish.id)
            {
                var previous=boat.position;
                for(int i=boat.pathIndex;i<boat.path.Count;i++){if(!SegmentSafe(previous,boat.path[i],dangers))return false;previous=boat.path[i];}
                if(boat.detourUntil>world.Time&&!Route(boat.position,boat.detour,boat.def.radius,dangers,ref budget,out _))return false;
            }
            return true;
        }
        public EntityState NearestFish(EntityState boat,List<EntityState> dangers,out float available,out float trip)
        {
            int budget=2;return FindFish(boat,dangers,ref budget,out available,out trip);
        }
        EntityState FindFish(EntityState boat,List<EntityState> dangers,ref int budget,out float available,out float trip)
        {
            available=0;trip=float.MaxValue;var candidates=new List<EntityState>();
            foreach(var fish in world.Entities)if(fish.def.id=="fish"&&world.GatherableResource(player,fish)&&PointSafe(fish.position,dangers))candidates.Add(fish);
            candidates.Sort((a,b)=>{int d=(a.position-boat.position).sqrMagnitude.CompareTo((b.position-boat.position).sqrMagnitude);return d!=0?d:a.id.CompareTo(b.id);});
            EntityState result=null;
            for(int i=0;i<Math.Min(MaximumFishCandidates,candidates.Count);i++)
            {
                var fish=candidates[i];var goal=world.WorkPoint(fish,boat);var arrival=new EntityState{def=boat.def,position=goal};
                if(world.EdgeDistance(arrival,fish)>1.5f||!Route(boat.position,goal,boat.def.radius,dangers,ref budget,out float distance))continue;
                available+=fish.remaining;
                if(distance<trip||distance==trip&&(result==null||fish.id<result.id)){result=fish;trip=distance;}
            }
            return result;
        }
    }
}
