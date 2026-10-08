using System;
using UnityEngine;
namespace Frontier
{
    public partial class World
    {
        public Cost EntityCost(int player,EntityDef definition)
        {
            var civ=Rules.Civ(Players[player].civilization);
            if((definition.tags&Tags.Villager)!=0&&civ.coinVillager)return new Cost(0,0,100);
            return definition.id=="house"?civ.houseCost:definition.cost;
        }
        public int CountingHouseLimit(int player)
        {
            int limit=Rules.Civ(Players[player].civilization).bankLimit;
            foreach(var id in Players[player].technologies){var t=Rules.Tech(id);if(t!=null)limit+=t.bankLimit;}
            return limit;
        }
        public float CountingHouseRate(int player)
        {
            float factor=1;foreach(var id in Players[player].technologies){var t=Rules.Tech(id);if(t!=null)factor+=t.bankIncome;}
            return Rules.Civ(Players[player].civilization).bankRate*factor;
        }
        public EntityDef WorkerDefinition(int player)
        {
            var id=Rules.Civ(Players[player].civilization).workerUnit;
            return Rules.Entity(string.IsNullOrEmpty(id)?"villager":id);
        }
        public int UnitAge(int player,EntityDef definition)
        {
            var overrides=Rules.Civ(Players[player].civilization).unitAges;
            if(overrides!=null)foreach(var item in overrides)if(item.unit==definition.id)return item.age;
            return definition.age;
        }
        public int WorkerLimit(int player)
        {
            int limit=Rules.Civ(Players[player].civilization).villagerLimit;
            foreach(var id in Players[player].technologies)limit+=Rules.Tech(id)?.villagerLimit??0;
            return Mathf.Clamp(limit,0,Rules.populationLimit);
        }
        public int UnitLimit(int player,EntityDef definition)
        {
            if((definition.tags&Tags.Villager)!=0)return WorkerLimit(player);
            if(definition.id=="native_scout")return 4;
            if(definition.id=="envoy")
            {
                // Project balance curve by actual current age: I/II/III/IV/V.
                switch(Players[player].age){case 1:return 3;case 2:return 5;case 3:return 8;case 4:return 13;default:return 18;}
            }
            return int.MaxValue;
        }
        public int WorkerCountIncludingProduction(int player)
        {
            int count=0;foreach(var entity in Entities)
            {
                if(!entity.Alive||entity.owner!=player)continue;
                if(entity.Worker)count++;
                foreach(var job in entity.queue)if(job.kind=="Train"&&(Rules.Entity(job.item).tags&Tags.Villager)!=0)count+=job.count;
            }
            return count;
        }
        public int UnitCountIncludingProduction(int player,string id)
        {
            int count=0;foreach(var e in Entities)
            {
                if(!e.Alive||e.owner!=player)continue;
                if(e.def.id==id)count++;
                foreach(var job in e.queue)if(job.kind=="Train"&&job.item==id)count+=job.count;
            }
            return count;
        }
        public string UnitLimitReason(int player,EntityDef definition,int count)
            =>((definition.tags&Tags.Villager)!=0?WorkerCountIncludingProduction(player):UnitCountIncludingProduction(player,definition.id))+count>UnitLimit(player,definition)?definition.name+" limit reached ("+UnitLimit(player,definition)+")":"";
        public static bool Sustainable(EntityState resource)=>resource!=null&&resource.def.kind==EntityKind.Building&&resource.def.resource!=ResourceKind.None;
        public string AgeReason(int player,EntityState town)
        {
            if(town==null||!town.Alive||town.owner!=player||town.def.id!="town"||!town.Complete)return "Select a completed town center";
            var p=Players[player];if(p.age>=MaxAge)return "Maximum era reached";
            if(p.politicalPhase!=PoliticalPhase.Empire)return "Revolution permanently retains age IV";
            if(town.queue.Count>=6)return "Production queue full";
            if(PoliticalCommitmentQueued(player))return "Age advancement or revolution already queued";
            var next=Rules.Age(p.age+1);if(next==null)return "Age undefined";
            return CanAfford(player,next.cost)?"":"Need "+next.cost;
        }
    }
}
