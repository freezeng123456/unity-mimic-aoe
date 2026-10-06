using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class Market073Tests
    {
        World world;EntityState market;
        PlayerState P=>world.Players[0];
        [SetUp] public void SetUp()
        {
            world=new World(Catalog.Load(),42,"ottoman","ottoman",5,MapTopology.Winding);
            // Controlled wallet and producer fixture; natural routes are measured separately.
            P.age=2;P.resources=new Cost(5000,5000,5000);market=world.Spawn("market",0,new Vector2(30,35));
        }
        GameCommand Trade(string resource="food",bool buy=false,int lots=1)=>new GameCommand
        {player=0,kind=CommandKind.Market,actors=new[]{market.id},item=resource,index=buy?1:0,count=lots};
        void Accepted(GameCommand command){var result=world.Issue(command);Assert.IsTrue(result.accepted,result.reason);}
        string State()=>JsonUtility.ToJson(world.CaptureState());

        [TestCase(false,320,85)] [TestCase(true,680,115)]
        public void BatchChargesTheSumOfMarginalPricesAndRecordsRealDebitAndReceipt(bool buy,int coin,int end)
        {
            var quote=world.QuoteMarket(0,market,ResourceKind.Food,buy,5);Assert.IsTrue(quote.Allowed,quote.Reason);
            Assert.AreEqual(coin,quote.Coin);Assert.AreEqual(end,quote.EndPrice);Assert.AreEqual(500,quote.Units);
            int events=world.Events.Count;Accepted(Trade(buy:buy,lots:5));
            Assert.AreEqual(5000+(buy?500:-500),P.resources.food);Assert.AreEqual(5000+(buy?-coin:coin),P.resources.coin);
            Assert.AreEqual(end-100,P.marketFoodPressure);Assert.AreEqual(0,P.marketWoodPressure);
            var rows=world.Events.Skip(events).ToArray();Assert.AreEqual(2,rows.Length);
            Assert.AreEqual("Payment",rows[0].kind);Assert.AreEqual("MarketTrade",rows[1].kind);
            Assert.IsTrue(rows.All(e=>e.hasEconomy));Assert.AreEqual(rows[0].economy.balanceAfter,rows[1].economy.balanceBefore);
            Assert.AreEqual(quote.Paid*-1,rows[0].economy.actualDelta);Assert.AreEqual(quote.Received,rows[1].economy.actualDelta);
            Assert.AreEqual(quote.Received,P.marketIncome);Assert.AreEqual(new Cost(),P.gathered);Assert.AreEqual(new Cost(),P.passiveIncome);
            Assert.AreEqual(quote.Paid,P.spent);Assert.AreEqual(buy?quote.Received:new Cost(),P.marketBought);Assert.AreEqual(buy?new Cost():quote.Paid,P.marketSold);
        }
        [Test] public void BatchAndFiveSinglesHaveEqualWalletAndPrices()
        {
            var other=World.Restore(world.Rules,world.CaptureState());Accepted(Trade(lots:5));
            for(int i=0;i<5;i++)Assert.IsTrue(other.Issue(Trade()).accepted);
            Assert.AreEqual(P.resources,other.Players[0].resources);Assert.AreEqual(P.marketFoodPressure,other.Players[0].marketFoodPressure);
            Assert.AreEqual(P.marketIncome,other.Players[0].marketIncome);
        }
        [TestCase(0)] [TestCase(-1)] [TestCase(6)] [TestCase(int.MaxValue)]
        public void InvalidLotCountsAreRejectedWithoutAnyMutation(int lots)
        {string before=State();Assert.IsFalse(world.Issue(Trade(lots:lots)).accepted);Assert.AreEqual(before,State());}
        [Test] public void UnaffordableBatchDoesNotPartiallySellOrChangeQuotes()
        {P.resources.food=450;string before=State();Assert.IsFalse(world.Issue(Trade(lots:5)).accepted);Assert.AreEqual(before,State());}
        [Test] public void WrongOwnerUnfinishedDestroyedAndWrongProducerCannotExchange()
        {
            var enemy=world.Spawn("market",1,new Vector2(90,90));var unfinished=world.Spawn("market",0,new Vector2(45,35),false);
            var town=world.Entities.First(e=>e.owner==0&&e.def.id=="town");
            foreach(var e in new[]{enemy,unfinished,town}){var c=Trade();c.actors=new[]{e.id};string before=State();Assert.IsFalse(world.Issue(c).accepted);Assert.AreEqual(before,State());}
            world.Remove(market);string state=State();Assert.IsFalse(world.Issue(Trade()).accepted);Assert.AreEqual(state,State());
        }
        [Test] public void MalformedDirectionResourceAndSelectionAreAtomic()
        {
            foreach(var c in new[]{Trade("coin"),Trade("Food"),Trade()})
            {if(c.item=="food")c.index=2;string before=State();Assert.IsFalse(world.Issue(c).accepted);Assert.AreEqual(before,State());}
            var selection=Trade();selection.actors=new[]{market.id,market.id};string state=State();Assert.IsFalse(world.Issue(selection).accepted);Assert.AreEqual(state,State());
        }
        [Test] public void PauseAndEliminationRejectExchange()
        {world.Paused=true;string before=State();Assert.IsFalse(world.Issue(Trade()).accepted);Assert.AreEqual(before,State());world.Paused=false;P.resigned=true;before=State();Assert.IsFalse(world.Issue(Trade()).accepted);Assert.AreEqual(before,State());}
        [Test] public void PriceFloorsCeilingsAndSpreadPreventRoundTripProfit()
        {
            P.resources=new Cost(100000,100000,100000);
            for(int i=0;i<40;i++)Accepted(Trade(lots:5));
            Assert.AreEqual(-50,P.marketFoodPressure);Assert.AreEqual(20,world.QuoteMarket(0,market,ResourceKind.Food,false).Coin);
            float before=P.resources.coin;Accepted(Trade(buy:true));Accepted(Trade());Assert.Less(P.resources.coin,before);
            for(int i=0;i<40;i++)Accepted(Trade(buy:true,lots:5));
            Assert.AreEqual(100,P.marketFoodPressure);Assert.AreEqual(230,world.QuoteMarket(0,market,ResourceKind.Food,true).Coin);
            before=P.resources.coin;Accepted(Trade());Accepted(Trade(buy:true));Assert.Less(P.resources.coin,before);
        }
        [Test] public void MultipleMarketsAndReplacementsRetainPlayerPriceButOpponentsHaveIndependentQuotes()
        {
            Accepted(Trade());var second=world.Spawn("market",0,new Vector2(42,36));var enemy=world.Spawn("market",1,new Vector2(90,90));
            Assert.AreEqual(67,world.QuoteMarket(0,second,ResourceKind.Food,false).Coin);
            Assert.AreEqual(70,world.QuoteMarket(1,enemy,ResourceKind.Food,false).Coin);
            world.Remove(market);market=world.Spawn("market",0,new Vector2(30,35));Assert.AreEqual(67,world.QuoteMarket(0,market,ResourceKind.Food,false).Coin);
        }
        [Test] public void SaturatedWalletCannotGenerateCoinsWithoutSpendingFood()
        {P.resources.food=1e20f;string before=State();Assert.IsFalse(world.Issue(Trade()).accepted);Assert.AreEqual(before,State());}
        [Test] public void ConfigurablePriceJumpCannotExceedTheSpreadAndEnableAnInfiniteProfitCycle()
        {
            var rules=SnapshotCodec.Clone(world.Rules);rules.market.priceStep=100;
            var invalid=new World(rules);var producer=invalid.Spawn("market",0,new Vector2(38,36));
            string before=JsonUtility.ToJson(invalid.CaptureState());
            Assert.IsFalse(invalid.QuoteMarket(0,producer,ResourceKind.Food,true).Allowed);
            Assert.IsFalse(invalid.Issue(new GameCommand{player=0,kind=CommandKind.Market,actors=new[]{producer.id},item="food",index=1}).accepted);
            Assert.AreEqual(before,JsonUtility.ToJson(invalid.CaptureState()));
        }
        [Test] public void QuotesAreReadOnlyAndColdDiskRestoreRetainsNextActualPriceAndLedger()
        {
            Accepted(Trade(lots:3));string before=State();world.QuoteMarket(0,market,ResourceKind.Wood,true,5);Assert.AreEqual(before,State());
            string path=Path.Combine(Path.GetTempPath(),"market073-"+Guid.NewGuid().ToString("N")+".json");
            try
            {
                File.WriteAllText(path,before);var restored=World.Restore(world.Rules,JsonUtility.FromJson<WorldSnapshot>(File.ReadAllText(path)));
                Assert.AreEqual(before,JsonUtility.ToJson(restored.CaptureState()));Accepted(Trade());Assert.IsTrue(restored.Issue(Trade()).accepted);
                Assert.AreEqual(State(),JsonUtility.ToJson(restored.CaptureState()));
            }
            finally{if(File.Exists(path))File.Delete(path);}
        }
        [Test] public void InvalidSavedMarketPressureIsRejected()
        {var snapshot=world.CaptureState();snapshot.players[0].state.marketFoodPressure=-51;Assert.Throws<ArgumentException>(()=>World.Restore(world.Rules,snapshot));}
        [Test] public void MarketCommandsReplayFromColdJsonThroughAllStepsAndBothSeekDirections()
        {
            var sim=new MatchSimulation(world);var identity=ReplayIdentity.Create(world.Rules,"market073-test","controlled073","test-engine");
            using(var recorder=new ReplayRecorder(sim,identity,anchorInterval:10,startKind:"controlled-market-fixture"))
            {
                Assert.IsTrue(recorder.Issue(Trade(lots:3)).accepted);for(int i=0;i<7;i++)Assert.IsTrue(sim.Step());
                Assert.IsTrue(recorder.Issue(Trade("wood",true,2)).accepted);for(int i=0;i<14;i++)Assert.IsTrue(sim.Step());
                var document=SnapshotCodec.Clone(recorder.Capture());string expected=State();
                using(var replay=new ReplaySession(world.Rules,document,identity))
                {
                    while(replay.CurrentStep<replay.EndStep)Assert.Greater(replay.Advance(3),0,replay.Error);
                    Assert.IsTrue(replay.Verified,replay.Error);Assert.AreEqual(expected,JsonUtility.ToJson(replay.World.CaptureState()));
                    Assert.IsTrue(replay.Seek(5));while(replay.IsSeeking)replay.AdvanceSeek(3);
                    Assert.IsTrue(replay.Seek(replay.EndStep));while(replay.IsSeeking)replay.AdvanceSeek(3);
                    Assert.IsEmpty(replay.Error);Assert.AreEqual(expected,JsonUtility.ToJson(replay.World.CaptureState()));
                }
            }
        }
        [Test] public void LanRetransmissionExecutesMarketPaymentOnlyOnce()
        {
            var sim=new MatchSimulation(world);using(var recorder=new ReplayRecorder(sim,ReplayIdentity.Create(world.Rules,"073-test","lan073","test")))
            {
                var authority=new LanAuthority(world,"market-match","market-epoch",recorder.Issue,(paused,reason)=>recorder.Pause(paused,reason));
                authority.BindPeer("owner",0,1);
                var request=new LanCommandRequest{matchId="market-match",epoch="market-epoch",peerId="owner",generation=1,sequence=1,operation=LanOperation.Command,command=Trade(lots:2)};
                authority.Submit(request);authority.Submit(request);var ack=authority.Drain(0,1).Single();Assert.IsTrue(ack.accepted,ack.reason);
                Assert.AreEqual(1,world.Events.Count(e=>e.kind=="MarketTrade"));Assert.AreEqual(4800,P.resources.food);Assert.AreEqual(5137,P.resources.coin);
                Assert.IsTrue(authority.Submit(request).accepted);Assert.IsEmpty(authority.Drain(1,2));Assert.AreEqual(1,world.Events.Count(e=>e.kind=="MarketTrade"));
            }
        }
        [Test] public void CoinOnlyShortageProtectsCompleteTradeStartupBeforeMilitaryConsumesLedgerFee()
        {
            foreach(var e in world.Entities.Where(e=>e.def.resource==ResourceKind.Coin))e.remaining=0;
            world.Spawn("barracks",0,new Vector2(38,32));world.Spawn("foundry",0,new Vector2(46,32));world.Spawn("stable",0,new Vector2(54,32));P.resources=new Cost(1800,935,371);
            var plan=new AiResourceContinuity072(world,0).Evaluate();Assert.IsTrue(plan.NeedsCoin);Assert.IsFalse(plan.NeedsWood);
            Assert.IsTrue(plan.TradeBootstrap);Assert.GreaterOrEqual(plan.Reserve.coin,150);Assert.GreaterOrEqual(plan.Reserve.wood,400);
        }
        [Test] public void LanFullAndDeltaRetainQuotesAndLedgerWithoutRepeatingTransactions()
        {
            var first=LanProjection.Full(world,"market-match","market-epoch",1,0);
            var replica=new LanReplica(world.Rules);replica.Install(SnapshotCodec.Clone(first));
            var playerReference=replica.World.Players[0];Accepted(Trade(lots:2));
            var next=LanProjection.Full(world,"market-match","market-epoch",2,0);
            Assert.IsTrue(replica.Apply(SnapshotCodec.Clone(LanProjection.Delta(first,next,world.Events))),replica.Error);
            Assert.AreSame(playerReference,replica.World.Players[0]);Assert.AreEqual(P.resources,playerReference.resources);
            Assert.AreEqual(P.marketFoodPressure,playerReference.marketFoodPressure);Assert.AreEqual(P.marketIncome,playerReference.marketIncome);
            Assert.AreEqual(JsonUtility.ToJson(world.CaptureState(false)),JsonUtility.ToJson(replica.World.CaptureState(false)));
            Assert.AreEqual(1,replica.World.Events.Count(e=>e.kind=="MarketTrade"));
        }
        [Test] public void AiSellsSurplusFoodForCoinAndPreservesTownWoodAndFoodObligations()
        {
            P.resources=new Cost(1600,500,11);var own=world.Entities.Where(e=>e.Alive&&e.owner==0).ToList();
            var plan=new AiResourceContinuity072.Plan{NeedsCoin=true,TownRecovery=true,CoinDemand=160,Reserve=new Cost(0,500,150)};
            Assert.IsTrue(AiMarket073.TryRelieve(world,0,own,plan,new Cost(800,0,0)));
            Assert.GreaterOrEqual(P.resources.coin,160);Assert.AreEqual(500,P.resources.wood);Assert.GreaterOrEqual(P.resources.food,800);
            Assert.IsFalse(AiMarket073.TryRelieve(world,0,own,plan,new Cost(800,0,0)),"No sale after obligation is funded");
        }
        [Test] public void AiCannotSellProtectedFoodOrTradeWithoutARealMarket()
        {
            P.resources=new Cost(850,500,11);var own=world.Entities.Where(e=>e.Alive&&e.owner==0).ToList();
            var plan=new AiResourceContinuity072.Plan{NeedsCoin=true,CoinDemand=160,Reserve=new Cost(0,500,150)};string before=State();
            Assert.IsFalse(AiMarket073.TryRelieve(world,0,own,plan,new Cost(800,0,0)));Assert.AreEqual(before,State());
            P.resources.food=2000;world.Remove(market);Assert.IsFalse(AiMarket073.TryRelieve(world,0,own,plan,new Cost()));
        }
        [Test] public void IntegratedAiIssuesPaidMarketCommandAndColdContinuationIsIdentical()
        {
            foreach(var e in world.Entities.Where(e=>e.def.resource==ResourceKind.Coin))e.remaining=0;
            world.Spawn("barracks",0,new Vector2(38,32));P.resources=new Cost(1800,935,11);
            var sim=new MatchSimulation(world,0);Assert.IsTrue(sim.Step());
            Assert.IsTrue(world.Events.Any(e=>e.player==0&&e.kind=="MarketTrade"),"Ordinary AI review must use the same paid command as a human");
            Assert.Greater(P.marketIncome.coin,0);Assert.Less(P.marketFoodPressure,0);
            var coldWorld=World.Restore(world.Rules,JsonUtility.FromJson<WorldSnapshot>(State()));
            var cold=MatchSimulation.Restore(coldWorld,SnapshotCodec.Clone(sim.CaptureState()));
            for(int i=0;i<25;i++){Assert.IsTrue(sim.Step());Assert.IsTrue(cold.Step());}
            Assert.AreEqual(State(),JsonUtility.ToJson(coldWorld.CaptureState()));
            Assert.AreEqual(JsonUtility.ToJson(sim.CaptureState()),JsonUtility.ToJson(cold.CaptureState()));
        }
        [Test] public void MarketBridgeDoesNotReleaseTheUnpaidLedgerFeeOrAlreadyPaidPostObligation()
        {
            foreach(var e in world.Entities.Where(e=>e.def.resource==ResourceKind.Coin))e.remaining=0;
            world.Spawn("barracks",0,new Vector2(38,32));P.resources=new Cost(1800,935,11);
            var own=world.Entities.Where(e=>e.Alive&&e.owner==0).ToList();var continuity=new AiResourceContinuity072(world,0);
            Assert.IsTrue(AiMarket073.TryRelieve(world,0,own,continuity.Evaluate(),new Cost()));
            var funded=continuity.Evaluate();Assert.IsFalse(funded.NeedsCoin);Assert.IsTrue(funded.CoinChannelNeeded);
            Assert.IsTrue(funded.TradeBootstrap);Assert.GreaterOrEqual(funded.Reserve.coin,150);
            var post=world.Spawn("trade_post",0,world.Get(world.TradeSockets[0].entity).position,false);
            var paidPost=continuity.Evaluate();Assert.IsTrue(paidPost.LedgerBootstrap);Assert.GreaterOrEqual(paidPost.Reserve.coin,150);
            var ledger=world.Rules.Tech("cargo_ledgers");
            post.queue.Add(new ProductionJob{kind="Research",item=ledger.id,duration=ledger.time,paid=ledger.cost});
            var pending=continuity.Evaluate();Assert.IsFalse(pending.LedgerBootstrap);Assert.AreEqual(0,pending.Reserve.coin,"Already paid ledger must not reserve its cost again");
        }
        [Test] public void UrgentCavalryCounterCannotSpendAnApprovedUnpaidLedgerFee()
        {
            foreach(var e in world.Entities.Where(e=>e.def.resource==ResourceKind.Coin))e.remaining=0;
            world.Spawn("barracks",0,new Vector2(38,32));P.resources=new Cost(1800,935,151);
            var ai=new SkirmishAI(world,0);var method=typeof(SkirmishAI).GetMethod("CanSpend",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            Assert.IsFalse((bool)method.Invoke(ai,new object[]{new Cost(120,0,80),new Cost(),"",true}),"Emergency spending must preserve the actual dependency fee");
            P.technologies.Add("cargo_ledgers");
            Assert.IsTrue((bool)method.Invoke(ai,new object[]{new Cost(120,0,80),new Cost(),"",true}),"After the dependency is paid there is no duplicate coin reservation");
        }
    }
}
