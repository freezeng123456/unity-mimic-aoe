using System;
using UnityEngine;
namespace Frontier
{
    public partial class World
    {
        public Cost EntityCost(int player,EntityDef definition)
        {
            var civ=Rules.Civ(Players[player].civilization);
            if(definition.id=="villager"&&civ.coinVillager)return new Cost(0,0,100);
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
        public int UnitLimit(int player,EntityDef definition)=>definition.id=="villager"?Rules.Civ(Players[player].civilization).villagerLimit:int.MaxValue;
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
            =>UnitCountIncludingProduction(player,definition.id)+count>UnitLimit(player,definition)?definition.name+" limit reached ("+UnitLimit(player,definition)+")":"";
        public static bool Sustainable(EntityState resource)=>resource!=null&&resource.def.kind==EntityKind.Building&&resource.def.resource!=ResourceKind.None;
        public string AgeReason(int player,EntityState town)
        {
            if(town==null||!town.Alive||town.owner!=player||town.def.id!="town"||!town.Complete)return "Select a completed town center";
            var p=Players[player];if(p.age>=MaxAge)return "Maximum era reached";
            if(town.queue.Count>=6)return "Production queue full";
            foreach(var e in Entities)if(e.Alive&&e.owner==player)foreach(var j in e.queue)if(j.kind=="Age")return "Age advancement already queued";
            var next=Rules.Age(p.age+1);if(next==null)return "Age undefined";
            return CanAfford(player,next.cost)?"":"Need "+next.cost;
        }
    }
}
