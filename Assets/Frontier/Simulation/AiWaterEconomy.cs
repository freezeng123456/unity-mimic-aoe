using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    // The river economy has no alternate resource ledger or privileged spawns.
    // Infrastructure, recruitment and targeting all go through human commands.
    public sealed class AiWaterEconomy
    {
        private readonly World world;
        private readonly int player;
        private readonly AiResourceContinuity072 continuity;
        private readonly AiNavalOperations navalOperations;
        private readonly AiWaterInvestment084 investment;
        private int surveyor;
        private float patrolAt;
        public AiWaterEconomy(World world,int player,AiResourceContinuity072 continuity=null)
        {this.world=world;this.player=player;this.continuity=continuity??new AiResourceContinuity072(world,player);navalOperations=new AiNavalOperations(world,player,this.continuity);investment=new AiWaterInvestment084(world,player);}
        public void BindBridgeArmy(AiArmyPlan072 army,AiRevolution071 revolution,AiMedicalSupport076 medical)=>navalOperations.BindBridgeArmy(army,revolution,medical);
        public void ValidateBridgeState()=>navalOperations.ValidateBridgeState();
        public AiWaterEconomySnapshot CaptureState()=>new AiWaterEconomySnapshot{surveyor=surveyor,patrolAt=patrolAt,navalOperations=navalOperations.CaptureState()};
        public void RestoreState(AiWaterEconomySnapshot state)
        {
            // A surveyor may have died since the last decision; the next normal survey validates it.
            AiSaveValidation.Require(state!=null&&state.surveyor>=0&&AiSaveValidation.Clock(state.patrolAt),"water economy state");
            surveyor=state.surveyor;patrolAt=state.patrolAt;navalOperations.RestoreState(state.navalOperations);
        }
        public Vector2 LandArmyDestination(EntityState unit,Vector2 destination)
        {
            if(!world.Layout.RequiresTransport||unit==null||!unit.Alive||unit.def.waterborne||unit.garrisonedIn!=0)return destination;
            int region=world.Layout.LandRegion(unit.position);
            // Terrain is public knowledge. Being on another island never gives
            // this controller an enemy entity or an unseen target position.
            if(region!=0&&!world.IsWater(destination)&&world.Layout.LandRegion(destination)==region)return destination;
            if((unit.def.tags&Tags.Explorer)!=0)
            {
                Vector2 chosen=unit.position;float farthest=-1;
                for(int p=0;p<world.Layout.LandingSites.Length;p++)
                {
                    foreach(var shore in world.Layout.LandingSites[p])Scout(shore);
                    foreach(var shore in world.Layout.DockSites[p])Scout(shore);
                }
                foreach(var shore in world.Layout.IslandLandingSites)Scout(shore);
                if(farthest>=0)return chosen;
                void Scout(Vector2 shore)
                {
                    if(world.Layout.LandRegion(shore)!=region)return;
                    Vector2 point=SafeRegionPoint(unit,shore-world.Layout.WaterDirection(shore)*6,region,0);
                    float distance=(point-unit.position).sqrMagnitude;
                    if(distance>farthest){farthest=distance;chosen=point;}
                }
            }
            Vector2 rally=unit.position;
            for(int p=0;p<world.Players.Length;p++)
            {
                Vector2 town=world.OpeningTownPosition(p);
                if(world.Layout.LandRegion(town)!=region)continue;
                float direction=p==0?1:-1;rally=town+new Vector2(direction*10,direction*10);break;
            }
            // Leave room for the normal army's formation offsets. A bounded
            // local search avoids launching a disconnected full-map path query.
            return SafeRegionPoint(unit,rally,region,3.75f);
        }
        private Vector2 SafeRegionPoint(EntityState unit,Vector2 center,int region,float margin)
        {
            for(int pass=0;pass<(margin>0?2:1);pass++)
            {
                float radius=unit.def.radius+(pass==0?margin:0);
                for(int ring=0;ring<=8;ring++)for(int y=-ring;y<=ring;y++)for(int x=-ring;x<=ring;x++)
                {
                    if(ring>0&&Mathf.Abs(x)!=ring&&Mathf.Abs(y)!=ring)continue;
                    Vector2 point=center+new Vector2(x,y);
                    if(world.Layout.LandRegion(point)==region&&!world.IsWater(point)&&world.Navigation.IsWalkable(point,radius))return point;
                }
            }
            return unit.position;
        }
        public void Tick(List<EntityState> own,List<EntityState> workers,List<EntityState> visibleEnemies,Cost reserve,bool saving,bool underAttack,List<EntityState> economicThreats=null)
        {
            if(!world.Layout.HasWater||world.Paused||world.IsMatchEnded||!world.IsPlayerActive(player))return;
            var safety=economicThreats??visibleEnemies;
            navalOperations.Tick(own,visibleEnemies,underAttack);
            AssignFishing(own,safety);
            CommandFleet(own,visibleEnemies);
            if(world.Players[player].age<2||saving||underAttack)return;
            var plan=investment.Evaluate(own);var dock=world.Get(plan.dockId);
            if(dock==null)
            {
                if(plan.survey)SurveyAndBuild(workers,safety,plan.dockPoint,plan.buildDock&&CanSpend(plan.cost,reserve));
                return;
            }
            if(!dock.Complete||dock.queue.Count>0)return;
            string next=plan.nextShip;
            if(next==""||world.ProductionReason(player,dock,next)!=""||!CanSpend(plan.cost,reserve))return;
            if(next=="fishing_boat"&&plan.fish!=null)Send(new GameCommand{kind=CommandKind.Rally,actors=new[]{dock.id},target=plan.fish.id});
            Send(new GameCommand{kind=CommandKind.Train,actors=new[]{dock.id},item=next});
        }
        public Cost Forecast(List<EntityState> own,bool saving)
        {
            if(!world.Layout.HasWater||world.Players[player].age<2||saving)return new Cost();
            return investment.Evaluate(own).cost;
        }
        private void SurveyAndBuild(List<EntityState> workers,List<EntityState> visible,Vector2 site,bool build)
        {
            // Public terrain is known; occupants and construction authority still
            // require current vision. A settler must physically survey the bank.
            EntityState worker=world.Get(surveyor);
            if(worker==null||!worker.Alive||worker.owner!=player||worker.garrisonedIn!=0||!worker.Worker||worker.def.waterborne)worker=null;
            if(worker!=null&&!AiResourceContinuity072.Safe(world,player,worker.position,visible))
            {
                if(worker.orders.Count==1&&worker.orders.Peek().kind==OrderKind.Move)
                    Send(new GameCommand{kind=CommandKind.Order,actors=new[]{worker.id},order=OrderKind.Stop});
                surveyor=0;return;
            }
            var sites=new[]{site};
            if(worker==null)
            {
                float best=float.MaxValue;
                foreach(var candidate in workers)
                {
                    if(!AiCivilizationEconomy.CanReassign(candidate)||!AiResourceContinuity072.Safe(world,player,candidate.position,visible))continue;
                    float d=(candidate.position-sites[0]).sqrMagnitude;
                    if(d<best){best=d;worker=candidate;}
                }
                if(worker==null)return;surveyor=worker.id;
            }
            foreach(var point in sites)
            {
                if(world.DockPlacementReason(point)!=""||!AiResourceContinuity072.Safe(world,player,point,visible))continue;
                if(world.Fog.At(player,point)!=VisibilityState.Visible)
                {
                    Vector2 survey=world.Navigation.NearestWalkable(point-world.DockWaterDirection(point)*4,worker.def.radius);
                    // A Move command uses the ordinary physical route. Check
                    // that route against current weapons rather than assuming
                    // an alternate safe route would become the commanded one.
                    var path=world.Navigation.FindPath(worker.position,survey,worker.def.radius);
                    bool safe=path.Count>=2&&(path[path.Count-1]-survey).sqrMagnitude<.01f;
                    for(int i=1;i<path.Count&&safe;i++)safe=AiResourceContinuity072.SegmentSafe(world,player,path[i-1],path[i],visible);
                    if(!safe)
                    {
                        if(worker.orders.Count==1&&worker.orders.Peek().kind==OrderKind.Move)
                            Send(new GameCommand{kind=CommandKind.Order,actors=new[]{worker.id},order=OrderKind.Stop});
                        continue;
                    }
                    if(worker.orders.Count==0||worker.orders.Peek().kind==OrderKind.Gather||worker.orders.Peek().kind==OrderKind.Move&&(worker.orders.Peek().point-survey).sqrMagnitude>1)
                        Send(new GameCommand{kind=CommandKind.Order,actors=new[]{worker.id},order=OrderKind.Move,point=survey});
                    return;
                }
                if(!build)return;
                if(world.CanBuild(player,"dock",point)!="")continue;
                if(!AiResourceContinuity072.TryConstructionWorker(world,player,world.Rules.Entity("dock"),point,
                    new List<EntityState>{worker},visible,out _))continue;
                if(Send(new GameCommand{kind=CommandKind.Build,actors=new[]{worker.id},item="dock",point=point}).accepted)surveyor=0;
                return;
            }
        }
        private void AssignFishing(List<EntityState> own,List<EntityState> enemies)
        {
            foreach(var boat in own)
            {
                if(!boat.Alive||!boat.def.fishingBoat)continue;
                bool dangerousGather=false;
                if(boat.orders.Count>0&&boat.orders.Peek().kind==OrderKind.Gather)
                {
                    var current=world.Get(boat.orders.Peek().target);
                    dangerousGather=!investment.FishingRouteSafe(boat,current,enemies);
                    if(!dangerousGather)continue;
                }
                var fish=investment.NearestFish(boat,enemies,out _,out _);
                if(fish!=null)
                {
                    if(dangerousGather||boat.orders.Count==0||boat.orders.Peek().kind!=OrderKind.Gather||boat.orders.Peek().target!=fish.id)
                        Send(new GameCommand{kind=CommandKind.Order,actors=new[]{boat.id},order=OrderKind.Gather,target=fish.id});
                }
                else if(dangerousGather||Threatened(boat.position,enemies))RetreatFishingBoat(boat,own,enemies);
            }
        }

        private bool Threatened(Vector2 point,List<EntityState> enemies)
        {
            foreach(var enemy in enemies)
                if(world.IsVisible(player,enemy)&&enemy.Military&&enemy.def.damage>0&&(enemy.def.waterborne||!enemy.forceMelee&&enemy.def.range>2)&&
                    (enemy.position-point).sqrMagnitude<Mathf.Pow(Mathf.Max(8,enemy.def.range+4),2))return true;
            return false;
        }

        private void RetreatFishingBoat(EntityState boat,List<EntityState> own,List<EntityState> enemies)
        {
            Vector2 goal=Vector2.zero;float best=float.MaxValue;
            var candidates=new List<Vector2>(world.Layout.PatrolPoints);
            foreach(var dock in own)
                if(dock.Alive&&dock.Complete&&dock.def.id=="dock")
                {
                    var direction=world.DockWaterDirection(dock.position);
                    if(direction!=Vector2.zero)candidates.Add(dock.position+direction*(World.DockQuayLength+boat.def.radius+1.5f));
                }
            foreach(var candidate in candidates)
            {
                if(Threatened(candidate,enemies)||!investment.EscapeWaterRouteSafe(boat,candidate,enemies))continue;
                float distance=(candidate-boat.position).sqrMagnitude;if(distance<best){goal=candidate;best=distance;}
            }
            if(best==float.MaxValue)
            {
                if(boat.orders.Count>0)Send(new GameCommand{kind=CommandKind.Order,actors=new[]{boat.id},order=OrderKind.Stop});
                return;
            }
            if(boat.orders.Count==0||boat.orders.Peek().kind!=OrderKind.Move||(boat.orders.Peek().point-goal).sqrMagnitude>1)
                Send(new GameCommand{kind=CommandKind.Order,actors=new[]{boat.id},order=OrderKind.Move,point=goal});
        }
        private void CommandFleet(List<EntityState> own,List<EntityState> enemies)
        {
            if(world.Time<patrolAt)return;patrolAt=world.Time+5;
            foreach(var boat in own)
            {
                if(!boat.Alive||!boat.def.waterborne||!boat.Military||boat.def.transportShip)continue;
                if(boat.orders.Count>0&&boat.orders.Peek().kind==OrderKind.DockRepair)continue;
                if(boat.hp<world.MaxHp(boat)*.45f&&AiResourceContinuity072.MayRepair(world,player,boat))
                {
                    var repairDock=own.Find(e=>e.Alive&&e.Complete&&e.def.id=="dock"&&!Threatened(e.position,enemies));
                    if(repairDock!=null&&Send(new GameCommand{kind=CommandKind.DockRepair,actors=new[]{boat.id},target=repairDock.id}).accepted)continue;
                }
                EntityState target=null;float best=float.MaxValue;
                foreach(var enemy in enemies)
                {
                    if(!world.IsVisible(player,enemy)||enemy.def.kind==EntityKind.Resource)continue;
                    // Only bombard banks in reach of navigable water. Inland
                    // enemies remain the land army's responsibility.
                    Vector2 water=world.NavigationFor(boat).NearestWalkable(enemy.position,boat.def.radius);
                    if(!world.IsWater(water)||Vector2.Distance(water,enemy.position)>boat.def.range+enemy.def.radius)continue;
                    float distance=(enemy.position-boat.position).sqrMagnitude;
                    if(distance<best){best=distance;target=enemy;}
                }
                if(target!=null)
                {
                    if(boat.orders.Count==0||boat.orders.Peek().kind!=OrderKind.Attack||boat.orders.Peek().target!=target.id)
                        Send(new GameCommand{kind=CommandKind.Order,actors=new[]{boat.id},order=OrderKind.Attack,target=target.id});
                }
                else if(boat.orders.Count==0)
                {
                    // Follow authored channel points, including branch heads
                    // and river mouths, without projecting patrols onto land.
                    Vector2 patrol=boat.position;float farthest=-1;
                    foreach(var candidate in world.Layout.PatrolPoints)
                    {
                        if(!world.NavigationFor(boat).IsWalkable(candidate,boat.def.radius))continue;
                        float distance=(candidate-boat.position).sqrMagnitude;
                        if(distance>farthest){farthest=distance;patrol=candidate;}
                    }
                    if(farthest>4)Send(new GameCommand{kind=CommandKind.Order,actors=new[]{boat.id},order=OrderKind.AttackMove,point=patrol});
                }
            }
        }
        private bool CanSpend(Cost cost,Cost reserve)
        {
            if(!world.CanAfford(player,cost))return false;
            return AiResourceContinuity072.CanSpend(world,player,cost,continuity.EnsureProtectedReserve(reserve));
        }
        private CommandResult Send(GameCommand command)
        {command.player=player;var result=world.Issue(command);if(result.accepted)continuity.InvalidateSourceSurvey();return result;}
    }
}
