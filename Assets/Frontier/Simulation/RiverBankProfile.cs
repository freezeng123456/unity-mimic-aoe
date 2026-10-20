using UnityEngine;

namespace Frontier
{
    // Read-only descriptive profile; it does not create water cells, resources,
    // obstacles, movement modifiers or a second terrain authority.
    public readonly struct RiverBankProfile
    {
        public readonly float Distance,InnerBend,OuterBend,Influence,RiseWidth;
        public RiverBankProfile(float distance,float inner,float outer,float influence)
        {
            Distance=distance;InnerBend=inner;OuterBend=outer;Influence=influence;
            // The 0.42m bank rise stays below the existing 0.4 slope contract.
            // Its minimum width is 2.2m, leaving room for smooth mask changes.
            RiseWidth=Mathf.Lerp(3,2.2f,outer);
        }
    }

    public static class RiverBankMath
    {
        public static float SignedCurvature(Vector2 incoming,Vector2 outgoing,float span)
        {
            if(incoming.sqrMagnitude<.000001f||outgoing.sqrMagnitude<.000001f||span<=.0001f)return 0;
            incoming.Normalize();outgoing.Normalize();
            return (incoming.x*outgoing.y-incoming.y*outgoing.x)/span;
        }
        public static float InwardScore(float curvature,Vector2 tangent,Vector2 offset)
        {
            if(tangent.sqrMagnitude<.000001f||offset.sqrMagnitude<.000001f)return 0;
            tangent.Normalize();offset.Normalize();
            float side=tangent.x*offset.y-tangent.y*offset.x;
            return Mathf.Clamp(curvature*30,-1,1)*Mathf.Clamp(side,-1,1);
        }
    }

    public sealed partial class MapLayout
    {
        float[] bankBend;
        struct BankBendFrame {public Vector2 tangent;public float curvature;}
        BankBendFrame[] bankFrames;

        void PrepareBankBendCache()
        {
            // First slice only: confluence/estuary/generated/scenario and legacy
            // layouts retain their prior profiles. No query scans channel samples.
            if(Id!=MapTopology.Winding||HasScenario||Channels.Length!=1)return;
            bankBend=new float[Resolution*Resolution];
            var points=Channels[0].Centers;bankFrames=new BankBendFrame[points.Length];
            const int window=8;
            for(int i=0;i<points.Length;i++)
            {
                int before=Mathf.Max(0,i-window),after=Mathf.Min(points.Length-1,i+window);
                Vector2 incoming=points[i]-points[before],outgoing=points[after]-points[i];
                Vector2 tangent=points[Mathf.Min(points.Length-1,i+1)]-points[Mathf.Max(0,i-1)];
                float span=(incoming.magnitude+outgoing.magnitude)*.5f;
                bankFrames[i]=new BankBendFrame{tangent=tangent.normalized,
                    curvature=RiverBankMath.SignedCurvature(incoming,outgoing,span)};
            }
        }
        void StoreBankBend(int index,Vector2 point,Vector2 center,int segment,float fraction)
        {
            if(bankBend==null||segment<0)return;
            var a=bankFrames[segment];var b=bankFrames[Mathf.Min(segment+1,bankFrames.Length-1)];
            bankBend[index]=RiverBankMath.InwardScore(Mathf.Lerp(a.curvature,b.curvature,fraction),
                Vector2.Lerp(a.tangent,b.tangent,fraction),point-center);
        }
        float CachedBankBend(Vector2 point)
        {
            float px=Mathf.Clamp(point.x/Cell,0,Resolution-1),py=Mathf.Clamp(point.y/Cell,0,Resolution-1);
            int x=Mathf.Min(Resolution-2,Mathf.FloorToInt(px)),y=Mathf.Min(Resolution-2,Mathf.FloorToInt(py));
            float tx=px-x,ty=py-y;int i=x+y*Resolution;
            return Mathf.Lerp(Mathf.Lerp(bankBend[i],bankBend[i+1],tx),
                Mathf.Lerp(bankBend[i+Resolution],bankBend[i+Resolution+1],tx),ty);
        }
        public float BankMorphologyInfluence(Vector2 point)
        {
            if(bankBend==null)return 0;
            float influence=1;
            foreach(var bridge in Bridges)
            {
                Vector2 local=bridge.Local(point);
                float x=Mathf.Max(0,Mathf.Abs(local.x)-bridge.HalfLength-bridge.RampLength-2);
                float y=Mathf.Max(0,Mathf.Abs(local.y)-bridge.HalfWidth-2);
                influence=Mathf.Min(influence,Mathf.SmoothStep(0,1,Mathf.Sqrt(x*x+y*y)/6));
            }
            // Shore-cache construction precedes DockSites. The profile itself
            // is queried only afterwards; this guard also makes that order clear.
            if(DockSites!=null)foreach(var bank in DockSites)foreach(var site in bank)
                influence=Mathf.Min(influence,Mathf.SmoothStep(0,1,(Vector2.Distance(point,site)-8)/6));
            foreach(var start in Starts)
                influence=Mathf.Min(influence,Mathf.SmoothStep(0,1,(Vector2.Distance(point,start.Town)-12)/6));
            return influence;
        }
        RiverBankProfile BankProfile(Vector2 point,float distance)
        {
            if(bankBend==null)return new RiverBankProfile(distance,0,0,0);
            float influence=BankMorphologyInfluence(point),bend=CachedBankBend(point)*influence;
            return new RiverBankProfile(distance,Mathf.Max(0,bend),Mathf.Max(0,-bend),influence);
        }
        public RiverBankProfile BankProfile(Vector2 point)=>BankProfile(point,SignedBankDistance(point));

        public Color BankLayers(Vector2 point)
        {
            var profile=BankProfile(point);float distance=profile.Distance;
            float tidal=Id==MapTopology.Estuary?Mathf.SmoothStep(0,1,(point.y-78)/18f):0;
            float noise=Mathf.PerlinNoise(point.x*.13f+3,point.y*.11f+8)-.5f;
            float wetWidth=Mathf.Lerp(2.6f,3.7f,tidal);
            float sandWidth=Mathf.Lerp(4,6,tidal);
            // Inner lobes reach farther onto existing flat land; outer banks
            // expose a shorter dark-soil rim. No new shoal is made navigable.
            wetWidth=Mathf.Lerp(wetWidth,1.8f,profile.OuterBend);
            sandWidth+=profile.InnerBend*2.5f-profile.OuterBend*1.6f;
            float damp=1-Mathf.SmoothStep(0,1,(distance-.25f-noise*.25f)/wetWidth);
            float shoulder=1-Mathf.SmoothStep(0,1,(distance-1.5f-noise*.65f)/sandWidth);
            float sand=(1-damp)*shoulder;
            var historical=new Color(damp,sand,1-damp-sand,1);
            if(TropicalBayContent081.Applies(this))return TropicalBankLayers081(point,distance,historical);
            return ConfluenceContent082.Applies(this)?ConfluenceBankLayers082(point,distance,historical):historical;
        }
    }
}
