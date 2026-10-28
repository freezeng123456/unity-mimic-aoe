using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    // Political strategy is derived from the frozen match seed/profile and the
    // world's own paid queues/phase. There is no extra controller state to lose
    // on cold recovery, and no hidden enemy economy or gifted resources.
    public sealed class AiRevolution071
    {
        readonly World world;
        readonly int player;
        readonly AiDifficultyProfile difficulty;
        public AiRevolution071(World world,int player,AiDifficultyProfile difficulty)
        {this.world=world;this.player=player;this.difficulty=difficulty;}
        public bool PrefersRevolution=>((world.Seed^player^(difficulty.Id=="hard"?1:0))&1)==0;
        public bool AllowsAgeAdvance=>world.Players[player].politicalPhase==PoliticalPhase.Empire&&!world.WorkerSourcesClosed(player)&&
            !(world.Players[player].age==4&&world.MaxAge==5&&world.RevolutionDefinition(player)!=null&&PrefersRevolution);
        public Cost StarterWorkerCost=>world.AutoRecruitSeconds(player)>0?new Cost():world.UnitCost(player,world.WorkerDefinition(player));
        public bool NeedsFirstCivilian
        {
            get
            {
                if(world.Players[player].politicalPhase!=PoliticalPhase.CivilianRebuilt)return false;
                foreach(var e in world.Entities)if(e.Alive&&e.owner==player)
                {
                    if(e.Worker&&!e.def.waterborne)return false;
                    foreach(var job in e.queue)if(job.kind=="Train"||job.kind=="Gift")
                    {
                        var produced=world.Rules.Entity(job.item);
                        if(produced!=null&&(produced.tags&Tags.Villager)!=0&&!produced.waterborne)return false;
                    }
                }
                return true;
            }
        }
        public Cost FirstWorkerReserve
        {
            get
            {
                if(world.Players[player].politicalPhase==PoliticalPhase.CivilianRebuilt&&!NeedsFirstCivilian)return new Cost();
                return StarterWorkerCost;
            }
        }
        public bool NeedsFirstPaidWorker=>NeedsFirstCivilian&&world.AutoRecruitSeconds(player)<=0;
        public Cost Reserve
        {
            get
            {
                var p=world.Players[player];
                if(p.politicalPhase==PoliticalPhase.CivilianRebuilt)return FirstWorkerReserve;
                if(p.politicalPhase==PoliticalPhase.Revolutionary||world.WorkerSourcesClosed(player))
                {
                    foreach(var e in world.Entities)if(e.Alive&&e.owner==player)
                        foreach(var job in e.queue)if(job.kind=="Research"&&job.item==World.CivilianRebuildingTechnology)return FirstWorkerReserve;
                    return (world.Rules.Tech(World.CivilianRebuildingTechnology)?.cost??new Cost())+FirstWorkerReserve;
                }
                if(!PrefersRevolution||p.politicalPhase!=PoliticalPhase.Empire||p.age!=4||world.MaxAge!=5||world.WorkerSourcesClosed(player))return new Cost();
                var rule=world.RevolutionDefinition(player);if(rule==null)return new Cost();
                int workers=0,soldiers=0,raiders=0;EntityState town=null;
                Vector2 home=world.OpeningTownPosition(player);
                foreach(var e in world.Entities)
                {
                    if(!e.Alive)continue;
                    if(e.owner==player)
                    {
                        if(e.Worker&&!e.def.waterborne)workers++;
                        else if(e.Military&&!e.def.waterborne&&(e.def.tags&Tags.Explorer)==0)soldiers++;
                        if(e.def.id=="town"&&e.Complete&&town==null)town=e;
                    }
                    else if(world.AreEnemies(player,e.owner)&&e.Military&&(e.def.tags&Tags.Explorer)==0&&world.IsVisible(player,e)&&
                        (e.position-home).sqrMagnitude<34*34)raiders++;
                }
                // Keep enough ordinary economy and a field army before giving up
                // all civilian extraction. Visible pressure releases the reserve
                // for defense rather than forcing a ninety-second commitment.
                if(town==null||workers<Math.Min(30,world.WorkerLimit(player))||soldiers<8||raiders>0&&soldiers<raiders*2+6)return new Cost();
                foreach(var e in world.Entities)if(e.Alive&&e.owner==player)
                    foreach(var job in e.queue)if(job.kind=="Age"||job.kind=="Revolution")return new Cost();
                // Pay the revolution only after its civilian recovery is funded.
                // This avoids depending on future shipment tokens or converting
                // the last extractors with no usable coin source afterwards.
                return rule.cost+(world.Rules.Tech(World.CivilianRebuildingTechnology)?.cost??new Cost())+StarterWorkerCost;
            }
        }
        public bool TryCommit(EntityState town)
        {
            if(town==null||!town.Alive||!town.Complete||town.queue.Count>0||world.Paused||world.IsMatchEnded||!world.IsPlayerActive(player))return false;
            if(world.Players[player].politicalPhase==PoliticalPhase.CivilianRebuilt)return false;
            var reserve=Reserve;if(reserve.food+reserve.wood+reserve.coin<=0||!world.CanAfford(player,reserve))return false;
            bool rebuilding=world.Players[player].politicalPhase==PoliticalPhase.Revolutionary;
            if(rebuilding)
            {
                if(world.ProductionReason(player,town,World.CivilianRebuildingTechnology,true)!="")return false;
                if(!world.Issue(new GameCommand{player=player,kind=CommandKind.Research,actors=new[]{town.id},item=World.CivilianRebuildingTechnology}).accepted)return false;
            }
            else
            {
                if(world.RevolutionReason(player,town)!="")return false;
                if(!world.Issue(new GameCommand{player=player,kind=CommandKind.Revolution,actors=new[]{town.id}}).accepted)return false;
            }
            world.Emit("AIStrategy",player,town.id,rebuilding?"Paid civilian rebuilding; retain revolutionary soldiers":"Paid revolution instead of age V");
            return true;
        }
        public bool TryFreeCivilianSeat(EntityState town,List<EntityState> visibleEnemies)
        {
            if(!NeedsFirstCivilian||town==null||!town.Alive||town.owner!=player||town.def.id!="town"||!town.Complete||town.queue.Count>0||
                world.Paused||world.IsMatchEnded||!world.IsPlayerActive(player)||!world.CanAfford(player,StarterWorkerCost))return false;
            if(visibleEnemies.Exists(e=>e.Alive&&e.Military&&world.IsVisible(player,e)&&(e.position-town.position).sqrMagnitude<34*34))return false;
            var worker=world.WorkerDefinition(player);var p=world.Players[player];
            int missing=worker.population-(Math.Min(p.populationCap,world.Rules.populationLimit)-p.usedPopulation-p.reservedPopulation);
            if(missing<=0)return false;
            string reason=world.AutoRecruitSeconds(player)>0?world.AutoRecruitReason(town):world.ProductionReason(player,town,worker.id);
            if(reason!="Need more housing")return false;
            EntityState weakest=null;
            foreach(var unit in world.Entities)
            {
                if(!unit.Alive||unit.owner!=player||unit.def.kind!=EntityKind.Unit||!unit.def.revolutionary||unit.def.waterborne||
                    unit.garrisonedIn!=0||unit.def.population!=1||!Safe(unit.position,visibleEnemies))continue;
                if(weakest==null||unit.hp<weakest.hp||unit.hp==weakest.hp&&unit.id<weakest.id)weakest=unit;
            }
            if(weakest==null||!world.Issue(new GameCommand{player=player,kind=CommandKind.Disband,actors=new[]{weakest.id}}).accepted)return false;
            world.Emit("AIStrategy",player,weakest.id,"Freed one population seat for the funded first civilian");
            return true;
        }
        bool Safe(Vector2 point,List<EntityState> visibleEnemies)
            =>!visibleEnemies.Exists(e=>e.Alive&&e.Military&&(e.def.tags&Tags.Explorer)==0&&world.IsVisible(player,e)&&
                (e.position-point).sqrMagnitude<Mathf.Pow(Mathf.Max(12,e.def.range+4),2));
        public bool CollectingOwnCrate(EntityState unit,List<EntityState> visibleEnemies)
        {
            if(unit==null||!unit.Alive||unit.owner!=player||!unit.def.revolutionary||unit.orders.Count==0||unit.orders.Peek().kind!=OrderKind.Gather)return false;
            var crate=world.Get(unit.orders.Peek().target);
            return world.RevolutionaryCanCollect(unit,crate)&&world.CanGather(unit,crate)&&Safe(unit.position,visibleEnemies)&&Safe(crate.position,visibleEnemies);
        }
        public bool KeepSupplyGuard(EntityState unit,List<EntityState> visibleEnemies)
        {
            if(CollectingOwnCrate(unit,visibleEnemies))return true;
            var reserve=Reserve;
            if(world.Players[player].politicalPhase!=PoliticalPhase.Revolutionary||reserve.food+reserve.wood+reserve.coin<=0||
                world.CanAfford(player,reserve)||!unit.def.revolutionary||unit.garrisonedIn!=0||unit.hold||!Safe(unit.position,visibleEnemies))return false;
            // Up to six lowest stable IDs stay near the town until a missing
            // recovery currency arrives. Otherwise an entire newly converted
            // force can march away before its real shipment crates land.
            int earlier=0;
            foreach(var e in world.Entities)if(e.Alive&&e.owner==player&&e.def.revolutionary&&e.garrisonedIn==0&&!e.hold&&e.id<unit.id)earlier++;
            if(earlier>=6)return false;
            Vector2 home=world.OpeningTownPosition(player);
            var point=world.Navigation.NearestWalkable(home+new Vector2(7+earlier%3*2,6+earlier/3*2),unit.def.radius);
            if(unit.orders.Count==0||unit.orders.Peek().kind!=OrderKind.Move||(unit.orders.Peek().point-point).sqrMagnitude>1)
                world.Issue(new GameCommand{player=player,kind=CommandKind.Order,actors=new[]{unit.id},order=OrderKind.Move,point=point});
            return true;
        }
        public void CollectCrates(List<EntityState> army,List<EntityState> visibleEnemies)
        {
            var soldiers=army.FindAll(e=>e.Alive&&e.def.revolutionary&&e.garrisonedIn==0&&!e.hold);
            if(soldiers.Count==0)return;
            int limit=Math.Min(6,Math.Max(1,soldiers.Count/6)),collectors=0;
            var assigned=new Dictionary<int,int>();
            foreach(var unit in soldiers)if(CollectingOwnCrate(unit,visibleEnemies))
            {collectors++;int target=unit.orders.Peek().target;assigned.TryGetValue(target,out int count);assigned[target]=count+1;}
            Cost reserve=Reserve;var r=world.Players[player].resources;
            foreach(var unit in soldiers)
            {
                if(collectors>=limit)break;
                if(CollectingOwnCrate(unit,visibleEnemies)||unit.hp<world.MaxHp(unit)*.3f||!Safe(unit.position,visibleEnemies))continue;
                if(unit.orders.Count>0&&unit.orders.Peek().kind!=OrderKind.AttackMove&&unit.orders.Peek().kind!=OrderKind.Gather)continue;
                EntityState chosen=null;float best=float.MaxValue;
                foreach(var crate in world.Entities)
                {
                    if(!world.RevolutionaryCanCollect(unit,crate)||!world.CanGather(unit,crate)||!Safe(crate.position,visibleEnemies))continue;
                    if(world.Layout.RequiresTransport&&world.Layout.LandRegion(unit.position)!=world.Layout.LandRegion(crate.position))continue;
                    assigned.TryGetValue(crate.id,out int count);if(count>=2)continue;
                    float distance=(unit.position-crate.position).sqrMagnitude;if(distance>24*24)continue;
                    float deficit=crate.def.resource==ResourceKind.Food?reserve.food-r.food:crate.def.resource==ResourceKind.Wood?reserve.wood-r.wood:reserve.coin-r.coin;
                    float score=distance+count*200-(deficit>0?800:0);
                    if(score<best){best=score;chosen=crate;}
                }
                if(chosen==null)continue;
                if(world.Issue(new GameCommand{player=player,kind=CommandKind.Order,actors=new[]{unit.id},order=OrderKind.Gather,target=chosen.id}).accepted)
                {collectors++;assigned.TryGetValue(chosen.id,out int count);assigned[chosen.id]=count+1;}
            }
        }
    }
}
