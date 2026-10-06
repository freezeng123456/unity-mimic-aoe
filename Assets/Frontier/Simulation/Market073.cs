using System;
using UnityEngine;

namespace Frontier
{
    [Serializable] public sealed class MarketRules
    {
        public int lotSize=100,maxLots=5,basePrice=100,spread=30,priceStep=3,minPrice=50,maxPrice=200;
        public bool Valid=>lotSize>0&&lotSize<=1000&&maxLots>0&&maxLots<=20&&spread>0&&priceStep>0&&
            minPrice>spread&&basePrice>=minPrice&&basePrice<=maxPrice&&maxPrice<=10000&&priceStep<=maxPrice-minPrice&&priceStep<2*spread;
    }
    public sealed class MarketQuote
    {
        public Cost Paid,Received;
        public int Lots,Units,Coin,StartPrice,EndPrice;
        public string Reason="";
        public bool Allowed=>Reason=="";
    }
    public partial class World
    {
        public MarketQuote QuoteMarket(int player,EntityState market,ResourceKind resource,bool buy,int lots=1)
        {
            var q=new MarketQuote{Lots=lots};var rules=Rules.market;var p=P(player);
            if(p==null||!IsPlayerActive(player)){q.Reason="Player is inactive";return q;}
            if(rules==null||!rules.Valid){q.Reason="Resource exchange is unavailable";return q;}
            if(resource!=ResourceKind.Food&&resource!=ResourceKind.Wood){q.Reason="Choose food or wood";return q;}
            if(lots<1||lots>rules.maxLots){q.Reason="Choose 1 to "+rules.maxLots+" lots";return q;}
            if(market==null||Get(market.id)!=market||!market.Alive||!market.Complete||market.owner!=player||market.def.id!="market")
            {q.Reason="Select your completed market";return q;}
            int pressure=resource==ResourceKind.Food?p.marketFoodPressure:p.marketWoodPressure;
            int price=rules.basePrice+pressure;
            if(price<rules.minPrice||price>rules.maxPrice){q.Reason="Invalid market quote";return q;}
            q.StartPrice=price+(buy?rules.spread:-rules.spread);q.Units=lots*rules.lotSize;
            for(int i=0;i<lots;i++)
            {
                q.Coin+=price+(buy?rules.spread:-rules.spread);
                price=Mathf.Clamp(price+(buy?rules.priceStep:-rules.priceStep),rules.minPrice,rules.maxPrice);
            }
            q.EndPrice=price;
            var goods=resource==ResourceKind.Food?new Cost(q.Units,0,0):new Cost(0,q.Units,0);
            q.Paid=buy?new Cost(0,0,q.Coin):goods;q.Received=buy?goods:new Cost(0,0,q.Coin);
            if(IsMatchEnded)q.Reason="Match has ended";
            else if(Paused)q.Reason="Resume before exchanging resources";
            else if(!SnapshotCodec.Finite(p.resources)||p.resources.food<q.Paid.food||p.resources.wood<q.Paid.wood||p.resources.coin<q.Paid.coin)
                q.Reason="Need "+q.Paid;
            else
            {
                // Reject float saturation rather than generating goods without a real debit.
                var remaining=Sub(p.resources,q.Paid);var after=remaining+q.Received;
                var debited=Sub(p.resources,remaining);var credited=Sub(after,remaining);
                if(!ExactMarketDelta(debited,q.Paid)||!ExactMarketDelta(credited,q.Received)||!SnapshotCodec.Finite(after))
                    q.Reason="Balance exceeds exchange precision";
            }
            return q;
        }
        static bool ExactMarketDelta(Cost actual,Cost expected)=>Mathf.Abs(actual.food-expected.food)<.01f&&
            Mathf.Abs(actual.wood-expected.wood)<.01f&&Mathf.Abs(actual.coin-expected.coin)<.01f;
        CommandResult ExchangeAtMarket(GameCommand c)
        {
            if(c.actors.Length!=1||c.index<0||c.index>1)return CommandResult.Fail("Select one market and choose buy or sell");
            ResourceKind resource=c.item=="food"?ResourceKind.Food:c.item=="wood"?ResourceKind.Wood:ResourceKind.None;
            bool buy=c.index==1;var market=Get(c.actors[0]);var q=QuoteMarket(c.player,market,resource,buy,c.count);
            if(!q.Allowed)return CommandResult.Fail(q.Reason);
            var p=Players[c.player];string description=(buy?"Buy ":"Sell ")+q.Units+" "+resource+" for "+q.Coin+" C";
            Pay(c.player,q.Paid,market.id,description);
            var before=p.resources;p.resources+=q.Received;p.marketIncome+=q.Received;
            if(buy)p.marketBought+=q.Received;else p.marketSold+=q.Paid;
            if(resource==ResourceKind.Food)p.marketFoodPressure=q.EndPrice-Rules.market.basePrice;else p.marketWoodPressure=q.EndPrice-Rules.market.basePrice;
            RecordTransaction("MarketTrade",p,market.id,0,description,q.Received,before);
            return CommandResult.Ok(description);
        }
    }
}
