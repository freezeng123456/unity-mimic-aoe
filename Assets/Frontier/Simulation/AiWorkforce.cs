using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    // Reassigns ordinary workers against a cost forecast supplied by strategy.
    // Own accounts and currently visible resources/threats are its only inputs.
    public sealed class AiWorkforce
    {
        private readonly World world;
        private readonly int player;
        private float rebalanceAt;
        private readonly List<EntityState> available=new List<EntityState>();
        private readonly List<EntityState> resources=new List<EntityState>();
        private readonly Dictionary<int,int> assigned=new Dictionary<int,int>();
        public AiWorkforce(World world,int player){this.world=world;this.player=player;}
        public AiWorkforceSnapshot CaptureState()=>new AiWorkforceSnapshot{rebalanceAt=rebalanceAt};
        public void RestoreState(AiWorkforceSnapshot state)
        {
            AiSaveValidation.Require(state!=null&&AiSaveValidation.Clock(state.rebalanceAt),"workforce clock");
            rebalanceAt=state.rebalanceAt;
            // Available workers and resource occupancy are rebuilt before each decision.
            available.Clear();resources.Clear();assigned.Clear();
        }
        public bool HasSafeSource(ResourceKind kind)=>resources.Exists(e=>e.def.resource==kind);
        public void Assign(List<EntityState> workers,Cost target,List<EntityState> visibleEnemies)
        {
            if(world.Paused||world.Winner>=0)return;
            available.Clear();resources.Clear();assigned.Clear();var current=new int[4];
            foreach(var worker in workers)
            {
                if(!worker.Alive||worker.owner!=player||worker.garrisonedIn!=0)continue;
                var order=worker.orders.Count>0?worker.orders.Peek():null;
                if(order!=null&&order.kind!=OrderKind.Gather)continue;
                available.Add(worker);
                if(order==null)continue;
                var resource=world.Get(order.target);if(resource==null||!resource.Alive)continue;
                current[(int)resource.def.resource]++;
                assigned.TryGetValue(resource.id,out int count);assigned[resource.id]=count+1;
            }
            if(available.Count==0)return;
            foreach(var resource in world.Entities)
            {
                if(resource.def.waterborne||!world.GatherableResource(player,resource))continue;
                bool threatened=false;
                foreach(var enemy in visibleEnemies)
                {
                    if(!world.IsVisible(player,enemy)||!enemy.Military||(enemy.def.tags&Tags.Explorer)!=0)continue;
                    float danger=Mathf.Max(12,enemy.def.range+4);
                    if((enemy.position-resource.position).sqrMagnitude<danger*danger){threatened=true;break;}
                }
                if(!threatened)resources.Add(resource);
            }
            var rate=new float[4];
            foreach(var resource in resources)
            {
                // Short-lived crates are collected opportunistically, not used to
                // predict sustained extraction rates for the whole workforce.
                if(resource.def.id.StartsWith("crate_"))continue;
                int kind=(int)resource.def.resource;
                rate[kind]=Mathf.Max(rate[kind],world.GatherRate(available[0],resource));
            }
            var p=world.Players[player];int banks=0;
            foreach(var entity in world.Entities)if(entity.Alive&&entity.owner==player&&entity.def.id=="bank"&&entity.Complete)banks++;
            float bankForecast=banks*world.CountingHouseRate(player)*60;
            float[] demand={0,Mathf.Max(0,target.food-p.resources.food),Mathf.Max(0,target.wood-p.resources.wood),Mathf.Max(0,target.coin-p.resources.coin-bankForecast)};
            float total=0;
            for(int kind=1;kind<4;kind++){demand[kind]=rate[kind]>0?demand[kind]/rate[kind]:0;total+=demand[kind];}
            if(total<.01f)
            {
                // If all immediate orders are funded, gather for a repeat of the
                // same production mix rather than idling or inventing income.
                demand[1]=rate[1]>0?target.food/rate[1]:0;
                demand[2]=rate[2]>0?target.wood/rate[2]:0;
                demand[3]=rate[3]>0?Mathf.Max(0,target.coin-bankForecast)/rate[3]:0;
                total=demand[1]+demand[2]+demand[3];
                if(total<.01f){for(int kind=1;kind<4;kind++){demand[kind]=rate[kind]>0?1:0;total+=demand[kind];}}
            }
            var wantedCounts=new int[4];var fraction=new float[4];int remaining=available.Count;
            for(int kind=1;kind<4;kind++)
            {
                float share=total>0?available.Count*demand[kind]/total:0;
                wantedCounts[kind]=Mathf.FloorToInt(share);fraction[kind]=share-wantedCounts[kind];remaining-=wantedCounts[kind];
            }
            while(remaining-->0&&total>0){int kind=1;for(int k=2;k<4;k++)if(fraction[k]>fraction[kind])kind=k;wantedCounts[kind]++;fraction[kind]=-1;}
            bool rebalance=world.Time>=rebalanceAt;if(rebalance)rebalanceAt=world.Time+12;
            int changes=0,limit=Mathf.Max(2,Mathf.CeilToInt(available.Count*.25f));
            // Idle workers fill shortages before already productive workers move.
            available.Sort((a,b)=>a.orders.Count.CompareTo(b.orders.Count));
            foreach(var worker in available)
            {
                var order=worker.orders.Count>0?worker.orders.Peek():null;
                var previous=order!=null?world.Get(order.target):null;
                bool unsafeTarget=previous!=null&&!resources.Contains(previous);
                if(order!=null&&!unsafeTarget&&(!rebalance||changes>=limit))continue;
                if(previous!=null&&!unsafeTarget&&current[(int)previous.def.resource]<=wantedCounts[(int)previous.def.resource])continue;
                int kind=1;for(int k=2;k<4;k++)if(wantedCounts[k]-current[k]>wantedCounts[kind]-current[kind])kind=k;
                EntityState best=null;float bestScore=float.MaxValue;
                foreach(var resource in resources)
                {
                    assigned.TryGetValue(resource.id,out int count);if(previous==resource)count--;
                    if(count>=8)continue;
                    float distance=(resource.position-worker.position).sqrMagnitude;
                    float score=distance+(resource.def.resource==(ResourceKind)kind?0:4000)+count*25;
                    if(resource.def.id.StartsWith("crate_")&&distance<24*24)score-=4500;
                    if(score<bestScore){bestScore=score;best=resource;}
                }
                if(best==null)
                {
                    if(unsafeTarget&&SeekShelter(worker,visibleEnemies))
                    {current[(int)previous.def.resource]--;if(assigned.ContainsKey(previous.id))assigned[previous.id]--;}
                    continue;
                }
                if(best==previous)continue;
                var result=world.Issue(new GameCommand{player=player,kind=CommandKind.Order,actors=new[]{worker.id},order=OrderKind.Gather,target=best.id});
                if(!result.accepted)continue;
                if(previous!=null){current[(int)previous.def.resource]--;if(assigned.ContainsKey(previous.id))assigned[previous.id]--;changes++;}
                current[(int)best.def.resource]++;assigned.TryGetValue(best.id,out int old);assigned[best.id]=old+1;
            }
        }
        private bool SeekShelter(EntityState worker,List<EntityState> visibleEnemies)
        {
            EntityState shelter=null;float nearest=float.MaxValue;
            foreach(var building in world.Entities)
            {
                if(!building.Alive||!building.Complete||building.owner!=player||building.def.garrison<=building.occupants.Count)continue;
                float distance=(building.position-worker.position).sqrMagnitude;if(distance<nearest){nearest=distance;shelter=building;}
            }
            if(shelter!=null)return world.Issue(new GameCommand{player=player,kind=CommandKind.Order,actors=new[]{worker.id},order=OrderKind.Garrison,target=shelter.id}).accepted;
            Vector2 away=Vector2.zero;
            foreach(var enemy in visibleEnemies)if(world.IsVisible(player,enemy)&&enemy.Military)away+=(worker.position-enemy.position).normalized;
            if(away.sqrMagnitude<.01f)return false;
            return world.Issue(new GameCommand{player=player,kind=CommandKind.Order,actors=new[]{worker.id},order=OrderKind.Move,point=worker.position+away.normalized*12}).accepted;
        }
    }
}
