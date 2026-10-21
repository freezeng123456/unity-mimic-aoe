using UnityEngine;

namespace Frontier
{
    public sealed partial class MapLayout
    {
        // Protection is shared by physical bank height and visible sediment.
        // Original bridge/ramp, quay, production-exit and land-deployment pads
        // retain the exact historical query. No extra mask, blocker or collider.
        public float EstuaryPreservationWeight083(Vector2 point)
        {
            if(!EstuaryContent083.Applies(this))return 0;
            float weight=1;
            foreach(var bridge in Bridges)
            {
                var p=bridge.Local(point);
                float x=Mathf.Max(0,Mathf.Abs(p.x)-bridge.HalfLength-bridge.RampLength-2);
                float y=Mathf.Max(0,Mathf.Abs(p.y)-bridge.HalfWidth-2);
                weight=Mathf.Min(weight,Mathf.SmoothStep(0,1,Mathf.Sqrt(x*x+y*y)/6));
            }
            foreach(var start in Starts)
                weight=Mathf.Min(weight,Mathf.SmoothStep(0,1,(Vector2.Distance(point,start.Town)-12)/6));
            if(DockSites!=null)foreach(var bank in DockSites)foreach(var dock in bank)
                weight=Mathf.Min(weight,Mathf.SmoothStep(0,1,(Vector2.Distance(point,dock)-EstuaryContent083.DockPreserveRadius)/6));
            for(int i=0;i<EstuaryContent083.LandingCount;i++)
                weight=Mathf.Min(weight,Mathf.SmoothStep(0,1,(Vector2.Distance(point,EstuaryContent083.Landing(i))-EstuaryContent083.LandingPreserveRadius)/4));
            for(int i=0;i<TradeRoute.Length-1;i++)
            {
                var a=TradeRoute[i];var delta=TradeRoute[i+1]-a;
                float t=Mathf.Clamp01(Vector2.Dot(point-a,delta)/Mathf.Max(.000001f,delta.sqrMagnitude));
                weight=Mathf.Min(weight,Mathf.SmoothStep(0,1,(Vector2.Distance(point,a+delta*t)-4)/4));
            }
            return weight;
        }
        public float EstuaryMouthWeight083(Vector2 point)
        {
            if(!EstuaryContent083.Applies(this))return 0;
            // Reuse the existing nearest-channel cache; bilinear interpolation
            // follows the river instead of cutting a horizontal colour boundary.
            float px=Mathf.Clamp(point.x/Cell,0,Resolution-1),py=Mathf.Clamp(point.y/Cell,0,Resolution-1);
            int x=Mathf.Min(Resolution-2,Mathf.FloorToInt(px)),y=Mathf.Min(Resolution-2,Mathf.FloorToInt(py));
            float tx=px-x,ty=py-y;int i=x+y*Resolution;
            var center=Vector2.Lerp(Vector2.Lerp(nearestCenter[i],nearestCenter[i+1],tx),
                Vector2.Lerp(nearestCenter[i+Resolution],nearestCenter[i+Resolution+1],tx),ty);
            return Mathf.SmoothStep(0,1,(center.y-80)/32);
        }
        public float EstuaryBankHeight083(Vector2 point,float distance,float historical)
        {
            if(distance<=0||distance>=10||!EstuaryContent083.Applies(this))return historical;
            float weight=EstuaryMouthWeight083(point)*EstuaryPreservationWeight083(point);
            if(weight<=0)return historical;
            // A low static tidal flat with a short water-edge lip and a broad
            // inland rise. Wet bed and navigable shoreline remain unchanged.
            float lip=Mathf.Lerp(WaterHeight-.02f,WaterHeight+.06f,Mathf.SmoothStep(0,1,distance/.8f));
            float flat=Mathf.Lerp(lip,0,Mathf.SmoothStep(0,1,(distance-2)/5));
            float shoulder=.09f*Mathf.SmoothStep(0,1,(distance-5)/1.5f)
                *(1-Mathf.SmoothStep(0,1,(distance-8)/2));
            return historical+weight*(flat+shoulder-historical);
        }
        public Color EstuaryBankLayers083(Vector2 point,float distance,Color historical)
        {
            if(distance<=0||distance>=10||!EstuaryContent083.Applies(this))return historical;
            float weight=EstuaryMouthWeight083(point)*EstuaryPreservationWeight083(point);
            if(weight<=0)return historical;
            // Extends the already-existing Estuary wet/sand layer treatment.
            // Layer weights and the low flat share one mouth/pad authority.
            float noise=Mathf.PerlinNoise(point.x*.13f+3,point.y*.11f+8)-.5f;
            float damp=1-Mathf.SmoothStep(0,1,(distance-.25f-noise*.25f)/4.8f);
            float rim=1-Mathf.SmoothStep(0,1,(distance-1.5f-noise*.65f)/8);
            float sand=(1-damp)*rim;
            return Color.Lerp(historical,new Color(damp,sand,1-damp-sand,1),weight);
        }
    }
}
