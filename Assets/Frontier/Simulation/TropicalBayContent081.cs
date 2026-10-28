using System;
using UnityEngine;

namespace Frontier
{
    // Source-surveyed, independently authored literals. Old-source probes
    // establish requested landing/path and pocket geometry; actual new-resource
    // placement and paid flow remain separate current-run acceptance gates.
    public readonly struct TropicalBayRegion081
    {
        public readonly string Id;
        public readonly int Bank;
        public readonly Vector2 BeachCenter,HeadlandCenter,NearLanding,ForwardLanding;
        public readonly Vector2 HeadlandMinePosition,VegetationCenter;
        internal TropicalBayRegion081(string id,int bank,Vector2 beach,Vector2 headland,
            Vector2 near,Vector2 forward,Vector2 mine,Vector2 vegetation)
        {Id=id;Bank=bank;BeachCenter=beach;HeadlandCenter=headland;NearLanding=near;
            ForwardLanding=forward;HeadlandMinePosition=mine;VegetationCenter=vegetation;}
    }

    // One map-owned immutable source; queries return values or fresh immutable
    // resource anchors. No World reference, mutable arrays, RNG or late moves.
    public static class TropicalBayContent081
    {
        public const int Revision=1,RegionCount=2,WoodCount=4,LandingCount=2;
        public const float DockPreserveRadius=8,LandingPreserveRadius=3;
        public static bool Applies(MapLayout layout)=>layout!=null&&layout.Id==MapTopology.Tropical
            &&!layout.Generated&&!layout.HasScenario&&layout.Starts.Length==2;
        public static TropicalBayRegion081 Region(int bank)
        {
            // Distinct actual shoreline requests, never 128-minus the other bank.
            // Central budget only:1mine+4trees perbank replaces the old two
            // central mines+8trees; existing home/safe-forward resources stay.
            switch(bank)
            {
                case 0:return new TropicalBayRegion081("west-bay-beach-headland",0,
                    new Vector2(62,64),new Vector2(56,66),new Vector2(42,34),new Vector2(58,60),
                    new Vector2(52,62),new Vector2(56,75));
                case 1:return new TropicalBayRegion081("east-bay-beach-headland",1,
                    new Vector2(89,68),new Vector2(92,57),new Vector2(80,88),new Vector2(90,60),
                    new Vector2(96,58),new Vector2(94,51));
                default:throw new ArgumentOutOfRangeException(nameof(bank));
            }
        }
        public static Vector2 Landing(int bank,int index)
        {
            var region=Region(bank);
            if(index==0)return region.NearLanding;
            if(index==1)return region.ForwardLanding;
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        public static MapResourceAnchor HeadlandMine(int bank)=>new MapResourceAnchor("mine",Region(bank).HeadlandMinePosition);
        public static MapResourceAnchor HeadlandWood(int bank,int index)
        {
            if(index<0||index>=WoodCount)throw new ArgumentOutOfRangeException(nameof(index));
            Region(bank); // validate both arguments before returning an anchor
            Vector2 position;
            if(bank==0)
            {
                switch(index){case 0:position=new Vector2(46,65);break;case 1:position=new Vector2(49,64);break;
                    case 2:position=new Vector2(51,68);break;default:position=new Vector2(47,70);break;}
            }
            else
            {
                switch(index){case 0:position=new Vector2(100,58);break;case 1:position=new Vector2(103,60);break;
                    case 2:position=new Vector2(101,64);break;default:position=new Vector2(98,62);break;}
            }
            return new MapResourceAnchor("tree",position);
        }
    }
}
