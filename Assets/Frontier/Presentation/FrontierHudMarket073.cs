using UnityEngine;
using UnityEngine.UI;

namespace Frontier
{
    public sealed partial class FrontierHud
    {
        Transform marketBox073;
        World marketWorld073;
        string marketMatch073="";
        int marketEntity073,marketPlayer073,marketLots073=1;
        Text marketWallet073,marketStatus073;
        Button marketBatch073;
        readonly Button[] marketButtons073=new Button[4];
        readonly Text[] marketLabels073=new Text[4];

        void CloseMarket073()
        {
            marketBox073=null;marketWorld073=null;marketMatch073="";marketEntity073=0;marketPlayer073=0;marketLots073=1;
            marketWallet073=null;marketStatus073=null;marketBatch073=null;
            for(int i=0;i<marketButtons073.Length;i++){marketButtons073[i]=null;marketLabels073[i]=null;}
        }
        bool MarketContext073=>marketBox073!=null&&modal!=null&&marketBox073.IsChildOf(modal)&&
            ReferenceEquals(marketWorld073,game.World)&&marketMatch073==game.MatchId&&marketPlayer073==game.LocalPlayerSlot&&!game.InMenu;
        void ShowMarket073(EntityState market)
        {
            if(!game.CanSubmitOrders){game.Notify("Resource exchange is unavailable while orders are read-only");return;}
            var world=game.World;
            if(market==null||world.Get(market.id)!=market||!market.Alive||!market.Complete||market.owner!=game.LocalPlayerSlot||market.def.id!="market")
            {game.Notify("Select your completed market");return;}
            var box=Modal("RESOURCE EXCHANGE",655);
            marketBox073=box;marketWorld073=world;marketMatch073=game.MatchId;marketEntity073=market.id;marketPlayer073=game.LocalPlayerSlot;marketLots073=1;
            marketWallet073=Label(box,"",new Rect(25,80,815,45),19,gold);marketWallet073.gameObject.name="Market wallet";
            marketStatus073=Label(box,"",new Rect(25,132,815,58),17,muted);marketStatus073.gameObject.name="Market status";
            marketBatch073=Button(box,"EXCHANGE: 100 UNITS",new Rect(25,200,397,54),()=>
            {
                if(!MarketContext073||!game.CanSubmitOrders||game.World.Rules.market==null||!game.World.Rules.market.Valid)return;
                marketLots073=marketLots073==1?Mathf.Min(5,game.World.Rules.market.maxLots):1;RefreshMarket073();
            },"Choose one lot or five lots. The full quantity and coin total are paid together.");
            marketBatch073.gameObject.name="Market batch";
            Label(box,"One payment for the full batch.\nFood and wood each have their own price.",new Rect(443,203,397,58),17,muted);
            string[] titles={"BUY FOOD","SELL FOOD","BUY WOOD","SELL WOOD"};
            for(int i=0;i<titles.Length;i++)
            {
                int row=i;
                var button=Button(box,titles[i],new Rect(25+(i%2)*418,278+(i/2)*128,397,115),()=>SubmitMarket073(row),"",false);
                marketButtons073[i]=button;marketLabels073[i]=button.GetComponentInChildren<Text>();
            }
            Button(box,"KEEP PLAYING",new Rect(25,545,815,52),CloseModal);
            Label(box,"Every purchase raises that resource's price; every sale lowers it.\nQuotes are shared by all of your markets and recalculated when you trade.",new Rect(25,606,815,42),15,muted);
            RefreshMarket073();
        }
        void RefreshMarket073()
        {
            if(marketBox073==null)return;
            bool context=MarketContext073;var world=context?game.World:null;
            var market=world?.Get(marketEntity073);bool canSubmit=context&&game.CanSubmitOrders;
            bool validMarket=market!=null&&market.Alive&&market.Complete&&market.owner==marketPlayer073&&market.def.id=="market";
            if(marketBatch073!=null)
            {
                marketBatch073.interactable=canSubmit&&validMarket&&world.Rules.market!=null&&world.Rules.market.Valid&&world.Rules.market.maxLots>1;
                int units=marketLots073*(world?.Rules.market?.lotSize??100);
                SetMarketText073(marketBatch073.GetComponentInChildren<Text>(),"EXCHANGE: "+units+" UNITS");
            }
            string reason=!context?"This market belongs to an earlier match":!validMarket?"Select your completed market":
                !canSubmit?game.IsWatching||game.Autoplay?"Observation is read-only":world.Paused?"Resume before exchanging resources":
                world.IsMatchEnded?"Match has ended":"Orders are currently unavailable":"";
            if(context)
            {
                var wallet=world.Players[marketPlayer073].resources;
                SetMarketText073(marketWallet073,$"FOOD {wallet.food:0.##}     WOOD {wallet.wood:0.##}     COIN {wallet.coin:0.##}");
            }
            else SetMarketText073(marketWallet073,"Market unavailable");
            SetMarketText073(marketStatus073,reason==""?"Exchange resources immediately at this completed market.\nPrices below include every lot in the selected batch.":reason);
            for(int i=0;i<marketButtons073.Length;i++)
            {
                var button=marketButtons073[i];if(button==null)continue;
                ResourceKind resource=i<2?ResourceKind.Food:ResourceKind.Wood;bool buy=i%2==0;
                var quote=context?world.QuoteMarket(marketPlayer073,market,resource,buy,marketLots073):null;
                bool allowed=canSubmit&&validMarket&&quote!=null&&quote.Allowed;button.interactable=allowed;
                string title=(buy?"BUY ":"SELL ")+resource.ToString().ToUpperInvariant();
                string detail=quote!=null&&quote.Units>0?quote.Units+" "+(resource==ResourceKind.Food?"F":"W")+"  ·  "+(buy?"PAY ":"RECEIVE ")+quote.Coin+" C":"Unavailable";
                string rowReason=reason!=""?reason:quote?.Reason??"Market unavailable";
                string next=rowReason!=""?rowReason:"Next "+(buy?"buy":"sale")+": "+(quote.EndPrice+(buy?world.Rules.market.spread:-world.Rules.market.spread))+" C / "+world.Rules.market.lotSize+" units";
                SetMarketText073(marketLabels073[i],title+"\n"+detail+"\n"+next);
                var hover=button.GetComponent<FrontierHover>();if(hover!=null)hover.message=title+"\n"+detail+"\n"+next;
            }
        }
        static void SetMarketText073(Text label,string value){if(label!=null&&label.text!=value)label.text=value;}
        void SubmitMarket073(int row)
        {
            // A held pointer may span a wallet, price or authority change. Keep
            // its Button alive and revalidate the actual command at release.
            if(!MarketContext073||!game.CanSubmitOrders){RefreshMarket073();return;}
            var market=game.World.Get(marketEntity073);ResourceKind resource=row<2?ResourceKind.Food:ResourceKind.Wood;bool buy=row%2==0;
            var quote=game.World.QuoteMarket(marketPlayer073,market,resource,buy,marketLots073);
            if(!quote.Allowed){game.Notify(quote.Reason);RefreshMarket073();return;}
            SubmitModalCommand(new GameCommand{player=marketPlayer073,kind=CommandKind.Market,actors=new[]{marketEntity073},
                item=resource==ResourceKind.Food?"food":"wood",index=buy?1:0,count=marketLots073},marketBox073,_=>RefreshMarket073());
        }
    }
}
