using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public partial class World
    {
        public const string CivilianRebuildingTechnology="civilian_rebuilding";
        private CommandResult DisbandLandUnits(GameCommand command)
        {
            int count=0;
            foreach(int id in command.actors)
            {
                var unit=Get(id);if(unit==null||!unit.Alive||unit.owner!=command.player||unit.def.kind!=EntityKind.Unit||unit.def.waterborne||unit.garrisonedIn!=0)continue;
                Remove(unit);Emit("UnitDisbanded",command.player,unit.id,"Land unit dismissed without refund or combat XP",1);count++;
            }
            return count>0?CommandResult.Ok("Disbanded "+count+" land units; population released"):CommandResult.Fail("Select your living land units outside a shelter or transport");
        }
        public RevolutionDef RevolutionDefinition(int player)
        {
            var p=P(player);return p==null?null:Array.Find(Rules.revolutions??Array.Empty<RevolutionDef>(),r=>r!=null&&r.civilization==p.civilization);
        }
        private bool PoliticalCommitmentQueued(int player)
        {
            foreach(var e in Entities)if(e.Alive&&e.owner==player)foreach(var job in e.queue)
                if(job.kind=="Age"||job.kind=="Revolution")return true;
            return false;
        }
        public bool WorkerSourcesClosed(int player)
        {
            if(P(player)==null)return true;
            if(P(player).politicalPhase==PoliticalPhase.Revolutionary)return true;
            foreach(var e in Entities)if(e.Alive&&e.owner==player)foreach(var job in e.queue)if(job.kind=="Revolution")return true;
            return false;
        }
        public bool CivilianWorkersEnabled(int player)=>P(player)!=null&&P(player).politicalPhase!=PoliticalPhase.Revolutionary;
        public List<string> ActiveShipmentCards(int player)
        {
            var p=P(player);if(p==null)return new List<string>();
            return p.politicalPhase==PoliticalPhase.Empire?new List<string>(p.deck):new List<string>(RevolutionDefinition(player)?.cards??Array.Empty<string>());
        }
        public string RevolutionReason(int player,EntityState town)
        {
            var p=P(player);if(p==null||!IsPlayerActive(player))return "Player is inactive";
            if(IsMatchEnded)return "Match has ended";if(Paused)return "Resume before issuing orders";
            if(town==null||!town.Alive||town.owner!=player||town.def.id!="town"||!town.Complete)return "Select a completed town center";
            var rule=RevolutionDefinition(player);if(rule==null)return "This civilization follows its existing age routes";
            if(p.politicalPhase!=PoliticalPhase.Empire)return "Revolution is a once-only commitment";
            if(p.age!=4||MaxAge!=5)return "Revolution requires age IV in a five-age match";
            var soldier=Rules.Entity(rule.unit);
            if(soldier==null||!soldier.revolutionary||soldier.waterborne||soldier.kind!=EntityKind.Unit)return "Revolution infantry is undefined";
            foreach(var e in Entities)if(e.Alive&&e.owner==player&&e.Worker&&!e.def.waterborne&&
                (e.def.population!=soldier.population||e.def.radius!=soldier.radius))return "Revolution must preserve settler population and physical clearance";
            if(town.queue.Count>=6)return "Production queue full";
            if(PoliticalCommitmentQueued(player))return "Age advancement or revolution already queued";
            return CanAfford(player,rule.cost)?"":"Need "+rule.cost;
        }
        private CommandResult StartRevolution(GameCommand command,EntityState town)
        {
            string reason=RevolutionReason(command.player,town);if(reason!="")return CommandResult.Fail(reason);
            var rule=RevolutionDefinition(command.player);
            Pay(command.player,rule.cost,town.id,rule.name);
            town.queue.Add(new ProductionJob{kind="Revolution",item=rule.id,paid=rule.cost,duration=rule.time});
            Emit("RevolutionQueued",command.player,town.id,rule.id);
            return CommandResult.Ok("Revolution queued; new settler training is closed until cancellation or civilian rebuilding");
        }
        public EntityDef RevolutionOutputDefinition(int player,EntityDef original)
        {
            // Already paid training, age rewards, manor gifts and shipments retain
            // their count and reservation. Their arrival uses the current phase.
            if(original!=null&&(original.tags&Tags.Villager)!=0&&!original.waterborne&&P(player)?.politicalPhase==PoliticalPhase.Revolutionary)
                return Rules.Entity(RevolutionDefinition(player).unit);
            return original;
        }
        private void CompleteRevolution(EntityState town,ProductionJob job)
        {
            var p=P(town.owner);var rule=RevolutionDefinition(town.owner);
            if(p.politicalPhase!=PoliticalPhase.Empire||p.age!=4||rule==null||rule.id!=job.item)
                throw new InvalidOperationException("Invalid revolution completion");
            var soldier=Rules.Entity(rule.unit);p.politicalPhase=PoliticalPhase.Revolutionary;p.revolutionId=rule.id;p.revolutionAt=Time;
            int converted=0;
            foreach(var unit in Entities)
            {
                if(!unit.Alive||unit.owner!=p.id||!unit.Worker||unit.def.waterborne)continue;
                float ratio=Mathf.Clamp01(unit.hp/MaxHp(unit));
                FlushGather(unit.id);unit.orders.Clear();InvalidateCommandPath(unit);pendingFineRecoveries.Remove(unit.id);
                unit.work=default;unit.fireAt=-1;unit.attackTarget=0;unit.hold=false;unit.forceMelee=false;unit.shielded=false;
                // The catalogue contract keeps population and physical radius
                // equal, so cargo, shelter and groups keep their existing IDs.
                unit.def=soldier;unit.hp=MaxHp(unit)*ratio;unit.status="Revolutionary";converted++;
                Emit("WorkerConverted",p.id,unit.id,rule.unit,1);
            }
            town.queue.RemoveAt(0);p.ageHistory.Add(rule.name+" @ "+Time.ToString("0.0")+"s (age IV)");
            Emit("RevolutionCompleted",p.id,town.id,rule.id,converted);
        }
        private void CompleteCivilianRebuilding(int player,int town)
        {
            var p=P(player);if(p.politicalPhase!=PoliticalPhase.Revolutionary)throw new InvalidOperationException("Civilian rebuilding requires a revolution");
            p.politicalPhase=PoliticalPhase.CivilianRebuilt;p.civilianRebuiltAt=Time;
            Emit("CivilianRebuilt",player,town,"Settler sources restored; revolutionary soldiers and age IV commitment retained");
        }
        public bool RevolutionaryCanCollect(EntityState unit,EntityState resource)
            =>unit!=null&&unit.Alive&&unit.def.revolutionary&&resource!=null&&resource.Alive&&resource.owner==unit.owner&&
              (resource.def.id=="crate_food"||resource.def.id=="crate_wood"||resource.def.id=="crate_coin");

        private void TickPoliticalShipments(PlayerState player,float dt)
        {
            var head=player.shipments[0];var card=Rules.Card(head.card);
            head.elapsed=Mathf.Min(card.travel,head.elapsed+dt);if(head.elapsed>=card.travel)Deliver(player,head,card);
            // A paid army arrival retains its place and still requires population
            // and an exit. During revolution, pure supplies behind that blocked
            // arrival may travel; otherwise full housing could prevent the very
            // crates needed to recover civilian construction. No token is refunded
            // and no resource is credited until its physical crate is collected.
            if(player.politicalPhase==PoliticalPhase.Empire||!player.shipments.Contains(head)||string.IsNullOrEmpty(card.unit)||
                head.elapsed<card.travel||string.IsNullOrEmpty(head.waiting))return;
            ShipmentJob supply=null;CardDef supplyCard=null;
            for(int i=1;i<player.shipments.Count;i++)
            {
                var candidate=Rules.Card(player.shipments[i].card);
                if(!string.IsNullOrEmpty(candidate.unit)||!string.IsNullOrEmpty(candidate.tech)||candidate.crates.food+candidate.crates.wood+candidate.crates.coin<=0)continue;
                supply=player.shipments[i];supplyCard=candidate;break;
            }
            if(supply==null)return;
            supply.elapsed=Mathf.Min(supplyCard.travel,supply.elapsed+dt);if(supply.elapsed>=supplyCard.travel)Deliver(player,supply,supplyCard);
        }
        private void TickPoliticalRewards(PlayerState player)
        {
            var head=player.rewards[0];DeliverReward(player,head);
            if(player.politicalPhase==PoliticalPhase.Empire||!player.rewards.Contains(head)||string.IsNullOrEmpty(head.unit)||string.IsNullOrEmpty(head.waiting))return;
            RewardJob supply=null;
            for(int i=1;i<player.rewards.Count;i++)if(string.IsNullOrEmpty(player.rewards[i].unit)&&
                player.rewards[i].crates.food+player.rewards[i].crates.wood+player.rewards[i].crates.coin>0){supply=player.rewards[i];break;}
            if(supply!=null)DeliverReward(player,supply);
        }

        private static void ValidateRevolutionSnapshot(RuleCatalog rules,WorldSnapshot snapshot)
        {
            void Require(bool valid,string message){if(!valid)throw new ArgumentException("Invalid saved match: revolution "+message);}
            for(int player=0;player<snapshot.players.Length;player++)
            {
                var saved=snapshot.players[player];var p=saved.state;
                var rule=Array.Find(rules.revolutions??Array.Empty<RevolutionDef>(),r=>r!=null&&r.civilization==p.civilization);
                Require(Enum.IsDefined(typeof(PoliticalPhase),p.politicalPhase)&&SnapshotCodec.Finite(p.revolutionAt)&&SnapshotCodec.Finite(p.civilianRebuiltAt),"phase or clock");
                bool completed=p.politicalPhase!=PoliticalPhase.Empire;
                Require(completed?rule!=null&&snapshot.maxAge==5&&p.age==4&&p.revolutionId==rule.id&&p.revolutionAt>=0&&p.revolutionAt<=snapshot.time:
                    string.IsNullOrEmpty(p.revolutionId)&&p.revolutionAt==0&&p.civilianRebuiltAt==0,"identity or age");
                bool rebuilt=p.politicalPhase==PoliticalPhase.CivilianRebuilt;
                Require(rebuilt?Array.IndexOf(saved.technologies,CivilianRebuildingTechnology)>=0&&p.civilianRebuiltAt>=p.revolutionAt&&p.civilianRebuiltAt<=snapshot.time:
                    Array.IndexOf(saved.technologies,CivilianRebuildingTechnology)<0&&p.civilianRebuiltAt==0,"civilian rebuilding receipt");
                Require(DeckLibrary.Validate(rules,p.civilization,saved.deck)=="","opening deck");
                var sent=new HashSet<string>();foreach(string id in saved.sentCards)
                {
                    var card=rules.Card(id);Require(card!=null&&sent.Add(id)&&(!card.revolutionOnly?Array.IndexOf(saved.deck,id)>=0:completed&&Array.IndexOf(rule.cards,id)>=0),"card history");
                }
                foreach(var shipment in saved.shipments)Require(sent.Contains(shipment.card),"unpaid shipment");
                // Revolutionaries arrive through worker conversion or their
                // accepted shipment cards, never direct production or age gifts.
                foreach(var reward in saved.rewards)
                    Require(reward!=null&&rules.Entity(reward.unit)?.revolutionary!=true,"direct revolutionary age reward");
                int commitments=0,used=0,reserved=0,rebuilds=0;
                foreach(var entity in snapshot.entities)if(entity.state.owner==player)
                {
                    var def=rules.Entity(entity.definition);
                    if(entity.state.Alive)
                    {
                        if(def.kind==EntityKind.Unit)used+=def.population;
                        Require(!def.revolutionary||completed,"soldier without completed revolution");
                        if(def.revolutionary)foreach(var order in entity.orders)
                            Require(order.kind!=OrderKind.Build&&order.kind!=OrderKind.Repair&&order.kind!=OrderKind.Manufacture,"infantry cannot perform civilian work");
                        Require(p.politicalPhase!=PoliticalPhase.Revolutionary||(def.tags&Tags.Villager)==0||def.waterborne,"civilian in military phase");
                    }
                    foreach(var job in entity.queue)
                    {
                        reserved+=job.population;
                        if(job.kind=="Train"||job.kind=="Gift")
                            Require(rules.Entity(job.item)?.revolutionary!=true,"direct revolutionary production");
                        if(job.kind=="Age"||job.kind=="Revolution")commitments++;
                        if(job.kind=="Age")Require(!completed,"age after revolution");
                        if(job.kind=="Revolution")Require(entity.state.Alive&&entity.state.Complete&&entity.definition=="town"&&!completed&&snapshot.maxAge==5&&p.age==4&&rule!=null&&job.item==rule.id&&
                            job.duration==rule.time&&job.count==1&&job.population==0&&job.paid.food==rule.cost.food&&job.paid.wood==rule.cost.wood&&job.paid.coin==rule.cost.coin&&job.elapsed<=job.duration,"queued commitment");
                        if(job.kind=="Research"&&job.item==CivilianRebuildingTechnology)
                        {
                            rebuilds++;var tech=rules.Tech(CivilianRebuildingTechnology);
                            Require(entity.state.Alive&&entity.state.Complete&&entity.definition==tech.building&&p.politicalPhase==PoliticalPhase.Revolutionary&&
                                job.duration==tech.time&&job.count==1&&job.population==0&&job.paid.food==tech.cost.food&&job.paid.wood==tech.cost.wood&&job.paid.coin==tech.cost.coin&&job.elapsed<=job.duration,"rebuilding queue");
                        }
                    }
                }
                Require(commitments<=1,"duplicate commitment");
                Require(rebuilds<=1,"duplicate civilian rebuilding");
                if(completed)
                {
                    Require(p.usedPopulation>=0&&p.reservedPopulation>=0,"negative population");
                    foreach(var ally in snapshot.neutralAllies??Array.Empty<NeutralAllianceState>())if(ally.owner==player)
                        foreach(var job in ally.jobs)reserved+=job.population;
                    Require(p.usedPopulation==used&&p.reservedPopulation==reserved,"population accounting");
                }
            }
        }
    }
}
