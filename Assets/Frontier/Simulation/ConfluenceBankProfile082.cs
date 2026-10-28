using UnityEngine;

namespace Frontier
{
    public sealed partial class MapLayout
    {
        // Shared geometry and layer authority, not a render-only shoreline
        // carve. Channels, union distance, wet bed and navigation are unchanged.
        public float ConfluencePreservationWeight082(Vector2 point)
        {
            if(!ConfluenceContent082.Applies(this))return 0;
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
                weight=Mathf.Min(weight,Mathf.SmoothStep(0,1,(Vector2.Distance(point,dock)-8)/6));
            weight=Mathf.Min(weight,Mathf.SmoothStep(0,1,(Vector2.Distance(point,ConfluenceContent082.Dock)-8)/6));
            weight=Mathf.Min(weight,Mathf.SmoothStep(0,1,(Vector2.Distance(point,ConfluenceContent082.NorthDockContext)-8)/6));
            weight=Mathf.Min(weight,Mathf.SmoothStep(0,1,(Vector2.Distance(point,ConfluenceContent082.SouthDockContext)-8)/6));
            for(int landing=0;landing<2;landing++)
                weight=Mathf.Min(weight,Mathf.SmoothStep(0,1,(Vector2.Distance(point,ConfluenceContent082.Landing(landing))-3)/4));
            return weight;
        }
        static float ConfluenceEllipse082(Vector2 point,Vector2 center,Vector2 normal,float across,float along)
        {
            var tangent=new Vector2(-normal.y,normal.x);var delta=point-center;
            float x=Vector2.Dot(delta,normal)/across,y=Vector2.Dot(delta,tangent)/along;
            return 1-Mathf.SmoothStep(0,1,Mathf.Sqrt(x*x+y*y));
        }
        void ConfluenceWeights082(Vector2 point,out float beach,out float shoulder)
        {
            beach=shoulder=0;if(!ConfluenceContent082.Applies(this))return;
            float protection=ConfluencePreservationWeight082(point);if(protection<=0)return;
            beach=Mathf.Max(ConfluenceEllipse082(point,new Vector2(50,66),Vector2.down,11,16),
                ConfluenceEllipse082(point,new Vector2(53,54),Vector2.up,10,14));
            // Surveyed upstream north-pocket shore: keep this dry-land
            // morphology clear of the actual bridge approach and dock pads.
            // Triangulation separately handles protected-bank interpolation.
            beach=Mathf.Max(beach,ConfluenceEllipse082(point,new Vector2(38,82),Vector2.left,3,9));
            shoulder=ConfluenceEllipse082(point,new Vector2(42,83),Vector2.left,5,8);
            beach*=protection;shoulder*=protection*(1-beach);
        }
        public float ConfluenceBankHeight082(Vector2 point,float distance,float historical)
        {
            if(distance<=0||!ConfluenceContent082.Applies(this))return historical;
            ConfluenceWeights082(point,out float beach,out float shoulder);
            if(beach<=0&&shoulder<=0)return historical;
            float broad=(WaterHeight-.02f)*(1-Mathf.SmoothStep(0,1,distance/5));
            float lift=.09f*Mathf.SmoothStep(0,1,(distance-3)/2)
                *(1-Mathf.SmoothStep(0,1,(distance-6)/3));
            return historical+beach*(broad-historical)+shoulder*lift;
        }
        public Color ConfluenceBankLayers082(Vector2 point,float distance,Color historical)
        {
            if(distance<=0||!ConfluenceContent082.Applies(this))return historical;
            ConfluenceWeights082(point,out float beach,out float shoulder);
            if(beach<=0&&shoulder<=0)return historical;
            float noise=Mathf.PerlinNoise(point.x*.13f+3,point.y*.11f+8)-.5f;
            float damp=1-Mathf.SmoothStep(0,1,(distance-.25f-noise*.25f)/(2.6f+beach*.4f));
            float rim=1-Mathf.SmoothStep(0,1,(distance-1.5f-noise*.65f)/(4+beach*2.8f+shoulder));
            float sand=(1-damp)*rim;
            return new Color(damp,sand,1-damp-sand,1);
        }
    }
}
