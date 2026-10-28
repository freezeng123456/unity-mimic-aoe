using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Frontier.Tests
{
    // Actual InputSystem -> EventSystem -> UGUI Button path, not onClick.Invoke.
    public sealed class HomeCityUiTests
    {
        private InputSettings.BackgroundBehavior oldBackground;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;
        private Mouse mouse;private GameObject session;private GameController game;
        [UnitySetUp] public IEnumerator Start()
        {
            // Batch-mode test runner has no focused native window. Only the fixture
            // routes synthetic devices to UGUI; restore both settings at teardown.
            oldBackground=InputSystem.settings.backgroundBehavior;oldEditorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            mouse=InputSystem.AddDevice<Mouse>();session=new GameObject("Home city native UI regression");
            game=session.AddComponent<GameController>();yield return null;
            game.StartMatch();game.enabled=false;game.World.Players[0].age=2;game.World.Players[0].shipmentTokens=3;
            game.Hud.ShowHomeCity();yield return null;Canvas.ForceUpdateCanvases();yield return null;

        }
        [UnityTearDown] public IEnumerator Finish(){Object.Destroy(session);yield return null;InputSystem.RemoveDevice(mouse);InputSystem.settings.backgroundBehavior=oldBackground;InputSystem.settings.editorInputBehaviorInPlayMode=oldEditorInput;}
        private Button Find(string prefix)=>session.GetComponentsInChildren<Button>().Single(b=>b.name.StartsWith(prefix));
        private Vector2 Center(Button b)=>RectTransformUtility.WorldToScreenPoint(null,((RectTransform)b.transform).TransformPoint(((RectTransform)b.transform).rect.center));
        private void Click(Vector2 point)
        {InputSystem.QueueStateEvent(mouse,new MouseState{position=point}.WithButton(MouseButton.Left));InputSystem.QueueStateEvent(mouse,new MouseState{position=point});}
        private int Ships()=>game.World.Events.Count(e=>e.kind=="ShipmentSent"&&e.player==0);
        private IEnumerator PrepareRecordedShipmentSlot()
        {
            // These four regressions replay a 0.2.5 input gesture, including one
            // exact card rectangle. Pin its card to the recorded seventh slot;
            // civilization preset ordering is separately tested and may evolve.
            var deck=game.World.Players[0].deck;deck.Remove("food_supply_2");deck.Insert(6,"food_supply_2");
            if(deck.Count>game.Rules.deckCapacity)deck.RemoveAt(deck.Count-1);
            Assert.AreEqual(25,deck.Count);Assert.AreEqual("food_supply_2",deck[6]);
            Assert.AreEqual("",DeckLibrary.Validate(game.Rules,game.World.Players[0].civilization,deck));
            game.Hud.ShowHomeCity();yield return null;Canvas.ForceUpdateCanvases();yield return null;
            Assert.IsNotNull(Find("NEXT 1/4"));Assert.IsNotNull(Find("FOOD CONVOY 2"));
        }
        private IEnumerator FinishAutoplayFixture()
        {
            game.Hud.CloseModal();game.Autoplay=true;
            foreach(var e in game.World.Entities.ToArray())
                if(e.Alive&&e.owner==1&&e.def.kind!=EntityKind.Resource)
                    game.World.ApplyDamage(e,100000,DamageKind.Siege,0);
            game.World.Tick(.1f);Assert.AreEqual(0,game.World.Winner);
            game.enabled=true;yield return null;yield return null;
            Assert.IsTrue(game.Hud.ResultVisible);
        }
        [UnityTest] public IEnumerator AutoplayEliminationResultAcceptsMainMenuPointer()
        {
            yield return FinishAutoplayFixture();
            var point=Center(Find("MAIN MENU"));Click(point);yield return null;yield return null;
            Assert.IsTrue(game.InMenu);Assert.IsFalse(game.Hud.ResultVisible);
            Assert.IsNotNull(Find("BEGIN SKIRMISH"));
        }
        [UnityTest] public IEnumerator AutoplayEliminationResultAcceptsPlayAgainPointer()
        {
            yield return FinishAutoplayFixture();var oldWorld=game.World;
            var point=Center(Find("PLAY AGAIN"));Click(point);yield return null;yield return null;
            Assert.AreNotSame(oldWorld,game.World);Assert.AreEqual(-1,game.World.Winner);
            Assert.IsFalse(game.Hud.ResultVisible);Assert.IsFalse(game.InMenu);
        }
        [UnityTest] public IEnumerator NextClicksRebuildPagesWithoutSendingAvailableCards()
        {
            var next=Center(Find("NEXT 1/"));Click(next);yield return null;yield return null;
            Assert.IsNotNull(Find("NEXT 2/"));Assert.AreEqual(0,Ships());
            Assert.Less(Vector2.Distance(next,Center(Find("NEXT 2/"))),.01f);
            Click(next);yield return null;yield return null;
            Assert.IsNotNull(Find("NEXT 3/"));Assert.AreEqual(0,Ships());Assert.AreEqual(3,game.World.Players[0].shipmentTokens);
        }
        [UnityTest] public IEnumerator UnlockingCardsUnderStationaryPointerDoesNotSendThemWhenNextIsClicked()
        {
            yield return PrepareRecordedShipmentSlot();
            var player=game.World.Players[0];player.age=1;player.shipmentTokens=0;yield return null;
            var card=Find("FOOD CONVOY 2");InputSystem.QueueStateEvent(mouse,new MouseState{position=Center(card)});yield return null;
            Assert.IsFalse(card.interactable);player.age=2;player.shipmentTokens=1;yield return null;
            Assert.IsTrue(card.interactable);Click(Center(Find("NEXT 1/")));yield return null;yield return null;
            Assert.IsNotNull(Find("NEXT 2/"));Assert.AreEqual(0,Ships());
        }
        [UnityTest] public IEnumerator NativeRecordedCardThenNextCoordinatesExplainOneShipmentAndPageTwo()
        {
            yield return PrepareRecordedShipmentSlot();
            // 0.2.5 native received points, normalized from a 2560x1440 render.
            var canvas=(RectTransform)session.GetComponentInChildren<Canvas>().transform;
            System.Func<Vector2,Vector2> fromNative=point=>RectTransformUtility.WorldToScreenPoint(null,canvas.TransformPoint(new Vector2(canvas.rect.xMin+point.x/1.6f,canvas.rect.yMax-(1440-point.y)/1.6f)));
            var cardPoint=fromNative(new Vector2(1008,415));
            Assert.IsTrue(RectTransformUtility.RectangleContainsScreenPoint((RectTransform)Find("FOOD CONVOY 2").transform,cardPoint));
            Click(cardPoint);yield return null;yield return null;
            Assert.AreEqual(1,Ships());Assert.IsTrue(game.World.Players[0].sentCards.Contains("food_supply_2"));
            Click(fromNative(new Vector2(1002,212)));yield return null;yield return null;
            Assert.IsNotNull(Find("NEXT 2/"));Assert.AreEqual(1,Ships());
        }
        [UnityTest] public IEnumerator PressNextReleaseOverCardDoesNotSendEither()
        {
            yield return PrepareRecordedShipmentSlot();
            InputSystem.QueueStateEvent(mouse,new MouseState{position=Center(Find("NEXT 1/"))}.WithButton(MouseButton.Left));yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=Center(Find("FOOD CONVOY 2"))});yield return null;yield return null;
            Assert.AreEqual(0,Ships());Assert.IsNotNull(Find("NEXT 1/"));
        }

        private EntityState PrepareBarracks(Cost resources)
        {
            game.Hud.CloseModal();var building=game.World.Spawn("barracks",0,new Vector2(45,25));
            game.World.Players[0].resources=resources;game.Controls.Select(building);game.Hud.InvalidateActions();return building;
        }
        private Button ProductionButton()=>session.GetComponentsInChildren<Button>().Single(b=>b.name.StartsWith("Musketeer")&&b.transform.parent.name=="Context actions");
        [UnityTest] public IEnumerator VisibleBatchControlPaysReservesAndRefundsWholeBatch()
        {
            var building=PrepareBarracks(new Cost(400,0,150));yield return null;yield return null;
            Click(Center(Find("TRAIN: SINGLE")));yield return null;yield return null;
            Assert.IsNotNull(Find("TRAIN: BATCH"));var button=ProductionButton();
            Assert.IsTrue(button.name.Contains("×5"));Assert.IsTrue(button.name.Contains("375F"));Assert.IsTrue(button.name.Contains("125C"));
            Click(Center(button));yield return null;yield return null;
            Assert.AreEqual(1,building.queue.Count);Assert.AreEqual(5,building.queue[0].count);
            var player=game.World.Players[0];Assert.AreEqual(25,player.resources.food);Assert.AreEqual(25,player.resources.coin);Assert.AreEqual(5,player.reservedPopulation);
            var cancel=session.GetComponentsInChildren<Button>().Single(b=>b.transform.parent.name=="Production queue");
            Click(Center(cancel));yield return null;yield return null;
            Assert.IsEmpty(building.queue);Assert.AreEqual(400,player.resources.food);Assert.AreEqual(150,player.resources.coin);Assert.AreEqual(0,player.reservedPopulation);
        }
        [UnityTest] public IEnumerator BatchButtonDisablesForWholeCostAndPopulationThenSingleRemainsAvailable()
        {
            var building=PrepareBarracks(new Cost(300,0,100));yield return null;yield return null;
            Assert.IsTrue(ProductionButton().interactable);Click(Center(Find("TRAIN: SINGLE")));yield return null;yield return null;
            Assert.IsFalse(ProductionButton().interactable);Click(Center(ProductionButton()));yield return null;yield return null;Assert.IsEmpty(building.queue);
            var player=game.World.Players[0];player.resources=new Cost(1000,1000,1000);player.populationCap=player.usedPopulation+4;game.Hud.InvalidateActions();yield return null;yield return null;
            Assert.IsFalse(ProductionButton().interactable);
            Click(Center(Find("TRAIN: BATCH")));yield return null;yield return null;Assert.IsTrue(ProductionButton().interactable);
        }
        [UnityTest] public IEnumerator MultiBuildingSelectionCanTrainWhenFirstQueueIsFull()
        {
            var first=PrepareBarracks(new Cost(2000,2000,2000));
            var second=game.World.Spawn("barracks",0,new Vector2(45,38));
            // Real paid orders fill the first queue, then the UI must find the other producer.
            for(int i=0;i<6;i++)Assert.IsTrue(game.World.Issue(new GameCommand{player=0,kind=CommandKind.Train,actors=new[]{first.id},item="musketeer",count=1}).accepted);
            game.Controls.Selected.Add(second.id);game.Hud.InvalidateActions();yield return null;yield return null;
            Assert.IsTrue(ProductionButton().interactable);Click(Center(ProductionButton()));yield return null;yield return null;
            Assert.AreEqual(6,first.queue.Count);Assert.AreEqual(1,second.queue.Count);
        }

        [UnityTest] public IEnumerator ReleaseInsideNextThenMoveAwaySameUpdateStillClicksNext()
        {
            var next=Center(Find("NEXT 1/"));
            InputSystem.QueueStateEvent(mouse,new MouseState{position=next}.WithButton(MouseButton.Left));yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=next});
            InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(10,Screen.height-10)});
            yield return null;yield return null;
            Assert.IsNotNull(Find("NEXT 2/"));Assert.AreEqual(0,Ships());
        }
        [UnityTest] public IEnumerator CompletedNextClickThenMoveToCardCannotSendThatCard()
        {
            yield return PrepareRecordedShipmentSlot();
            Click(Center(Find("NEXT 1/")));
            InputSystem.QueueStateEvent(mouse,new MouseState{position=Center(Find("FOOD CONVOY 2"))});
            yield return null;yield return null;
            Assert.AreEqual(0,Ships(),"A later hover position must not replace the completed click's coordinates");
            Assert.IsNotNull(Find("NEXT 2/"));
        }

        [UnityTest] public IEnumerator TwoCompletedNextClicksInOneUpdateReachPageThree()
        {
            var point=Center(Find("NEXT 1/"));Click(point);Click(point);
            // Page creation needs one canvas render before the next preserved edge.
            yield return null;yield return null;yield return null;
            Assert.IsNotNull(Find("NEXT 3/"));Assert.AreEqual(0,Ships());
        }
        [UnityTest] public IEnumerator HeldUiPressSurvivesPeriodicActionRefresh()
        {
            PrepareBarracks(new Cost(1000,1000,1000));yield return null;yield return null;
            var point=Center(Find("TRAIN: SINGLE"));
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point}.WithButton(MouseButton.Left));yield return null;
            yield return new WaitForSecondsRealtime(1.1f);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return null;yield return null;
            Assert.IsNotNull(Find("TRAIN: BATCH"));Assert.IsTrue(ProductionButton().name.Contains("×5"));
        }
        [UnityTest] public IEnumerator ClickCoincidingWithPeriodicActionRebuildIsNotDropped()
        {
            PrepareBarracks(new Cost(1000,1000,1000));yield return null;yield return null;
            var point=Center(Find("TRAIN: SINGLE"));
            // A complete down/up may arrive before Hud.Update, with the physical
            // button already released. Force the real periodic-refresh branch.
            typeof(FrontierHud).GetField("refreshAt",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(game.Hud,-1f);
            Click(point);yield return null;yield return null;yield return null;
            Assert.IsNotNull(Find("TRAIN: BATCH"),"A completed click must survive the action bar's same-frame rebuild");
        }
        [UnityTest] public IEnumerator GroupsRemainAtSamePointWhenLeadingSoldierDies()
        {
            game.Hud.CloseModal();var first=game.World.Spawn("musketeer",0,new Vector2(35,25));var survivor=game.World.Spawn("longbow",0,new Vector2(36,25));
            game.Controls.Select(first);game.Controls.Selected.Add(survivor.id);yield return null;yield return null;
            var originalPoint=Center(Find("GROUPS"));
            game.World.ApplyDamage(first,10000,DamageKind.Siege,1);yield return null;yield return null;
            Assert.AreEqual(survivor.id,game.Controls.FirstSelected.id);
            Assert.Less(Vector2.Distance(originalPoint,Center(Find("GROUPS"))),.01f,"Shared squad commands must not move when the leading soldier dies");
            Click(originalPoint);yield return null;yield return null;yield return null;
            Assert.IsNotNull(Find("SAVE 1"));
        }
        [UnityTest] public IEnumerator MinimapClickBeforeRebuiltUiCannotClearItsPendingLayout()
        {
            PrepareBarracks(new Cost(1000,1000,1000));yield return null;yield return null;
            var point=Center(Find("TRAIN: SINGLE"));var mini=(RectTransform)session.GetComponentInChildren<RawImage>().transform;
            var mapPoint=RectTransformUtility.WorldToScreenPoint(null,mini.TransformPoint(mini.rect.center));
            typeof(FrontierHud).GetField("refreshAt",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(game.Hud,-1f);
            Click(mapPoint);Click(point);yield return null;yield return null;yield return null;
            Assert.IsNotNull(Find("TRAIN: BATCH"),"A minimap edge must not make not-yet-rendered UI appear ready");
        }
    }
}
