using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public partial class World
    {
        public float AutoRecruitSeconds(int player)
        {
            if(P(player)==null)return 0;
            var seconds=Rules.Civ(Players[player].civilization).autoVillagerSeconds;
            return seconds<=0?0:seconds/CivilizationFactor(player,t=>t.autoVillagerSpeed);
        }

        public string AutoRecruitReason(EntityState town)
        {
            if(town==null||!town.Alive||town.def.id!="town"||P(town.owner)==null)return "Requires a living town center";
            if(AutoRecruitSeconds(town.owner)<=0)return "This civilization trains settlers manually";
            if(!town.Complete)return "Town center under construction";
            if(IsMatchEnded)return "Match has ended";
            if(town.autoRecruitPaused)return "Paused by player";
            if(town.queue.Count>0)return "Town center queue busy";
            var worker=WorkerDefinition(town.owner);
            string limit=UnitLimitReason(town.owner,worker,1);if(limit!="")return limit;
            var p=Players[town.owner];
            if(p.usedPopulation+p.reservedPopulation+worker.population>Mathf.Min(p.populationCap,Rules.populationLimit))return "Need more housing";
            if(town.autoRecruitElapsed>=AutoRecruitSeconds(town.owner)&&!FindSpawnPositions(town,1,worker.radius,out _))return "Town center exit blocked";
            return "";
        }

        private void TickAutoRecruitment(EntityState town,float dt)
        {
            if(dt<=0||Paused||IsMatchEnded||town.def.id!="town"||!IsPlayerActive(town.owner)||P(town.owner)==null)return;
            float seconds=AutoRecruitSeconds(town.owner);
            if(seconds<=0||AutoRecruitReason(town)!="")return;
            // One ready recruit at most; a blocked exit, cap or queue never
            // accumulates several invisible villagers for a later burst.
            town.autoRecruitElapsed=Mathf.Min(seconds,town.autoRecruitElapsed+dt);
            if(town.autoRecruitElapsed<seconds)return;
            var worker=WorkerDefinition(town.owner);
            if(!FindSpawnPositions(town,1,worker.radius,out var points)){town.status="Town center exit blocked";return;}
            var recruit=Spawn(worker.id,town.owner,points[0]);
            AssignProducerRally(town,new List<EntityState>{recruit});
            town.autoRecruitElapsed=0;town.status="Automatic settler recruited";
            Emit("AutomaticWorkerProduced",town.owner,town.id,worker.id,1,recruit.id);
        }
    }
}
