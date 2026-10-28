using UnityEngine;

namespace Frontier
{
    public sealed partial class MapLayout
    {
        // Localized, shared authority profile. It NEVER changes SignedBankDistance,
        // IsWater, bridge heights, slope blockers, nav-cell ownership or fish.
        // MapLayout.BankHeight calls this only after its existing distance<=0
        // branch; BankLayers passes its exact historical color as the fallback.
        public float TropicalPreservationWeight081(Vector2 point)
        {
            if(!TropicalBayContent081.Applies(this))return 0;
            float weight=1;
            foreach(var bridge in Bridges)
            {
                var p=bridge.Local(point);
                float x=Mathf.Max(0,Mathf.Abs(p.x)-bridge.HalfLength-bridge.RampLength-2);
                float y=Mathf.Max(0,Mathf.Abs(p.y)-bridge.HalfWidth-2);
                weight=Mathf.Min(weight,Mathf.SmoothStep(0,1,Mathf.Sqrt(x*x+y*y)/6));
            }
            // An 8m exact disk covers the rotated4x4 warehouse,3x3 quay and
            // 6.4m spawn exit + largest current vessel radius. Test those actual
            // rectangles/exit explicitly; the disk is not a second footprint.
            if(DockSites!=null)foreach(var bank in DockSites)foreach(var dock in bank)
                weight=Mathf.Min(weight,Mathf.SmoothStep(0,1,(Vector2.Distance(point,dock)-TropicalBayContent081.DockPreserveRadius)/6));
            foreach(var start in Starts)
                weight=Mathf.Min(weight,Mathf.SmoothStep(0,1,(Vector2.Distance(point,start.Town)-12)/6));
            for(int bank=0;bank<TropicalBayContent081.RegionCount;bank++)
                for(int landing=0;landing<TropicalBayContent081.LandingCount;landing++)
                    weight=Mathf.Min(weight,Mathf.SmoothStep(0,1,(Vector2.Distance(point,TropicalBayContent081.Landing(bank,landing))-TropicalBayContent081.LandingPreserveRadius)/4));
            return weight;
        }
        static float Ellipse081(Vector2 point,Vector2 center,Vector2 water,float across,float along)
        {
            var tangent=new Vector2(-water.y,water.x);var delta=point-center;
            float x=Vector2.Dot(delta,water)/across,y=Vector2.Dot(delta,tangent)/along;
            return 1-Mathf.SmoothStep(0,1,Mathf.Sqrt(x*x+y*y));
        }
        void TropicalWeights081(Vector2 point,out float beach,out float headland)
        {
            beach=headland=0;if(!TropicalBayContent081.Applies(this))return;
            float protection=TropicalPreservationWeight081(point);if(protection<=0)return;
            for(int bank=0;bank<TropicalBayContent081.RegionCount;bank++)
            {
                var region=TropicalBayContent081.Region(bank);
                beach=Mathf.Max(beach,Ellipse081(point,region.BeachCenter,WaterDirection(region.BeachCenter),10,17));
                headland=Mathf.Max(headland,Ellipse081(point,region.HeadlandCenter,WaterDirection(region.HeadlandCenter),11,14));
            }
            // Adjacent features blend conservatively; a broad beach does not
            // receive a simultaneous raised-headland shoulder.
            beach*=protection;headland*=protection*(1-beach);
        }
        public float TropicalBankHeight081(Vector2 point,float distance,float historical)
        {
            if(distance<=0||!TropicalBayContent081.Applies(this))return historical;
            TropicalWeights081(point,out float beach,out float headland);
            if(beach<=0&&headland<=0)return historical; // bit-exact protected pads
            float shore=WaterHeight-.02f;
            float broad=shore*(1-Mathf.SmoothStep(0,1,distance/5.5f));
            float cut=shore*(1-Mathf.SmoothStep(0,1,distance/2.2f));
            // A14cm dry shoulder, not a cliff/blocker. Compact support ends8m
            // inland. Its actual gradients must pass the retained slope gate.
            float shoulder=.14f*Mathf.SmoothStep(0,1,(distance-2.2f)/2)
                *(1-Mathf.SmoothStep(0,1,(distance-5)/3));
            return historical+beach*(broad-historical)+headland*(cut+shoulder-historical);
        }
        public Color TropicalBankLayers081(Vector2 point,float distance,Color historical)
        {
            if(distance<=0||!TropicalBayContent081.Applies(this))return historical;
            TropicalWeights081(point,out float beach,out float headland);
            if(beach<=0&&headland<=0)return historical;
            float noise=Mathf.PerlinNoise(point.x*.13f+3,point.y*.11f+8)-.5f;
            float wetWidth=Mathf.Lerp(2.6f,1.85f,headland);
            float sandWidth=4+beach*3.2f-headland*1.5f;
            float damp=1-Mathf.SmoothStep(0,1,(distance-.25f-noise*.25f)/wetWidth);
            float shoulder=1-Mathf.SmoothStep(0,1,(distance-1.5f-noise*.65f)/sandWidth);
            float sand=(1-damp)*shoulder;
            return new Color(damp,sand,1-damp-sand,1);
        }
    }
}
