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
        private int surveyor;
        private float patrolAt;
        public AiWaterEconomy(World world,int player){this.world=world;this.player=player;}
        public void Tick(List<EntityState> own,List<EntityState> workers,List<EntityState> visibleEnemies,Cost reserve,bool saving,bool underAttack)
        {
            if(world.MapId!="river"||world.Paused||world.Winner>=0)return;
            var dock=own.Find(e=>e.Alive&&e.def.id=="dock");
            AssignFishing(own,visibleEnemies);
            CommandFleet(own,visibleEnemies);
            if(world.Players[player].age<2||saving||underAttack)return;
            if(dock==null)
            {
                if(!CanSpend(world.EntityCost(player,world.Rules.Entity("dock")),reserve))return;
                SurveyAndBuild(workers);return;
            }
            if(!dock.Complete||dock.queue.Count>0)return;
            int fishing=world.UnitCountIncludingProduction(player,"fishing_boat"),gunboats=world.UnitCountIncludingProduction(player,"gunboat");
            var fish=NearestFish(dock.position,visibleEnemies);
            string next=fishing<2&&fish!=null?"fishing_boat":gunboats<2?"gunboat":"";
            if(next==""||!CanSpend(world.UnitCost(player,world.Rules.Entity(next)),reserve))return;
            if(fish!=null)Send(new GameCommand{kind=CommandKind.Rally,actors=new[]{dock.id},target=fish.id});
            Send(new GameCommand{kind=CommandKind.Train,actors=new[]{dock.id},item=next});
        }
        public Cost Forecast(List<EntityState> own,bool saving)
        {
            if(world.MapId!="river"||world.Players[player].age<2||saving)return new Cost();
            if(!own.Exists(e=>e.Alive&&e.def.id=="dock"))return world.EntityCost(player,world.Rules.Entity("dock"));
            var dock=own.Find(e=>e.Alive&&e.def.id=="dock");
            if(world.UnitCountIncludingProduction(player,"fishing_boat")<2&&dock!=null&&NearestFish(dock.position,new List<EntityState>())!=null)return world.UnitCost(player,world.Rules.Entity("fishing_boat"));
            return world.UnitCountIncludingProduction(player,"gunboat")<2?world.UnitCost(player,world.Rules.Entity("gunboat")):new Cost();
        }
        private void SurveyAndBuild(List<EntityState> workers)
        {
            // Public terrain is known; occupants and construction authority still
            // require current vision. A settler must physically survey the bank.
            EntityState worker=world.Get(surveyor);
            if(worker==null||!worker.Alive||worker.owner!=player||worker.garrisonedIn!=0||!worker.Worker||worker.def.waterborne)worker=null;
            float shoreX=player==0?55:73,preferredY=player==0?28:100;
            if(worker==null)
            {
                float best=float.MaxValue;
                foreach(var candidate in workers)
                {
                    if(!AiCivilizationEconomy.CanReassign(candidate))continue;
                    float d=(candidate.position-new Vector2(shoreX,preferredY)).sqrMagnitude;
                    if(d<best){best=d;worker=candidate;}
                }
                if(worker==null)return;surveyor=worker.id;
            }
            for(int step=0;step<13;step++)
            {
                int offset=step==0?0:(step+1)/2*(step%2==1?1:-1);
                Vector2 point=new Vector2(shoreX,preferredY+offset*4);
                if(world.DockPlacementReason(point)!="")continue;
                if(world.Fog.At(player,point)!=VisibilityState.Visible)
                {
                    if(worker.orders.Count==0||worker.orders.Peek().kind==OrderKind.Gather)
                        Send(new GameCommand{kind=CommandKind.Order,actors=new[]{worker.id},order=OrderKind.Move,point=point+new Vector2(player==0?-4:4,0)});
                    return;
                }
                if(world.CanBuild(player,"dock",point)!="")continue;
                if(Send(new GameCommand{kind=CommandKind.Build,actors=new[]{worker.id},item="dock",point=point}).accepted)surveyor=0;
                return;
            }
        }
        private EntityState NearestFish(Vector2 point,List<EntityState> enemies)
        {
            EntityState chosen=null;float nearest=float.MaxValue;
            foreach(var fish in world.Entities)
            {
                if(fish.def.id!="fish"||!world.GatherableResource(player,fish))continue;
                bool dangerous=false;
                foreach(var enemy in enemies)
                    if(world.IsVisible(player,enemy)&&enemy.Military&&(enemy.position-fish.position).sqrMagnitude<Mathf.Pow(Mathf.Max(8,enemy.def.range+3),2)){dangerous=true;break;}
                if(dangerous)continue;
                float distance=(fish.position-point).sqrMagnitude;
                if(distance<nearest){nearest=distance;chosen=fish;}
            }
            return chosen;
        }
        private void AssignFishing(List<EntityState> own,List<EntityState> enemies)
        {
            foreach(var boat in own)
            {
                if(!boat.Alive||!boat.def.fishingBoat)continue;
                if(boat.orders.Count>0&&boat.orders.Peek().kind==OrderKind.Gather&&world.CanGather(boat,world.Get(boat.orders.Peek().target)))continue;
                var fish=NearestFish(boat.position,enemies);
                if(fish!=null)Send(new GameCommand{kind=CommandKind.Order,actors=new[]{boat.id},order=OrderKind.Gather,target=fish.id});
            }
        }
        private void CommandFleet(List<EntityState> own,List<EntityState> enemies)
        {
            if(world.Time<patrolAt)return;patrolAt=world.Time+5;
            foreach(var boat in own)
            {
                if(!boat.Alive||!boat.def.waterborne||!boat.Military)continue;
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
                    Vector2 patrol=new Vector2(64,boat.position.y<64?100:28);
                    Send(new GameCommand{kind=CommandKind.Order,actors=new[]{boat.id},order=OrderKind.AttackMove,point=patrol});
                }
            }
        }
        private bool CanSpend(Cost cost,Cost reserve)
        {
            if(!world.CanAfford(player,cost))return false;var r=world.Players[player].resources;
            return r.food-cost.food>=Math.Min(r.food,reserve.food)&&r.wood-cost.wood>=Math.Min(r.wood,reserve.wood)&&r.coin-cost.coin>=Math.Min(r.coin,reserve.coin);
        }
        private CommandResult Send(GameCommand command){command.player=player;return world.Issue(command);}
    }
}
