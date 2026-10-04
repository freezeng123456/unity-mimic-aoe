using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    // Civilization choices use the same paid queues, visible threats and command
    // authority as players. No worker progress, cargo or resources are injected.
    public sealed class AiCivilizationEconomy
    {
        readonly World world;
        readonly int player;
        float manufactureAt;
        public AiCivilizationEconomy(World world,int player){this.world=world;this.player=player;}
        public AiCivilizationEconomySnapshot CaptureState()=>new AiCivilizationEconomySnapshot{manufactureAt=manufactureAt};
        public void RestoreState(AiCivilizationEconomySnapshot state)
        {
            AiSaveValidation.Require(state!=null&&AiSaveValidation.Clock(state.manufactureAt),"manufacturing clock");
            manufactureAt=state.manufactureAt;
        }
        public string Building=>world.Players[player].civilization=="wayfarer"?"waystation":world.Players[player].civilization=="highland"?"workshop":world.Players[player].civilization=="ottoman"?"mosque":"";
        public static bool CanReassign(EntityState worker)=>worker.Alive&&worker.Worker&&worker.garrisonedIn==0&&!worker.hold&&worker.orders.Count<=1&&(worker.orders.Count==0||worker.orders.Peek().kind==OrderKind.Gather);
        public void Tick(List<EntityState> own,List<EntityState> workers,List<EntityState> visible,Cost reserve,bool saving,bool underAttack)
        {
            if(world.Paused||world.Winner>=0)return;
            if(Building=="waystation")Trade(own,visible,reserve,saving,underAttack);
            if(Building=="workshop")Manufacture(own,workers,visible,reserve,saving,underAttack);
            if(Building=="mosque"&&!saving&&!underAttack)ResearchMosque(own,reserve);
        }
        public Cost Forecast(List<EntityState> own,bool saving)
        {
            if(saving)return new Cost();
            var p=world.Players[player];
            if(Building=="mosque")return NextMosqueTechnology(own)?.cost??new Cost();
            if(p.age<2)return new Cost();
            if(Building=="waystation")
            {
                if(p.resources.coin>=TradeCoinTarget(own))return new Cost();
                int carts=world.UnitCountIncludingProduction(player,"caravan");
                return World.CaravanLoadCost*(carts*2)+(carts<Math.Min(4,p.age)?world.UnitCost(player,world.Rules.Entity("caravan")):new Cost());
            }
            if(Building=="workshop"&&p.prefabCharges<1)
                foreach(var recipe in world.Rules.manufactureRecipes)
                    if(string.IsNullOrEmpty(recipe.requires)||p.technologies.Contains(recipe.requires))return recipe.cost;
            return new Cost();
        }
        // Free settlers still need a paid infrastructure economy. Keep mosque
        // investment visible to gathering and other military queues so repeated
        // recruitment cannot consume every wood/coin increment first.
        public Cost TechnologyReserve(List<EntityState> own,bool saving,bool underAttack)
            =>saving||underAttack?new Cost():NextMosqueTechnology(own)?.cost??new Cost();
        TechDef NextMosqueTechnology(List<EntityState> own)
        {
            if(Building!="mosque"||!own.Exists(e=>e.Alive&&e.Complete&&e.def.id=="mosque"&&e.queue.Count==0))return null;
            var p=world.Players[player];int workers=0;
            foreach(var e in own)if(e.Alive&&e.Worker)workers++;
            bool needLimit=workers>=world.WorkerLimit(player)-4;
            TechDef speed=null,limit=null;
            foreach(var t in world.Rules.techs)
            {
                if(t.building!="mosque"||t.age>p.age||p.technologies.Contains(t.id)||
                    !string.IsNullOrEmpty(t.civilization)&&t.civilization!=p.civilization||
                    !string.IsNullOrEmpty(t.requires)&&!p.technologies.Contains(t.requires))continue;
                bool pending=own.Exists(e=>e.queue.Exists(j=>j.kind=="Research"&&j.item==t.id))||p.shipments.Exists(s=>world.Rules.Card(s.card)?.tech==t.id);
                if(pending)continue;
                if(t.villagerLimit>0&&limit==null)limit=t;
                if(t.autoVillagerSpeed>0&&speed==null)speed=t;
            }
            return needLimit&&limit!=null?limit:speed;
        }
        void ResearchMosque(List<EntityState> own,Cost reserve)
        {
            var upgrade=NextMosqueTechnology(own);if(upgrade==null||!CanSpend(upgrade.cost,reserve))return;
            var mosque=own.Find(e=>e.Alive&&e.Complete&&e.def.id=="mosque"&&e.queue.Count==0);
            if(world.ProductionReason(player,mosque,upgrade.id,true)=="")Send(new GameCommand{kind=CommandKind.Research,actors=new[]{mosque.id},item=upgrade.id});
        }
        void Trade(List<EntityState> own,List<EntityState> visible,Cost reserve,bool saving,bool underAttack)
        {
            var stations=own.FindAll(e=>e.Alive&&e.Complete&&e.def.id=="waystation");
            var p=world.Players[player];
            bool tradeNeeded=!saving&&p.resources.coin<TradeCoinTarget(own);
            // Loading happens automatically between AI reviews at only 60F,
            // below every infantry and settler cost. Protect the next worker
            // plus one affordable infantry order before allowing empty carts
            // to reload, even before an age-saving plan can become eligible.
            float foodBudget=Mathf.Max(0,p.resources.food-reserve.food-RecruitmentFoodReserve(own));
            foreach(var cart in own)
            {
                if(!cart.Alive||cart.def.id!="caravan"||cart.garrisonedIn!=0)continue;
                if(cart.routeLeg==2)continue; // Paid cargo always gets its chance to arrive.
                if(!tradeNeeded||foodBudget<World.CaravanLoadCost.food)
                {
                    if(cart.routeLeg==1||cart.routeLeg==3)Send(new GameCommand{kind=CommandKind.Order,actors=new[]{cart.id},order=OrderKind.Stop});
                    continue;
                }
                // Each empty cart reserves its own next load. Several carts
                // must not all be authorized against the same 60 food.
                if(cart.routeLeg!=0){foodBudget-=World.CaravanLoadCost.food;continue;}
                EntityState chosen=null;float best=float.MaxValue;
                foreach(var station in stations)
                {
                    if(Threatened(station,visible)||world.CaravanRouteReason(player,cart,station)!="")continue;
                    float distance=(station.position-cart.position).sqrMagnitude;
                    if(distance<best){best=distance;chosen=station;}
                }
                if(chosen!=null&&Send(new GameCommand{kind=CommandKind.TradeRoute,actors=new[]{cart.id},target=chosen.id}).accepted)foodBudget-=World.CaravanLoadCost.food;
            }
            if(!tradeNeeded||underAttack||foodBudget<World.CaravanLoadCost.food)return;
            int goal=Math.Min(4,p.age);
            if(world.UnitCountIncludingProduction(player,"caravan")>=goal)return;
            foreach(var station in stations)
            {
                if(station.queue.Count>0||Threatened(station,visible)||world.ProductionReason(player,station,"caravan")!="")continue;
                var cost=world.UnitCost(player,world.Rules.Entity("caravan"));if(!CanSpend(cost,reserve+World.CaravanLoadCost))return;
                Send(new GameCommand{kind=CommandKind.Train,actors=new[]{station.id},item="caravan"});return;
            }
        }
        float RecruitmentFoodReserve(List<EntityState> own)
        {
            float minimum=float.PositiveInfinity;var p=world.Players[player];
            foreach(var producer in own)
            {
                if(!producer.Alive||!producer.Complete||producer.def.kind!=EntityKind.Building||producer.queue.Count>0)continue;
                foreach(var unit in world.Rules.entities)
                {
                    if(unit.kind!=EntityKind.Unit||unit.civilian||(unit.tags&Tags.Villager)!=0||unit.producer!=producer.def.id||world.UnitAge(player,unit)>p.age||!world.EntityAvailable(player,unit))continue;
                    float food=world.UnitCost(player,unit).food;if(food>0)minimum=Mathf.Min(minimum,food);
                }
            }
            return float.IsPositiveInfinity(minimum)?0:minimum;
        }
        float TradeCoinTarget(List<EntityState> own)
        {
            var p=world.Players[player];float target=p.age<world.MaxAge?world.Rules.Age(p.age+1)?.cost.coin??0:0;
            // Fund the next era and two ordinary recruitment rounds at current
            // producers. Coin already above those real costs does not justify
            // exchanging scarce food indefinitely.
            foreach(var producer in own)
            {
                if(!producer.Alive||!producer.Complete||producer.def.kind!=EntityKind.Building)continue;
                float largest=0;
                foreach(var unit in world.Rules.entities)
                    if(unit.kind==EntityKind.Unit&&!unit.civilian&&(unit.tags&Tags.Villager)==0&&unit.producer==producer.def.id&&world.UnitAge(player,unit)<=p.age&&world.EntityAvailable(player,unit))
                        largest=Mathf.Max(largest,world.UnitCost(player,unit).coin);
                target+=largest*2;
            }
            return target;
        }
        void Manufacture(List<EntityState> own,List<EntityState> workers,List<EntityState> visible,Cost reserve,bool saving,bool underAttack)
        {
            var workshop=own.Find(e=>e.Alive&&e.Complete&&e.def.id=="workshop");
            var assigned=new List<EntityState>();
            foreach(var worker in workers)
            {
                if(worker.orders.Count==0||worker.orders.Peek().kind!=OrderKind.Manufacture)continue;
                if(workshop!=null&&worker.orders.Peek().target==workshop.id)assigned.Add(worker);
                else Send(new GameCommand{kind=CommandKind.Order,actors=new[]{worker.id},order=OrderKind.Stop});
            }
            if(workshop==null)return;
            bool threatened=Threatened(workshop,visible);
            // Keep an economy outside the workshop; early investment is two
            // workers, later queues may use three or four. Travelling assignees
            // count here so each decision does not recruit another four workers.
            int desired=threatened?0:Math.Min(4,Math.Max(1,workers.Count/8));
            if(saving&&workshop.queue.Count==0)desired=0;
            for(int i=assigned.Count-1;i>=desired;i--)
            {Send(new GameCommand{kind=CommandKind.Order,actors=new[]{assigned[i].id},order=OrderKind.Stop});assigned.RemoveAt(i);}
            var candidates=workers.FindAll(CanReassign);
            candidates.Sort((a,b)=>(a.position-workshop.position).sqrMagnitude.CompareTo((b.position-workshop.position).sqrMagnitude));
            foreach(var worker in candidates)
            {
                if(assigned.Count>=desired)break;
                var result=Send(new GameCommand{kind=CommandKind.Order,actors=new[]{worker.id},order=OrderKind.Manufacture,target=workshop.id});
                if(result.accepted)assigned.Add(worker);
            }
            var p=world.Players[player];
            if(saving||underAttack||threatened||workshop.queue.Count>0||world.AssignedArtisans(workshop)==0||p.prefabCharges>=1||world.Time<manufactureAt)return;
            foreach(var recipe in world.Rules.manufactureRecipes)
            {
                if(world.ManufactureReason(player,workshop,recipe.id)!=""||!CanSpend(recipe.cost,reserve))continue;
                var result=Send(new GameCommand{kind=CommandKind.Manufacture,actors=new[]{workshop.id},item=recipe.id});
                if(result.accepted)manufactureAt=world.Time+90;
                return;
            }
        }
        public bool Support(EntityState unit,List<EntityState> ownArmy,List<EntityState> visible)
        {
            if(unit.def.repairArtillery)
            {
                EntityState target=null;float nearest=float.MaxValue;
                foreach(var candidate in ownArmy)
                {
                    if(!candidate.Alive||(candidate.def.tags&Tags.Artillery)==0||candidate.hp>=world.MaxHp(candidate)-.01f)continue;
                    float distance=(unit.position-candidate.position).sqrMagnitude;
                    if(distance<nearest){target=candidate;nearest=distance;}
                }
                // Engineers travel with the army; repairs use the normal paid
                // order and are not interrupted by each strategic review.
                if(target!=null&&nearest<24*24)
                {
                    if(unit.orders.Count==0||unit.orders.Peek().kind!=OrderKind.Repair||unit.orders.Peek().target!=target.id)
                        return Send(new GameCommand{kind=CommandKind.Order,actors=new[]{unit.id},order=OrderKind.Repair,target=target.id}).accepted;
                    return true;
                }
            }
            if(unit.def.shieldAbility)
            {
                bool coveringGuns=ownArmy.Exists(e=>e.Alive&&(e.def.tags&Tags.Artillery)!=0&&(e.position-unit.position).sqrMagnitude<8*8);
                float deployDistance=coveringGuns?10:Mathf.Max(3,unit.def.range+1);
                bool enemyNear=visible.Exists(e=>e.Alive&&e.Military&&world.IsVisible(player,e)&&(unit.position-e.position).sqrMagnitude<deployDistance*deployDistance);
                if(enemyNear)
                {
                    if(!unit.shielded&&world.Time>=unit.shieldReadyAt)
                        return Send(new GameCommand{kind=CommandKind.ToggleShield,actors=new[]{unit.id}}).accepted;
                    if(unit.shielded)return true;
                }
            }
            return false;
        }
        bool Threatened(EntityState location,List<EntityState> visible)=>visible.Exists(enemy=>enemy.Alive&&enemy.Military&&(enemy.def.tags&Tags.Explorer)==0&&world.IsVisible(player,enemy)&&(enemy.position-location.position).sqrMagnitude<Mathf.Pow(Mathf.Max(12,enemy.def.range+4),2));
        bool CanSpend(Cost cost,Cost reserve)
        {
            if(!world.CanAfford(player,cost))return false;var r=world.Players[player].resources;
            return r.food-cost.food>=Math.Min(r.food,reserve.food)&&r.wood-cost.wood>=Math.Min(r.wood,reserve.wood)&&r.coin-cost.coin>=Math.Min(r.coin,reserve.coin);
        }
        CommandResult Send(GameCommand command){command.player=player;return world.Issue(command);}
    }
}
