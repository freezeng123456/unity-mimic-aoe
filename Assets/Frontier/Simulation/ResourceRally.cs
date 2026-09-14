using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public partial class World
    {
        public bool GatherableResource(int player,EntityState target)=>IsVisible(player,target)&&target.Complete&&
            target.def.resource!=ResourceKind.None&&(target.def.kind==EntityKind.Resource||target.owner==player)&&
            (target.remaining>0||Sustainable(target));

        private CommandResult SetRally(GameCommand command)
        {
            var target=Get(command.target);
            // Validate before touching any selected building. Hidden or invalid
            // targets cannot overwrite a previously valid player intention.
            if(command.target!=0&&!GatherableResource(command.player,target))
                return CommandResult.Fail("Choose a visible available resource or ordinary terrain");
            Vector2 point=Navigation.NearestWalkable(target?.position??command.point,.5f);int count=0;
            foreach(int id in command.actors)
            {
                var building=Get(id);
                if(building==null||!building.Alive||building.owner!=command.player||building.def.kind!=EntityKind.Building)continue;
                building.rally=point;building.rallyTarget=command.target;building.rallyResource=target?.def.resource??ResourceKind.None;count++;
                Emit("RallySet",command.player,building.id,command.target==0?"Location rally set":"Resource rally set",1,command.target,point);
            }
            if(count==0)return CommandResult.Fail("Select a building");
            return CommandResult.Ok(command.target==0?"Rally point set":"Resource rally set — new settlers will gather");
        }

        private EntityState NearbyRallyResource(EntityState producer)
        {
            // Only use the resource kind observed when the command was accepted.
            // A remembered area outside current sight cannot reveal replacements.
            if(producer.rallyResource==ResourceKind.None||Fog.At(producer.owner,producer.rally)!=VisibilityState.Visible)return null;
            EntityState nearest=null;float best=18*18;
            foreach(var candidate in Entities)
            {
                if(candidate.def.resource!=producer.rallyResource||!GatherableResource(producer.owner,candidate))continue;
                float distance=(candidate.position-producer.rally).sqrMagnitude;
                if(distance<=best&&(nearest==null||distance<best)){best=distance;nearest=candidate;}
            }
            return nearest;
        }

        private void AssignProducerRally(EntityState producer,List<EntityState> group)
        {
            if(group.Count==0)return;
            var moving=new List<EntityState>(group);
            if(producer.rallyTarget!=0)
            {
                var workers=new List<int>();foreach(var unit in group)if(unit.Worker)workers.Add(unit.id);
                var resource=Get(producer.rallyTarget);bool retargeted=false;
                if(!GatherableResource(producer.owner,resource))
                {
                    resource=workers.Count>0?NearbyRallyResource(producer):null;
                    retargeted=resource!=null;
                }
                bool assigned=false;
                if(resource!=null&&workers.Count>0)
                {
                    var result=Issue(new GameCommand{player=producer.owner,kind=CommandKind.Order,actors=workers.ToArray(),order=OrderKind.Gather,target=resource.id});
                    if(result.accepted)
                    {
                        assigned=true;moving.RemoveAll(unit=>unit.Worker);
                        if(retargeted)Emit("RallyRetargeted",producer.owner,producer.id,"Gathering the same resource near the original flag",workers.Count,resource.id,resource.position);
                        foreach(int id in workers)Emit("RallyAssigned",producer.owner,id,"Automatic resource gathering",0,resource.id,resource.position);
                    }
                }
                if(!assigned&&(resource==null||workers.Count>0))
                    Emit("RallyFallback",producer.owner,producer.id,"Resource unavailable — moving to the recorded rally point",group.Count,producer.rallyTarget,producer.rally);
            }
            // Keep the player's flag and military formation at the recorded point.
            if((producer.rally-producer.position).sqrMagnitude>1)AssignAutomaticRally(moving,producer.rally);
        }
    }
}
