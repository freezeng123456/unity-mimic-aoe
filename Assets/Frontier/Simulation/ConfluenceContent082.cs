using System;
using UnityEngine;

namespace Frontier
{
    // Immutable ordinary two-player confluence anchors, surveyed on the real
    // 0.24 opening. The existing central mine and woodland already serve the
    // northern pocket: no new resource, late relocation, budget or RNG here.
    public static class ConfluenceContent082
    {
        public const int Revision=1,WoodCount=4;
        public static bool Applies(MapLayout layout)=>layout!=null&&layout.Id==MapTopology.Tributaries
            &&!layout.Generated&&!layout.HasScenario&&layout.Starts.Length==2;
        public static Vector2 NorthLanding=>new Vector2(50,70);
        public static Vector2 SouthLanding=>new Vector2(53.4f,54.1f);
        public static Vector2 Dock=>new Vector2(52,49);
        public static Vector2 SouthDockContext=>new Vector2(44,56);
        public static Vector2 NorthDockContext=>new Vector2(62,68);
        public static Vector2 Landmark=>new Vector2(44,85);
        public static MapResourceAnchor NorthMine=>new MapResourceAnchor("mine",new Vector2(54,67));
        public static MapResourceAnchor NorthWood(int index)
        {
            switch(index)
            {
                case 0:return new MapResourceAnchor("tree",new Vector2(43,77));
                case 1:return new MapResourceAnchor("tree",new Vector2(45.5f,73));
                case 2:return new MapResourceAnchor("tree",new Vector2(47,71));
                case 3:return new MapResourceAnchor("tree",new Vector2(48.5f,73));
                default:throw new ArgumentOutOfRangeException(nameof(index));
            }
        }
        public static Vector2 Landing(int index)
        {
            if(index==0)return SouthLanding;
            if(index==1)return NorthLanding;
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }
}
