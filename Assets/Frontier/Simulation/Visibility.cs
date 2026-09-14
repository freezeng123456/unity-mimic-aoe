using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public enum VisibilityState { Unexplored,Explored,Visible }
    public sealed class Visibility
    {
        private readonly int width,height;
        private readonly VisibilityState[,,] cells;
        public Visibility(int width=128,int height=128,int players=2){this.width=width;this.height=height;cells=new VisibilityState[players,width,height];}
        public void Tick(List<EntityState> entities)
        {
            for(int p=0;p<cells.GetLength(0);p++)for(int x=0;x<width;x++)for(int y=0;y<height;y++)if(cells[p,x,y]==VisibilityState.Visible)cells[p,x,y]=VisibilityState.Explored;
            foreach(var e in entities)if(e.Alive&&e.owner>=0&&e.owner<cells.GetLength(0)&&e.garrisonedIn==0&&e.def.vision>0)Reveal(e.owner,e.position,Mathf.CeilToInt(e.def.vision));
        }
        public VisibilityState At(int player,Vector2 point)
        {
            if(player<0||player>=cells.GetLength(0)||point.x<0||point.y<0||point.x>=width||point.y>=height)return VisibilityState.Unexplored;
            return cells[player,Mathf.Clamp(Mathf.RoundToInt(point.x),0,width-1),Mathf.Clamp(Mathf.RoundToInt(point.y),0,height-1)];
        }
        public bool IsVisible(int player,EntityState entity)=>entity.owner==player||At(player,entity.position)==VisibilityState.Visible;
        public void Reveal(int player,Vector2 point,int radius)
        {
            if(player<0||player>=cells.GetLength(0))return;
            int cx=Mathf.RoundToInt(point.x),cy=Mathf.RoundToInt(point.y);
            for(int x=Mathf.Max(0,cx-radius);x<=Mathf.Min(width-1,cx+radius);x++)for(int y=Mathf.Max(0,cy-radius);y<=Mathf.Min(height-1,cy+radius);y++)
                if((x-point.x)*(x-point.x)+(y-point.y)*(y-point.y)<=radius*radius)cells[player,x,y]=VisibilityState.Visible;
        }
    }
}
