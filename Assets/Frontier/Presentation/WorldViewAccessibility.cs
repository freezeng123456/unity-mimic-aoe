using UnityEngine;

namespace Frontier
{
    public sealed partial class WorldView
    {
        LineRenderer guidedMarker;
        void UpdateGuidedMarker()
        {
            var session=game.Guided;bool active=!game.InMenu&&session!=null&&session.Status==GuidedPlayStatus.Running;
            if(!active){if(guidedMarker!=null)SetActive(guidedMarker.gameObject,false);return;}
            if(guidedMarker==null){guidedMarker=NewLine("Current guided objective",.16f);guidedMarker.loop=true;guidedMarker.positionCount=32;TintLine(guidedMarker,new Color(1,.82f,.24f));}
            SetActive(guidedMarker.gameObject,true);var point=session.ObjectivePoint;
            float height=game.World.SurfaceHeight(point,game.World.Layout.IsWater(point))+.2f;
            for(int i=0;i<32;i++){float angle=i*Mathf.PI*2/32;guidedMarker.SetPosition(i,new Vector3(point.x+Mathf.Sin(angle)*2,height,point.y+Mathf.Cos(angle)*2));}
        }
        public void ApplyPreferences()
        {
            nextFog=0;
            foreach(var entry in visuals)
            {
                var visual=entry.Value;visual.ringTintReady=false;
                if(visual.roleMarker!=null)SetActive(visual.roleMarker.gameObject,game.Options.friendEnemyPatterns);
            }
        }
        LineRenderer BuildRoleMarker(EntityState entity,Transform root)
        {
            if(entity.owner<0||entity.def.kind==EntityKind.Resource)return null;
            var line=NewLine("Player "+(entity.owner+1)+" visibility marker",.10f,root);line.useWorldSpace=false;line.loop=true;
            int sides=entity.owner==0?16:entity.owner==1?3:4;line.positionCount=sides;
            float radius=entity.def.kind==EntityKind.Building?Mathf.Max(entity.def.width,entity.def.depth)*.65f:entity.def.radius+.55f;
            float offset=entity.owner==2?Mathf.PI/4:0;
            for(int i=0;i<sides;i++){float angle=offset+i*Mathf.PI*2/sides;line.SetPosition(i,new Vector3(Mathf.Sin(angle)*radius,.15f,Mathf.Cos(angle)*radius));}
            TintLine(line,game.World.AreAllies(0,entity.owner)?new Color(.16f,.78f,.89f):new Color(.98f,.96f,.78f));
            line.gameObject.SetActive(game.Options.friendEnemyPatterns);return line;
        }
        void MarkPlayerPattern(EntityState entity)
        {
            int x=Mathf.RoundToInt(entity.position.x),y=Mathf.RoundToInt(entity.position.y);
            Color32 cue=game.World.AreAllies(0,entity.owner)?new Color32(45,214,237,255):new Color32(252,245,200,255);
            for(int dy=-2;dy<=2;dy++)for(int dx=-2;dx<=2;dx++)
            {
                bool mark=entity.owner==0?(dx==0||dy==0):entity.owner==1?Mathf.Abs(dx)==Mathf.Abs(dy):entity.owner==2?Mathf.Abs(dx)+Mathf.Abs(dy)==2:Mathf.Max(Mathf.Abs(dx),Mathf.Abs(dy))==2;
                int px=x+dx,py=y+dy;if(mark&&px>=0&&px<128&&py>=0&&py<128)mapPixels[py*128+px]=cue;
            }
        }
    }
}
