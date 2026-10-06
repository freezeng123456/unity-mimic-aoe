using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public static class AiMarket073
    {
        // Sell a bounded surplus only when ordinary safe sources cannot cover
        // an actual coin obligation. Paid queues are already out of the wallet.
        public static bool TryRelieve(World world,int player,List<EntityState> own,AiResourceContinuity072.Plan plan,Cost obligations)
        {
            if(plan==null||!plan.NeedsCoin||world.Players[player].age<2)return false;
            var market=own.Find(e=>e.Alive&&e.Complete&&e.owner==player&&e.def.id=="market");
            if(market==null)return false;
            var p=world.Players[player];var reserve=plan.ProtectedReserve(obligations);
            // Keep food for another defensive training cycle, even if no age is pending.
            reserve.food=Mathf.Max(reserve.food,400);
            float goal=Mathf.Max(plan.CoinDemand,reserve.coin);
            if(p.resources.coin+.001f>=goal)return false;
            int chosen=0;int maximum=world.Rules.market?.maxLots??0;
            for(int lots=1;lots<=maximum;lots++)
            {
                var quote=world.QuoteMarket(player,market,ResourceKind.Food,false,lots);
                if(!quote.Allowed||!AiResourceContinuity072.CanSpend(world,player,quote.Paid,reserve))break;
                chosen=lots;if(p.resources.coin+quote.Received.coin+.001f>=goal)break;
            }
            if(chosen==0)return false;
            return world.Issue(new GameCommand{player=player,kind=CommandKind.Market,actors=new[]{market.id},item="food",index=0,count=chosen}).accepted;
        }
    }
}
