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
    public class OrderedInputTests
    {
        private Mouse mouse;
        private Keyboard keyboard;
        private GameObject session;
        private GameController game;
        private EntityState[] workers;
        private EntityState town,explorer;

        [UnitySetUp] public IEnumerator StartSession()
        {
            mouse=InputSystem.AddDevice<Mouse>();keyboard=InputSystem.AddDevice<Keyboard>();
            session=new GameObject("Ordered native input regression");game=session.AddComponent<GameController>();
            yield return null;game.StartMatch();game.enabled=false;
            // Freeze simulation and AI, not input. Orders must remain observable;
            // World.Paused would reject them and hide the bug being tested.
            yield return null;
            workers=game.World.Entities.Where(e=>e.owner==0&&e.Worker).ToArray();
            town=game.World.Entities.First(e=>e.owner==0&&e.def.id=="town");
            explorer=game.World.Entities.First(e=>e.owner==0&&e.def.id=="explorer");
            game.Controls.Select(town);
        }
        [UnityTearDown] public IEnumerator EndSession()
        {
            Object.Destroy(session);yield return null;
            InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(keyboard);
        }
        private Vector2 ScreenPoint(Vector2 point,float height=0)=>game.GameCamera.WorldToScreenPoint(new Vector3(point.x,height,point.y));
        private void KeyState(params Key[] keys)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
        private void Click(Vector2 point,MouseButton button=MouseButton.Right)
        {
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point}.WithButton(button));
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point});
        }
        private void PressRight(Vector2 point)=>InputSystem.QueueStateEvent(mouse,new MouseState{position=point}.WithButton(MouseButton.Right));
        private void DragWorkers()
        {
            var points=workers.Select(e=>ScreenPoint(e.position,1)).ToArray();
            var low=new Vector2(points.Min(p=>p.x)-10,points.Min(p=>p.y)-10);
            var high=new Vector2(points.Max(p=>p.x)+10,points.Max(p=>p.y)+10);
            Assert.IsFalse(game.Hud.BlocksWorldInput(low),"Fixture drag must start in the world");
            InputSystem.QueueStateEvent(mouse,new MouseState{position=low}.WithButton(MouseButton.Left));
            InputSystem.QueueStateEvent(mouse,new MouseState{position=high}.WithButton(MouseButton.Left));
            InputSystem.QueueStateEvent(mouse,new MouseState{position=high});
        }
        private EntityState Resource()=>game.World.Entities.First(e=>e.def.id=="berry"&&Vector2.Distance(e.position,town.position)<20);
        private int OrderEvents()=>game.World.Events.Count(e=>e.kind=="Command"&&e.player==0);
        private void AssertSelectedWorkers()=>CollectionAssert.AreEquivalent(workers.Select(e=>e.id),game.Controls.Selected);

        [UnityTest] public IEnumerator BoxThenRightClickUsesNewSelection()
        {
            var resource=Resource();var oldRally=town.rally;
            DragWorkers();Click(ScreenPoint(resource.position));yield return null;yield return null;
            AssertSelectedWorkers();Assert.AreEqual(oldRally,town.rally,"Old town selection must not receive a rally command");
            foreach(var worker in workers){Assert.AreEqual(1,worker.orders.Count);Assert.AreEqual(OrderKind.Gather,worker.orders.Peek().kind);Assert.AreEqual(resource.id,worker.orders.Peek().target);}
            Assert.AreEqual(1,OrderEvents());
        }
        [UnityTest] public IEnumerator RightClickBeforeBoxKeepsOldActorAndItsOwnPosition()
        {
            game.Controls.Select(explorer);var destination=new Vector2(40,37);
            Click(ScreenPoint(destination));DragWorkers();yield return null;yield return null;
            AssertSelectedWorkers();Assert.AreEqual(1,explorer.orders.Count);
            Assert.Less(Vector2.Distance(destination,explorer.orders.Peek().point),.1f);
            Assert.IsTrue(workers.All(e=>e.orders.Count==0));Assert.AreEqual(1,OrderEvents());
        }
        [UnityTest] public IEnumerator TwoShiftRightClicksRetainBothDestinations()
        {
            game.Controls.Select(explorer);var a=new Vector2(40,37);var b=new Vector2(44,32);
            var previous=new UnitOrder(OrderKind.Move,new Vector2(35,35));explorer.orders.Enqueue(previous);
            KeyState(Key.LeftShift);Click(ScreenPoint(a));Click(ScreenPoint(b));KeyState();yield return null;yield return null;
            var orders=explorer.orders.ToArray();Assert.AreEqual(3,orders.Length);Assert.AreSame(previous,orders[0]);
            Assert.Less(Vector2.Distance(a,orders[1].point),.1f);Assert.Less(Vector2.Distance(b,orders[2].point),.1f);
            Assert.AreEqual(2,OrderEvents());
        }
        [UnityTest] public IEnumerator ShiftReleasedBetweenRightClicksReplacesPreviousQueue()
        {
            game.Controls.Select(explorer);explorer.orders.Enqueue(new UnitOrder(OrderKind.Move,new Vector2(35,35)));
            KeyState(Key.LeftShift);Click(ScreenPoint(new Vector2(40,37)));KeyState();Click(ScreenPoint(new Vector2(44,32)));
            yield return null;yield return null;
            Assert.AreEqual(2,OrderEvents(),"Both discrete commands must be accepted even though the latter replaces the first");
            Assert.AreEqual(1,explorer.orders.Count);Assert.Less(Vector2.Distance(new Vector2(44,32),explorer.orders.Peek().point),.1f);
        }
        [UnityTest] public IEnumerator BoxThenCommandDigitSavesNewSelection()
        {
            DragWorkers();KeyState(Key.LeftMeta,Key.Digit1);KeyState();yield return null;yield return null;
            Assert.AreEqual(workers.Length,game.Controls.GroupCount(1));game.Controls.Select(town);
            KeyState(Key.Digit1);KeyState();yield return null;yield return null;AssertSelectedWorkers();
        }
        [UnityTest] public IEnumerator SaveBeforeBoxPreservesPreviousSelection()
        {
            KeyState(Key.LeftCtrl,Key.Digit2);KeyState();DragWorkers();yield return null;yield return null;
            AssertSelectedWorkers();KeyState(Key.Digit2);KeyState();yield return null;yield return null;
            CollectionAssert.AreEqual(new[]{town.id},game.Controls.Selected);
        }
        [UnityTest] public IEnumerator FriendlyUnitDoesNotHideTreasureFromExplorerCommand()
        {
            var treasure=game.World.Treasures.First();var chest=game.World.Get(treasure.entity);
            foreach(int guard in treasure.guards)game.World.Remove(game.World.Get(guard));
            workers[0].position=chest.position;game.World.Fog.Reveal(0,chest.position,12);game.Controls.Focus(chest.position);
            game.Controls.Select(explorer);PressRight(ScreenPoint(chest.position));yield return null;yield return null;
            Assert.AreEqual(1,explorer.orders.Count);Assert.AreEqual(OrderKind.Claim,explorer.orders.Peek().kind);Assert.AreEqual(chest.id,explorer.orders.Peek().target);
            Click(ScreenPoint(workers[0].position,1),MouseButton.Left);yield return null;yield return null;
            Assert.AreEqual(workers[0].id,game.Controls.FirstSelected.id,"Left click must retain unit selection priority");
        }
        [UnityTest] public IEnumerator FriendlyUnitDoesNotHideResourceFromWorkerCommand()
        {
            var resource=Resource();workers[1].position=resource.position;game.Controls.Select(workers[0]);
            PressRight(ScreenPoint(resource.position));yield return null;yield return null;
            Assert.AreEqual(OrderKind.Gather,workers[0].orders.Peek().kind);Assert.AreEqual(resource.id,workers[0].orders.Peek().target);
        }
        [UnityTest] public IEnumerator GuardedTreasureStillRejectsClaimWhenFriendlyUnitOverlaps()
        {
            var camp=game.World.Treasures.First(t=>t.guards.Count>0);var chest=game.World.Get(camp.entity);workers[0].position=chest.position;
            // Keep linked guardians alive but outside the click footprint. The
            // assertion concerns Claim validation, not selecting a nearby enemy.
            foreach(int id in camp.guards)game.World.Get(id).position=chest.position+Vector2.up*15;
            game.World.Fog.Reveal(0,chest.position,12);game.Controls.Focus(chest.position);game.Controls.Select(explorer);
            PressRight(ScreenPoint(chest.position));yield return null;yield return null;
            Assert.AreEqual(0,explorer.orders.Count,string.Join(", ",explorer.orders.Select(o=>o.kind+" target="+o.target+" "+game.World.Get(o.target)?.def.id))+"; "+game.Notice);StringAssert.Contains("guardians",game.Notice);
        }
        [UnityTest] public IEnumerator EscapeClosingModalDiscardsSameFrameWorldPointer()
        {
            game.Controls.Select(explorer);game.Hud.ShowHomeCity();
            KeyState(Key.Escape);PressRight(ScreenPoint(new Vector2(40,37)));yield return null;yield return null;
            Assert.IsFalse(game.Hud.ModalVisible);Assert.AreEqual(0,explorer.orders.Count);Assert.AreEqual(0,OrderEvents());
            KeyState();InputSystem.QueueStateEvent(mouse,new MouseState{position=ScreenPoint(new Vector2(40,37))});
            yield return new WaitForSecondsRealtime(.25f);Click(ScreenPoint(new Vector2(40,37)));yield return null;yield return null;
            Assert.AreEqual(1,explorer.orders.Count,"Fresh input after the UI barrier must work");
        }
        [UnityTest] public IEnumerator ResumeButtonDiscardsSameFrameRightClick()
        {
            game.Controls.Select(explorer);game.TogglePause();
            var resume=session.GetComponentsInChildren<Button>().First(b=>b.GetComponentInChildren<Text>().text=="RESUME");
            resume.onClick.Invoke();PressRight(ScreenPoint(new Vector2(40,37)));yield return null;yield return null;
            Assert.IsFalse(game.IsPaused);Assert.AreEqual(0,explorer.orders.Count);Assert.AreEqual(0,OrderEvents());
        }
        [UnityTest] public IEnumerator AttackKeyBeforeClickUsesThatClickAsAttackMove()
        {
            game.Controls.Select(explorer);var destination=new Vector2(40,37);
            KeyState(Key.A);KeyState();Click(ScreenPoint(destination),MouseButton.Left);yield return null;yield return null;
            Assert.AreEqual(1,explorer.orders.Count);Assert.AreEqual(OrderKind.AttackMove,explorer.orders.Peek().kind);
            Assert.IsFalse(game.Controls.AttackMode);
        }
        [UnityTest] public IEnumerator RightClickOnMinimapIssuesExactlyOneOrder()
        {
            game.Controls.Select(explorer);var mini=session.GetComponentsInChildren<RectTransform>().First(t=>t.name=="Interactive minimap");
            var point=RectTransformUtility.WorldToScreenPoint(null,mini.TransformPoint(mini.rect.center));
            Click(point);yield return null;yield return null;
            Assert.AreEqual(1,OrderEvents());Assert.AreEqual(1,explorer.orders.Count);
            Assert.Less(Vector2.Distance(new Vector2(64,64),explorer.orders.Peek().point),3);
        }
        [UnityTest] public IEnumerator AttackMoveMinimapClickAndReleaseIssuesOneOrder()
        {
            game.Controls.Select(explorer);var mini=session.GetComponentsInChildren<RectTransform>().First(t=>t.name=="Interactive minimap");
            var point=RectTransformUtility.WorldToScreenPoint(null,mini.TransformPoint(mini.rect.center));
            KeyState(Key.A);KeyState();Click(point,MouseButton.Left);yield return null;yield return null;
            Assert.AreEqual(1,OrderEvents());Assert.AreEqual(OrderKind.AttackMove,explorer.orders.Peek().kind);Assert.IsFalse(game.Controls.AttackMode);
        }
        [UnityTest] public IEnumerator OpeningHomeCityBlocksLaterPointerAndGroupKeys()
        {
            DragWorkers();KeyState(Key.H);KeyState();KeyState(Key.LeftMeta,Key.Digit1);KeyState();Click(ScreenPoint(new Vector2(40,37)));
            yield return null;yield return null;
            AssertSelectedWorkers();Assert.IsTrue(game.Hud.ModalVisible);Assert.AreEqual(0,game.Controls.GroupCount(1));Assert.AreEqual(0,OrderEvents());
        }
        [UnityTest] public IEnumerator ShiftSelectionUsesEachReleaseSnapshot()
        {
            Click(ScreenPoint(workers[0].position,1),MouseButton.Left);
            KeyState(Key.LeftShift);Click(ScreenPoint(explorer.position,1),MouseButton.Left);KeyState();
            Click(ScreenPoint(town.position),MouseButton.Left);yield return null;yield return null;
            CollectionAssert.AreEqual(new[]{town.id},game.Controls.Selected);
        }
        [UnityTest] public IEnumerator AllTenControlGroupDigitsRoundTrip()
        {
            var digits=new[]{Key.Digit0,Key.Digit1,Key.Digit2,Key.Digit3,Key.Digit4,Key.Digit5,Key.Digit6,Key.Digit7,Key.Digit8,Key.Digit9};
            for(int n=0;n<digits.Length;n++)
            {
                game.Controls.Select(workers[n%workers.Length]);KeyState(Key.LeftMeta,digits[n]);KeyState();yield return null;yield return null;
                Assert.AreEqual(1,game.Controls.GroupCount(n),"Group "+n);game.Controls.Select(town);
                KeyState(digits[n]);KeyState();yield return null;yield return null;Assert.AreEqual(workers[n%workers.Length].id,game.Controls.FirstSelected.id,"Group "+n);
            }
        }
        [UnityTest] public IEnumerator HiddenEnemyCannotBecomeRightClickAttackTarget()
        {
            var enemy=game.World.Entities.First(e=>e.owner==1&&e.Worker);game.Controls.Select(explorer);game.Controls.Focus(enemy.position);
            Assert.IsFalse(game.World.IsVisible(0,enemy));Click(ScreenPoint(enemy.position,1));yield return null;yield return null;
            Assert.AreEqual(1,explorer.orders.Count);Assert.AreEqual(OrderKind.Move,explorer.orders.Peek().kind);Assert.AreEqual(0,explorer.orders.Peek().target);
        }
        [UnityTest] public IEnumerator RestartDiscardsCapturedEdgesAndControlGroups()
        {
            game.Controls.Select(explorer);game.Controls.SaveGroup(1);
            Click(ScreenPoint(new Vector2(40,37)));KeyState(Key.Digit1);KeyState();
            InputSystem.Update();game.StartMatch();yield return null;yield return null;
            Assert.AreEqual(0,game.Controls.Selected.Count);Assert.AreEqual(0,game.Controls.GroupCount(1));Assert.AreEqual(0,OrderEvents());
            KeyState(Key.T);KeyState();yield return null;yield return null;
            Assert.AreEqual("town",game.Controls.FirstSelected.def.id,"Fresh input in restarted match must still work");
        }
    }
}
