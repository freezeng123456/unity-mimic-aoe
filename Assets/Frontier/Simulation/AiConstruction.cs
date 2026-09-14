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
            if(world.Paused||world.Winner>=0)return;
            foreach(var foundation in own)
            {
                if(!foundation.Alive||foundation.owner!=player||foundation.def.kind!=EntityKind.Building||foundation.Complete||
                    world.Fog.At(player,foundation.position)!=VisibilityState.Visible)continue;
                bool assigned=false;
                foreach(var worker in workers)
                {
                    if(!worker.Alive||worker.owner!=player||worker.garrisonedIn!=0)continue;
                    foreach(var order in worker.orders)if(order.kind==OrderKind.Build&&order.target==foundation.id){assigned=true;break;}
                    if(assigned)break;
                }
                if(assigned)continue;
                bool threatened=false;
                foreach(var enemy in visibleEnemies)
                {
                    if(!world.IsVisible(player,enemy)||!enemy.Military||(enemy.def.tags&Tags.Explorer)!=0)continue;
                    float danger=Mathf.Max(12,enemy.def.range+4);
                    if((enemy.position-foundation.position).sqrMagnitude<danger*danger){threatened=true;break;}
                }
                if(threatened)continue;
                EntityState replacement=null;float nearest=float.MaxValue;
                foreach(var worker in workers)
                {
                    if(!worker.Alive||worker.owner!=player||!worker.Worker||worker.garrisonedIn!=0||worker.hold||worker.orders.Count>1||
                        worker.orders.Count==1&&worker.orders.Peek().kind!=OrderKind.Gather)continue;
                    float distance=(worker.position-foundation.position).sqrMagnitude;
                    if(distance<nearest){nearest=distance;replacement=worker;}
                }
                if(replacement==null)return;
                var result=world.Issue(new GameCommand{player=player,kind=CommandKind.Order,actors=new[]{replacement.id},order=OrderKind.Build,target=foundation.id,point=foundation.position});
                if(result.accepted)world.Emit("AIConstructionResumed",player,replacement.id,"Resuming the existing foundation",1,foundation.id,foundation.position);
                // Bound recovery work and accepted orders to one foundation per decision.
                return;
            }
        }
    }
}
