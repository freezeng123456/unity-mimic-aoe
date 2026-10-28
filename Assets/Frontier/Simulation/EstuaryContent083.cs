using System;
using UnityEngine;

namespace Frontier
{
    // Ordinary two-player Estuary only. Original resource identities, channels,
    // bridges, trade, DockSites and AI mode are intentionally not rewritten.
    // Requests resolved and publicly unloaded the largest artillery for both
    // owners with both original northern docks present: ConfluenceEstuary22.
    public static class EstuaryContent083
    {
        public const int Revision=1,LandingCount=2;
        public const float DockPreserveRadius=8,LandingPreserveRadius=3;
        public static bool Applies(MapLayout layout)=>layout!=null&&layout.Id==MapTopology.Estuary
            &&!layout.Generated&&!layout.HasScenario&&layout.Starts.Length==2;
        public static Vector2 WestMouthLanding=>new Vector2(41,92);
        public static Vector2 EastMouthLanding=>new Vector2(72,92);
        public static Vector2 Landing(int index)
        {
            if(index==0)return WestMouthLanding;
            if(index==1)return EastMouthLanding;
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }
}
