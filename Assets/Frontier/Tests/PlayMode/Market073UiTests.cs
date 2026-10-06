using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Frontier.Tests
{
    public sealed class Market073UiTests:NativeInputFixture
    {
        [Serializable] sealed class MarketReport {public MarketPlayerReport[] players;}
        [Serializable] sealed class MarketPlayerReport
        {
            public int player,marketFoodPressure,marketWoodPressure;
            public Cost gathered,spent,passiveIncome,tradeIncome,marketIncome,marketSold,marketBought,resources,refunded;
        }
        const BindingFlags PrivateInstance=BindingFlags.Instance|BindingFlags.NonPublic;
        PlayerState Player=>game.World.Players[0];
        Button ActiveButton(string name)=>session.GetComponentsInChildren<Button>().Single(b=>b.isActiveAndEnabled&&b.name==name);
        Text TextNamed(string name)=>session.GetComponentsInChildren<Text>().Single(t=>t.isActiveAndEnabled&&t.name==name);
        static string Caption(Button button)=>button.GetComponentInChildren<Text>().text;
        int Trades=>game.World.Events.Count(e=>e.kind=="MarketTrade"&&e.player==0);
        void Refresh()=>typeof(FrontierHud).GetMethod("Update",PrivateInstance).Invoke(game.Hud,null);
        IEnumerator Prepare(Cost? resources=null)
        {
            Player.age=2;Player.resources=resources??new Cost(2000,2000,2000);
            var market=SpawnHere("market",0);game.Controls.Select(market);yield return Ready();
            Assert.IsTrue(ActiveButton("RESOURCE EXCHANGE").interactable);
        }
        void Open(){ActiveButton("RESOURCE EXCHANGE").onClick.Invoke();Assert.IsTrue(game.Hud.ModalVisible);}
        static void EqualCost(Cost expected,Cost actual)
        {Assert.AreEqual(expected.food,actual.food,.001f);Assert.AreEqual(expected.wood,actual.wood,.001f);Assert.AreEqual(expected.coin,actual.coin,.001f);}

        [UnityTest] public IEnumerator EntryRequiresOwnedCompletedMarket()
        {
            yield return Prepare();Assert.IsNotNull(ActiveButton("RESOURCE EXCHANGE"));
            var foreign=SpawnHere("market",1,site+Vector2.right*15);game.Controls.Select(foreign);yield return Ready();
            Assert.IsFalse(session.GetComponentsInChildren<Button>().Any(b=>b.isActiveAndEnabled&&b.name=="RESOURCE EXCHANGE"));
            var foundation=SpawnHere("market",0,site+Vector2.up*15,false);game.Controls.Select(foundation);yield return Ready();
            Assert.IsFalse(session.GetComponentsInChildren<Button>().Any(b=>b.isActiveAndEnabled&&b.name=="RESOURCE EXCHANGE"));
        }
        [UnityTest] public IEnumerator RealTradeButtonsPayAndReceiveQuotedResourcesAndRefreshPrices()
        {
            yield return Prepare();Open();var buy=ActiveButton("BUY FOOD");var sell=ActiveButton("SELL WOOD");
            StringAssert.Contains("100 F  ·  PAY 130 C",Caption(buy));StringAssert.Contains("100 W  ·  RECEIVE 70 C",Caption(sell));
            var before=Player.resources;buy.onClick.Invoke();EqualCost(new Cost(before.food+100,before.wood,before.coin-130),Player.resources);
            Assert.AreEqual(3,Player.marketFoodPressure);Assert.AreEqual(1,Trades);Assert.AreSame(buy,ActiveButton("BUY FOOD"));
            StringAssert.Contains("PAY 133 C",Caption(buy));StringAssert.Contains("RECEIVE 73 C",Caption(ActiveButton("SELL FOOD")));
            before=Player.resources;sell.onClick.Invoke();EqualCost(new Cost(before.food,before.wood-100,before.coin+70),Player.resources);
            Assert.AreEqual(-3,Player.marketWoodPressure);Assert.AreEqual(2,Trades);Assert.AreSame(sell,ActiveButton("SELL WOOD"));
            StringAssert.Contains("RECEIVE 67 C",Caption(sell));StringAssert.Contains("COIN "+Player.resources.coin.ToString("0.##"),TextNamed("Market wallet").text);
        }
        [UnityTest] public IEnumerator FiveLotToggleKeepsButtonsAndUsesExactAtomicTotal()
        {
            yield return Prepare();Open();var buttons=new[]{ActiveButton("BUY FOOD"),ActiveButton("SELL FOOD"),ActiveButton("BUY WOOD"),ActiveButton("SELL WOOD")};
            var batch=ActiveButton("Market batch");batch.onClick.Invoke();StringAssert.Contains("500 UNITS",Caption(batch));
            for(int i=0;i<buttons.Length;i++)Assert.AreSame(buttons[i],ActiveButton(buttons[i].name));
            StringAssert.Contains("500 F  ·  PAY 680 C",Caption(buttons[0]));StringAssert.Contains("500 W  ·  RECEIVE 320 C",Caption(buttons[3]));
            var before=Player.resources;buttons[0].onClick.Invoke();EqualCost(new Cost(before.food+500,before.wood,before.coin-680),Player.resources);
            Assert.AreEqual(15,Player.marketFoodPressure);Assert.AreEqual(1,Trades);
            StringAssert.Contains("PAY 755 C",Caption(buttons[0]));StringAssert.Contains("RECEIVE 395 C",Caption(buttons[1]));
            batch.onClick.Invoke();StringAssert.Contains("100 UNITS",Caption(batch));StringAssert.Contains("100 F  ·  PAY 145 C",Caption(buttons[0]));
            Assert.AreSame(batch,ActiveButton("Market batch"));
        }
        [UnityTest] public IEnumerator UnaffordableBatchIsDisabledAndForcedStaleClickCannotPartiallyPay()
        {
            yield return Prepare(new Cost(499,2000,679));Open();var buy=ActiveButton("BUY FOOD");var sell=ActiveButton("SELL FOOD");
            Assert.IsTrue(buy.interactable);Assert.IsTrue(sell.interactable);ActiveButton("Market batch").onClick.Invoke();
            Assert.IsFalse(buy.interactable);Assert.IsFalse(sell.interactable);StringAssert.Contains("PAY 680 C",Caption(buy));StringAssert.Contains("Need",Caption(sell));
            var before=Player.resources;int trades=Trades;buy.onClick.Invoke();sell.onClick.Invoke();
            EqualCost(before,Player.resources);Assert.AreEqual(0,Player.marketFoodPressure);Assert.AreEqual(trades,Trades);
            Player.resources=new Cost(500,2000,680);Refresh();Assert.IsTrue(buy.interactable);Assert.IsTrue(sell.interactable);
            sell.onClick.Invoke();EqualCost(new Cost(0,2000,1000),Player.resources);Assert.AreEqual(-15,Player.marketFoodPressure);Assert.AreEqual(trades+1,Trades);
        }
        [UnityTest] public IEnumerator PriceChangedByAnotherMarketIsRequotedAtClickWithoutRebuilding()
        {
            yield return Prepare();Open();var buy=ActiveButton("BUY FOOD");var other=SpawnHere("market",0,site+Vector2.up*15);
            Assert.IsTrue(game.World.Issue(new GameCommand{player=0,kind=CommandKind.Market,actors=new[]{other.id},item="food",index=1,count=1}).accepted);
            StringAssert.Contains("PAY 130 C",Caption(buy));var before=Player.resources;buy.onClick.Invoke();
            EqualCost(new Cost(before.food+100,before.wood,before.coin-133),Player.resources);Assert.AreEqual(6,Player.marketFoodPressure);
            Assert.AreSame(buy,ActiveButton("BUY FOOD"));StringAssert.Contains("PAY 136 C",Caption(buy));
            StringAssert.Contains("RECEIVE 76 C",Caption(ActiveButton("SELL FOOD")));
        }
        [UnityTest] public IEnumerator DestroyedMarketDisablesExistingExchangeAndOldEntryCannotReopenIt()
        {
            yield return Prepare();var entry=ActiveButton("RESOURCE EXCHANGE");var market=game.Controls.FirstSelected;Open();var buy=ActiveButton("BUY FOOD");
            game.World.Remove(market);Refresh();Assert.IsTrue(game.Hud.ModalVisible);Assert.AreSame(buy,ActiveButton("BUY FOOD"));
            Assert.IsFalse(buy.interactable);Assert.IsFalse(ActiveButton("Market batch").interactable);StringAssert.Contains("Select your completed market",TextNamed("Market status").text);
            var before=Player.resources;int trades=Trades;buy.onClick.Invoke();EqualCost(before,Player.resources);Assert.AreEqual(trades,Trades);
            game.Hud.CloseModal();entry.onClick.Invoke();Assert.IsFalse(game.Hud.ModalVisible);
        }
        [UnityTest] public IEnumerator AutoplayStateChangeDisablesOpenExchangeAndRejectsOldEntry()
        {
            yield return Prepare();var entry=ActiveButton("RESOURCE EXCHANGE");Open();var buy=ActiveButton("BUY FOOD");var before=Player.resources;int trades=Trades;
            game.Autoplay=true;Refresh();Assert.IsFalse(game.CanSubmitOrders);Assert.IsFalse(buy.interactable);
            Assert.IsFalse(ActiveButton("Market batch").interactable);StringAssert.Contains("read-only",TextNamed("Market status").text);
            buy.onClick.Invoke();EqualCost(before,Player.resources);Assert.AreEqual(trades,Trades);
            game.Hud.CloseModal();entry.onClick.Invoke();Assert.IsFalse(game.Hud.ModalVisible);yield return Ready();
            Assert.IsFalse(session.GetComponentsInChildren<Button>().Any(b=>b.isActiveAndEnabled&&b.name=="RESOURCE EXCHANGE"));
            game.Autoplay=false;
        }
        [UnityTest] public IEnumerator PauseAndDisconnectedLanAuthorityDisableAndResumeSameButtons()
        {
            yield return Prepare();Open();var buy=ActiveButton("BUY FOOD");var before=Player.resources;int trades=Trades;
            game.World.Paused=true;Refresh();Assert.IsFalse(buy.interactable);StringAssert.Contains("Resume",TextNamed("Market status").text);
            buy.onClick.Invoke();EqualCost(before,Player.resources);Assert.AreEqual(trades,Trades);
            game.World.Paused=false;Refresh();Assert.IsTrue(buy.interactable);Assert.AreSame(buy,ActiveButton("BUY FOOD"));
            var role=typeof(GameController).GetField("lanRole",PrivateInstance);role.SetValue(game,GameSessionRole.LanClient);
            try
            {
                Assert.IsFalse(game.IsLanConnected);Refresh();Assert.IsFalse(game.CanSubmitOrders);Assert.IsFalse(buy.interactable);
                buy.onClick.Invoke();EqualCost(before,Player.resources);Assert.AreEqual(trades,Trades);
            }
            finally{role.SetValue(game,GameSessionRole.Single);}
            Refresh();Assert.IsTrue(buy.interactable);Assert.AreSame(buy,ActiveButton("BUY FOOD"));
        }
        [UnityTest] public IEnumerator HeldPointerSurvivesLiveQuoteRefreshAndReleaseUsesCurrentPrice()
        {
            yield return Prepare();Open();yield return Ready();var buy=ActiveButton("BUY FOOD");var other=SpawnHere("market",0,site+Vector2.up*15);
            Vector2 point=ButtonCenter(buy);InputSystem.QueueStateEvent(mouse,new MouseState{position=point}.WithButton(MouseButton.Left));yield return Ready();
            Assert.IsTrue(game.World.Issue(new GameCommand{player=0,kind=CommandKind.Market,actors=new[]{other.id},item="food",index=1,count=1}).accepted);
            yield return Ready();Assert.AreSame(buy,ActiveButton("BUY FOOD"));StringAssert.Contains("PAY 133 C",Caption(buy));
            var before=Player.resources;int trades=Trades;InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return Ready();
            EqualCost(new Cost(before.food+100,before.wood,before.coin-133),Player.resources);Assert.AreEqual(trades+1,Trades);
            Assert.AreSame(buy,ActiveButton("BUY FOOD"));Assert.IsTrue(game.Hud.ModalVisible);
        }
        [UnityTest] public IEnumerator ActualReportSeparatesMarketReceiptsAndReconcilesPaidUiTrades()
        {
            yield return Prepare();Open();var initial=Player.resources;
            ActiveButton("BUY FOOD").onClick.Invoke();ActiveButton("SELL WOOD").onClick.Invoke();Assert.AreEqual(2,Trades);
            string directory=Path.Combine(Path.GetTempPath(),"frontier-market-report073-"+Guid.NewGuid().ToString("N"));
            var reportDirectory=typeof(GameController).GetField("reportDirectoryOverride",PrivateInstance);var previous=reportDirectory.GetValue(game);
            try
            {
                reportDirectory.SetValue(game,directory);
                typeof(GameController).GetMethod("WriteReport",PrivateInstance).Invoke(game,new object[]{"controlled-market-ui073"});
                string raw=File.ReadAllText(Directory.GetFiles(directory,"*.json").Single());
                foreach(string key in new[]{"marketIncome","marketSold","marketBought","marketFoodPressure","marketWoodPressure"})StringAssert.Contains("\""+key+"\"",raw);
                var report=JsonUtility.FromJson<MarketReport>(raw).players.Single(p=>p.player==0);
                EqualCost(new Cost(100,0,70),report.marketIncome);EqualCost(new Cost(0,100,0),report.marketSold);EqualCost(new Cost(100,0,0),report.marketBought);
                Assert.AreEqual(3,report.marketFoodPressure);Assert.AreEqual(-3,report.marketWoodPressure);
                EqualCost(Player.resources,report.resources);EqualCost(Player.spent,report.spent);
                EqualCost(new Cost(),report.gathered);EqualCost(new Cost(),report.passiveIncome);EqualCost(new Cost(),report.tradeIncome);
                EqualCost(initial+report.marketIncome+report.refunded+report.spent*-1,report.resources);
                string events=File.ReadAllText(Directory.GetFiles(directory,"*-events.jsonl").Single());
                Assert.AreEqual(2,events.Split('\n').Count(line=>line.Contains("\"kind\":\"MarketTrade\"")));
            }
            finally
            {
                reportDirectory.SetValue(game,previous);if(Directory.Exists(directory))Directory.Delete(directory,true);
            }
        }
    }
}
