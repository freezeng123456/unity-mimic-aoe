using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    // Terrain patrol intent uses public map dimensions and own observations only.
    // Ordinary AttackMove and navigation still execute every chosen ground order.
    public sealed class AiSearchPlan
    {
        private readonly World world;
        private readonly int player;
        private readonly Vector2 enemyStart;
        private readonly List<Vector2> points=new List<Vector2>();
        private readonly bool[] processed;
        private bool visitedStart;
        private int current=-1,pass;
        private float selectedAt;
        public int Revision {get;private set;}

        public AiSearchPlan(World world,int player,Vector2 enemyStart)
        {
            this.world=world;this.player=player;this.enemyStart=enemyStart;
            int columns=Math.Max(1,Mathf.CeilToInt((world.Navigation.Width-2)/18f));
            int rows=Math.Max(1,Mathf.CeilToInt((world.Navigation.Height-2)/18f));
            for(int y=0;y<rows;y++)for(int x=0;x<columns;x++)
                points.Add(new Vector2(1+(x+.5f)*(world.Navigation.Width-2)/columns,1+(y+.5f)*(world.Navigation.Height-2)/rows));
            processed=new bool[points.Count];
        }
        private bool Active(EntityState unit)=>unit.Alive&&unit.owner==player&&unit.Military&&unit.garrisonedIn==0;
        public void ObserveStart(List<EntityState> soldiers)
        {
            if(visitedStart||world.Paused||world.Winner>=0||world.Fog.At(player,enemyStart)!=VisibilityState.Visible)return;
            foreach(var unit in soldiers)if(Active(unit)&&(unit.position-enemyStart).sqrMagnitude<=12*12){visitedStart=true;return;}
        }
        public bool TryDestination(List<EntityState> soldiers,out Vector2 destination)
        {
            destination=enemyStart;
            if(!visitedStart||world.Paused||world.Winner>=0)return false;
            EntityState leader=null;foreach(var unit in soldiers)if(Active(unit)){leader=unit;break;}
            if(leader==null)return false;
            if(current>=0)
            {
                bool arrived=false;
                if(world.Fog.At(player,points[current])==VisibilityState.Visible)
                    foreach(var unit in soldiers)if(Active(unit)&&(unit.position-points[current]).sqrMagnitude<=4*4){arrived=true;break;}
                if(!arrived&&world.Time-selectedAt<90){destination=points[current];return true;}
                if(!arrived)world.Emit("AISearchDeferred",player,leader.id,"Patrol point deferred until a later pass",pass+1,0,points[current]);
                // A timeout is not proof of an empty region. It is retried next pass.
                processed[current]=true;current=-1;
            }
            for(int attempt=0;attempt<2&&current<0;attempt++)
            {
                float nearest=float.MaxValue;
                for(int i=0;i<points.Count;i++)
                {
                    if(processed[i])continue;
                    float distance=(points[i]-leader.position).sqrMagnitude;
                    if(distance<nearest){nearest=distance;current=i;}
                }
                if(current<0){Array.Clear(processed,0,processed.Length);pass++;}
            }
            if(current<0)return false;
            selectedAt=world.Time;Revision++;destination=points[current];
            world.Emit("AISearchPoint",player,leader.id,"Searching a ground location; no known enemy target",pass+1,0,destination);
            return true;
        }
    }
}
