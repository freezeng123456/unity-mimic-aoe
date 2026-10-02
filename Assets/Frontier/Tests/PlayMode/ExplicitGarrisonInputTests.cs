using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Frontier.Tests
{
    public sealed class ExplicitGarrisonInputTests : NativeInputFixture
    {
        private EntityState worker,town;
        // Reflection keeps the same tests compilable against frozen 0.2.6.
        private bool InGarrisonMode=>(bool?)(typeof(RtsInput).GetField("GarrisonMode")?.GetValue(game.Controls))??false;
        private IEnumerator Prepare()
        {
            town=SpawnHere("town",0);worker=SpawnHere("villager",0,site+Vector2.right*5);
            town.hp-=400;game.Controls.Select(worker);yield return Ready();
        }
        private IEnumerator Begin()
        {yield return Press(Key.G);Assert.IsTrue(InGarrisonMode,"G must enter a visible targeting mode");}
        private IEnumerator OrderShelterAndSelectTown(EntityState actor)
        {
            game.Controls.Select(actor);yield return Ready();yield return Begin();
            yield return Click(ScreenPoint(town.position));
            Assert.AreEqual(OrderKind.Garrison,actor.orders.Single().kind);
            yield return Click(ScreenPoint(town.position));
            Assert.AreEqual(town.id,game.Controls.FirstSelected.id);
        }
        private void EnterShelter(EntityState actor)
        {
            for(int i=0;i<50&&actor.garrisonedIn==0;i++)game.World.Tick(.1f);
            Assert.AreEqual(town.id,actor.garrisonedIn,"The real ordered movement must reach the shelter");
        }
        private void DelayPeriodicActions()
        {
            // Isolate event-driven refresh from the one-second polling fallback.
            // Input, simulation and HUD Update still use their normal paths.
            typeof(FrontierHud).GetField("refreshAt",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
                .SetValue(game.Hud,Time.unscaledTime+10);
        }
        private Button AssertUngarrison(int count)
        {
            var button=FindButton("UNGARRISON");
            Assert.AreEqual("UNGARRISON "+count,button.GetComponentInChildren<Text>().text,"Action and shelter detail must agree before periodic polling");
            Assert.AreEqual(count>0,button.interactable);
            Assert.IsTrue(session.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("Sheltered "+count+"/"+town.def.garrison)));
            return button;
        }
        [UnityTest] public IEnumerator ShelterArrivalAndNativeReleaseRefreshActionsBeforePeriodicPolling()
        {
            yield return Prepare();yield return OrderShelterAndSelectTown(worker);
            AssertUngarrison(0);DelayPeriodicActions();EnterShelter(worker);yield return Ready();
            var release=AssertUngarrison(1);DelayPeriodicActions();
            yield return Click(ButtonCenter(release));
            Assert.AreEqual(0,worker.garrisonedIn);Assert.IsEmpty(town.occupants);AssertUngarrison(0);
            Assert.AreEqual(1,game.World.Events.Count(e=>e.kind=="Ungarrison"&&e.entity==town.id));
        }
        [UnityTest] public IEnumerator DisabledUngarrisonHeldDuringArrivalRequiresFreshClick()
        {
            yield return Prepare();yield return OrderShelterAndSelectTown(worker);
            var disabled=AssertUngarrison(0);DelayPeriodicActions();var point=ButtonCenter(disabled);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point}.WithButton(MouseButton.Left));yield return Ready();
            EnterShelter(worker);yield return Ready();
            Assert.AreSame(disabled,FindButton("UNGARRISON"),"An existing disabled gesture must not bind to a newly enabled action");
            Assert.IsFalse(disabled.interactable);Assert.IsTrue(disabled.gameObject.activeInHierarchy);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return Ready();
            Assert.AreEqual(town.id,worker.garrisonedIn);Assert.AreEqual(0,game.World.Events.Count(e=>e.kind=="Ungarrison"&&e.entity==town.id));
            var release=AssertUngarrison(1);yield return Click(ButtonCenter(release));
            Assert.AreEqual(0,worker.garrisonedIn);AssertUngarrison(0);
        }
        [UnityTest] public IEnumerator HeldUngarrisonSurvivesAnotherArrivalAndReleasesCurrentGarrison()
        {
            yield return Prepare();yield return OrderShelterAndSelectTown(worker);EnterShelter(worker);yield return Ready();
            var incoming=SpawnHere("villager",0,site+Vector2.left*5);
            yield return OrderShelterAndSelectTown(incoming);var release=AssertUngarrison(1);DelayPeriodicActions();var point=ButtonCenter(release);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point}.WithButton(MouseButton.Left));yield return Ready();
            EnterShelter(incoming);yield return Ready();
            Assert.AreSame(release,FindButton("UNGARRISON"),"A same-town held action must keep its original pointer capture");
            Assert.IsTrue(release.gameObject.activeInHierarchy);Assert.AreEqual(2,town.occupants.Count);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return Ready();
            Assert.AreEqual(0,worker.garrisonedIn);Assert.AreEqual(0,incoming.garrisonedIn);Assert.IsEmpty(town.occupants);AssertUngarrison(0);
            Assert.AreEqual(1,game.World.Events.Count(e=>e.kind=="Ungarrison"&&e.entity==town.id));
        }
        [UnityTest] public IEnumerator ExplicitDamagedTownOrderUsesGarrisonAndEntersShelter()
        {
            yield return Prepare();yield return Begin();yield return Click(ScreenPoint(town.position));
            Assert.AreEqual(OrderKind.Garrison,worker.orders.Single().kind);Assert.IsFalse(InGarrisonMode);
            for(int i=0;i<50&&worker.garrisonedIn==0;i++)game.World.Tick(.1f);
            Assert.AreEqual(town.id,worker.garrisonedIn);Assert.IsTrue(town.occupants.Contains(worker.id));
        }
        [UnityTest] public IEnumerator OrdinaryRightClickOnDamagedTownStillRepairs()
        {
            yield return Prepare();yield return Click(ScreenPoint(town.position),MouseButton.Right);
            Assert.AreEqual(OrderKind.Repair,worker.orders.Single().kind);
        }
        [UnityTest] public IEnumerator InvalidTargetKeepsModeAndDoesNotSelectOrOrderTerrain()
        {
            yield return Prepare();yield return Begin();var point=ScreenPoint(site+new Vector2(-10,8));AssertWorldPoint(point);
            yield return Click(point);Assert.IsEmpty(worker.orders);Assert.IsTrue(InGarrisonMode);Assert.AreEqual(worker.id,game.Controls.FirstSelected?.id);
            StringAssert.Contains("Garrison",game.Notice);
            yield return Click(ScreenPoint(town.position));Assert.AreEqual(OrderKind.Garrison,worker.orders.Single().kind);
        }
        [UnityTest] public IEnumerator InvalidBuildingEligibilityKeepsMode()
        {
            yield return Prepare();
            var enemy=SpawnHere("tower",1,site+new Vector2(-8,5));
            var incomplete=SpawnHere("tower",0,site+new Vector2(0,8),false);
            var barracks=SpawnHere("barracks",0,site+new Vector2(8,7));yield return Ready();yield return Begin();
            foreach(var b in new[]{enemy,incomplete,barracks})
            {yield return Click(ScreenPoint(b.position));Assert.IsTrue(InGarrisonMode);Assert.IsEmpty(worker.orders);StringAssert.Contains("Garrison",game.Notice);}
        }
        [UnityTest] public IEnumerator OverlappingEnemyDoesNotStealExplicitShelterTarget()
        {
            yield return Prepare();SpawnHere("pike",1,town.position);yield return Ready();yield return Begin();
            yield return Click(ScreenPoint(town.position));Assert.AreEqual(OrderKind.Garrison,worker.orders.Single().kind);
            Assert.AreEqual(town.id,worker.orders.Single().target);
        }
        [UnityTest] public IEnumerator RightClickEscapeAndGToggleCancelWithoutOrders()
        {
            yield return Prepare();yield return Begin();yield return Click(ScreenPoint(site+Vector2.up*8),MouseButton.Right);
            Assert.IsFalse(InGarrisonMode);Assert.IsEmpty(worker.orders);
            yield return Begin();yield return Press(Key.Escape);Assert.IsFalse(InGarrisonMode);Assert.IsFalse(game.IsPaused);Assert.IsEmpty(worker.orders);
            yield return new WaitForSecondsRealtime(.25f);yield return Begin();yield return Press(Key.G);Assert.IsFalse(InGarrisonMode);Assert.IsEmpty(worker.orders);
        }
        [UnityTest] public IEnumerator ShiftAppendsGarrisonAfterExistingMove()
        {
            yield return Prepare();Assert.IsTrue(game.World.Issue(new GameCommand{player=0,kind=CommandKind.Order,actors=new[]{worker.id},order=OrderKind.Move,point=site+Vector2.right*8}).accepted);
            yield return Begin();InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftShift));
            yield return Click(ScreenPoint(town.position));InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            CollectionAssert.AreEqual(new[]{OrderKind.Move,OrderKind.Garrison},worker.orders.Select(o=>o.kind).ToArray());
        }
        [UnityTest] public IEnumerator AttackBuildAndRestartClearTargetMode()
        {
            yield return Prepare();yield return Begin();yield return Press(Key.A);Assert.IsFalse(InGarrisonMode);Assert.IsTrue(game.Controls.AttackMode);
            yield return Begin();Assert.IsFalse(game.Controls.AttackMode);yield return Press(Key.B);Assert.IsFalse(InGarrisonMode);Assert.IsTrue(game.Hud.ModalVisible);
            game.Hud.CloseModal();yield return new WaitForSecondsRealtime(.25f);yield return Begin();game.StartMatch();yield return Ready();
            Assert.IsFalse(InGarrisonMode);Assert.IsEmpty(game.Controls.Selected);
        }
        [UnityTest] public IEnumerator NoEligibleActorKeepsModeAndReportsRejection()
        {
            yield return Prepare();yield return Begin();game.Controls.Selected.Clear();yield return Click(ScreenPoint(town.position));
            Assert.IsTrue(InGarrisonMode);Assert.IsEmpty(worker.orders);StringAssert.Contains("mobile",game.Notice.ToLowerInvariant());
        }
        [UnityTest] public IEnumerator HudButtonUsesPointerAndDoesNotOverlapCommonActions()
        {
            yield return Prepare();var buttons=session.GetComponentsInChildren<Button>().Where(b=>b.transform.parent.name=="Context actions").ToArray();
            var details=ButtonCenter(FindButton("DETAILS"));var groups=ButtonCenter(FindButton("GROUPS"));
            for(int i=0;i<buttons.Length;i++)for(int j=i+1;j<buttons.Length;j++)
                Assert.Greater(Vector2.Distance(ButtonCenter(buttons[i]),ButtonCenter(buttons[j])),5,"Overlapping buttons: "+buttons[i].name+" / "+buttons[j].name);
            yield return Click(ButtonCenter(FindButton("GARRISON [G]")));Assert.IsTrue(InGarrisonMode);
            yield return new WaitForSecondsRealtime(.25f);yield return Click(ButtonCenter(FindButton("ATTACK MOVE")));
            Assert.IsFalse(InGarrisonMode);Assert.IsTrue(game.Controls.AttackMode);
            var musk=SpawnHere("musketeer",0,site+Vector2.left*5);game.Controls.Select(musk);yield return Ready();
            Assert.Less(Vector2.Distance(details,ButtonCenter(FindButton("DETAILS"))),.1f);
            Assert.Less(Vector2.Distance(groups,ButtonCenter(FindButton("GROUPS"))),.1f);
        }
    }
}
