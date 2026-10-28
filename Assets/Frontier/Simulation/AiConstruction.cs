using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    // Restore interrupted jobs through ordinary commands; never create a second
    // foundation or add progress, income, or population outside the simulation.
    public sealed class AiConstruction
    {
        private readonly World world;
        private readonly int player;
        public AiConstruction(World world,int player){this.world=world;this.player=player;}

        public void ResumeOne(List<EntityState> own,List<EntityState> workers,List<EntityState> visibleEnemies)
        {
            if(world.Paused||world.IsMatchEnded||!world.IsPlayerActive(player))return;
            foreach(var foundation in own)
            {
                if(!foundation.Alive||foundation.owner!=player||foundation.def.kind!=EntityKind.Building||foundation.Complete||
                    world.Fog.At(player,foundation.position)!=VisibilityState.Visible)continue;
                if(!AiResourceContinuity072.Safe(world,player,foundation.position,visibleEnemies))continue;
                bool assigned=false;
                foreach(var worker in workers)
                {
                    if(!worker.Alive||worker.owner!=player||worker.garrisonedIn!=0)continue;
                    foreach(var order in worker.orders)if(order.kind==OrderKind.Build&&order.target==foundation.id)
                    {
                        // Preserve explicit queues. A single blocked AI builder
                        // must not permanently claim a paid foundation.
                        if(worker.orders.Count>1||AiResourceContinuity072.TryConstructionWorker(world,player,foundation.def,foundation.position,
                            new List<EntityState>{worker},visibleEnemies,out _,foundation.id))assigned=true;
                        else world.Issue(new GameCommand{player=player,kind=CommandKind.Order,actors=new[]{worker.id},order=OrderKind.Stop});
                        break;
                    }
                    if(assigned)break;
                }
                if(assigned)continue;
                if(!AiResourceContinuity072.TryConstructionWorker(world,player,foundation.def,foundation.position,
                    workers,visibleEnemies,out var replacement,foundation.id))continue;
                var result=world.Issue(new GameCommand{player=player,kind=CommandKind.Order,actors=new[]{replacement.id},order=OrderKind.Build,target=foundation.id,point=foundation.position});
                if(result.accepted)world.Emit("AIConstructionResumed",player,replacement.id,"Resuming the existing foundation",1,foundation.id,foundation.position);
                // Bound recovery work and accepted orders to one foundation per decision.
                return;
            }
        }
    }
}
