using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public sealed partial class MapLayout
    {
        private static readonly Dictionary<string,MapLayout> rosterLayouts=new Dictionary<string,MapLayout>();
        private static readonly Queue<string> rosterLayoutOrder=new Queue<string>();
        public static MapLayout Create(string id,int seed,int players)
        {
            if(players<2||players>4)throw new ArgumentOutOfRangeException(nameof(players));
            var original=Create(id,seed);if(players==2)return original;
            string key=id+":"+seed.ToString(System.Globalization.CultureInfo.InvariantCulture)+":"+players;
            lock(rosterLayouts)
            {
                if(rosterLayouts.TryGetValue(key,out var known))return known;
                var navigation=new GridNavigation();navigation.ConfigureTerrain(p=>original.IsWater(p)&&!original.IsBridge(p)||original.IsBridgeSideBarrierCell(p));
                var starts=new MapStartLayout[players];
                for(int player=0;player<players;player++)
                {
                    var first=original.Starts[player%2].Town;
                    Vector2 desired=player<2?first:new Vector2(first.x,128-first.y),chosen=Vector2.zero;float best=float.MaxValue;
                    for(int y=15;y<=113;y++)for(int x=15;x<=113;x++)
                    {
                        var p=new Vector2(x,y);float score=(p-desired).sqrMagnitude;
                        if(score>=best||!navigation.IsAreaFree(p,9,9)||original.BridgeFootprintOverlaps(p,15,15)||original.SignedBankDistance(p)<9)continue;
                        bool close=false;for(int other=0;other<player;other++)if((p-starts[other].Town).sqrMagnitude<42*42){close=true;break;}if(close)continue;
                        chosen=p;best=score;
                    }
                    if(best==float.MaxValue)throw new InvalidOperationException("No safe multiplayer opening on "+id+" for player "+player);
                    starts[player]=new MapStartLayout(player,chosen,43);
                }
                var layout=new MapLayout(id,original.Channels,original.Bridges,original.FishPoints,original.PatrolPoints,original.TradeRoute,
                    seed,starts,(Vector2[])original.NeutralSites.Clone(),(Vector2[])original.ObjectiveSites.Clone(),original.RequiresTransport);
                foreach(var start in starts)FillOpeningResources(start);
                rosterLayouts.Add(key,layout);rosterLayoutOrder.Enqueue(key);
                while(rosterLayoutOrder.Count>12)rosterLayouts.Remove(rosterLayoutOrder.Dequeue());
                return layout;
            }
        }
        private static void FillOpeningResources(MapStartLayout start)
        {
            int index=0;float sx=start.Town.x<64?1:-1,sy=start.Town.y<64?1:-1;
            void Add(string id,Vector2 offset,bool owned=false)=>start.Resources[index++]=new MapResourceAnchor(id,start.Town+new Vector2(offset.x*sx,offset.y*sy),owned);
            Add("berry",new Vector2(8,-7));Add("berry",new Vector2(11,-7));
            for(int i=0;i<5;i++)Add("deer",new Vector2(-8+i*1.8f,9+i%2*1.8f));
            Add("mine",new Vector2(-10,-5));
            for(int i=0;i<18;i++)Add("tree",new Vector2(-14+i%6*2.2f,-12-i/6*2.2f));
            Add("crate_food",new Vector2(5,1),true);Add("crate_wood",new Vector2(5,3),true);
            Add("mine",new Vector2(22,7));
            for(int i=0;i<9;i++)Add("tree",new Vector2(17+i%3*2.1f,19+i/3*2.1f));
            for(int i=0;i<5;i++)Add("deer",new Vector2(9+i*2,25));
        }
        public bool InOpeningTerritory(int player,Vector2 point)
        {
            if(player<0||player>=Starts.Length)return false;
            float distance=(point-Starts[player].Town).sqrMagnitude;
            for(int other=0;other<Starts.Length;other++)if(other!=player&&(point-Starts[other].Town).sqrMagnitude+16<distance)return false;
            return true;
        }
        private Vector2[][] MultiPlayerShoreSites(bool landing)
        {
            var sites=new Vector2[Starts.Length][];var navigation=new GridNavigation();
            navigation.ConfigureTerrain(p=>IsWater(p)&&!IsBridge(p)||IsBridgeSideBarrierCell(p));
            foreach(var start in Starts)navigation.SetBlocked(start.Town,4,true);
            for(int player=0;player<Starts.Length;player++)
            {
                var chosen=new List<Vector2>();if(!HasWater){sites[player]=chosen.ToArray();continue;}
                for(int alternative=0;alternative<2;alternative++)
                {
                    Vector2 point=Vector2.zero;float best=float.MaxValue;
                    for(int y=9;y<=119;y++)for(int x=9;x<=119;x++)
                    {
                        var p=new Vector2(x,y);float bank=SignedBankDistance(p);
                        if(bank<2.8f||bank>4.2f||!InStartingRegion(player,p)||!InOpeningTerritory(player,p)||BridgeFootprintOverlaps(p,10,10))continue;
                        // A dry warehouse can clear a town's occupied cells and
                        // still fail the full rotated warehouse/quay separation
                        // rule. Reserve a yard around every roster town, including
                        // the larger footprint used by the opening search above.
                        bool nearTown=false;foreach(var start in Starts)if((p-start.Town).sqrMagnitude<14*14){nearTown=true;break;}if(nearTown)continue;
                        var direction=WaterDirection(p);if(direction.sqrMagnitude<.9f||!HarborLandClear(p,direction)||SignedBankDistance(p+direction*7)>-2.2f)continue;
                        bool close=false;
                        foreach(var current in chosen)if((p-current).sqrMagnitude<12*12){close=true;break;}
                        if(landing)foreach(var current in DockSites[player])if((p-current).sqrMagnitude<10*10){close=true;break;}
                        if(close)continue;
                        float score=(p-Starts[player].Town).sqrMagnitude+Mathf.Abs(bank-3.3f);
                        if(score>=best)continue;point=p;best=score;
                    }
                    if(best<float.MaxValue)chosen.Add(point);
                }
                if(chosen.Count==0)throw new InvalidOperationException("No usable "+(landing?"landing":"harbor")+" on "+Id+" for player "+player);
                sites[player]=chosen.ToArray();
            }
            return sites;
        }
    }
}
